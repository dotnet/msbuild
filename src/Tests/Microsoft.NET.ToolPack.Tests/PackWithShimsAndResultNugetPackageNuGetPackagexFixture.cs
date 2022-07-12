// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using Xunit.Abstractions;
using System.Xml.Linq;
using System;

namespace Microsoft.NET.ToolPack.Tests
{
    public class NupkgOfPackWithShimsFixture : IDisposable
    {
        public const string _customToolCommandName = "customToolCommandName";
        private const string _packageVersion = "1.0.0";
        public NupkgOfPackWithShimsFixture()
        {
        }

        public void Dispose()
        {
        }

        public ITestOutputHelper Log { get; private set; }
        public TestAssetsManager TestAssetsManager { get; private set; }
        public void Init(ITestOutputHelper log, TestAssetsManager testAssetsManager)
        {
            Log = log;
            TestAssetsManager = testAssetsManager;
        }
        public Dictionary<(bool multiTarget, string targetFramework), string> assetMap
            = new Dictionary<(bool multiTarget, string targetFramework), string>();

        public string GetTestToolPackagePath(bool multiTarget, string targetFramework)
        {
            var mapKey = (multiTarget, targetFramework);
            if (assetMap.ContainsKey(mapKey))
            {
                return assetMap[mapKey];
            }
            else
            {
                var package = SetupNuGetPackage(multiTarget,
                    targetFramework);
                assetMap[mapKey] = package;
                return package;
            }
        }

        private string SetupNuGetPackage(
            bool multiTarget,
            string targetFramework)
        {
            TestAsset helloWorldAsset = CreateTestAsset(
                multiTarget,
                nameof(NupkgOfPackWithShimsFixture) + multiTarget + targetFramework,
                targetFramework);

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);
            packCommand.Execute().Should().Pass();

            return packCommand.GetNuGetPackage(packageVersion: _packageVersion);
        }

        private TestAsset CreateTestAsset(
            bool multiTarget,
            string uniqueName,
            string targetFramework)
        {
            return TestAssetsManager
                .CopyTestAsset("PortableTool", uniqueName)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "PackAsToolShimRuntimeIdentifiers", $"win-x64;{ToolsetInfo.LatestMacRuntimeIdentifier}-x64"));
                    propertyGroup.Add(new XElement(ns + "ToolCommandName", _customToolCommandName));
                })
                .WithTargetFrameworkOrFrameworks(targetFramework, multiTarget);
        }
    }
}
