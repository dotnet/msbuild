// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class BlazorReadSatelliteAssemblyFileTest
    {
        [Fact]
        public void WritesAndReadsRoundTrip()
        {
            // Arrange/Act
            var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var writer = new BlazorWriteSatelliteAssemblyFile
            {
                BuildEngine = Mock.Of<IBuildEngine>(),
                WriteFile = new TaskItem(tempFile),
                SatelliteAssembly = new[]
                {
                    new TaskItem("Resources.fr.wasm", new Dictionary<string, string>
                    {
                        ["Culture"] = "fr",
                        ["DestinationSubDirectory"] = "fr\\",
                    }),
                    new TaskItem("Resources.ja-jp.wasm", new Dictionary<string, string>
                    {
                        ["Culture"] = "ja-jp",
                        ["DestinationSubDirectory"] = "ja-jp\\",
                    }),
                },
            };

            var reader = new BlazorReadSatelliteAssemblyFile
            {
                BuildEngine = Mock.Of<IBuildEngine>(),
                ReadFile = new TaskItem(tempFile),
            };

            writer.Execute();

            File.Exists(tempFile).Should().BeTrue();

            reader.Execute();

            reader.SatelliteAssembly.Should().Contain(assembly =>
                assembly.ItemSpec == "Resources.fr.wasm" &&
                assembly.GetMetadata("Culture") == "fr" &&
                assembly.GetMetadata("DestinationSubDirectory") == "fr\\"
            );

            reader.SatelliteAssembly.Should().Contain(assembly =>
                assembly.ItemSpec == "Resources.ja-jp.wasm" &&
                assembly.GetMetadata("Culture") == "ja-jp" &&
                assembly.GetMetadata("DestinationSubDirectory") == "ja-jp\\"
            );
        }
    }
}
