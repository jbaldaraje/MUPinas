// <copyright file="EnterMarketPlaceAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Action which warps a player between Lorencia and the Loren Market, triggered by the 'Warp'
/// button of the Market Union Member Julia window. It warps both ways, depending on which side
/// the player is currently on.
/// </summary>
public class EnterMarketPlaceAction
{
    /// <summary>
    /// The map number of the Loren Market.
    /// </summary>
    private const short LorenMarketMapNumber = 79;

    /// <summary>
    /// The map number of Lorencia, where the player is warped back to.
    /// </summary>
    private const short LorenciaMapNumber = 0;

    /// <summary>
    /// Warps the player between Lorencia and the Loren Market through Market Union Member Julia.
    /// </summary>
    /// <param name="player">The player who requested the warp.</param>
    public async ValueTask WarpToMarketPlaceAsync(Player player)
    {
        var currentMap = player.CurrentMap;
        var openedNpc = player.OpenedNpc;
        if (player.PlayerState.CurrentState != PlayerState.NpcDialogOpened
            || currentMap is null
            || openedNpc?.Definition.NpcWindow != NpcWindow.JuliaWarpMarketServer
            || openedNpc.CurrentMap != currentMap)
        {
            return;
        }

        await this.WarpBetweenMapsAsync(player).ConfigureAwait(false);
    }

    /// <summary>
    /// Warps the player between Lorencia and the Loren Market using the in-game shortcut.
    /// </summary>
    /// <param name="player">The player who requested the warp.</param>
    public async ValueTask WarpToMarketPlaceFromCommandAsync(Player player)
    {
        if (player.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MarketPlaceTravelNotAllowed)).ConfigureAwait(false);
            return;
        }

        await this.WarpBetweenMapsAsync(player).ConfigureAwait(false);
    }

    private async ValueTask WarpBetweenMapsAsync(Player player)
    {
        var currentMap = player.CurrentMap;
        if (currentMap is null
            || currentMap.Definition.Number is not (LorenciaMapNumber or LorenMarketMapNumber)
            || !currentMap.Definition.IsActive
            || !player.IsAlive
            || player.CurrentMiniGame is not null
            || player.TradingPartner is not null)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MarketPlaceTravelNotAllowed)).ConfigureAwait(false);
            return;
        }

        if (player.ShopStorage?.StoreOpen ?? false)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MarketPlaceCloseStore)).ConfigureAwait(false);
            return;
        }

        var targetMapNumber = currentMap.Definition.Number == LorenMarketMapNumber
            ? LorenciaMapNumber
            : LorenMarketMapNumber;
        var targetMap = await player.GameContext.GetMapAsync((ushort)targetMapNumber).ConfigureAwait(false);

        // The map instance may still exist, after the map got deactivated.
        if (targetMap is null
            || targetMap.MapId != targetMapNumber
            || !targetMap.Definition.IsActive)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MapIsNotActive)).ConfigureAwait(false);
            return;
        }

        var spawnGate = targetMap.SafeZoneSpawnGate ?? targetMap.Definition.GetSafezoneGate(targetMap.Terrain);
        if (spawnGate?.Map is not { IsActive: true } spawnMap || spawnMap.Number != targetMapNumber)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MapIsNotActive)).ConfigureAwait(false);
            return;
        }

        var walkablePoint = targetMap.Terrain.GetWalkableCoordinate(spawnGate);
        if (walkablePoint is not { } point)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MapIsNotActive)).ConfigureAwait(false);
            return;
        }

        var targetGate = new ExitGate
        {
            Map = spawnMap,
            X1 = point.X,
            X2 = point.X,
            Y1 = point.Y,
            Y2 = point.Y,
            Direction = spawnGate.Direction,
        };
        player.OpenedNpc = null;
        await player.WarpToAsync(targetGate).ConfigureAwait(false);
    }
}
