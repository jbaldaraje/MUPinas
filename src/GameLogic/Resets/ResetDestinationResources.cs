// <copyright file="ResetDestinationResources.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

/// <summary>Localized metadata for reset destination settings in the admin panel.</summary>
public static class ResetDestinationResources
{
    /// <summary>Gets the destination label.</summary>
    public static string Destination => PlugInResources.ResourceManager.GetString("ResetConfiguration_Destination_Name")!;

    /// <summary>Gets the class-home option.</summary>
    public static string ClassHome => PlugInResources.ResourceManager.GetString("ResetDestination_ClassHome_Name")!;

    /// <summary>Gets the Lorencia option.</summary>
    public static string Lorencia => PlugInResources.ResourceManager.GetString("ResetDestination_Lorencia_Name")!;

    /// <summary>Gets the stay-in-place option.</summary>
    public static string StayOnCurrentMap => PlugInResources.ResourceManager.GetString("ResetDestination_StayOnCurrentMap_Name")!;
}
