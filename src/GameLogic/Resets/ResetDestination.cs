// <copyright file="ResetDestination.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Defines where a character is placed after a reset.
/// </summary>
public enum ResetDestination
{
    /// <summary>
    /// Uses the character class's configured home map, preserving the legacy behavior.
    /// </summary>
    [Display(Name = nameof(PlugInResources.ResetDestination_ClassHome_Name), ResourceType = typeof(PlugInResources))]
    ClassHome,

    /// <summary>
    /// Returns the character to Lorencia.
    /// </summary>
    [Display(Name = nameof(PlugInResources.ResetDestination_Lorencia_Name), ResourceType = typeof(PlugInResources))]
    Lorencia,

    /// <summary>
    /// Keeps the character on the current map and at the current position.
    /// </summary>
    [Display(Name = nameof(PlugInResources.ResetDestination_StayOnCurrentMap_Name), ResourceType = typeof(PlugInResources))]
    StayOnCurrentMap,
}
