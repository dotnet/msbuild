// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A version number coupled with a reason why this version number
    /// was chosen.
    /// </summary>
    internal struct UnificationVersion
    {
        internal string referenceFullPath;
        internal Version version;
        internal UnificationReason reason;
    }
}
