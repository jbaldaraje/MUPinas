// <copyright file="TestingCrowdProfile.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

using MUnique.OpenMU.GameLogic.Bots;

/// <summary>A valid configured level with ordinary starter gear and no fabricated reset history.</summary>
internal sealed class TestingCrowdProfile(int level) : BotStartupProfile
{
    /// <inheritdoc />
    public override int MinLevel => level;

    /// <inheritdoc />
    public override int MaxLevel => level;

    /// <inheritdoc />
    public override byte StarterItemLevel => 0;

    /// <inheritdoc />
    public override bool EquipStarterArmor => true;

    /// <inheritdoc />
    public override int GetStartLevel(int minLevel, int maxLevel) => Math.Clamp(level, minLevel, maxLevel);

    /// <inheritdoc />
    public override int GetSeededResets(int maxSeededResets) => 0;
}
