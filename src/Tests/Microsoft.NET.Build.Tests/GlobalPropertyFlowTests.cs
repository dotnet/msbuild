// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

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

            List<string> arguments = new();
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

            bool appIsSelfContainedOrRuntimeSpecific = passSelfContained || passRuntimeIdentifier;

            ValidateProperties(testAsset, _testProject, expectSelfContained: passSelfContained, expectRuntimeIdentifier: appIsSelfContainedOrRuntimeSpecific);
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

            bool appIsSelfContainedOrRuntimeSpecific = passSelfContained || passRuntimeIdentifier;

            ValidateProperties(testAsset, _testProject, expectSelfContained: passSelfContained, expectRuntimeIdentifier: appIsSelfContainedOrRuntimeSpecific);
            ValidateProperties(testAsset, _referencedProject, expectSelfContained: passSelfContained, expectRuntimeIdentifier: appIsSelfContainedOrRuntimeSpecific);
        }


        [RequiresMSBuildVersionTheory("17.4.0.41702")]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void TestGlobalPropertyFlowToExeWithSelfContainedFalse(bool passSelfContained, bool passRuntimeIdentifier)
        {
            _referencedProject.IsExe = true;
            _referencedProject.SelfContained = "false";

            string identifier = passSelfContained.ToString() + "_" + passRuntimeIdentifier;

            var testAsset = Build(passSelfContained, passRuntimeIdentifier, identifier: identifier);

            bool appIsSelfContainedOrRuntimeSpecific = passSelfContained || passRuntimeIdentifier;

            ValidateProperties(testAsset, _testProject, expectSelfContained: passSelfContained, expectRuntimeIdentifier: appIsSelfContainedOrRuntimeSpecific);
            ValidateProperties(testAsset, _referencedProject, expectSelfContained: passSelfContained, expectRuntimeIdentifier: appIsSelfContainedOrRuntimeSpecific);
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

            bool appIsSelfContainedOrRuntimeSpecific = passSelfContained || passRuntimeIdentifier;

            ValidateProperties(testAsset, _testProject, expectSelfContained: passSelfContained, expectRuntimeIdentifier: appIsSelfContainedOrRuntimeSpecific);
            // We added a rid to the referenced project so it should have one always.
            ValidateProperties(testAsset, _referencedProject, expectSelfContained: passSelfContained, expectRuntimeIdentifier: true,
                expectedRuntimeIdentifier: passRuntimeIdentifier ? EnvironmentInfo.GetCompatibleRid() : _referencedProject.RuntimeIdentifier);
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

            string identifier = passSelfContained.ToString() + "_" + passRuntimeIdentifier;

            // in net 7 or below this means to build self contained but not in net8 as the properties are independent.
            bool appIsSelfContainedOrRuntimeSpecific = passSelfContained || passRuntimeIdentifier;

            var testAsset = Build(passSelfContained, passRuntimeIdentifier, identifier: identifier);

            ValidateProperties(testAsset, _testProject, expectSelfContained: appIsSelfContainedOrRuntimeSpecific, expectRuntimeIdentifier: appIsSelfContainedOrRuntimeSpecific,
                thisTargetFramework: "net6.0");
            ValidateProperties(testAsset, _testProject, expectSelfContained: passSelfContained, expectRuntimeIdentifier: appIsSelfContainedOrRuntimeSpecific,
                thisTargetFramework: ToolsetInfo.CurrentTargetFramework); ;
            ValidateProperties(testAsset, _referencedProject, expectSelfContained: false, expectRuntimeIdentifier: false,
                thisTargetFramework: "net6.0");
            ValidateProperties(testAsset, _referencedProject, expectSelfContained: passSelfContained, expectRuntimeIdentifier: appIsSelfContainedOrRuntimeSpecific,
                thisTargetFramework: ToolsetInfo.CurrentTargetFramework);
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

        private static void ValidateProperties(TestAsset testAsset, TestProject testProject, bool expectSelfContained, bool expectRuntimeIdentifier, string thisTargetFramework = null, string expectedRuntimeIdentifier = "")
        {
            thisTargetFramework = thisTargetFramework ?? testProject.TargetFrameworks;

            var properties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: thisTargetFramework);


            if (expectSelfContained)
            {
                properties["SelfContained"].ToLowerInvariant().Should().Be("true");
            }
            else
            {
                properties["SelfContained"].ToLowerInvariant().Should().BeOneOf("false", "");
            }


            if (expectRuntimeIdentifier)
            {
                if (!string.IsNullOrEmpty(expectedRuntimeIdentifier))
                {
                    properties["RuntimeIdentifier"].Should().Be(expectedRuntimeIdentifier);
                }
                else
                {
                    properties["RuntimeIdentifier"].Should().NotBeEmpty();
                }
            }
            else
            {
                properties["RuntimeIdentifier"].Should().BeEmpty();
            }

        }

    }
}
