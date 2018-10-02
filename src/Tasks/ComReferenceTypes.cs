// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Predefined typelib wrapper types.
    /// </summary>
    internal static class ComReferenceTypes
    {
        internal const string tlbimp = "tlbimp";
        internal const string aximp = "aximp";
        internal const string primary = "primary";
        internal const string primaryortlbimp = "primaryortlbimp";

        /// <summary>
        /// returns true is refType equals tlbimp
        /// </summary>
        internal static bool IsTlbImp(string refType)
        {
            return (string.Compare(refType, ComReferenceTypes.tlbimp, StringComparison.OrdinalIgnoreCase) == 0);
        }

        /// <summary>
        /// returns true is refType equals aximp
        /// </summary>
        internal static bool IsAxImp(string refType)
        {
            return (string.Compare(refType, ComReferenceTypes.aximp, StringComparison.OrdinalIgnoreCase) == 0);
        }

        /// <summary>
        /// returns true is refType equals pia
        /// </summary>
        internal static bool IsPia(string refType)
        {
            return (string.Compare(refType, ComReferenceTypes.primary, StringComparison.OrdinalIgnoreCase) == 0);
        }

        /// <summary>
        /// returns true if refType equals primaryortlbimp, which is basically an unknown reference type
        /// </summary>
        internal static bool IsPiaOrTlbImp(string refType)
        {
            return (string.Compare(refType, ComReferenceTypes.primaryortlbimp, StringComparison.OrdinalIgnoreCase) == 0);
        }
    }
}
