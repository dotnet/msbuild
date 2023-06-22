// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// Describes the type of user interface to display.
    /// </summary>
    public enum UIChoice : uint
    {
        /// <summary>
        /// Display all UI.
        /// </summary>
        WTD_UI_ALL = 1,

        /// <summary>
        /// Display no UI.
        /// </summary>
        WTD_UI_NONE = 2,

        /// <summary>
        /// Do not display any negative UI.
        /// </summary>
        WTD_UI_NOBAD = 3,

        /// <summary>
        /// Do not display any positive UI.
        /// </summary>
        WTD_UI_NOGOOD = 4
    }
}
