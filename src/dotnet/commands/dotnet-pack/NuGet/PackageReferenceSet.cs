// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;

namespace NuGet.Legacy
{
    public class PackageReferenceSet
    {
        public PackageReferenceSet(IEnumerable<string> references)
            : this((NuGetFramework)null, references)
        {
        }

        public PackageReferenceSet(string targetFramework, IEnumerable<string> references)
            : this(targetFramework != null ? NuGetFramework.Parse(targetFramework) : null, references)
        {
        }

        public PackageReferenceSet(NuGetFramework targetFramework, IEnumerable<string> references)
        {
            if (references == null)
            {
                throw new ArgumentNullException(nameof(references));
            }

            TargetFramework = targetFramework;
            References = references.ToArray();
        }

        public IReadOnlyCollection<string> References { get; }

        public NuGetFramework TargetFramework { get; }

    }
}
