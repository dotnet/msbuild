// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ShellShim;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.Text.Json;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class AppHostShellShimMakerMock : IAppHostShellShimMaker
    {
        private static IFileSystem _fileSystem;

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
