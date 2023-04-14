// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Reflection;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal abstract class ExtensionAssemblyLoader
    {
        public abstract void AddAssemblyLocation(string filePath);

        public abstract Assembly Load(string assemblyName);

        public abstract Assembly LoadFromPath(string filePath);
    }
}
