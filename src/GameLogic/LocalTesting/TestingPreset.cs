// <copyright file="TestingPreset.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

using System.Globalization;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;

/// <summary>Applies the reversible preset through existing game-configuration properties.</summary>
public static class TestingPreset
{
    private static readonly AttributeDefinition[] StatDefinitions =
    [
        Stats.BaseStrength, Stats.BaseAgility, Stats.BaseVitality, Stats.BaseEnergy, Stats.BaseLeadership,
        Stats.TotalStrength, Stats.TotalAgility, Stats.TotalVitality, Stats.TotalEnergy, Stats.TotalLeadership,
    ];

    /// <summary>Captures original values once, before enabling the preset.</summary>
    /// <param name="game">The installed configuration.</param>
    /// <param name="settings">The persisted plugin settings and rollback values.</param>
    public static void Capture(GameConfiguration game, LocalTestingConfiguration settings)
    {
        settings.OriginalExperienceRate ??= game.ExperienceRate;
        settings.OriginalPreventExperienceOverflow ??= game.PreventExperienceOverflow;
        foreach (var group in OrdinaryGroups(game, settings))
        {
            settings.DropBaselines.TryAdd(group.GetId(), group.Chance);
        }

        foreach (var definition in CappedDefinitions(game))
        {
            settings.CapBaselines.TryAdd(definition.Id, definition.MaximumValue);
        }
    }

    /// <summary>Applies values relative to the captured baseline, never multiplying an already adjusted chance.</summary>
    /// <param name="game">The live configuration.</param>
    /// <param name="settings">The validated preset.</param>
    public static void Apply(GameConfiguration game, LocalTestingConfiguration settings)
    {
        Validator.ValidateObject(settings, new ValidationContext(settings), true);
        if (!settings.OriginalExperienceRate.HasValue)
        {
            throw new InvalidOperationException("Capture and persist the original settings before applying the preset.");
        }

        game.ExperienceRate = settings.NormalExperienceRate;
        game.PreventExperienceOverflow = true;
        foreach (var group in OrdinaryGroups(game, settings))
        {
            if (settings.DropBaselines.TryGetValue(group.GetId(), out var baseline))
            {
                group.Chance = TestingRateMath.DropProbability(baseline, settings.ItemDropMultiplier);
            }
        }

        foreach (var definition in CappedDefinitions(game))
        {
            definition.MaximumValue = StatDefinitions.Contains(definition) ? settings.MaximumStat : settings.MaximumAttackSpeed;
        }
    }

    /// <summary>Restores only the properties owned by this preset.</summary>
    /// <param name="game">The live configuration.</param>
    /// <param name="settings">The persisted rollback snapshot.</param>
    public static void Restore(GameConfiguration game, LocalTestingConfiguration settings)
    {
        if (settings.OriginalExperienceRate is not { } original)
        {
            return;
        }

        game.ExperienceRate = original;
        game.PreventExperienceOverflow = settings.OriginalPreventExperienceOverflow ?? false;
        var allGroups = game.DropItemGroups.Concat(game.Maps.SelectMany(m => m.DropItemGroups
            .Concat(m.MonsterSpawns.SelectMany(s => s.MonsterDefinition?.DropItemGroups ?? []))));
        foreach (var group in allGroups.DistinctBy(g => g.GetId()))
        {
            if (settings.DropBaselines.TryGetValue(group.GetId(), out var chance))
            {
                group.Chance = chance;
            }
        }

        foreach (var definition in CappedDefinitions(game))
        {
            if (settings.CapBaselines.TryGetValue(definition.Id, out var cap))
            {
                definition.MaximumValue = cap;
            }
        }
    }

    /// <summary>Finds ordinary monster/map item tables; money, quest and item/event reward tables are excluded.</summary>
    /// <param name="game">The installed configuration.</param>
    /// <param name="settings">The eligible-map selection.</param>
    /// <returns>Distinct eligible drop groups.</returns>
    public static IEnumerable<DropItemGroup> OrdinaryGroups(GameConfiguration game, LocalTestingConfiguration settings)
    {
        var allowed = settings.EligibleMaps.Split(',').Select(s => short.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToHashSet();
        var maps = game.Maps.Where(m => allowed.Contains(m.Number) && m.Discriminator == 0);
        return maps.SelectMany(m => m.DropItemGroups.Concat(m.MonsterSpawns
                .Where(s => s.SpawnTrigger == SpawnTrigger.Automatic && s.MonsterDefinition?.ObjectKind == NpcObjectKind.Monster)
                .SelectMany(s => s.MonsterDefinition!.DropItemGroups)))
            .Where(g => g is not ItemDropItemGroup && g.ItemType != SpecialItemType.Money
                && g.Monster is null && !g.PossibleItems.Any(i => i.IsQuestItem))
            .DistinctBy(g => g.GetId());
    }

    private static IEnumerable<AttributeDefinition> CappedDefinitions(GameConfiguration game)
        => game.Attributes.Where(a => StatDefinitions.Contains(a) || a == Stats.AttackSpeed || a == Stats.MagicSpeed);
}
