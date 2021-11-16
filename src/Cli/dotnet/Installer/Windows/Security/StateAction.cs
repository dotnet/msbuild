// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// Describes the action to be taken in relation to the state data.
    /// </summary>
    public enum StateAction : uint
    {
        /// <summary>
        /// Ignore the hWVTStateData member.
        /// </summary>
        WTD_STATEACTION_IGNORE = 0,

        /// <summary>
        /// Verify the trust of the object (typically a file) that is specified by the dwUnionChoice member. The hWVTStateData member will receive a handle to the state data. This handle must be freed by specifying the WTD_STATEACTION_CLOSE action in a subsequent call.
        /// </summary>
        WTD_STATEACTION_VERIFY = 1,

        /// <summary>
        /// Free the hWVTStateData member previously allocated with the WTD_STATEACTION_VERIFY action. This action must be specified for every use of the WTD_STATEACTION_VERIFY action.
        /// </summary>
        WTD_STATEACTION_CLOSE = 2,

        /// <summary>
        /// Write the catalog data to a WINTRUST_DATA structure and then cache that structure. This action only applies when the dwUnionChoice member contains WTD_CHOICE_CATALOG.
        /// </summary>
        WTD_STATEACTION_AUTO_CACHE = 3,

        /// <summary>
        /// Flush any cached catalog data. This action only applies when the dwUnionChoice member contains WTD_CHOICE_CATALOG.
        /// </summary>
        WTD_STATEACTION_AUTO_CACHE_FLUSH = 4
    }
}
