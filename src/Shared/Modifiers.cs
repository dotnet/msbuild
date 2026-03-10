// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FrameworkItemSpecModifiers = Microsoft.Build.Framework.ItemSpecModifiers;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for file IO.
    /// </summary>
    /// <comment>
    /// Partial class in order to reduce the amount of sharing into different assemblies
    /// </comment>
    internal static partial class FileUtilities
    {
        /// <summary>
        /// Encapsulates the definitions of the item-spec modifiers a.k.a. reserved item metadata.
        /// </summary>
        internal static class ItemSpecModifiers
        {
            internal static string FullPath => FrameworkItemSpecModifiers.FullPath;
            internal static string RootDir => FrameworkItemSpecModifiers.RootDir;
            internal static string Filename => FrameworkItemSpecModifiers.Filename;
            internal static string Extension => FrameworkItemSpecModifiers.Extension;
            internal static string RelativeDir => FrameworkItemSpecModifiers.RelativeDir;
            internal static string Directory => FrameworkItemSpecModifiers.Directory;
            internal static string RecursiveDir => FrameworkItemSpecModifiers.RecursiveDir;
            internal static string Identity => FrameworkItemSpecModifiers.Identity;
            internal static string ModifiedTime => FrameworkItemSpecModifiers.ModifiedTime;
            internal static string CreatedTime => FrameworkItemSpecModifiers.CreatedTime;
            internal static string AccessedTime => FrameworkItemSpecModifiers.AccessedTime;
            internal static string DefiningProjectFullPath => FrameworkItemSpecModifiers.DefiningProjectFullPath;
            internal static string DefiningProjectDirectory => FrameworkItemSpecModifiers.DefiningProjectDirectory;
            internal static string DefiningProjectName => FrameworkItemSpecModifiers.DefiningProjectName;
            internal static string DefiningProjectExtension => FrameworkItemSpecModifiers.DefiningProjectExtension;

            /// <inheritdoc cref="FrameworkItemSpecModifiers.All"/>
            internal static string[] All
                => FrameworkItemSpecModifiers.All;

            /// <inheritdoc cref="FrameworkItemSpecModifiers.IsItemSpecModifier(string)"/>
            internal static bool IsItemSpecModifier(string name)
                => FrameworkItemSpecModifiers.IsItemSpecModifier(name);

            /// <inheritdoc cref="FrameworkItemSpecModifiers.IsDefiningProjectModifier(string)"/>
            internal static bool IsDefiningProjectModifier(string name)
                => FrameworkItemSpecModifiers.IsDefiningProjectModifier(name);

            /// <inheritdoc cref="FrameworkItemSpecModifiers.IsDerivableItemSpecModifier(string)"/>
            internal static bool IsDerivableItemSpecModifier(string name)
                => FrameworkItemSpecModifiers.IsDerivableItemSpecModifier(name);

            /// <inheritdoc cref="FrameworkItemSpecModifiers.GetItemSpecModifier(string, string, string, string)"/>
            internal static string GetItemSpecModifier(string currentDirectory, string itemSpec, string definingProjectEscaped, string modifier)
                => FrameworkItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, definingProjectEscaped, modifier);

            /// <inheritdoc cref="FrameworkItemSpecModifiers.GetItemSpecModifier(string, string, string, string, ref string)"/>
            internal static string GetItemSpecModifier(string currentDirectory, string itemSpec, string definingProjectEscaped, string modifier, ref string fullPath)
                => FrameworkItemSpecModifiers.GetItemSpecModifier(currentDirectory, itemSpec, definingProjectEscaped, modifier, ref fullPath);
        }
    }
}
