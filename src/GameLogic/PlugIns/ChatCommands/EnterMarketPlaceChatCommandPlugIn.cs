// <copyright file="EnterMarketPlaceChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Provides a shortcut to warp between Lorencia and the Loren Market.
/// </summary>
[Guid("A4A9388C-8E52-49C8-834B-796E012114E2")]
[PlugIn]
[Display(Name = "Enter Loren Market", Description = "Warps between Lorencia and Loren Market.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class EnterMarketPlaceChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/market";

    private readonly EnterMarketPlaceAction _enterMarketPlaceAction = new();

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public ValueTask HandleCommandAsync(Player player, string command)
    {
        return this._enterMarketPlaceAction.WarpToMarketPlaceFromCommandAsync(player);
    }
}
