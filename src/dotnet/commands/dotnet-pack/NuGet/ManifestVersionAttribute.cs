// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace NuGet.Legacy
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    internal sealed class ManifestVersionAttribute : Attribute
    {
        public ManifestVersionAttribute(int version)
        {
            Version = version;
        }

        public int Version { get; private set; }
    }
}
