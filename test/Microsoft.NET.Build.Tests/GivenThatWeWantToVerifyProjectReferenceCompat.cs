using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using FluentAssertions;
using System.Runtime.InteropServices;
using System.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToVerifyProjectReferenceCompat : SdkTest
    {
        [Theory]
        [InlineData("net45", "netstandard1.0", true)]
        [InlineData("net45", "netstandard1.1", true)]
        [InlineData("net45", "net45", true)]

        [InlineData("net451", "netstandard1.0", true)]
        [InlineData("net451", "netstandard1.1", true)]
        [InlineData("net451", "netstandard1.2", true)]
        [InlineData("net451", "net45", true)]
        [InlineData("net451", "net451", true)]

        [InlineData("net46", "netstandard1.0", true)]
        [InlineData("net46", "netstandard1.1", true)]
        [InlineData("net46", "netstandard1.2", true)]
        [InlineData("net46", "netstandard1.3", true)]
        [InlineData("net46", "net45", true)]
        [InlineData("net46", "net451", true)]
        [InlineData("net46", "net46", true)]

        [InlineData("net461", "netstandard1.0", true)]
        [InlineData("net461", "netstandard1.1", true)]
        [InlineData("net461", "netstandard1.2", true)]
        [InlineData("net461", "netstandard1.3", true)]
        [InlineData("net461", "netstandard1.4", true)]
        [InlineData("net461", "netstandard1.5", true)]
        [InlineData("net461", "netstandard1.6", true)]
        [InlineData("net461", "netstandard2.0", true)]
        [InlineData("net461", "net45", true)]
        [InlineData("net461", "net451", true)]
        [InlineData("net461", "net46", true)]
        [InlineData("net461", "net461", true)]

        [InlineData("net462", "netstandard1.0", true)]
        [InlineData("net462", "netstandard1.1", true)]
        [InlineData("net462", "netstandard1.2", true)]
        [InlineData("net462", "netstandard1.3", true)]
        [InlineData("net462", "netstandard1.4", true)]
        [InlineData("net462", "netstandard1.5", true)]
        [InlineData("net462", "netstandard1.6", true)]
        [InlineData("net462", "netstandard2.0", true)]
        [InlineData("net462", "net45", true)]
        [InlineData("net462", "net451", true)]
        [InlineData("net462", "net46", true)]
        [InlineData("net462", "net461", true)]
        [InlineData("net462", "net462", true)]

        [InlineData("netstandard1.0", "netstandard1.0", true)]

        [InlineData("netstandard1.1", "netstandard1.0", true)]
        [InlineData("netstandard1.1", "netstandard1.1", true)]

        [InlineData("netstandard1.2", "netstandard1.0", true)]
        [InlineData("netstandard1.2", "netstandard1.1", true)]
        [InlineData("netstandard1.2", "netstandard1.2", true)]

        [InlineData("netstandard1.3", "netstandard1.0", true)]
        [InlineData("netstandard1.3", "netstandard1.1", true)]
        [InlineData("netstandard1.3", "netstandard1.2", true)]
        [InlineData("netstandard1.3", "netstandard1.3", true)]

        [InlineData("netstandard1.4", "netstandard1.0", true)]
        [InlineData("netstandard1.4", "netstandard1.1", true)]
        [InlineData("netstandard1.4", "netstandard1.2", true)]
        [InlineData("netstandard1.4", "netstandard1.3", true)]
        [InlineData("netstandard1.4", "netstandard1.4", true)]

        [InlineData("netstandard1.5", "netstandard1.0", true)]
        [InlineData("netstandard1.5", "netstandard1.1", true)]
        [InlineData("netstandard1.5", "netstandard1.2", true)]
        [InlineData("netstandard1.5", "netstandard1.3", true)]
        [InlineData("netstandard1.5", "netstandard1.4", true)]
        [InlineData("netstandard1.5", "netstandard1.5", true)]

        [InlineData("netstandard1.6", "netstandard1.0", true)]
        [InlineData("netstandard1.6", "netstandard1.1", true)]
        [InlineData("netstandard1.6", "netstandard1.2", true)]
        [InlineData("netstandard1.6", "netstandard1.3", true)]
        [InlineData("netstandard1.6", "netstandard1.4", true)]
        [InlineData("netstandard1.6", "netstandard1.5", true)]
        [InlineData("netstandard1.6", "netstandard1.6", true)]

        [InlineData("netstandard2.0", "netstandard1.0", true)]
        [InlineData("netstandard2.0", "netstandard1.1", true)]
        [InlineData("netstandard2.0", "netstandard1.2", true)]
        [InlineData("netstandard2.0", "netstandard1.3", true)]
        [InlineData("netstandard2.0", "netstandard1.4", true)]
        [InlineData("netstandard2.0", "netstandard1.5", true)]
        [InlineData("netstandard2.0", "netstandard1.6", true)]
        [InlineData("netstandard2.0", "netstandard2.0", true)]

        [InlineData("netcoreapp1.0", "netstandard1.0", true)]
        [InlineData("netcoreapp1.0", "netstandard1.1", true)]
        [InlineData("netcoreapp1.0", "netstandard1.2", true)]
        [InlineData("netcoreapp1.0", "netstandard1.3", true)]
        [InlineData("netcoreapp1.0", "netstandard1.4", true)]
        [InlineData("netcoreapp1.0", "netstandard1.5", true)]
        [InlineData("netcoreapp1.0", "netstandard1.6", true)]
        [InlineData("netcoreapp1.0", "netcoreapp1.0", true)]

        [InlineData("netcoreapp1.1", "netstandard1.0", true)]
        [InlineData("netcoreapp1.1", "netstandard1.1", true)]
        [InlineData("netcoreapp1.1", "netstandard1.2", true)]
        [InlineData("netcoreapp1.1", "netstandard1.3", true)]
        [InlineData("netcoreapp1.1", "netstandard1.4", true)]
        [InlineData("netcoreapp1.1", "netstandard1.5", true)]
        [InlineData("netcoreapp1.1", "netstandard1.6", true)]
        [InlineData("netcoreapp1.1", "netcoreapp1.0", true)]
        [InlineData("netcoreapp1.1", "netcoreapp1.1", true)]

        [InlineData("netcoreapp2.0", "netstandard1.0", true)]
        [InlineData("netcoreapp2.0", "netstandard1.1", true)]
        [InlineData("netcoreapp2.0", "netstandard1.2", true)]
        [InlineData("netcoreapp2.0", "netstandard1.3", true)]
        [InlineData("netcoreapp2.0", "netstandard1.4", true)]
        [InlineData("netcoreapp2.0", "netstandard1.5", true)]
        [InlineData("netcoreapp2.0", "netstandard1.6", true)]
        [InlineData("netcoreapp2.0", "netstandard2.0", true)]
        [InlineData("netcoreapp2.0", "netcoreapp1.0", true)]
        [InlineData("netcoreapp2.0", "netcoreapp1.1", true)]
        [InlineData("netcoreapp2.0", "netcoreapp2.0", true)]

        public void It_checks_for_valid_project_reference_compat(string referencerTarget, string dependencyTarget, bool buildSucceeds)
        {
            string identifier = "_" + referencerTarget.ToString() + "_" + dependencyTarget.ToString();
            //  MSBuild isn't happy with semicolons in the path when doing file exists checks
            identifier = identifier.Replace(';', '_');

            TestProject referencerProject = GetTestProject("Referencer", referencerTarget, true);
            TestProject dependencyProject = GetTestProject("Dependency", dependencyTarget, true);
            referencerProject.ReferencedProjects.Add(dependencyProject);

            //  Set the referencer project as an Exe unless it targets .NET Standard
            if (!referencerProject.ShortTargetFrameworkIdentifiers.Contains("netstandard"))
            {
                referencerProject.IsExe = true;
            }

            var testAsset = _testAssetsManager.CreateTestProject(referencerProject, nameof(It_checks_for_valid_project_reference_compat), identifier);
            var restoreCommand = testAsset.GetRestoreCommand(relativePath: "Referencer").Execute().Should().Pass();
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "Referencer");

            var buildCommand = new BuildCommand(Stage0MSBuild, appProjectDirectory);
            if (!buildSucceeds)
            {
                buildCommand = buildCommand.CaptureStdOut();
            }

            var result = buildCommand.Execute();

            if (buildSucceeds)
            {
                result.Should().Pass();
            }
            else
            {
                result.Should().Fail()
                    .And.HaveStdOutContaining("It cannot be referenced by a project that targets");
            }
        }

        TestProject GetTestProject(string name, string target, bool isSdkProject)
        {
            TestProject ret = new TestProject()
            {
                Name = name,
                IsSdkProject = isSdkProject
            };

            if (isSdkProject)
            {
                ret.TargetFrameworks = target;
                //  Workaround for .NET Core 2.0 and .NET Framework 4.6.2
                if (target.Equals("netcoreapp2.0", StringComparison.OrdinalIgnoreCase) || target.Equals("net462", StringComparison.OrdinalIgnoreCase))
                {
                    ret.RuntimeFrameworkVersion = RepoInfo.NetCoreApp20Version;
                }
            }
            else
            {
                ret.TargetFrameworkVersion = target;
            }

            return ret;
        }
    }
}
