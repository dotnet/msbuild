// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.CommandFactory
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
