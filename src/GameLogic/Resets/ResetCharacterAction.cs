// <copyright file="ResetCharacterAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.Runtime.CompilerServices;
using System.Threading;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.LocalTesting;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.GameLogic.Views.NPC;

/// <summary>
/// Action to reset a character.
/// </summary>
public class ResetCharacterAction
{
    private static readonly ConditionalWeakTable<Player, ResetRequestState> ResetGates = new();

    private readonly Player _player;
    private readonly NonPlayerCharacter? _npc;
    private readonly LogoutAction _logoutAction = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetCharacterAction"/> class.
    /// </summary>
    /// <param name="player">Player to reset.</param>
    /// <param name="npc">NPC which the player talks to to initiate the reset action.</param>
    public ResetCharacterAction(Player player, NonPlayerCharacter? npc = null)
    {
        this._player = player;
        this._npc = npc;
    }

    /// <summary>
    /// Reset specific character.
    /// </summary>
    /// <param name="requestedDestination">An optional destination for this request only; explicit requests use live warps.</param>
    public async ValueTask ResetCharacterAsync(ResetDestination? requestedDestination = null)
    {
        var resetGate = ResetGates.GetValue(this._player, static _ => new ResetRequestState());
        if (!resetGate.TryBegin())
        {
            await this.ShowMessageAsync("ResetAlreadyInProgress").ConfigureAwait(false);
            return;
        }

        try
        {
            await this._player.RunPersistenceExclusiveAsync(() => this.ResetCharacterExclusiveAsync(requestedDestination)).ConfigureAwait(false);
        }
        finally
        {
            resetGate.End();
        }
    }

    private async ValueTask ResetCharacterExclusiveAsync(ResetDestination? requestedDestination)
    {
        var resetFeature = this._player.GameContext.FeaturePlugIns.GetPlugIn<ResetFeaturePlugIn>();
        if (resetFeature is null)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.ResetNotEnabled)).ConfigureAwait(false);
            return;
        }

        var currentState = this._player.PlayerState.CurrentState;
        if (currentState != PlayerState.EnteredWorld
            && !(this._npc is not null && currentState == PlayerState.NpcDialogOpened))
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.CantResetWithOpenedWindows)).ConfigureAwait(false);
            return;
        }

        if (this._player.Attributes is null || this._player.SelectedCharacter is null)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.NotEnteredTheGame)).ConfigureAwait(false);
            return;
        }

        if (!this._player.IsAlive)
        {
            await this.ShowMessageAsync("ResetWhileDead").ConfigureAwait(false);
            return;
        }

        if (this._player.CurrentMiniGame is not null
            || (this._player.CurrentMap is { } instance && this._player.GameContext.MiniGames.Maps.Contains(instance)))
        {
            await this.ShowMessageAsync("ResetInMiniGame").ConfigureAwait(false);
            return;
        }

        if (this._player.TradingPartner is not null || (this._player.ShopStorage?.StoreOpen ?? false))
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.CantResetWithOpenedWindows)).ConfigureAwait(false);
            return;
        }

        var currentMap = this._player.CurrentMap;
        var currentPosition = this._player.Position;
        if (currentMap is null
            || currentMap.Definition.Discriminator != 0
            || !TestingMapAvailability.IsHosted(this._player.GameContext, currentMap.Definition.Number)
            || !currentMap.Terrain.WalkMap[currentPosition.X, currentPosition.Y])
        {
            await this.ShowMessageAsync("ResetPositionInvalid").ConfigureAwait(false);
            return;
        }

        var configuration = resetFeature.Configuration;
        if (configuration is null)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.ResetNotConfigured)).ConfigureAwait(false);
            return;
        }

        var destination = requestedDestination ?? configuration.Destination;
        var logOut = !requestedDestination.HasValue && configuration.LogOut;
        if (!Enum.IsDefined(typeof(ResetDestination), destination))
        {
            await this.ShowMessageAsync("ResetDestinationUnavailable").ConfigureAwait(false);
            return;
        }

        var destinationGate = destination == ResetDestination.StayOnCurrentMap
            ? null
            : await this.GetDestinationSpawnGateAsync(destination).ConfigureAwait(false);
        if (destination != ResetDestination.StayOnCurrentMap && destinationGate is null)
        {
            await this.ShowMessageAsync("ResetDestinationUnavailable").ConfigureAwait(false);
            return;
        }

        var resetProgression = ResetProgressionCalculator.Calculate(this.GetResetCount(), (int)this._player.Attributes[Stats.PointsPerReset], configuration);

        if (this._player.Level < configuration.RequiredLevel)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.RequiredLevelForReset), configuration.RequiredLevel).ConfigureAwait(false);
            return;
        }

        if (configuration.ResetLimit > 0 && resetProgression.NextResetCount > configuration.ResetLimit)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.MaximumResetsReached), configuration.ResetLimit).ConfigureAwait(false);
            return;
        }

        if (!await this.TryConsumeResetCostsAsync(configuration, resetProgression).ConfigureAwait(false))
        {
            return;
        }

        this._player.Attributes[Stats.Resets] = resetProgression.NextResetCount;
        this._player.Attributes[Stats.Level] = configuration.LevelAfterReset;
        this._player.SelectedCharacter.Experience = 0;
        this.UpdateStats(configuration, resetProgression);

        if (destination != ResetDestination.StayOnCurrentMap && logOut)
        {
            this.PlaceCharacterAtGate(destinationGate!);
        }

        if (destination != ResetDestination.StayOnCurrentMap && logOut)
        {
            await this._logoutAction.LogoutAsync(this._player, LogoutType.BackToCharacterSelection).ConfigureAwait(false);
        }
        else
        {
            await this.UpdateClientStatsAsync(configuration).ConfigureAwait(false);
            if (destination != ResetDestination.StayOnCurrentMap)
            {
                await this._player.WarpToAsync(destinationGate!).ConfigureAwait(false);
            }

            if (!await this._player.SaveProgressAsync().ConfigureAwait(false))
            {
                this._player.Logger.LogError("Reset persistence failed for {Character}; no completion was acknowledged.", this._player.Name);
                return;
            }

            await this.ShowMessageAsync("ResetCompleted", resetProgression.NextResetCount).ConfigureAwait(false);
            await this._player.ShowBlueMessageAsync($"@LT1 reset {resetProgression.NextResetCount}").ConfigureAwait(false);
            await TestingResetStatus.SendAsync(this._player, configuration).ConfigureAwait(false);
        }
    }

    private async ValueTask ShowMessageAsync(string messageKey, params object?[] args)
    {
        var message = this._player.GetLocalizedMessage(messageKey, args);

        if (this._npc is null)
        {
            await this._player.ShowBlueMessageAsync("@LT1 feedback " + message).ConfigureAwait(false);
            await this._player.ShowBlueMessageAsync(message).ConfigureAwait(false);
            return;
        }

        await this._player.InvokeViewPlugInAsync<IShowMessageOfObjectPlugIn>(p => p.ShowMessageOfObjectAsync(message, this._npc)).ConfigureAwait(false);
    }

    private int GetResetCount()
    {
        return (int)this._player.Attributes![Stats.Resets];
    }

    private async ValueTask<bool> TryConsumeResetCostsAsync(ResetConfiguration configuration, ResetProgression resetProgression)
    {
        var requiredItems = await this.GetRequiredItemsToConsumeAsync(configuration, resetProgression.RequiredItemAmount).ConfigureAwait(false);
        if (requiredItems is null)
        {
            return false;
        }

        if (this._player.Money < resetProgression.RequiredZen)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.NotEnoughMoneyForReset), resetProgression.RequiredZen).ConfigureAwait(false);
            return false;
        }

        if (resetProgression.RequiredZen > 0 && !this._player.TryRemoveMoney(resetProgression.RequiredZen))
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.NotEnoughMoneyForReset), resetProgression.RequiredZen).ConfigureAwait(false);
            return false;
        }

        foreach (var item in requiredItems)
        {
            await this._player.DestroyInventoryItemAsync(item).ConfigureAwait(false);
        }

        return true;
    }

    private async ValueTask<IList<Item>?> GetRequiredItemsToConsumeAsync(ResetConfiguration configuration, int requiredItemAmount)
    {
        if (requiredItemAmount <= 0 || configuration.RequiredResetItem is null)
        {
            return [];
        }

        if (this._player.Inventory is null)
        {
            return null;
        }

        var requiredDefinition = configuration.RequiredResetItem;
        var requiredItems = this._player.Inventory.Items
            .Where(item => item.Definition is { } definition
                           && definition.Group == requiredDefinition.Group
                           && definition.Number == requiredDefinition.Number)
            .Take(requiredItemAmount)
            .ToList();
        if (requiredItems.Count < requiredItemAmount)
        {
            await this.ShowMessageAsync(
                nameof(PlayerMessage.NotEnoughItemsForReset),
                requiredItemAmount,
                configuration.RequiredResetItem.Name).ConfigureAwait(false);
            return null;
        }

        return requiredItems;
    }

    private void UpdateStats(ResetConfiguration configuration, ResetProgression resetProgression)
    {
        if (configuration.ResetStats)
        {
            this._player.SelectedCharacter!.CharacterClass!.StatAttributes
                .Where(s => s.IncreasableByPlayer)
                .ForEach(s => this._player.Attributes![s.Attribute] = s.BaseValue);
        }

        if (configuration.ReplacePointsPerReset)
        {
            this._player.SelectedCharacter!.LevelUpPoints = resetProgression.TotalPointsAfterReset;
        }
        else
        {
            this._player.SelectedCharacter!.LevelUpPoints += resetProgression.PointsForReset;
        }
    }

    private async ValueTask<ExitGate?> GetDestinationSpawnGateAsync(ResetDestination destination)
    {
        var selectedCharacter = this._player.SelectedCharacter!;
        var destinationMapNumber = destination switch
        {
            ResetDestination.ClassHome => selectedCharacter.CharacterClass?.HomeMap?.Number,
            ResetDestination.Lorencia => (short)0,
            _ => null,
        };
        if (destinationMapNumber is not { } mapNumber || mapNumber < 0)
        {
            return null;
        }

        var destinationMap = await this._player.GameContext.GetMapAsync((ushort)mapNumber).ConfigureAwait(false);
        if (destinationMap is null
            || destinationMap.MapId != mapNumber
            || !TestingMapAvailability.IsHosted(this._player.GameContext, mapNumber))
        {
            return null;
        }

        var spawnGate = destinationMap.SafeZoneSpawnGate ?? destinationMap.Definition.GetSafezoneGate(destinationMap.Terrain);
        if (spawnGate is null)
        {
            return null;
        }

        var spawnMap = spawnGate.Map;
        if (spawnMap is null || spawnMap.Number != mapNumber)
        {
            return null;
        }

        var walkablePoint = destinationMap.Terrain.GetWalkableCoordinate(spawnGate);
        if (walkablePoint is not { } point)
        {
            return null;
        }

        return new ExitGate
        {
            Map = destinationMap.Definition,
            X1 = point.X,
            X2 = point.X,
            Y1 = point.Y,
            Y2 = point.Y,
            Direction = spawnGate.Direction,
        };
    }

    private void PlaceCharacterAtGate(ExitGate gate)
    {
        var character = this._player.SelectedCharacter!;
        character.PositionX = gate.X1;
        character.PositionY = gate.Y1;
        character.CurrentMap = gate.Map;
        this._player.Rotation = gate.Direction;
    }

    private async ValueTask UpdateClientStatsAsync(ResetConfiguration configuration)
    {
        if (configuration.ResetStats)
        {
            await this._player.InvokeViewPlugInAsync<IUpdateCharacterBaseStatsPlugIn>(p => p.UpdateCharacterBaseStatsAsync()).ConfigureAwait(false);
        }

        await this._player.InvokeViewPlugInAsync<IUpdateLevelPlugIn>(p => p.UpdateLevelAsync()).ConfigureAwait(false);
        await this._player.InvokeViewPlugInAsync<IUpdateCharacterStatsPlugIn>(p => p.UpdateCharacterStatsAsync()).ConfigureAwait(false);
    }

    private sealed class ResetRequestState
    {
        private int _running;

        public bool TryBegin() => Interlocked.CompareExchange(ref this._running, 1, 0) == 0;

        public void End() => Volatile.Write(ref this._running, 0);
    }
}
