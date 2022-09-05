// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class PublishItemsOutputGroupOutputsTests : SdkTest
    {
        public PublishItemsOutputGroupOutputsTests(ITestOutputHelper log) : base(log)
        {
        }

        private readonly static List<string> FrameworkAssemblies = new List<string>()
        {
            "api-ms-win-core-console-l1-1-0.dll",
            "System.Runtime.dll",
            "WindowsBase.dll",
        };

        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void RunPublishItemsOutputGroupOutputsTest(bool specifyRid, bool singleFile)
        {
            var testProject = new TestProject()
            {
                Name = "TestPublishItemsOutputGroupOutputs",
                TargetFrameworks = "net6.0",
                IsExe = true
            };

            testProject.AdditionalProperties["RuntimeIdentifiers"] = "win-x86";
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";
            if (specifyRid)
            {
                testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x86";
            }

            if (singleFile)
            {
                testProject.AdditionalProperties["PublishSingleFile"] = "true";
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: specifyRid.ToString() + singleFile.ToString());

            var restoreCommand = new RestoreCommand(testAsset);
            restoreCommand
                .Execute()
                .Should()
                .Pass();

            var command = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.Path, testProject.Name),
                testProject.TargetFrameworks,
                "PublishItemsOutputGroupOutputs",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "Publish",
                MetadataNames = { "OutputPath" },
            };

            command.Execute().Should().Pass();
            var items = from item in command.GetValuesWithMetadata()
                        select new
                        {
                            Identity = item.value,
                            OutputPath = Path.GetFileName(Path.GetFullPath(Path.Combine(testAsset.Path, testProject.Name, item.metadata["OutputPath"])))
                        };

            Log.WriteLine("PublishItemsOutputGroupOutputs contains '{0}' items:", items.Count());
            foreach (var item in items)
            {
                Log.WriteLine("    '{0}': OutputPath = '{1}'", item.Identity, item.OutputPath);
            }

            // Check for the main exe
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string exeSuffix = specifyRid ? ".exe" : Constants.ExeSuffix;
                items.Should().ContainSingle(i => i.OutputPath.Equals($"{testProject.Name}{exeSuffix}", StringComparison.OrdinalIgnoreCase));
            }

            // Framework assemblies should be there if we specified and rid and this isn't in the single file case
            if (specifyRid && !singleFile)
            {
                FrameworkAssemblies.ForEach(fa => items.Should().ContainSingle(i => i.OutputPath.Equals(fa, StringComparison.OrdinalIgnoreCase)));
                items.Should().Contain(i => i.OutputPath.Equals($"{testProject.Name}.deps.json", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                FrameworkAssemblies.ForEach(fa => items.Should().NotContain(i => i.OutputPath.Equals(fa, StringComparison.OrdinalIgnoreCase)));
            }
        }
    }
}
