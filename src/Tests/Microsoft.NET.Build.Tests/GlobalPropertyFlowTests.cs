// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GlobalPropertyFlowTests : SdkTest
    {
        TestProject _testProject;
        TestProject _referencedProject;

        public GlobalPropertyFlowTests(ITestOutputHelper log) : base(log)
        {
            _referencedProject = new TestProject("ReferencedProject")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = false
            };

            _testProject = new TestProject("TestProject")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };
            _testProject.ReferencedProjects.Add(_referencedProject);

            _testProject.RecordProperties("RuntimeIdentifier", "SelfContained");
            _referencedProject.RecordProperties("RuntimeIdentifier", "SelfContained");
        }

        TestAsset Build(bool passSelfContained, bool passRuntimeIdentifier, [CallerMemberName] string callingMethod = "", string identifier = "")
        {
            var testAsset = _testAssetsManager.CreateTestProject(_testProject, callingMethod: callingMethod, identifier: identifier);

            var arguments = GetDotnetArguments(passSelfContained, passRuntimeIdentifier);

            new DotnetBuildCommand(testAsset, arguments.ToArray())
                .Execute()
                .Should()
                .Pass();

            return testAsset;
        }

        List<string> GetDotnetArguments(bool passSelfContained, bool passRuntimeIdentifier)
        {
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid();

            List<string> arguments = new List<string>();
            if (passSelfContained)
            {
                arguments.Add("--self-contained");
            }
            if (passRuntimeIdentifier)
            {
                arguments.Add("-r");
                arguments.Add(runtimeIdentifier);
            }

            return arguments;
        }

        [RequiresMSBuildVersionTheory("17.4.0.41702")]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void TestGlobalPropertyFlowToLibrary(bool passSelfContained, bool passRuntimeIdentifier)
        {
            var testAsset = Build(passSelfContained, passRuntimeIdentifier, identifier: passSelfContained.ToString() + "_" + passRuntimeIdentifier);

            bool buildingSelfContained = passSelfContained || passRuntimeIdentifier;

            ValidateProperties(testAsset, _testProject, expectSelfContained: buildingSelfContained, expectRuntimeIdentifier: buildingSelfContained);
            ValidateProperties(testAsset, _referencedProject, expectSelfContained: false, expectRuntimeIdentifier: false);
        }

        [RequiresMSBuildVersionTheory("17.4.0.41702")]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void TestGlobalPropertyFlowToExe(bool passSelfContained, bool passRuntimeIdentifier)
        {
            _referencedProject.IsExe = true;

            var testAsset = Build(passSelfContained, passRuntimeIdentifier, identifier: passSelfContained.ToString() + "_" + passRuntimeIdentifier);

            bool buildingSelfContained = passSelfContained || passRuntimeIdentifier;

            ValidateProperties(testAsset, _testProject, expectSelfContained: buildingSelfContained, expectRuntimeIdentifier: buildingSelfContained);
            ValidateProperties(testAsset, _referencedProject, expectSelfContained: buildingSelfContained, expectRuntimeIdentifier: buildingSelfContained);
        }


        [RequiresMSBuildVersionTheory("17.4.0.41702")]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void TestGlobalPropertyFlowToExeWithSelfContainedFalse(bool passSelfContained, bool passRuntimeIdentifier)
        {
            _referencedProject.IsExe = true;
            _referencedProject.AdditionalProperties["SelfContained"] = "false";

            string identifier = passSelfContained.ToString() + "_" + passRuntimeIdentifier;

            if (!passSelfContained && passRuntimeIdentifier)
            {
                //  This combination results in a build error because it ends up being a self-contained Exe referencing a framework dependent one
                var testAsset = _testAssetsManager.CreateTestProject(_testProject, identifier: identifier);

                new DotnetBuildCommand(testAsset, "-r", EnvironmentInfo.GetCompatibleRid())
                    .Execute()
                    .Should()
                    .Fail()
                    .And
                    .HaveStdOutContaining("NETSDK1150");
            }
            else
            {

                var testAsset = Build(passSelfContained, passRuntimeIdentifier, identifier: identifier);

                bool buildingSelfContained = passSelfContained || passRuntimeIdentifier;

                ValidateProperties(testAsset, _testProject, expectSelfContained: buildingSelfContained, expectRuntimeIdentifier: buildingSelfContained);
                //  SelfContained will only flow to referenced project if it's explicitly passed in this case
                ValidateProperties(testAsset, _referencedProject, expectSelfContained: passSelfContained, expectRuntimeIdentifier: buildingSelfContained);
            }
        }

        [RequiresMSBuildVersionTheory("17.4.0.41702")]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void TestGlobalPropertyFlowToLibraryWithRuntimeIdentifier(bool passSelfContained, bool passRuntimeIdentifier)
        {
            //  Set a RuntimeIdentifier in the referenced project that is different from what is passed in on the command line
            _referencedProject.RuntimeIdentifier = $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64";

            var testAsset = Build(passSelfContained, passRuntimeIdentifier, identifier: passSelfContained.ToString() + "_" + passRuntimeIdentifier);

            bool buildingSelfContained = passSelfContained || passRuntimeIdentifier;

            ValidateProperties(testAsset, _testProject, expectSelfContained: buildingSelfContained, expectRuntimeIdentifier: buildingSelfContained);
            ValidateProperties(testAsset, _referencedProject, expectSelfContained: passSelfContained, expectRuntimeIdentifier: buildingSelfContained,
                //  Right now passing "--self-contained" also causes the RuntimeIdentifier to be passed as a global property.
                //  That should change with https://github.com/dotnet/sdk/pull/26143, which will likely require updating this and other tests in this class
                expectedRuntimeIdentifier: buildingSelfContained ? "" : _referencedProject.RuntimeIdentifier);
        }

        [RequiresMSBuildVersionTheory("17.4.0.41702")]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void TestGlobalPropertyFlowToMultitargetedProject(bool passSelfContained, bool passRuntimeIdentifier)
        {
            _testProject.TargetFrameworks = $"net6.0;{ToolsetInfo.CurrentTargetFramework}";

            _referencedProject.TargetFrameworks = $"net6.0;{ToolsetInfo.CurrentTargetFramework}";
            _referencedProject.IsExe = true;
            _referencedProject.ProjectChanges.Add(project =>
            {
                project.Root.Element("PropertyGroup").Add(XElement.Parse(@"<OutputType Condition=""'$(TargetFramework)' == 'net6.0'"">Library</OutputType>"));
            });

            var testAsset = Build(passSelfContained, passRuntimeIdentifier, identifier: passSelfContained.ToString() + "_" + passRuntimeIdentifier);

            bool buildingSelfContained = passSelfContained || passRuntimeIdentifier;

            ValidateProperties(testAsset, _testProject, expectSelfContained: buildingSelfContained, expectRuntimeIdentifier: buildingSelfContained,
                targetFramework: "net6.0");
            ValidateProperties(testAsset, _testProject, expectSelfContained: buildingSelfContained, expectRuntimeIdentifier: buildingSelfContained,
                targetFramework: ToolsetInfo.CurrentTargetFramework);
            ValidateProperties(testAsset, _referencedProject, expectSelfContained: false, expectRuntimeIdentifier: false,
                targetFramework: "net6.0");
            ValidateProperties(testAsset, _referencedProject, expectSelfContained: buildingSelfContained, expectRuntimeIdentifier: buildingSelfContained,
                targetFramework: ToolsetInfo.CurrentTargetFramework);
        }

        [RequiresMSBuildVersionTheory("17.4.0.41702", Skip = "https://github.com/dotnet/msbuild/issues/8154")]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void TestGlobalPropertyFlowInSolution(bool passSelfContained, bool passRuntimeIdentifier)
        {
            var identifier = passSelfContained.ToString() + "_" + passRuntimeIdentifier;

            var testAsset = _testAssetsManager.CreateTestProject(_testProject, identifier: identifier);

            new DotnetNewCommand(Log, "sln")
                .WithVirtualHive()
                .WithWorkingDirectory(testAsset.TestRoot)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log, "sln", "add", _testProject.Name)
                .WithWorkingDirectory(testAsset.TestRoot)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log, "sln", "add", _referencedProject.Name)
                .WithWorkingDirectory(testAsset.TestRoot)
                .Execute()
                .Should()
                .Pass();

            var arguments = GetDotnetArguments(passSelfContained, passRuntimeIdentifier);

            if (passRuntimeIdentifier)
            {
                new DotnetBuildCommand(Log, arguments.ToArray())
                    .WithWorkingDirectory(testAsset.TestRoot)
                    .Execute()
                    .Should()
                    .Fail()
                    .And
                    .HaveStdOutContaining("NETSDK1134");
            }
            else
            {
                new DotnetBuildCommand(Log, arguments.ToArray())
                    .WithWorkingDirectory(testAsset.TestRoot)
                    .Execute()
                    .Should()
                    .Pass();
            }
        }

        private static void ValidateProperties(TestAsset testAsset, TestProject testProject, bool expectSelfContained, bool expectRuntimeIdentifier, string targetFramework = null, string expectedRuntimeIdentifier = "")
        {
            targetFramework = targetFramework ?? testProject.TargetFrameworks;


            if (string.IsNullOrEmpty(expectedRuntimeIdentifier) && (expectSelfContained || expectRuntimeIdentifier))
            {
                //  RuntimeIdentifier might be inferred, so look at the output path to figure out what the actual value used was
                string dir = (Path.Combine(testAsset.TestRoot, testProject.Name, "bin", "Debug", targetFramework));
                expectedRuntimeIdentifier = Path.GetFileName(Directory.GetDirectories(dir).Single());
            }

            var properties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: targetFramework);
            if (expectSelfContained)
            {
                properties["SelfContained"].ToLowerInvariant().Should().Be("true");
            }
            else
            {
                properties["SelfContained"].ToLowerInvariant().Should().BeOneOf("false", "");
            }

            properties["RuntimeIdentifier"].Should().Be(expectedRuntimeIdentifier);
        }

    }
}
