// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Framework
{
    internal static class ImmutableDictionaryExtensions
    {
        /// <summary>
        /// An empty dictionary pre-configured with a comparer for metadata dictionaries.
        /// </summary>
        public static readonly ImmutableDictionary<string, string> EmptyMetadata =
            ImmutableDictionary<string, string>.Empty.WithComparers(MSBuildNameIgnoreCaseComparer.Default);
    }
}
