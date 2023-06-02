// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

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
