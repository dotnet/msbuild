// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
