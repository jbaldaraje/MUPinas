// <copyright file="LocalTestingChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

using System.Globalization;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.PlugIns;

/// <summary>Uses the authenticated chat action path for native local-testing controls.</summary>
/// <remarks>Replies beginning @LT1 are bounded ASCII status records, not player chat or executable content.</remarks>
[PlugIn]
[Guid("7D718EA3-E2AC-4A8F-8F06-CB0CE954E2F7")]
[Display(Name = nameof(LocalTestingResources.CommandName), Description = nameof(LocalTestingResources.CommandDescription), ResourceType = typeof(LocalTestingResources))]
public sealed class LocalTestingChatCommandPlugIn : IChatCommandPlugIn
{
    /// <inheritdoc />
    public string Key => "/localtest";

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (player.PlayerState.CurrentState != PlayerState.EnteredWorld || command.Length > 120)
        {
            return;
        }

        var feature = player.GameContext.FeaturePlugIns.GetPlugIn<LocalTestingFeaturePlugIn>();
        if (feature is null)
        {
            await player.ShowBlueMessageAsync("@LT1 error unavailable").ConfigureAwait(false);
            await player.ShowLocalizedBlueMessageAsync("LocalTestingUnavailable").ConfigureAwait(false);
            return;
        }

        var arguments = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (arguments.Length == 1 || (arguments.Length == 2 && arguments[1] == "status"))
        {
            await SendStatusAsync(player, feature).ConfigureAwait(false);
            return;
        }

        if (player.SelectedCharacter?.CharacterStatus != CharacterStatus.GameMaster)
        {
            player.Logger.LogWarning("LocalTesting audit actor={Actor} operation=denied", player.Name);
            await player.ShowBlueMessageAsync("@LT1 error denied").ConfigureAwait(false);
            await player.ShowLocalizedBlueMessageAsync("LocalTestingDenied").ConfigureAwait(false);
            return;
        }

        try
        {
            if (arguments.Length == 3 && arguments[1] == "preview")
            {
                await PreviewAsync(player, feature.Configuration ?? new(), arguments[2]).ConfigureAwait(false);
            }
            else
            {
                await feature.SaveAsync(player, settings => Change(settings, arguments)).ConfigureAwait(false);
                await SendStatusAsync(player, feature).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ValidationException or ArgumentException)
        {
            await player.ShowBlueMessageAsync("@LT1 error invalid").ConfigureAwait(false);
            await player.ShowLocalizedBlueMessageAsync("LocalTestingInvalid").ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            player.Logger.LogError(exception, "LocalTesting audit actor={Actor} operation=failed", player.Name);
            await player.ShowBlueMessageAsync("@LT1 error failed").ConfigureAwait(false);
            await player.ShowLocalizedBlueMessageAsync("LocalTestingFailed").ConfigureAwait(false);
        }
    }

    private static void Change(LocalTestingConfiguration settings, string[] arguments)
    {
        switch (arguments.Length, arguments[1])
        {
            case (3, "enabled"):
                settings.Enabled = arguments[2] switch { "1" => true, "0" => false, _ => throw new FormatException() };
                break;
            case (4, "caps"):
                settings.MaximumAttackSpeed = int.Parse(arguments[2], CultureInfo.InvariantCulture);
                settings.MaximumStat = int.Parse(arguments[3], CultureInfo.InvariantCulture);
                break;
            case (4, "rates"):
                settings.NormalExperienceRate = float.Parse(arguments[2], CultureInfo.InvariantCulture);
                settings.ItemDropMultiplier = double.Parse(arguments[3], CultureInfo.InvariantCulture);
                break;
            case (6, "spots"):
                settings.MonstersPerMap = int.Parse(arguments[2], CultureInfo.InvariantCulture);
                settings.MonstersPerSpot = int.Parse(arguments[3], CultureInfo.InvariantCulture);
                settings.RespawnSeconds = int.Parse(arguments[4], CultureInfo.InvariantCulture);
                settings.SpotSpacing = int.Parse(arguments[5], CultureInfo.InvariantCulture);
                break;
            case (3, "apply"):
                settings.ActiveMonsterMaps = arguments[2] == "all" ? settings.EligibleMaps : arguments[2];
                break;
            case (3, "cleanup"):
                settings.ActiveMonsterMaps = arguments[2] == "all" ? string.Empty
                    : string.Join(',', settings.ActiveMonsterMaps.Split(',').Where(m => m.Trim() != arguments[2]));
                break;
            case (10, "crowd"):
                settings.CrowdCount = int.Parse(arguments[2], CultureInfo.InvariantCulture);
                settings.CrowdMap = short.Parse(arguments[3], CultureInfo.InvariantCulture);
                settings.CrowdX = byte.Parse(arguments[4], CultureInfo.InvariantCulture);
                settings.CrowdY = byte.Parse(arguments[5], CultureInfo.InvariantCulture);
                settings.CrowdRadius = int.Parse(arguments[6], CultureInfo.InvariantCulture);
                settings.CrowdBehavior = arguments[7];
                settings.CrowdLifetimeSeconds = int.Parse(arguments[8], CultureInfo.InvariantCulture);
                settings.CrowdLevel = int.Parse(arguments[9], CultureInfo.InvariantCulture);
                settings.CrowdExpiresUtc = DateTime.UtcNow.AddSeconds(settings.CrowdLifetimeSeconds);
                settings.CrowdExcludedNames = string.Empty;
                break;
            case (3, "remove"):
                if (arguments[2] == "all")
                {
                    settings.CrowdCount = 0;
                }
                else
                {
                    settings.CrowdExcludedNames = string.Join(',', settings.CrowdExcludedNames.Split(',', StringSplitOptions.RemoveEmptyEntries).Append(arguments[2]).Distinct(StringComparer.OrdinalIgnoreCase));
                }

                break;
            case (2, "stop"):
                settings.CrowdCount = 0;
                settings.ActiveMonsterMaps = string.Empty;
                settings.Enabled = false;
                break;
            default:
                throw new FormatException();
        }
    }

    private static async ValueTask SendStatusAsync(Player player, LocalTestingFeaturePlugIn feature)
    {
        var settings = feature.Configuration ?? new();
        var game = player.GameContext.Configuration;
        var connected = (await player.GameContext.GetPlayersAsync().ConfigureAwait(false))
            .Count(p => p.Account?.IsBot == false && p is not Offline.OfflinePlayer);
        var cap = game.Attributes.FirstOrDefault(a => a == Stats.AttackSpeed)?.MaximumValue ?? 200;
        var stat = game.Attributes.FirstOrDefault(a => a == Stats.BaseStrength)?.MaximumValue ?? 0;
        await player.ShowBlueMessageAsync(FormattableString.Invariant($"@LT1 status {(settings.Enabled ? 1 : 0)} {settings.NormalExperienceRate} {settings.ItemDropMultiplier} {settings.MaximumAttackSpeed} {settings.MaximumStat} {(player.SelectedCharacter?.CharacterStatus == CharacterStatus.GameMaster ? 1 : 0)}")).ConfigureAwait(false);
        await player.ShowBlueMessageAsync(FormattableString.Invariant($"@LT1 effective {player.GameContext.ExperienceRate} {player.GameContext.MasterExperienceRate} {cap} {stat} {connected} {settings.MonstersPerMap} {settings.MonstersPerSpot} {settings.RespawnSeconds} {settings.SpotSpacing}")).ConfigureAwait(false);
        if (feature.Metrics() is { } metrics)
        {
            await player.ShowBlueMessageAsync(FormattableString.Invariant($"@LT1 metrics {metrics.Cpu:F2} {metrics.MemoryMiB:F2} {metrics.MaintenanceMs:F2} {metrics.Monsters}")).ConfigureAwait(false);
        }

        var eligible = settings.EligibleMaps.Split(',').ToHashSet();
        foreach (var map in game.Maps.Where(m => m.Discriminator == 0).DistinctBy(m => m.Number).Take(100))
        {
            var allowed = eligible.Contains(map.Number.ToString(CultureInfo.InvariantCulture)) && TestingMapAvailability.IsHosted(player.GameContext, map.Number);
            await player.ShowBlueMessageAsync($"@LT1 map {map.Number} {(allowed ? 1 : 0)} {map.Name.GetTranslation(player.Culture)}").ConfigureAwait(false);
        }

        if (player.GameContext.FeaturePlugIns.GetPlugIn<ResetFeaturePlugIn>()?.Configuration is { } reset)
        {
            await TestingResetStatus.SendAsync(player, reset).ConfigureAwait(false);
        }

        var groups = TestingPreset.OrdinaryGroups(game, settings).ToArray();
        await player.ShowBlueMessageAsync(FormattableString.Invariant($"@LT1 drop {(settings.Enabled ? settings.ItemDropMultiplier : 1)} {groups.Length} {groups.Count(g => g.Chance >= 1)}")).ConfigureAwait(false);
        var crowd = feature.Crowd(player.GameContext).ToArray();
        await player.ShowBlueMessageAsync(FormattableString.Invariant($"@LT1 crowd {settings.CrowdCount} {crowd.Length} {settings.CrowdMap} {settings.CrowdBehavior} {settings.CrowdLifetimeSeconds}")).ConfigureAwait(false);
        foreach (var member in crowd)
        {
            await player.ShowBlueMessageAsync($"@LT1 bot {member.Name} {member.Class}").ConfigureAwait(false);
        }

        foreach (var map in game.Maps.Where(m => m.Discriminator == 0 && settings.ActiveMonsterMaps.Split(',').Contains(m.Number.ToString(CultureInfo.InvariantCulture))))
        {
            var population = feature.Population(player.GameContext, map.Number);
            await player.ShowBlueMessageAsync(FormattableString.Invariant($"@LT1 population {map.Number} {settings.MonstersPerMap} {population.Allocated} {population.Alive}")).ConfigureAwait(false);
        }
    }

    private static async ValueTask PreviewAsync(Player player, LocalTestingConfiguration settings, string selection)
    {
        foreach (var map in player.GameContext.Configuration.Maps.Where(m => m.Discriminator == 0
            && (selection == "all" || m.Number.ToString(CultureInfo.InvariantCulture) == selection)))
        {
            var plan = TestingSpawnPlanner.Preview(map, settings);
            if (!TestingMapAvailability.IsHosted(player.GameContext, map.Number))
            {
                plan.Positions.Clear();
                plan.Reason = "not_hosted";
            }

            await player.ShowBlueMessageAsync(FormattableString.Invariant($"@LT1 preview {map.Number} {plan.Requested} {plan.Positions.Count} {(string.IsNullOrEmpty(plan.Reason) ? "ok" : plan.Reason)}")).ConfigureAwait(false);
        }
    }
}
