// <copyright file="LocalTestingPresetTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.LocalTesting;
using Model = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>Verifies reversibility, exclusions and deterministic safe population planning.</summary>
[TestFixture]
public class LocalTestingPresetTests
{
    /// <summary>Custom targets must never create a lone monster outside a two- or three-monster spot.</summary>
    [TestCase(30, 3, 30)]
    [TestCase(100, 3, 100)]
    [TestCase(101, 2, 100)]
    [TestCase(151, 3, 151)]
    public void CustomTargetsKeepWholeSpots(int target, int group, int expected)
    {
        var map = new Model.GameMapDefinition { Number = 0, TerrainData = new byte[65539] };
        map.MonsterSpawns.Add(new Model.MonsterSpawnArea
        {
            SpawnTrigger = SpawnTrigger.Automatic,
            MonsterDefinition = new Model.MonsterDefinition { ObjectKind = NpcObjectKind.Monster },
        });
        var plan = TestingSpawnPlanner.Preview(map, new LocalTestingConfiguration { MonstersPerMap = target, MonstersPerSpot = group });
        Assert.That(plan.Positions, Has.Count.EqualTo(expected));
        foreach (var point in plan.Positions)
        {
            var neighbors = plan.Positions.Count(p => p != point && Math.Abs(p.X - point.X) <= 2 && Math.Abs(p.Y - point.Y) <= 2);
            Assert.That(neighbors, Is.InRange(1, 2));
        }
    }

    /// <summary>Repeated applies never compound probabilities and a changed map selection still restores the original tables.</summary>
    [Test]
    public void PresetRestoresAfterMapSelectionChanges()
    {
        var game = new Model.GameConfiguration { ExperienceRate = 9999, MasterExperienceRate = 999 };
        var map = new Model.GameMapDefinition { Number = 0 };
        game.Maps.Add(map);
        var item = new Model.DropItemGroup { Id = Guid.NewGuid(), Chance = 0.0001 };
        var money = new Model.DropItemGroup { Id = Guid.NewGuid(), Chance = 0.5, ItemType = SpecialItemType.Money };
        map.DropItemGroups.Add(item);
        map.DropItemGroups.Add(money);
        var speed = new Model.AttributeDefinition { Id = Stats.AttackSpeed.Id, MaximumValue = 200 };
        var strength = new Model.AttributeDefinition { Id = Stats.BaseStrength.Id };
        game.Attributes.Add(speed);
        game.Attributes.Add(strength);
        var settings = new LocalTestingConfiguration { Enabled = true };
        TestingPreset.Capture(game, settings);
        TestingPreset.Apply(game, settings);
        TestingPreset.Apply(game, settings);
        Assert.Multiple(() =>
        {
            Assert.That(game.ExperienceRate, Is.EqualTo(10000));
            Assert.That(game.MasterExperienceRate, Is.EqualTo(999));
            Assert.That(item.Chance, Is.EqualTo(0.1).Within(0.000001));
            Assert.That(money.Chance, Is.EqualTo(0.5));
            Assert.That(speed.MaximumValue, Is.EqualTo(800));
            Assert.That(strength.MaximumValue, Is.EqualTo(65535));
        });
        settings.EligibleMaps = "2";
        TestingPreset.Restore(game, settings);
        Assert.Multiple(() =>
        {
            Assert.That(game.ExperienceRate, Is.EqualTo(9999));
            Assert.That(game.PreventExperienceOverflow, Is.False);
            Assert.That(item.Chance, Is.EqualTo(0.0001));
            Assert.That(speed.MaximumValue, Is.EqualTo(200));
            Assert.That(strength.MaximumValue, Is.Null);
        });
    }

    /// <summary>A preview must leave natural spawns intact and choose reproducible points outside town and gate areas.</summary>
    [Test]
    public void PlannerKeepsClearOfTownAndGates()
    {
        var map = new Model.GameMapDefinition { Number = 0, TerrainData = new byte[65539] };
        for (var y = 90; y <= 130; y++)
        {
            for (var x = 90; x <= 130; x++)
            {
                map.TerrainData[3 + (y * 256) + x] = 1;
            }
        }
        map.ExitGates.Add(new Model.ExitGate { X1 = 60, X2 = 65, Y1 = 60, Y2 = 65 });
        map.MonsterSpawns.Add(new Model.MonsterSpawnArea
        {
            SpawnTrigger = SpawnTrigger.Automatic,
            MonsterDefinition = new Model.MonsterDefinition { ObjectKind = NpcObjectKind.Monster },
        });
        var settings = new LocalTestingConfiguration();
        var first = TestingSpawnPlanner.Preview(map, settings);
        var second = TestingSpawnPlanner.Preview(map, settings);
        Assert.Multiple(() =>
        {
            Assert.That(first.Positions, Has.Count.EqualTo(30));
            Assert.That(first.Positions.Distinct().Count(), Is.EqualTo(30));
            Assert.That(second.Positions, Is.EqualTo(first.Positions));
            Assert.That(first.Positions.Any(p => p.X >= 88 && p.X <= 132 && p.Y >= 88 && p.Y <= 132), Is.False);
            Assert.That(first.Positions.Any(p => p.X >= 54 && p.X <= 71 && p.Y >= 54 && p.Y <= 71), Is.False);
            Assert.That(map.MonsterSpawns, Has.Count.EqualTo(1));
        });
        map.Discriminator = 1;
        Assert.That(TestingSpawnPlanner.Preview(map, settings).Reason, Is.EqualTo("restricted_map"));
        map.Discriminator = 0;
        map.Number = 58;
        Assert.That(TestingSpawnPlanner.Preview(map, settings).Reason, Is.EqualTo("restricted_map"));
    }
}
