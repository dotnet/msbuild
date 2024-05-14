// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class PopulateSupportedArchitectures_Tests
    {
        private static string TestAssetsRootPath { get; } = Path.Combine(
            Path.GetDirectoryName(typeof(PopulateSupportedArchitectures_Tests).Assembly.Location) ?? AppContext.BaseDirectory,
            "TestResources",
            "Manifests");

        private readonly ITestOutputHelper _testOutput;

        public PopulateSupportedArchitectures_Tests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Theory]
        [InlineData("testManifestWithInvalidSupportedArchs.manifest", false)]
        [InlineData("testManifestWithApplicationDefined.manifest", true)]
        [InlineData(null, true)]
        public void ManifestPopulationCheck(string manifestName, bool expectedResult)
        {
            PopulateSupportedArchitectures task = new PopulateSupportedArchitectures()
            {
                BuildEngine = new MockEngine(_testOutput)
            };

            using (TestEnvironment env = TestEnvironment.Create())
            {
                var tempOutput = env.CreateFolder().Path;
                task.OutputPath = tempOutput;
                if (!string.IsNullOrEmpty(manifestName))
                {
                    task.ApplicationManifestPath = Path.Combine(TestAssetsRootPath, manifestName);
                }

                var result = task.Execute();

                result.ShouldBe(expectedResult);

                if (result)
                {
                    var generatedManifest = task.ManifestPath;
                    var expectedManifest = Path.Combine(TestAssetsRootPath, $"{manifestName ?? "default.win32manifest"}_expected");

                    XmlDocument expectedDoc = new XmlDocument();
                    XmlDocument actualDoc = new XmlDocument();

                    expectedDoc.Load(generatedManifest);
                    actualDoc.Load(expectedManifest);

                    expectedDoc.OuterXml.ShouldBe(actualDoc.OuterXml);
                    expectedDoc.InnerXml.ShouldBe(actualDoc.InnerXml);
                }
            }
        }
    }
}
#endif
