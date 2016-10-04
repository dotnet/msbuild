// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.CommandResolution
{
    internal class ResourceAssemblyInfo
    {
        public string Culture { get; }
        public string RelativePath { get; }

        public ResourceAssemblyInfo(string culture, string relativePath)
        {
            Culture = culture;
            RelativePath = relativePath;
        }
    }
}