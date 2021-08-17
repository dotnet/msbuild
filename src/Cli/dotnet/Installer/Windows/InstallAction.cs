// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Describes the various actions associated with an MSI.
    /// </summary>
    public enum InstallAction
    {
        None,
        Install,
        Uninstall,
        Repair,
        Rollback,
        MinorUpdate,
        MajorUpgrade,
        Downgrade,
    }
}
