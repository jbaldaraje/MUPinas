// <copyright file="EnterMarketPlaceActionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests for the Loren Market warp action.
/// </summary>
[TestFixture]
public class EnterMarketPlaceActionTest
{
    /// <summary>
    /// Verifies that the shortcut warps between Lorencia and the Loren Market using valid spawn gates.
    /// </summary>
    [Test]
    public async Task ShortcutWarpsToMarketAndBackAsync()
    {
        var (gameContext, lorencia, market, _) = CreateGameContext();
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
        var action = new EnterMarketPlaceAction();

        await action.WarpToMarketPlaceFromCommandAsync(player).ConfigureAwait(false);

        Assert.That(player.CurrentMap, Is.Null);
        Assert.That(player.SelectedCharacter!.CurrentMap, Is.SameAs(market));
        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
        Assert.That(player.CurrentMap!.Definition, Is.SameAs(market));
        Assert.That(player.PlayerState.CurrentState, Is.SameAs(PlayerState.EnteredWorld));

        await action.WarpToMarketPlaceFromCommandAsync(player).ConfigureAwait(false);

        Assert.That(player.CurrentMap, Is.Null);
        Assert.That(player.SelectedCharacter.CurrentMap, Is.SameAs(lorencia));
        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
        Assert.That(player.CurrentMap!.Definition, Is.SameAs(lorencia));
        Assert.That(player.PlayerState.CurrentState, Is.SameAs(PlayerState.EnteredWorld));
    }

    /// <summary>
    /// Verifies that the shortcut does not allow teleporting from an unrelated map.
    /// </summary>
    [Test]
    public async Task ShortcutDoesNotWarpFromAnUnrelatedMapAsync()
    {
        var (gameContext, _, market, unrelatedMap) = CreateGameContext();
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
        await player.WarpToAsync(unrelatedMap.ExitGates.Single()).ConfigureAwait(false);
        await player.ClientReadyAfterMapChangeAsync().ConfigureAwait(false);
        var currentMap = player.CurrentMap;

        await new EnterMarketPlaceAction().WarpToMarketPlaceFromCommandAsync(player).ConfigureAwait(false);

        Assert.That(player.CurrentMap, Is.SameAs(currentMap));
        Assert.That(player.SelectedCharacter!.CurrentMap, Is.SameAs(unrelatedMap));
        Assert.That(market.Number, Is.EqualTo(79));
    }

    private static (IGameContext Context, GameMapDefinition Lorencia, GameMapDefinition Market, GameMapDefinition Unrelated) CreateGameContext()
    {
        var gameConfiguration = PlayerTestHelper.CreateGameConfiguration();
        var lorencia = gameConfiguration.Maps.Single();
        lorencia.Number = 0;
        lorencia.IsActive = true;
        AddSpawnGate(lorencia);

        var market = CreateMap(79);
        AddSpawnGate(market);
        gameConfiguration.Maps.Add(market);

        var unrelatedMap = CreateMap(1);
        AddSpawnGate(unrelatedMap);
        gameConfiguration.Maps.Add(unrelatedMap);

        var mapInitializer = new MapInitializer(gameConfiguration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(
            gameConfiguration,
            new InMemoryPersistenceContextProvider(),
            mapInitializer,
            new NullLoggerFactory(),
            new PlugInManager(null, new NullLoggerFactory(), null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;

        return (gameContext, lorencia, market, unrelatedMap);
    }

    private static GameMapDefinition CreateMap(short number)
    {
        var map = new Mock<GameMapDefinition>();
        map.SetupAllProperties();
        map.Setup(definition => definition.DropItemGroups).Returns([]);
        map.Setup(definition => definition.MonsterSpawns).Returns([]);
        map.Setup(definition => definition.ExitGates).Returns(new List<ExitGate>());
        map.Object.Number = number;
        map.Object.IsActive = true;
        map.Object.TerrainData = new byte[ushort.MaxValue + 3];
        return map.Object;
    }

    private static void AddSpawnGate(GameMapDefinition map)
    {
        map.ExitGates.Add(new ExitGate
        {
            Map = map,
            X1 = 10,
            X2 = 13,
            Y1 = 10,
            Y2 = 13,
            IsSpawnGate = true,
        });
    }
}
