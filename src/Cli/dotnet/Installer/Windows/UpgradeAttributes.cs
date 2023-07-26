// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Flags that determine how other columns in the Upgrade table are interpreted.
    /// </summary>
    [Flags]
    public enum UpgradeAttributes
    {
        /// <summary>
        /// Migrates feature states by enabling the logic in the MigrateFeatureStates action
        /// </summary>
        MigrateFeatures = 0x001,

        /// <summary>
        /// Detects products and applications but does not remove.
        /// </summary>
        OnlyDetect = 0x002,

        /// <summary>
        /// Continues installation upon failure to remove a product or application.
        /// </summary>
        IgnoreRemoveFailure = 0x004,

        /// <summary>
        /// Detects the range of versions including the value in VersionMin.
        /// </summary>
        VersionMinInclusive = 0x100,

        /// <summary>
        /// Detects the range of versions including the value in VersionMax.
        /// </summary>
        VersionMaxInclusive = 0x200,

        /// <summary>
        /// Detects all languages, excluding the languages listed in the Language column.
        /// </summary>
        LanguagesExclusive = 0x400
    }
}
