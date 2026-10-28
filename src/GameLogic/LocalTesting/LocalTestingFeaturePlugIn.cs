// <copyright file="LocalTestingFeaturePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;
using Nito.AsyncEx;

/// <summary>Opt-in, reversible settings and bounded test populations for a local game server.</summary>
[PlugIn]
[Guid("F8B99D44-3A7F-466B-B002-847514626E9C")]
[Display(Name = nameof(LocalTestingResources.FeatureName), Description = nameof(LocalTestingResources.FeatureDescription), ResourceType = typeof(LocalTestingResources))]
public sealed class LocalTestingFeaturePlugIn : IFeaturePlugIn, IPeriodicTaskPlugIn, IDisabledByDefault,
    ISupportCustomConfiguration<LocalTestingConfiguration>, ISupportDefaultCustomConfiguration
{
    // A plugin instance serves every game server. Take this lock before configuration or population locks;
    // methods called inside it must not acquire it again. Baseline capture and persistence are serialized.
    private readonly AsyncLock _lock = new();
    private readonly ConcurrentDictionary<IGameContext, PopulationState> _states = new();
    private readonly Dictionary<GameConfiguration, string> _applied = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<PlugInManager> _managers = [];
    private int _running;
    private double _lastCpuMilliseconds;
    private long _lastSample;
    private double _cpuPercent;
    private double _memoryMegabytes;
    private double _maintenanceMilliseconds;
    private bool _hasMetrics;

    /// <inheritdoc />
    public LocalTestingConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new LocalTestingConfiguration();

    /// <inheritdoc />
    public void ForceStart()
    {
        // Settings are checked on each periodic tick; no additional timer is needed.
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (Interlocked.CompareExchange(ref this._running, 1, 0) != 0)
        {
            return;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            using var guard = await this._lock.LockAsync().ConfigureAwait(false);
            if (!gameContext.PlugInManager.IsPlugInActive(typeof(LocalTestingFeaturePlugIn)))
            {
                return;
            }

            this.RegisterLifecycle(gameContext.PlugInManager);
            await this.PruneStoppedServersAsync().ConfigureAwait(false);
            var state = this._states.GetOrAdd(gameContext, _ => new PopulationState());
            if (gameContext.IsDisposed || gameContext.IsDisposing || gameContext.ArePeriodicTasksStopped)
            {
                await state.Spawns.RemoveAsync().ConfigureAwait(false);
                await state.Crowd.RemoveAsync().ConfigureAwait(false);
                this._states.TryRemove(gameContext, out _);
                return;
            }

            await this.SynchronizeAsync(gameContext, state).ConfigureAwait(false);
            this.SampleMetrics(started);
        }
        catch (Exception exception)
        {
            gameContext.LoggerFactory.CreateLogger<LocalTestingFeaturePlugIn>().LogError(exception, "Local testing synchronization failed; request was not reported successful.");
        }
        finally
        {
            Interlocked.Exchange(ref this._running, 0);
        }
    }

    /// <summary>Saves validated admin changes and applies them without a process restart.</summary>
    /// <param name="player">The authenticated game master making the request.</param>
    /// <param name="change">The bounded edit.</param>
    public async ValueTask SaveAsync(Player player, Action<LocalTestingConfiguration> change)
    {
        if (player.SelectedCharacter?.CharacterStatus != CharacterStatus.GameMaster)
        {
            throw new UnauthorizedAccessException();
        }

        using var guard = await this._lock.LockAsync().ConfigureAwait(false);
        if (!player.GameContext.PlugInManager.IsPlugInActive(typeof(LocalTestingFeaturePlugIn)))
        {
            throw new InvalidOperationException("The testing plugin was deactivated while the request was waiting.");
        }

        this.RegisterLifecycle(player.GameContext.PlugInManager);
        var settings = JsonSerializer.Deserialize<LocalTestingConfiguration>(JsonSerializer.Serialize(this.Configuration ?? new()))!;
        change(settings);
        settings.PopulationServerId = player.GameContext is IGameServerContext gameServer ? gameServer.Id : 0;
        Validate(settings);
        TestingPreset.Capture(player.GameContext.Configuration, settings);
        player.Logger.LogInformation("LocalTesting audit actor={Actor} operation=save requested={Requested}", player.Name, AuditSettings(settings));
        await PersistAsync(player.GameContext, settings).ConfigureAwait(false);
        this.Configuration = settings;
        var state = this._states.GetOrAdd(player.GameContext, _ => new PopulationState());
        await this.SynchronizeAsync(player.GameContext, state).ConfigureAwait(false);
        player.Logger.LogInformation("LocalTesting audit actor={Actor} operation=applied requested={Requested}", player.Name, AuditSettings(settings));
    }

    /// <summary>Gets owned population counts, separate from configured natural spawns.</summary>
    /// <param name="context">The game server.</param>
    /// <param name="map">The map number.</param>
    /// <returns>The allocated and alive populations.</returns>
    public (int Allocated, int Alive) Population(IGameContext context, short map)
        => this._states.TryGetValue(context, out var state) ? state.Spawns.Population(map) : default;

    /// <summary>Gets active testing characters, excluding real players and the existing bot feature.</summary>
    /// <param name="context">The game server.</param>
    /// <returns>Owned names and class numbers.</returns>
    public IEnumerable<(string Name, byte Class)> Crowd(IGameContext context)
        => this._states.TryGetValue(context, out var state)
            ? state.Crowd.Players.Select(p => (p.Name, p.SelectedCharacter?.CharacterClass?.Number ?? (byte)0)).ToArray() : [];

    /// <summary>Gets measured process metrics and owned monsters; maintenance timing covers this feature, not the whole game loop.</summary>
    /// <returns>The metrics, or null until a CPU sampling interval has completed.</returns>
    public (double Cpu, double MemoryMiB, double MaintenanceMs, int Monsters)? Metrics()
        => this._hasMetrics ? (this._cpuPercent, this._memoryMegabytes, this._maintenanceMilliseconds, this._states.Values.Sum(s => s.Spawns.Count)) : null;

    private static string Fingerprint(LocalTestingConfiguration settings)
        => FormattableString.Invariant($"{settings.Enabled}:{settings.NormalExperienceRate}:{settings.ItemDropMultiplier}:{settings.MaximumAttackSpeed}:{settings.MaximumStat}:{settings.EligibleMaps}");

    private static string SpawnFingerprint(LocalTestingConfiguration settings)
        => FormattableString.Invariant($"{settings.Enabled}:{settings.MonstersPerMap}:{settings.MonstersPerSpot}:{settings.SpotSpacing}:{settings.RespawnSeconds}:{settings.GlobalMonsterLimit}:{settings.PopulationServerId}:{settings.MaximumWorkingSetMegabytes}");

    private static string AuditSettings(LocalTestingConfiguration settings)
        => JsonSerializer.Serialize(new
        {
            Profile = Fingerprint(settings),
            Spawns = SpawnFingerprint(settings),
            settings.ActiveMonsterMaps,
            settings.CrowdCount,
            settings.CrowdMap,
            settings.CrowdX,
            settings.CrowdY,
            settings.CrowdRadius,
            settings.CrowdBehavior,
            settings.CrowdExpiresUtc,
            settings.CrowdExcludedNames,
        });

    private static void Validate(LocalTestingConfiguration settings)
    {
        Validator.ValidateObject(settings, new ValidationContext(settings), true);
        if (!float.IsFinite(settings.NormalExperienceRate) || !double.IsFinite(settings.ItemDropMultiplier)
            || settings.CrowdCount > settings.MaximumCrowdBots)
        {
            throw new ValidationException("Non-finite rates or excessive crowd counts are not accepted.");
        }

        if (ParseMaps(settings.EligibleMaps).Any(m => m is < 0 or > byte.MaxValue))
        {
            throw new ValidationException("Map numbers must fit the native client's supported map range.");
        }

        var active = ParseMaps(settings.ActiveMonsterMaps);
        if (active.Except(ParseMaps(settings.EligibleMaps)).Any() || active.Count * settings.MonstersPerMap > settings.GlobalMonsterLimit)
        {
            throw new ValidationException("The requested maps exceed the allowlist or global population limit.");
        }
    }

    private static HashSet<short> ParseMaps(string text)
        => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => short.Parse(s, CultureInfo.InvariantCulture)).ToHashSet();

    private static async ValueTask PersistAsync(IGameContext context, LocalTestingConfiguration settings)
    {
        using var persistence = context.PersistenceContextProvider.CreateNewContext();
        var game = (await persistence.GetAsync<GameConfiguration>().ConfigureAwait(false)).FirstOrDefault()
            ?? throw new InvalidOperationException("The persisted game configuration is unavailable.");
        var plugin = game.PlugInConfigurations.FirstOrDefault(p => p.TypeId == typeof(LocalTestingFeaturePlugIn).GUID)
            ?? throw new InvalidOperationException("Enable the local testing plugin through the admin panel first.");
        plugin.SetConfiguration(settings, context.PlugInManager.CustomConfigReferenceHandler);
        if (!await persistence.SaveChangesAsync().ConfigureAwait(false))
        {
            throw new InvalidOperationException("The testing settings could not be persisted.");
        }
    }

    private void SampleMetrics(long started)
    {
        using var process = Process.GetCurrentProcess();
        var now = Stopwatch.GetTimestamp();
        var cpu = process.TotalProcessorTime.TotalMilliseconds;
        if (this._lastSample != 0)
        {
            var elapsed = Stopwatch.GetElapsedTime(this._lastSample, now).TotalMilliseconds;
            this._cpuPercent = Math.Clamp((cpu - this._lastCpuMilliseconds) / elapsed / Environment.ProcessorCount * 100, 0, 100);
            this._hasMetrics = true;
        }

        this._lastSample = now;
        this._lastCpuMilliseconds = cpu;
        this._memoryMegabytes = process.WorkingSet64 / (1024.0 * 1024);
        this._maintenanceMilliseconds = Stopwatch.GetElapsedTime(started, now).TotalMilliseconds;
    }

    private void RegisterLifecycle(PlugInManager manager)
    {
        if (this._managers.Add(manager))
        {
            manager.PlugInDeactivated += this.OnPlugInDeactivated;
        }
    }

    private void OnPlugInDeactivated(object? sender, PlugInEventArgs args)
    {
        if (args.PlugInType != typeof(LocalTestingFeaturePlugIn))
        {
            return;
        }

        _ = this.SuspendAsync(sender as PlugInManager);
    }

    private async ValueTask SuspendAsync(PlugInManager? manager)
    {
        try
        {
            using var guard = await this._lock.LockAsync().ConfigureAwait(false);
            if (manager?.IsPlugInActive(typeof(LocalTestingFeaturePlugIn)) == true)
            {
                return;
            }

            foreach (var pair in this._states)
            {
                await pair.Value.Spawns.RemoveAsync().ConfigureAwait(false);
                await pair.Value.Crowd.RemoveAsync().ConfigureAwait(false);
                if (this.Configuration is { } settings)
                {
                    TestingPreset.Restore(pair.Key.Configuration, settings);
                }

                pair.Key.LoggerFactory.CreateLogger<LocalTestingFeaturePlugIn>().LogInformation("LocalTesting audit operation=plugin-deactivated result=baseline-restored");
            }

            if (this._states.Keys.FirstOrDefault() is { } context)
            {
                await this.RefreshPlayersAsync(context).ConfigureAwait(false);
            }

            this._states.Clear();
            this._applied.Clear();
        }
        catch (Exception exception)
        {
            foreach (var context in this._states.Keys)
            {
                context.LoggerFactory.CreateLogger<LocalTestingFeaturePlugIn>().LogError(exception, "LocalTesting deactivation cleanup failed.");
            }
        }
    }

    private async ValueTask PruneStoppedServersAsync()
    {
        foreach (var pair in this._states.Where(p => p.Key is GameContext c && (c.IsDisposed || c.IsDisposing || c.ArePeriodicTasksStopped)).ToArray())
        {
            await pair.Value.Spawns.RemoveAsync().ConfigureAwait(false);
            await pair.Value.Crowd.RemoveAsync().ConfigureAwait(false);
            this._states.TryRemove(pair.Key, out _);
        }

        foreach (var game in this._applied.Keys.Where(g => !this._states.Keys.Any(c => ReferenceEquals(c.Configuration, g))).ToArray())
        {
            this._applied.Remove(game);
        }
    }

    private async ValueTask SynchronizeAsync(IGameContext context, PopulationState state)
    {
        var settings = this.Configuration ??= new LocalTestingConfiguration();
        Validate(settings);
        var fingerprint = Fingerprint(settings);
        if (!this._applied.TryGetValue(context.Configuration, out var previous) || previous != fingerprint)
        {
            if (settings.Enabled)
            {
                TestingPreset.Capture(context.Configuration, settings);
                await PersistAsync(context, settings).ConfigureAwait(false);
                TestingPreset.Restore(context.Configuration, settings);
                TestingPreset.Apply(context.Configuration, settings);
            }
            else
            {
                TestingPreset.Restore(context.Configuration, settings);
            }

            this._applied[context.Configuration] = fingerprint;
            context.LoggerFactory.CreateLogger<LocalTestingFeaturePlugIn>().LogInformation(
                "LocalTesting preset enabled={Enabled} effectiveXp={Experience} effectiveMasterXp={MasterExperience} attackSpeedCap={Speed} statCap={Stat}",
                settings.Enabled,
                context.ExperienceRate,
                context.MasterExperienceRate,
                context.Configuration.Attributes.FirstOrDefault(a => a == Stats.AttackSpeed)?.MaximumValue,
                context.Configuration.Attributes.FirstOrDefault(a => a == Stats.BaseStrength)?.MaximumValue);
            await this.RefreshPlayersAsync(context).ConfigureAwait(false);
        }

        await this.SynchronizePopulationsAsync(context, state, settings, SpawnFingerprint(settings)).ConfigureAwait(false);
        await this.SynchronizeCrowdAsync(context, state, settings).ConfigureAwait(false);
    }

    private async ValueTask RefreshPlayersAsync(IGameContext context)
    {
        foreach (var server in this._states.Keys.Append(context).Distinct())
        {
            foreach (var player in await server.GetPlayersAsync().ConfigureAwait(false))
            {
                if (player.Attributes is { } attributes)
                {
                    foreach (var attribute in attributes.OfType<ComposableAttribute>())
                    {
                        attribute.Invalidate();
                    }
                }

                await player.InvokeViewPlugInAsync<IUpdateCharacterStatsPlugIn>(p => p.UpdateCharacterStatsAsync()).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask SynchronizePopulationsAsync(IGameContext context, PopulationState state, LocalTestingConfiguration settings, string fingerprint)
    {
        if (state.Fingerprint != fingerprint)
        {
            await state.Spawns.RemoveAsync().ConfigureAwait(false);
            state.Fingerprint = fingerprint;
        }

        if (!settings.Enabled || settings.PopulationServerId != (context is IGameServerContext server ? server.Id : 0))
        {
            await state.Spawns.RemoveAsync().ConfigureAwait(false);
            return;
        }

        var requestedMaps = ParseMaps(settings.ActiveMonsterMaps);
        foreach (var removed in state.Spawns.Maps.Except(requestedMaps))
        {
            await state.Spawns.RemoveAsync(removed).ConfigureAwait(false);
        }

        if (DateTime.UtcNow < state.NextMapActivationUtc)
        {
            return;
        }

        state.NextMapActivationUtc = DateTime.UtcNow.AddSeconds(settings.MapActivationIntervalSeconds);
        using var process = Process.GetCurrentProcess();
        if (process.WorkingSet64 > (long)settings.MaximumWorkingSetMegabytes * 1024 * 1024)
        {
            context.LoggerFactory.CreateLogger<LocalTestingFeaturePlugIn>().LogWarning("LocalTesting activation paused at the configured memory ceiling {LimitMiB} MiB.", settings.MaximumWorkingSetMegabytes);
            return;
        }

        // Activate at most one map per bounded stage, allowing resource inspection and emergency cleanup.
        foreach (var number in requestedMaps.Except(state.Spawns.Maps))
        {
            var map = context.Configuration.Maps.FirstOrDefault(m => m.Number == number && m.Discriminator == 0)
                ?? throw new ValidationException("The requested ordinary map does not exist.");
            var available = settings.GlobalMonsterLimit - this._states.Values.Sum(s => s.Spawns.Count);
            await state.Spawns.ApplyAsync(context, map, settings, available).ConfigureAwait(false);
            context.LoggerFactory.CreateLogger<LocalTestingFeaturePlugIn>().LogInformation("LocalTesting map={Map} allocated={Allocated} alive={Alive}", number, state.Spawns.Population(number).Allocated, state.Spawns.Population(number).Alive);
            break;
        }
    }

    private async ValueTask SynchronizeCrowdAsync(IGameContext context, PopulationState state, LocalTestingConfiguration settings)
    {
        var requested = JsonSerializer.Serialize(new
        {
            settings.Enabled,
            settings.PopulationServerId,
            settings.CrowdCount,
            settings.CrowdMap,
            settings.CrowdX,
            settings.CrowdY,
            settings.CrowdRadius,
            settings.CrowdBehavior,
            settings.CrowdLevel,
            settings.CrowdLifetimeSeconds,
            settings.CrowdExpiresUtc,
            settings.CrowdExcludedNames,
        });
        if (state.Crowd.Expired || (settings.CrowdExpiresUtc is { } deadline && DateTime.UtcNow >= deadline))
        {
            if (settings.CrowdCount != 0)
            {
                settings.CrowdCount = 0;
                await PersistAsync(context, settings).ConfigureAwait(false);
            }

            await state.Crowd.RemoveAsync().ConfigureAwait(false);
            return;
        }

        if (!settings.Enabled || settings.CrowdCount == 0 || settings.PopulationServerId != (context is IGameServerContext server ? server.Id : 0))
        {
            await state.Crowd.RemoveAsync().ConfigureAwait(false);
            state.CrowdFingerprint = string.Empty;
            return;
        }

        if (state.CrowdFingerprint != requested)
        {
            var others = this._states.Values.Where(s => s != state).Sum(s => s.Crowd.Count);
            if (others + settings.CrowdCount > settings.MaximumCrowdBots)
            {
                throw new ValidationException("The process-wide crowd limit would be exceeded.");
            }

            try
            {
                await state.Crowd.ApplyAsync(context, settings).ConfigureAwait(false);
            }
            finally
            {
                // Failed activations stay failed until an administrator submits a fresh activation.
                // Retrying a faulty character factory every second would create an unbounded error loop.
                state.CrowdFingerprint = requested;
            }
        }

        await state.Crowd.TickAsync().ConfigureAwait(false);
    }

    private sealed class PopulationState
    {
        public OwnedTestingSpawns Spawns { get; } = new();

        public DateTime NextMapActivationUtc { get; set; }

        public string Fingerprint { get; set; } = string.Empty;

        public OwnedTestingCrowd Crowd { get; } = new();

        public string CrowdFingerprint { get; set; } = string.Empty;
    }
}
