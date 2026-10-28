// <copyright file="TestingSpawnPlanner.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

using MUnique.OpenMU.Pathfinding;

/// <summary>Plans deterministic, separated test spots without modifying natural spawns.</summary>
public static class TestingSpawnPlanner
{
    private const int MapSide = 256;
    private const int InteractionClearance = 6;
    private const int WallClearance = 2;

    /// <summary>Plans a population on one map, keeping all points outside unsafe areas.</summary>
    /// <param name="map">The installed map definition.</param>
    /// <param name="settings">The bounded preset settings.</param>
    /// <returns>The proposed points or an explicit exclusion reason.</returns>
    public static TestingMapPlan Preview(GameMapDefinition map, LocalTestingConfiguration settings)
    {
        Validator.ValidateObject(settings, new ValidationContext(settings), true);
        var plan = new TestingMapPlan { Map = map.Number, Requested = settings.MonstersPerMap };
        var allowed = settings.EligibleMaps.Split(',').Select(s => short.TryParse(s.Trim(), out var n) ? n : (short)-1);
        if (!allowed.Contains(map.Number) || map.Discriminator != 0)
        {
            plan.Reason = "restricted_map";
            return plan;
        }

        if (map.TerrainData is not { Length: >= (MapSide * MapSide) + 3 })
        {
            plan.Reason = "terrain_missing";
            return plan;
        }

        if (!map.MonsterSpawns.Any(s => s.SpawnTrigger == SpawnTrigger.Automatic && s.MonsterDefinition?.ObjectKind == NpcObjectKind.Monster
            && string.IsNullOrEmpty(s.MonsterDefinition.IntelligenceTypeName)))
        {
            plan.Reason = "no_natural_monster_definition";
            return plan;
        }

        return FillPlan(map, settings, plan);
    }

    private static TestingMapPlan FillPlan(GameMapDefinition map, LocalTestingConfiguration settings, TestingMapPlan plan)
    {
        var terrain = new GameMapTerrain(map);
        var candidates = new List<Point>();
        for (var y = InteractionClearance; y < MapSide - InteractionClearance; y++)
        {
            for (var x = InteractionClearance; x < MapSide - InteractionClearance; x++)
            {
                var point = new Point((byte)x, (byte)y);
                if (IsClear(map, terrain, point))
                {
                    candidates.Add(point);
                }
            }
        }

        new Random(map.Number).Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(candidates));
        var centers = new List<Point>();
        foreach (var center in candidates)
        {
            if (centers.Any(p => Distance(p, center) < settings.SpotSpacing))
            {
                continue;
            }

            var remaining = plan.Requested - plan.Positions.Count;
            if (remaining < 2)
            {
                plan.Reason = "partial_group";
                return plan;
            }

            // Finish four remaining monsters as two pairs, rather than a triple and a singleton.
            var groupSize = settings.MonstersPerSpot == 3 && remaining is 2 or 4 ? 2 : settings.MonstersPerSpot;
            var group = GroupPoints(center, groupSize).ToArray();
            if (group.Any(p => !IsClear(map, terrain, p)))
            {
                continue;
            }

            centers.Add(center);
            plan.Positions.AddRange(group);
            if (plan.Positions.Count == plan.Requested)
            {
                return plan;
            }
        }

        plan.Reason = "insufficient_clear_ground";
        return plan;
    }

    private static IEnumerable<Point> GroupPoints(Point center, int count)
    {
        yield return center;
        yield return new Point((byte)(center.X + 1), center.Y);
        if (count == 3)
        {
            yield return new Point(center.X, (byte)(center.Y + 1));
        }
    }

    private static bool IsClear(GameMapDefinition map, GameMapTerrain terrain, Point point)
    {
        for (var y = point.Y - WallClearance; y <= point.Y + WallClearance; y++)
        {
            for (var x = point.X - WallClearance; x <= point.X + WallClearance; x++)
            {
                if (x < 0 || y < 0 || x >= MapSide || y >= MapSide || !terrain.WalkMap[x, y] || terrain.SafezoneMap[x, y])
                {
                    return false;
                }
            }
        }

        return !map.EnterGates.Any(g => Near(point, g.X1, g.Y1, g.X2, g.Y2))
            && !map.ExitGates.Any(g => Near(point, g.X1, g.Y1, g.X2, g.Y2))
            && !map.MonsterSpawns.Any(s => s.MonsterDefinition?.ObjectKind != NpcObjectKind.Monster && Near(point, s.X1, s.Y1, s.X2, s.Y2));
    }

    private static bool Near(Point point, byte x1, byte y1, byte x2, byte y2)
        => point.X >= x1 - InteractionClearance && point.X <= x2 + InteractionClearance
            && point.Y >= y1 - InteractionClearance && point.Y <= y2 + InteractionClearance;

    private static int Distance(Point a, Point b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
