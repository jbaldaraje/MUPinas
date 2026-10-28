// <copyright file="ResetCharacterActionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests for <see cref="ResetCharacterAction"/>.
/// </summary>
[TestFixture]
public class ResetCharacterActionTest
{
    /// <summary>
    /// Verifies that configurations which only use the legacy flag retain their class-home behavior.
    /// </summary>
    [Test]
    public void LegacyMoveHomeSettingDoesNotAssumeLorencia()
    {
        var configuration = new ResetConfiguration();

        Assert.That(configuration.Destination, Is.EqualTo(ResetDestination.ClassHome));

        configuration.MoveHome = false;
        Assert.That(configuration.Destination, Is.EqualTo(ResetDestination.StayOnCurrentMap));
    }

    /// <summary>
    /// Verifies that reset is rejected when required items are missing, without charging zen.
    /// </summary>
    [Test]
    public async Task NotEnoughItemsRejectsResetAndKeepsZenAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;

        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);

        await AddRequiredItemsAsync(player, requiredItem, 1).ConfigureAwait(false);
        var action = new ResetCharacterAction(player, await CreateResetNpcAsync(player).ConfigureAwait(false));

        await action.ResetCharacterAsync().ConfigureAwait(false);

        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(0));
        Assert.That(player.Money, Is.EqualTo(1_000));
        Assert.That(player.Inventory!.Items.Count(i => i.Definition == requiredItem), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that reset consumes configured zen and required items when all conditions are met.
    /// </summary>
    [Test]
    public async Task EnoughItemsAndZenConsumesCostsOnceAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;

        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);

        await AddRequiredItemsAsync(player, requiredItem, 2).ConfigureAwait(false);
        Assert.That(player.Level, Is.EqualTo(400));
        Assert.That(player.Money, Is.EqualTo(1_000));
        Assert.That(
            player.Inventory!.Items.Count(i => i.Definition is { } definition && definition.Group == requiredItem.Group && definition.Number == requiredItem.Number),
            Is.EqualTo(2));
        var progression = ResetProgressionCalculator.Calculate((int)player.Attributes[Stats.Resets], (int)player.Attributes[Stats.PointsPerReset], configuration);
        Assert.That(progression.RequiredItemAmount, Is.EqualTo(2));
        Assert.That(progression.RequiredZen, Is.EqualTo(500));
        var action = new ResetCharacterAction(player, await CreateResetNpcAsync(player).ConfigureAwait(false));

        await action.ResetCharacterAsync().ConfigureAwait(false);

        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(1));
        Assert.That(player.Money, Is.EqualTo(500));
        Assert.That(player.Inventory!.Items.Count(i => i.Definition == requiredItem), Is.EqualTo(0));
        Assert.That(player.SelectedCharacter!.LevelUpPoints, Is.EqualTo(800));
        Assert.That((int)player.Attributes[Stats.Level], Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies cumulative point replacement across multiple resets when point tiers are configured.
    /// </summary>
    [Test]
    public async Task ReplacePointsUsesCumulativeTierTotalAcrossResetsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 2_000;

        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);

        await AddRequiredItemsAsync(player, requiredItem, 4).ConfigureAwait(false);
        var action = new ResetCharacterAction(player, await CreateResetNpcAsync(player).ConfigureAwait(false));

        await action.ResetCharacterAsync().ConfigureAwait(false);
        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(1));
        Assert.That(player.SelectedCharacter!.LevelUpPoints, Is.EqualTo(800));
        Assert.That(player.Money, Is.EqualTo(1_500));

        player.Attributes[Stats.Level] = 400;
        await action.ResetCharacterAsync().ConfigureAwait(false);

        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(2));
        Assert.That(player.SelectedCharacter!.LevelUpPoints, Is.EqualTo(1_600));
        Assert.That(player.Money, Is.EqualTo(500));
    }

    /// <summary>
    /// Verifies add mode behavior when replace mode is disabled.
    /// </summary>
    [Test]
    public async Task TieredPointsAreAddedWhenReplaceIsDisabledAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;
        player.SelectedCharacter!.LevelUpPoints = 1_000;

        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        configuration.ReplacePointsPerReset = false;
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);

        await AddRequiredItemsAsync(player, requiredItem, 2).ConfigureAwait(false);
        var action = new ResetCharacterAction(player, await CreateResetNpcAsync(player).ConfigureAwait(false));

        await action.ResetCharacterAsync().ConfigureAwait(false);

        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(1));
        Assert.That(player.SelectedCharacter!.LevelUpPoints, Is.EqualTo(1_800));
    }

    /// <summary>
    /// Verifies that an explicit stay destination preserves the live map and position and never logs out.
    /// </summary>
    [Test]
    public async Task StayDestinationKeepsPositionAndDoesNotLogOutAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.IsAlive = true;
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;

        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        configuration.Destination = ResetDestination.StayOnCurrentMap;
        configuration.MoveHome = true;
        configuration.LogOut = true;
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);
        await AddRequiredItemsAsync(player, requiredItem, 2).ConfigureAwait(false);
        var map = player.CurrentMap;
        var position = player.Position;
        var action = new ResetCharacterAction(player, await CreateResetNpcAsync(player).ConfigureAwait(false));

        await action.ResetCharacterAsync().ConfigureAwait(false);

        Assert.That(player.CurrentMap, Is.SameAs(map));
        Assert.That(player.Position, Is.EqualTo(position));
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(1));
        Assert.That((int)player.Attributes[Stats.Level], Is.EqualTo(1));
        Assert.That(player.Money, Is.EqualTo(500));
    }

    /// <summary>
    /// Verifies that returning to Lorencia uses the normal map-transition handshake and a walkable spawn tile.
    /// </summary>
    [Test]
    public async Task LorenciaDestinationUsesMapTransitionAsync()
    {
        var player = await CreatePlayerWithLorenciaSpawnGateAsync().ConfigureAwait(false);
        player.IsAlive = true;
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;

        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        configuration.Destination = ResetDestination.Lorencia;
        configuration.LogOut = false;
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);
        await AddRequiredItemsAsync(player, requiredItem, 2).ConfigureAwait(false);
        player.Position = new MUnique.OpenMU.Pathfinding.Point(1, 1);
        var action = new ResetCharacterAction(player, await CreateResetNpcAsync(player).ConfigureAwait(false));

        await action.ResetCharacterAsync().ConfigureAwait(false);

        Assert.That(player.CurrentMap, Is.Null);
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.ChangingMap));
        Assert.That(player.SelectedCharacter!.CurrentMap!.Number, Is.EqualTo(0));
        Assert.That(player.Position, Is.EqualTo(new MUnique.OpenMU.Pathfinding.Point(30, 40)));
        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that two overlapping reset requests cannot both consume the same reset opportunity.
    /// </summary>
    [Test]
    public async Task ConcurrentResetRequestsAreSerializedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.IsAlive = true;
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;

        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);
        await AddRequiredItemsAsync(player, requiredItem, 2).ConfigureAwait(false);
        var action = new ResetCharacterAction(player, await CreateResetNpcAsync(player).ConfigureAwait(false));

        await Task.WhenAll(action.ResetCharacterAsync().AsTask(), action.ResetCharacterAsync().AsTask()).ConfigureAwait(false);

        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(1));
        Assert.That(player.Money, Is.EqualTo(500));
        Assert.That(player.Inventory!.Items.Count(i => i.Definition == requiredItem), Is.EqualTo(0));
    }

    private static async ValueTask AddRequiredItemsAsync(Player player, ItemDefinition requiredItem, int count)
    {
        for (byte i = 0; i < count; i++)
        {
            var item = player.PersistenceContext.CreateNew<Item>();
            item.Definition = requiredItem;
            item.Durability = 1;
            var added = await player.Inventory!.AddItemAsync(item).ConfigureAwait(false);
            Assert.That(added, Is.True);
        }
    }

    private static async ValueTask<NonPlayerCharacter> CreateResetNpcAsync(Player player)
    {
        var spawnArea = new MonsterSpawnArea
        {
            X1 = 125,
            X2 = 125,
            Y1 = 125,
            Y2 = 125,
        };

        var definition = new MonsterDefinition
        {
            Number = ResetCharacterNpcPlugin.LeoTheHelperNumber,
            ObjectKind = NpcObjectKind.PassiveNpc,
            Designation = "Reset Helper",
        };

        var map = await player.GameContext.GetMapAsync(0).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Could not resolve map 0 for NPC test setup.");
        return new NonPlayerCharacter(spawnArea, definition, map);
    }

    private static async ValueTask<Player> CreatePlayerWithLorenciaSpawnGateAsync()
    {
        var gameConfiguration = PlayerTestHelper.CreateGameConfiguration();
        var lorenciaDefinition = gameConfiguration.Maps.Single();
        lorenciaDefinition.ExitGates.Add(new ExitGate
        {
            Map = lorenciaDefinition,
            X1 = 30,
            X2 = 30,
            Y1 = 40,
            Y2 = 40,
            IsSpawnGate = true,
        });

        var loggerFactory = new NullLoggerFactory();
        var mapInitializer = new MapInitializer(gameConfiguration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var gameContext = new GameContext(
            gameConfiguration,
            new InMemoryPersistenceContextProvider(),
            mapInitializer,
            loggerFactory,
            new PlugInManager(null, loggerFactory, null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;
        return await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
    }

    private ResetConfiguration CreateConfiguration(ItemDefinition requiredItem)
    {
        return new ResetConfiguration
        {
            RequiredLevel = 400,
            LevelAfterReset = 1,
            RequiredMoney = 500,
            MultiplyRequiredMoneyByResetCount = true,
            RequiredResetItem = requiredItem,
            ItemCostTiers =
            [
                new() { MinimumResetCount = 1, RequiredItemAmount = 2 },
            ],
            PointsTiers =
            [
                new() { MinimumResetCount = 1, PointsGranted = 800 },
            ],
            MoveHome = false,
            LogOut = false,
            ResetStats = false,
            ReplacePointsPerReset = true,
        };
    }
}
