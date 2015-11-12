// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet
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
