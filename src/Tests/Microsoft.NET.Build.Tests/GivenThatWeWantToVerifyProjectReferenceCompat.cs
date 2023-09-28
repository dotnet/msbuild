// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToVerifyProjectReferenceCompat : SdkTest
    {
        public GivenThatWeWantToVerifyProjectReferenceCompat(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("net45", "Full", "netstandard1.0 netstandard1.1 net45", true, true)]
        [InlineData("net451", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 net45 net451", true, true)]
        [InlineData("net46", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 net45 net451 net46", true, true)]
        [InlineData("net461", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 net45 net451 net46 net461", true, true)]
        [InlineData("net462", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 net45 net451 net46 net461 net462", true, true)]
        [InlineData("netstandard1.0", "Full", "netstandard1.0", true, true)]
        [InlineData("netstandard1.1", "Full", "netstandard1.0 netstandard1.1", true, true)]
        [InlineData("netstandard1.2", "Full", "netstandard1.0 netstandard1.1 netstandard1.2", true, true)]
        [InlineData("netstandard1.3", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3", true, true)]
        [InlineData("netstandard1.4", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4", true, true)]
        [InlineData("netstandard1.5", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5", true, true)]
        [InlineData("netstandard1.6", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6", true, true)]
        [InlineData("netstandard2.0", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0", true, true)]
        [InlineData("netcoreapp1.0", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netcoreapp1.0", true, true)]
        [InlineData("netcoreapp1.1", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netcoreapp1.0 netcoreapp1.1", true, true)]
        [InlineData("netcoreapp2.0", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 netcoreapp1.0 netcoreapp1.1 netcoreapp2.0", true, true)]

        public void Project_reference_compat(string referencerTarget, string testIDPostFix, string rawDependencyTargets,
                bool restoreSucceeds, bool buildSucceeds)
        {
            string identifier = "_TestID_" + referencerTarget + "_" + testIDPostFix;

            TestProject referencerProject = GetTestProject("Referencer", referencerTarget, true);
            List<string> dependencyTargets = rawDependencyTargets.Split(',', ';', ' ').ToList();
            int dependencyTargetNamingIndex = 1;
            foreach (string dependencyTarget in dependencyTargets)
            {
                TestProject dependencyProject = GetTestProject("Dependency" + dependencyTargetNamingIndex++, dependencyTarget, true);
                referencerProject.ReferencedProjects.Add(dependencyProject);
            }

            //  Skip running test if not running on Windows
            //        https://github.com/dotnet/sdk/issues/335
            if (!(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || referencerProject.BuildsOnNonWindows))
            {
                return;
            }

            //  Set the referencer project as an Exe unless it targets .NET Standard
            if (!referencerProject.TargetFrameworkIdentifiers.Contains(ConstantStringValues.NetstandardTargetFrameworkIdentifier))
            {
                referencerProject.IsExe = true;
            }

            var testAsset = _testAssetsManager.CreateTestProject(referencerProject, nameof(Project_reference_compat), identifier);
            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: referencerProject.Name);
            if (restoreSucceeds)
            {
                restoreCommand.Execute().Should().Pass();
            }
            else
            {
                restoreCommand.Execute().Should().Fail();
            }

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand.Execute();

            if (buildSucceeds)
            {
                result.Should().Pass();
            }
            else
            {
                result.Should().Fail().And.HaveStdOutContaining("It cannot be referenced by a project that targets");
            }

        }

        TestProject GetTestProject(string name, string target, bool isSdkProject)
        {
            TestProject ret = new()
            {
                Name = name,
                IsSdkProject = isSdkProject
            };

            if (isSdkProject)
            {
                ret.TargetFrameworks = target;
            }
            else
            {
                ret.TargetFrameworkVersion = target;
            }

            return ret;
        }

        bool AllProjectsBuildOnNonWindows(TestProject referencerProject)
        {
            return (referencerProject.BuildsOnNonWindows && referencerProject.ReferencedProjects.All(rp => rp.BuildsOnNonWindows));
        }

    }
}
