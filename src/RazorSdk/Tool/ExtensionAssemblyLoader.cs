// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
