// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.NET.Sdk.Razor.Tool.Tests
{
    internal class TestDefaultExtensionAssemblyLoader : DefaultExtensionAssemblyLoader
    {
        public TestDefaultExtensionAssemblyLoader(string baseDirectory)
            : base(baseDirectory)
        {
        }

        protected override Assembly LoadFromPathUnsafeCore(string filePath)
        {
            // Force a load from streams so we don't lock the files on disk. This way we can test
            // shadow copying without leaving a mess behind.
            var bytes = File.ReadAllBytes(filePath);
            var stream = new MemoryStream(bytes);
            return LoadContext.LoadFromStream(stream);
        }
    }
}
