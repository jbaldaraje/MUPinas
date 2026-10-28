// <copyright file="NewNpcsInScopeRemoteViewTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.GameServer.RemoteView.World;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Pathfinding;

/// <summary>Tests NPC visibility packets when a player enters a populated map.</summary>
[TestFixture]
public class NewNpcsInScopeRemoteViewTests
{
    private const int NpcCount = byte.MaxValue + 45;

    /// <summary>A map with more than 255 visible NPCs still sends complete, ordered packets.</summary>
    /// <param name="version">The view packet variant.</param>
    [TestCase(0)]
    [TestCase(75)]
    [TestCase(95)]
    public async ValueTask PopulatedMapUsesMultipleNpcPacketsAsync(int version)
    {
        var (observer, output) = CastleSiegeRemoteViewTestHelper.CreatePlayer();
        INewNpcsInScopePlugIn view = version switch
        {
            75 => new NewNpcsInScopePlugIn075(observer),
            95 => new NewNpcsInScopePlugIn095(observer),
            _ => new NewNpcsInScopePlugIn(observer),
        };
        var map = new GameMap(new GameMapDefinition(), TimeSpan.FromSeconds(10), 8);
        var definition = new MonsterDefinition { Number = 20 };
        var npcs = Enumerable.Range(1, NpcCount)
            .Select(id => new NonPlayerCharacter(new MonsterSpawnArea(), definition, map)
            {
                Id = (ushort)id,
                Position = new Point(23, 26),
            })
            .ToArray();

        await view.NewNpcsInScopeAsync(npcs, false).ConfigureAwait(false);

        var bytes = output.ToArray();
        var firstLength = GetRequiredSize(version, byte.MaxValue);
        var secondLength = GetRequiredSize(version, NpcCount - byte.MaxValue);
        Assert.Multiple(() =>
        {
            Assert.That(bytes, Has.Length.EqualTo(firstLength + secondLength));
            Assert.That(bytes[0], Is.EqualTo(0xC2));
            Assert.That(bytes[4], Is.EqualTo(byte.MaxValue));
            Assert.That(bytes[firstLength], Is.EqualTo(0xC2));
            Assert.That(bytes[firstLength + 4], Is.EqualTo(NpcCount - byte.MaxValue));
            Assert.That((bytes[1] << 8) | bytes[2], Is.EqualTo(firstLength));
            Assert.That((bytes[firstLength + 1] << 8) | bytes[firstLength + 2], Is.EqualTo(secondLength));
            Assert.That((bytes[firstLength + 5] << 8) | bytes[firstLength + 6], Is.EqualTo(byte.MaxValue + 1));
        });
    }

    private static int GetRequiredSize(int version, int count) => version switch
    {
        75 => AddNpcsToScope075Ref.GetRequiredSize(count),
        95 => AddNpcsToScope095Ref.GetRequiredSize(count),
        _ => AddNpcsToScopeRef.GetRequiredSize(count),
    };
}
