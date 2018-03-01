// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.DotNet.ToolPackage
{
    internal class PackageVersion
    {
        public PackageVersion(string packageVersion)
        {
            if (packageVersion == null)
            {
                Value = Path.GetRandomFileName();
                IsPlaceholder = true;
            }
            else
            {
                Value = packageVersion;
                IsPlaceholder = false;
            }
        }

        public bool IsPlaceholder { get; }
        public string Value { get; }
        public bool IsConcreteValue => !IsPlaceholder;
    }
}
