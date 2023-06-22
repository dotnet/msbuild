// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ShellShim;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.Text.Json;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class AppHostShellShimMakerMock : IAppHostShellShimMaker
    {
        private readonly IFileSystem _fileSystem;

        public AppHostShellShimMakerMock(IFileSystem fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystemWrapper();
        }

        public void CreateApphostShellShim(FilePath entryPoint, FilePath shimPath)
        {
            var shim = new FakeShim
            {
                Runner = "dotnet",
                ExecutablePath = entryPoint.Value
            };

            _fileSystem.File.WriteAllText(
                shimPath.Value,
                JsonSerializer.Serialize(shim));
        }

        public class FakeShim
        {
            public string Runner { get; set; }
            public string ExecutablePath { get; set; }
        }
    }
}
