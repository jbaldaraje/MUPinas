// <copyright file="OwnedTestingCrowd.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

using System.Collections.Concurrent;
using System.Diagnostics;
using MUnique.OpenMU.GameLogic.Bots;
using MUnique.OpenMU.Pathfinding;

/// <summary>Owns only the named local-testing crowd; existing bot managers and real players are untouched.</summary>
internal sealed class OwnedTestingCrowd
{
    private readonly ConcurrentDictionary<string, TestingCrowdPlayer> _players = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _expiresUtc;
    private LocalTestingConfiguration? _settings;

    /// <summary>Gets the active number of owned character entities.</summary>
    public int Count => this._players.Count;

    /// <summary>Gets a stable snapshot of character entities for status reporting.</summary>
    public IEnumerable<TestingCrowdPlayer> Players => this._players.Values.ToArray();

    /// <summary>Gets a value indicating whether this activation has reached its cleanup deadline.</summary>
    public bool Expired => this.Count > 0 && DateTime.UtcNow >= this._expiresUtc;

    /// <summary>Creates a bounded target crowd through the existing bot character factory and offline-player lifecycle.</summary>
    /// <param name="context">The server owning the population.</param>
    /// <param name="settings">The validated crowd settings.</param>
    public async ValueTask ApplyAsync(IGameContext context, LocalTestingConfiguration settings)
    {
        using var process = Process.GetCurrentProcess();
        EnsureMemoryBudget(process, settings);
        var map = context.Configuration.Maps.FirstOrDefault(m => m.Number == settings.CrowdMap && m.Discriminator == 0)
            ?? throw new ValidationException("The crowd map is unavailable.");
        if (!settings.EligibleMaps.Split(',').Contains(settings.CrowdMap.ToString(System.Globalization.CultureInfo.InvariantCulture))
            && !(settings.CrowdMap == 79 && settings.CrowdBehavior != "combat"))
        {
            throw new ValidationException("This map is not eligible for crowd testing.");
        }

        var liveMap = await context.GetMapAsync((ushort)map.Number).ConfigureAwait(false)
            ?? throw new ValidationException("The crowd map is not hosted on this server.");
        var points = Points(map, liveMap.Terrain, settings).ToArray();
        if (points.Length == 0)
        {
            throw new ValidationException("No safe walkable crowd position exists within the selected radius.");
        }

        new Random(42).Shuffle(points);
        var server = context is IGameServerContext gameServer ? gameServer.Id : 0;
        var prefix = $"tc{server:X2}";
        var generator = new BotGenerator(context, context.LoggerFactory.CreateLogger<OwnedTestingCrowd>(), prefix, testingNames: true);
        await generator.EnsureBotsAsync(settings.CrowdCount, 1, new TestingCrowdProfile(settings.CrowdLevel)).ConfigureAwait(false);
        await this.RemoveAsync().ConfigureAwait(false);
        this._settings = settings;
        this._expiresUtc = settings.CrowdExpiresUtc ?? DateTime.UtcNow.AddSeconds(settings.CrowdLifetimeSeconds);
        try
        {
            for (var index = 1; index <= settings.CrowdCount; index++)
            {
                EnsureMemoryBudget(process, settings);
                var login = $"{prefix}{index:D4}";
                var player = new TestingCrowdPlayer(context, settings.CrowdBehavior);
                try
                {
                    var account = await player.PersistenceContext.GetAccountByLoginNameAsync(login).ConfigureAwait(false);
                    var character = account?.Characters.SingleOrDefault();
                    if (account is not { IsBot: true } || character is null || !character.Name.StartsWith("Test", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("An account in the test namespace does not belong to this crowd.");
                    }

                    if (settings.CrowdExcludedNames.Split(',').Contains(character.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        await player.DisposeAsync().ConfigureAwait(false);
                        continue;
                    }

                    var point = points[(index - 1) % points.Length];
                    character.CurrentMap = map;
                    character.PositionX = point.X;
                    character.PositionY = point.Y;
                    player.MuHelperSettings = new BotMuHelperSettings();
                    if (!await player.InitializeAsync(login, character.Name).ConfigureAwait(false))
                    {
                        throw new InvalidOperationException("A test character could not enter the world.");
                    }

                    this._players[player.Name] = player;
                    player.StartBehavior();
                }
                catch
                {
                    await player.StopAsync().ConfigureAwait(false);
                    await player.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
        }
        catch
        {
            await this.RemoveAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Removes a selected owned character, or all owned entities, without deleting accounts.</summary>
    /// <param name="name">The owned character name, or null for all.</param>
    public async ValueTask RemoveAsync(string? name = null)
    {
        foreach (var pair in this._players.Where(p => name is null || string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            if (!pair.Value.IsDisposed)
            {
                await pair.Value.StopAsync().ConfigureAwait(false);
                await pair.Value.DisposeAsync().ConfigureAwait(false);
            }

            this._players.TryRemove(pair.Key, out _);
        }
    }

    /// <summary>Moves wandering entities one validated adjacent tile per maintenance tick.</summary>
    public async ValueTask TickAsync()
    {
        if (this._settings is not { CrowdBehavior: "wander" } settings)
        {
            return;
        }

        foreach (var player in this._players.Values)
        {
            if (player is not { IsAlive: true, IsWalking: false, CurrentMap: { } map })
            {
                continue;
            }

            var x = player.Position.X + Random.Shared.Next(-1, 2);
            var y = player.Position.Y + Random.Shared.Next(-1, 2);
            if (x is < 1 or > 254 || y is < 1 or > 254 || Math.Abs(x - settings.CrowdX) > settings.CrowdRadius
                || Math.Abs(y - settings.CrowdY) > settings.CrowdRadius || !map.Terrain.WalkMap[x, y])
            {
                continue;
            }

            var target = new Point((byte)x, (byte)y);
            if (target != player.Position)
            {
                await player.WalkOneStepAsync(target).ConfigureAwait(false);
            }
        }
    }

    private static void EnsureMemoryBudget(Process process, LocalTestingConfiguration settings)
    {
        process.Refresh();
        if (process.WorkingSet64 > (long)settings.MaximumWorkingSetMegabytes * 1024 * 1024)
        {
            throw new ValidationException("The process memory ceiling was reached; the partial crowd was removed.");
        }
    }

    private static IEnumerable<Point> Points(GameMapDefinition map, GameMapTerrain terrain, LocalTestingConfiguration settings)
    {
        for (var y = Math.Max(1, settings.CrowdY - settings.CrowdRadius); y <= Math.Min(254, settings.CrowdY + settings.CrowdRadius); y++)
        {
            for (var x = Math.Max(1, settings.CrowdX - settings.CrowdRadius); x <= Math.Min(254, settings.CrowdX + settings.CrowdRadius); x++)
            {
                if (terrain.WalkMap[x, y] && (settings.CrowdBehavior != "combat" || !terrain.SafezoneMap[x, y])
                    && !map.EnterGates.Any(g => x >= g.X1 - 2 && x <= g.X2 + 2 && y >= g.Y1 - 2 && y <= g.Y2 + 2)
                    && !map.MonsterSpawns.Any(s => s.MonsterDefinition?.ObjectKind != NpcObjectKind.Monster
                        && x >= s.X1 - 2 && x <= s.X2 + 2 && y >= s.Y1 - 2 && y <= s.Y2 + 2))
                {
                    yield return new Point((byte)x, (byte)y);
                }
            }
        }
    }
}
