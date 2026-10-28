// <copyright file="LocalTestingResources.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.LocalTesting;

/// <summary>Localized plugin metadata backed by the existing plugin resource bundle.</summary>
public static class LocalTestingResources
{
    /// <summary>Gets the feature name.</summary>
    public static string FeatureName => PlugInResources.ResourceManager.GetString("LocalTestingFeature_Name")!;

    /// <summary>Gets the feature description.</summary>
    public static string FeatureDescription => PlugInResources.ResourceManager.GetString("LocalTestingFeature_Description")!;

    /// <summary>Gets the command name.</summary>
    public static string CommandName => PlugInResources.ResourceManager.GetString("LocalTestingCommand_Name")!;

    /// <summary>Gets the command description.</summary>
    public static string CommandDescription => PlugInResources.ResourceManager.GetString("LocalTestingCommand_Description")!;

    /// <summary>Gets the market command name.</summary>
    public static string MarketCommandName => PlugInResources.ResourceManager.GetString("MarketCommand_Name")!;

    /// <summary>Gets the market command description.</summary>
    public static string MarketCommandDescription => PlugInResources.ResourceManager.GetString("MarketCommand_Description")!;
}
