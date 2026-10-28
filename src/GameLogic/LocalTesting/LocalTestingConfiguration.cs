// <copyright file="LocalTestingConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

using System.ComponentModel;

/// <summary>Admin-panel editable limits for the named macOS local testing preset.</summary>
public sealed class LocalTestingConfiguration
{
    /// <summary>Gets or sets a value indicating whether the preset is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the normal experience multiplier relative to the engine's 1x formula.</summary>
    [Range(1, 100000)]
    public float NormalExperienceRate { get; set; } = 10000;

    /// <summary>Gets or sets the ordinary item-drop probability multiplier.</summary>
    [Range(1, 10000)]
    public double ItemDropMultiplier { get; set; } = 1000;

    /// <summary>Gets or sets the cap for attack and magic speed.</summary>
    [Range(1, ushort.MaxValue)]
    public int MaximumAttackSpeed { get; set; } = 800;

    /// <summary>Gets or sets the base and displayed total stat cap supported by the client.</summary>
    [Range(1, ushort.MaxValue)]
    public int MaximumStat { get; set; } = ushort.MaxValue;

    /// <summary>Gets or sets ordinary maps allowed for testing; event, instanced and boss maps are absent by default.</summary>
    public string EligibleMaps { get; set; } = "0,1,2,3,4,7,8,10,33,37,38,51,56,57,63,80,81";

    /// <summary>Gets or sets the target number of generated monsters on a map.</summary>
    [Range(30, 200)]
    public int MonstersPerMap { get; set; } = 30;

    /// <summary>Gets or sets the number of monsters grouped in each spot.</summary>
    [Range(2, 3)]
    public int MonstersPerSpot { get; set; } = 3;

    /// <summary>Gets or sets the minimum distance between spot centers, in tiles.</summary>
    [Range(4, 64)]
    public int SpotSpacing { get; set; } = 12;

    /// <summary>Gets or sets the respawn delay of generated monsters, in seconds.</summary>
    [Range(1, 300)]
    public int RespawnSeconds { get; set; } = 3;

    /// <summary>Gets or sets the global limit of generated test monsters per game server.</summary>
    [Range(100, 10000)]
    public int GlobalMonsterLimit { get; set; } = 3000;

    /// <summary>Gets or sets the delay between map activation stages, in seconds.</summary>
    [Range(1, 60)]
    public int MapActivationIntervalSeconds { get; set; } = 5;

    /// <summary>Gets or sets the process memory ceiling which pauses further test spawns.</summary>
    [Range(256, 65536)]
    public int MaximumWorkingSetMegabytes { get; set; } = 6144;

    /// <summary>Gets or sets the requested map populations. Empty means all generated populations are disabled.</summary>
    public string ActiveMonsterMaps { get; set; } = string.Empty;

    /// <summary>Gets or sets the game-server identifier which owns testing populations.</summary>
    [Range(0, 255)]
    public int PopulationServerId { get; set; }

    /// <summary>Gets or sets the target crowd population; zero removes all active test characters.</summary>
    [Range(0, 100)]
    public int CrowdCount { get; set; }

    /// <summary>Gets or sets the ordinary map for the crowd.</summary>
    public short CrowdMap { get; set; }

    /// <summary>Gets or sets the crowd center tile X.</summary>
    public byte CrowdX { get; set; } = 135;

    /// <summary>Gets or sets the crowd center tile Y.</summary>
    public byte CrowdY { get; set; } = 125;

    /// <summary>Gets or sets the bounded spread radius in tiles.</summary>
    [Range(1, 30)]
    public int CrowdRadius { get; set; } = 8;

    /// <summary>Gets or sets the crowd behavior: idle, wander or combat.</summary>
    [RegularExpression("^(idle|wander|combat)$")]
    public string CrowdBehavior { get; set; } = "idle";

    /// <summary>Gets or sets the generated level; existing test characters retain their progression.</summary>
    [Range(1, 400)]
    public int CrowdLevel { get; set; } = 100;

    /// <summary>Gets or sets automatic cleanup time in seconds.</summary>
    [Range(30, 3600)]
    public int CrowdLifetimeSeconds { get; set; } = 300;

    /// <summary>Gets or sets the persisted deadline, so restarting cannot renew an expired crowd.</summary>
    [ScaffoldColumn(false)]
    [Browsable(false)]
    public DateTime? CrowdExpiresUtc { get; set; }

    /// <summary>Gets or sets owned names removed from the current activation.</summary>
    [ScaffoldColumn(false)]
    [Browsable(false)]
    public string CrowdExcludedNames { get; set; } = string.Empty;

    /// <summary>Gets or sets the original normal XP rate for rollback.</summary>
    [ScaffoldColumn(false)]
    [Browsable(false)]
    public float? OriginalExperienceRate { get; set; }

    /// <summary>Gets or sets the original XP-overflow setting.</summary>
    [ScaffoldColumn(false)]
    [Browsable(false)]
    public bool? OriginalPreventExperienceOverflow { get; set; }

    /// <summary>Gets or sets baseline probabilities keyed by persistent object identity for rollback across restarts.</summary>
    [ScaffoldColumn(false)]
    [Browsable(false)]
    public Dictionary<Guid, double> DropBaselines { get; set; } = [];

    /// <summary>Gets or sets baseline limits keyed by persistent object identity for rollback across restarts.</summary>
    [ScaffoldColumn(false)]
    [Browsable(false)]
    public Dictionary<Guid, float?> CapBaselines { get; set; } = [];

    /// <summary>Gets or sets the global crowd limit, separate from real players.</summary>
    [Range(1, 100)]
    public int MaximumCrowdBots { get; set; } = 100;
}
