// <copyright file="ResetChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which handles reset command.
/// </summary>
/// <seealso cref="MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.IChatCommandPlugIn" />
[Guid("90B35404-AADE-4F22-B5D2-4CD59B8BB4C8")]
[PlugIn]
[Display(Name = nameof(PlugInResources.ResetChatCommandPlugIn_Name), Description = nameof(PlugInResources.ResetChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command)]
public class ResetChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/reset";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var arguments = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        ResetDestination? destination = arguments.Length == 1 ? null : arguments.Length == 2 ? arguments[1] switch
        {
            "stay" => ResetDestination.StayOnCurrentMap,
            "lorencia" => ResetDestination.Lorencia,
            _ => (ResetDestination)(-1),
        } : (ResetDestination)(-1);
        var resetAction = new ResetCharacterAction(player);
        await resetAction.ResetCharacterAsync(destination).ConfigureAwait(false);
    }
}