// <copyright file="TestingResetStatus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Resets;

/// <summary>Refreshes authoritative next-reset requirements without replacing the action's result message.</summary>
internal static class TestingResetStatus
{
    /// <summary>Sends the configured requirements and costs for the next reset.</summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="configuration">The active reset rules.</param>
    internal static async ValueTask SendAsync(Player player, ResetConfiguration configuration)
    {
        var resets = (int)(player.Attributes?[Stats.Resets] ?? 0);
        var progression = ResetProgressionCalculator.Calculate(resets, (int)(player.Attributes?[Stats.PointsPerReset] ?? 0), configuration);
        var itemName = configuration.RequiredResetItem is { } item ? (item.Name.GetTranslation(player.Culture) ?? string.Empty) : string.Empty;
        await player.ShowBlueMessageAsync(FormattableString.Invariant($"@LT1 resetinfo {configuration.RequiredLevel} {progression.RequiredZen} {progression.RequiredItemAmount} {resets} {configuration.ResetLimit ?? 0} {itemName}")).ConfigureAwait(false);
    }
}
