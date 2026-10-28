// <copyright file="OwnedTestingSpawns.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

using System.Collections.Concurrent;
using MUnique.OpenMU.GameLogic.NPC;

/// <summary>Owns transient test monsters separately from configured natural spawns.</summary>
internal sealed class OwnedTestingSpawns
{
    private readonly ConcurrentDictionary<short, ConcurrentBag<Monster>> _maps = [];

    /// <summary>Gets the number of owned monsters, including temporarily dead monsters awaiting respawn.</summary>
    public int Count => this._maps.Values.Sum(m => m.Count);

    /// <summary>Gets the configured map numbers which have owned populations.</summary>
    public IEnumerable<short> Maps => this._maps.Keys.ToArray();

    /// <summary>Gets the allocated and currently alive populations on one map.</summary>
    /// <param name="map">The map number.</param>
    /// <returns>The allocated and alive counts.</returns>
    public (int Allocated, int Alive) Population(short map)
        => this._maps.TryGetValue(map, out var monsters) ? (monsters.Count, monsters.Count(m => m.IsAlive && !m.IsDisposed)) : default;

    /// <summary>Replaces only this feature's population on one map.</summary>
    /// <param name="context">The running game server.</param>
    /// <param name="map">The map definition.</param>
    /// <param name="settings">The validated settings.</param>
    /// <param name="available">The remaining process-wide capacity, including this map's old population.</param>
    public async ValueTask ApplyAsync(IGameContext context, GameMapDefinition map, LocalTestingConfiguration settings, int available)
    {
        var plan = TestingSpawnPlanner.Preview(map, settings);
        if (plan.Positions.Count == 0 || plan.Positions.Count > available)
        {
            throw new InvalidOperationException($"Test population rejected for map {map.Number}: {plan.Reason}, capacity {available}.");
        }

        var liveMap = await context.GetMapAsync((ushort)map.Number).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The selected map is not active on this game server.");
        var source = map.MonsterSpawns.Where(s => s.SpawnTrigger == SpawnTrigger.Automatic
                && s.MonsterDefinition?.ObjectKind == NpcObjectKind.Monster
                && string.IsNullOrEmpty(s.MonsterDefinition.IntelligenceTypeName))
            .OrderBy(s => s.MonsterDefinition!.Number).Select(s => s.MonsterDefinition).FirstOrDefault()
            ?? throw new InvalidOperationException("No ordinary monster definition is available.");
        var definition = source.Clone(context.Configuration);
        definition.RespawnDelay = TimeSpan.FromSeconds(settings.RespawnSeconds);
        await this.RemoveAsync(map.Number).ConfigureAwait(false);
        var owned = new ConcurrentBag<Monster>();
        this._maps[map.Number] = owned;
        try
        {
            foreach (var point in plan.Positions)
            {
                var area = new MonsterSpawnArea
                {
                    GameMap = map,
                    MonsterDefinition = definition,
                    SpawnTrigger = SpawnTrigger.Automatic,
                    Quantity = 1,
                    X1 = point.X,
                    X2 = point.X,
                    Y1 = point.Y,
                    Y2 = point.Y,
                };
                var monster = new Monster(area, definition, liveMap, context.DropGenerator, new BasicMonsterIntelligence(), context.PlugInManager, context.PathFinderPool);
                owned.Add(monster);
                monster.Initialize();
                await liveMap.AddAsync(monster).ConfigureAwait(false);
                monster.OnSpawn();
            }
        }
        catch
        {
            await this.RemoveAsync(map.Number).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Stops pending respawns before disposing owned entities.</summary>
    /// <param name="map">One map, or null for all owned populations.</param>
    public async ValueTask RemoveAsync(short? map = null)
    {
        foreach (var number in this._maps.Keys.Where(n => map is null || map == n).ToArray())
        {
            var monsters = this._maps[number];
            foreach (var monster in monsters)
            {
                monster.SpawnArea.SpawnTrigger = SpawnTrigger.OnceAtEventStart;
                await monster.DisposeAsync().ConfigureAwait(false);
            }

            this._maps.TryRemove(number, out _);
        }
    }
}
