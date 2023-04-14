// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using FluentAssertions;
using Moq;
using Xunit;

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
                    new TaskItem("Resources.fr.dll", new Dictionary<string, string>
                    {
                        ["Culture"] = "fr",
                        ["DestinationSubDirectory"] = "fr\\",
                    }),
                    new TaskItem("Resources.ja-jp.dll", new Dictionary<string, string>
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
                assembly.ItemSpec == "Resources.fr.dll" && 
                assembly.GetMetadata("Culture") == "fr" &&
                assembly.GetMetadata("DestinationSubDirectory") == "fr\\"
            );

            reader.SatelliteAssembly.Should().Contain(assembly =>
                assembly.ItemSpec == "Resources.ja-jp.dll" && 
                assembly.GetMetadata("Culture") == "ja-jp" &&
                assembly.GetMetadata("DestinationSubDirectory") == "ja-jp\\"
            );
        }
    }
}
