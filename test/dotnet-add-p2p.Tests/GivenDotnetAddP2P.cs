// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
using Msbuild.Tests.Utilities;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.Cli.Add.P2P.Tests
{
    public class GivenDotnetAddP2P : TestBase
    {
        const string FrameworkNet451Arg = "-f net451";
        const string ConditionFrameworkNet451 = "== 'net451'";
        const string FrameworkNetCoreApp10Arg = "-f netcoreapp1.0";
        const string ConditionFrameworkNetCoreApp10 = "== 'netcoreapp1.0'";
        const string ProjectNotCompatibleErrorMessageRegEx = "Project `[^`]*` cannot be added due to incompatible targeted frameworks between the two projects\\. Please review the project you are trying to add and verify that is compatible with the following targets\\:";
        const string ProjectDoesNotTargetFrameworkErrorMessageRegEx = "Project `[^`]*` does not target framework `[^`]*`.";
        static readonly string[] DefaultFrameworks = new string[] { "netcoreapp1.0", "net451" };
        
        private TestSetup Setup([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(Setup), string identifier = "")
        {
            return new TestSetup(
                TestAssets.Get(TestSetup.TestGroup, TestSetup.ProjectName)
                    .CreateInstance(callingMethod: callingMethod, identifier: identifier)
                    .WithSourceFiles()
                    .Root
                    .FullName);
        }

        private ProjDir NewDir([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            return new ProjDir(TestAssetsManager.CreateTestDirectory(callingMethod: callingMethod, identifier: identifier).Path);
        }

        private ProjDir NewLib([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var dir = NewDir(callingMethod: callingMethod, identifier: identifier);

            try
            {
                new NewCommand()
                    .WithWorkingDirectory(dir.Path)
                    .ExecuteWithCapturedOutput("-t Lib")
                .Should().Pass();
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new Exception($"Intermittent error in `dotnet new` occurred when running it in dir `{dir.Path}`\nException:\n{e}");
            }

            return dir;
        }

        private static void SetTargetFrameworks(ProjDir proj, string[] frameworks)
        {
            var csproj = proj.CsProj();
            csproj.AddProperty("TargetFrameworks", string.Join(";", frameworks));
            csproj.Save();
        }

        private ProjDir NewLibWithFrameworks([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var ret = NewLib(callingMethod: callingMethod, identifier: identifier);
            SetTargetFrameworks(ret, DefaultFrameworks);
            return ret;
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new AddP2PCommand().Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new AddP2PCommand()
                    .WithProject("one two three")
                    .Execute("proj.csproj");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("Unrecognized command or argument");
        }

        [Theory]
        [InlineData("idontexist.csproj")]
        [InlineData("ihave?inv@lid/char\\acters")]
        public void WhenNonExistingProjectIsPassedItPrintsErrorAndUsage(string projName)
        {
            var setup = Setup();

            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .WithProject(projName)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("Could not find");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]
        public void WhenBrokenProjectIsPassedItPrintsErrorAndUsage()
        {
            string projName = "Broken/Broken.csproj";
            var setup = Setup();

            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .WithProject(projName)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain(" is invalid.");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]
        public void WhenMoreThanOneProjectExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(Path.Combine(setup.TestRoot, "MoreThanOne"))
                    .Execute($"\"{setup.ValidRefCsprojRelToOtherProjPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("more than one");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]
        public void WhenNoProjectsExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("not find any");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]
        public void ItAddsRefWithoutCondAndPrintsStatus()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void ItAddsRefWithCondAndPrintsStatus()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg} \"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithoutCondIsPresentItAddsDifferentRefWithoutCond()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{setup.LibCsprojPath}\"")
                .Should().Pass();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondIsPresentItAddsDifferentRefWithCond()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg} \"{setup.LibCsprojPath}\"")
                .Should().Pass();

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg} \"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondIsPresentItAddsRefWithDifferentCond()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNetCoreApp10Arg} \"{setup.ValidRefCsprojPath}\"")
                .Should().Pass();

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg} \"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithConditionIsPresentItAddsDifferentRefWithoutCond()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg} \"{setup.LibCsprojPath}\"")
                .Should().Pass();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();

            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithNoCondAlreadyExistsItDoesntDuplicate()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{setup.ValidRefCsprojPath}\"")
                .Should().Pass();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");

            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondOnItemAlreadyExistsItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithExistingRefCondOnItem"));

            string contentBefore = proj.CsProjContent();
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(proj.Path)
                    .WithProject(proj.CsProjPath)
                    .Execute($"{FrameworkNet451Arg} \"{setup.LibCsprojRelPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithCondOnItemGroupAlreadyExistsItDoesntDuplicate()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg} \"{setup.ValidRefCsprojPath}\"")
                .Should().Pass();

            var csprojContentBefore = lib.CsProjContent();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg} \"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");
            lib.CsProjContent().Should().BeEquivalentTo(csprojContentBefore);
        }

        [Fact]
        public void WhenRefWithCondWithWhitespaceOnItemGroupExistsItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithExistingRefCondWhitespaces"));

            string contentBefore = proj.CsProjContent();
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(proj.Path)
                    .WithProject(proj.CsProjName)
                    .Execute($"{FrameworkNet451Arg} \"{setup.LibCsprojRelPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithoutCondAlreadyExistsInNonUniformItemGroupItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefNoCondNonUniform"));

            string contentBefore = proj.CsProjContent();
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(proj.Path)
                    .WithProject(proj.CsProjName)
                    .Execute($"\"{setup.LibCsprojRelPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithoutCondAlreadyExistsInNonUniformItemGroupItAddsDifferentRefInDifferentGroup()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefNoCondNonUniform"));

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .WithProject(proj.CsProjPath)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondAlreadyExistsInNonUniformItemGroupItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefCondNonUniform"));

            string contentBefore = proj.CsProjContent();
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(proj.Path)
                    .WithProject(proj.CsProjName)
                    .Execute($"{FrameworkNet451Arg} \"{setup.LibCsprojRelPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithCondAlreadyExistsInNonUniformItemGroupItAddsDifferentRefInDifferentGroup()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefCondNonUniform"));

            int condBefore = proj.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .WithProject(proj.CsProjPath)
                    .Execute($"{FrameworkNet451Arg} \"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenEmptyItemGroupPresentItAddsRefInIt()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "EmptyItemGroup"));

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(setup.TestRoot)
                    .WithProject(proj.CsProjPath)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void ItAddsMultipleRefsNoCondToTheSameItemGroup()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{setup.LibCsprojPath}\" \"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project").And.NotContain("already has a reference");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.LibCsprojName).Should().Be(1);
        }

        [Fact]
        public void ItAddsMultipleRefsWithCondToTheSameItemGroup()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"{FrameworkNet451Arg}  \"{setup.LibCsprojPath}\" \"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project").And.NotContain("already has a reference");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.LibCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenProjectNameIsNotPassedItFindsItAndAddsReference()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void ItAddsRefBetweenImports()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            cmd.StdErr.Should().BeEmpty();

            int state = 0;
            foreach (var el in lib.CsProj().AllChildren)
            {
                var import = el as ProjectImportElement;
                var projRef = el as ProjectItemElement;
                switch (state)
                {
                    case 0:
                        if (import != null && import.Project.EndsWith(".props"))
                        {
                            state++;
                        }
                        break;
                    case 1:
                        if (projRef != null && projRef.ItemType == "ProjectReference" && projRef.Include.Contains(setup.ValidRefCsprojName))
                        {
                            state++;
                        }
                        break;
                    case 2:
                        if (import != null && import.Project.EndsWith(".targets"))
                        {
                            state++;
                        }
                        break;
                }
            }

            state.Should().Be(3);
        }

        [Fact]
        public void WhenPassedReferenceDoesNotExistItShowsAnError()
        {
            var lib = NewLibWithFrameworks();

            var contentBefore = lib.CsProjContent();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute("\"IDoNotExist.csproj\"");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("does not exist");
            lib.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenPassedMultipleRefsAndOneOfthemDoesNotExistItCancelsWholeOperation()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            var contentBefore = lib.CsProjContent();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(lib.CsProjPath)
                .Execute($"\"{setup.ValidRefCsprojPath}\" \"IDoNotExist.csproj\"");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("does not exist");
            cmd.StdErr.Should().NotMatchRegex("(.*does not exist.*){2,}");
            lib.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenPassedReferenceIsUsingSlashesItNormalizesItToBackslashes()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"\"{setup.ValidRefCsprojPath.Replace('\\', '/')}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojRelPath.Replace('/', '\\')).Should().Be(1);
        }

        [Fact]
        public void WhenReferenceIsRelativeAndProjectIsNotInCurrentDirectoryReferencePathIsFixed()
        {
            var setup = Setup();
            var proj = new ProjDir(setup.LibDir);

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(setup.TestRoot)
                .WithProject(setup.LibCsprojPath)
                .Execute($"\"{setup.ValidRefCsprojRelPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            cmd.StdErr.Should().BeEmpty();
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojRelToOtherProjPath.Replace('/', '\\')).Should().Be(1);
        }

        [Fact]
        public void ItCanAddReferenceWithConditionOnCompatibleFramework()
        {
            var setup = Setup();
            var lib = new ProjDir(setup.LibDir);
            var net45lib = new ProjDir(Path.Combine(setup.TestRoot, "Net45Lib"));

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new AddP2PCommand()
                    .WithProject(lib.CsProjPath)
                    .Execute($"{FrameworkNet451Arg} \"{net45lib.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(net45lib.CsProjName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void ItCanAddRefWithoutCondAndTargetingSupersetOfFrameworksAndOneOfReferencesCompatible()
        {
            var setup = Setup();
            var lib = new ProjDir(setup.LibDir);
            var net452netcoreapp10lib = new ProjDir(Path.Combine(setup.TestRoot, "Net452AndNetCoreApp10Lib"));

            int noCondBefore = net452netcoreapp10lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                    .WithProject(net452netcoreapp10lib.CsProjPath)
                    .Execute($"\"{lib.CsProjPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = net452netcoreapp10lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(lib.CsProjName).Should().Be(1);
        }

        [Theory]
        [InlineData("net45")]
        [InlineData("net40")]
        [InlineData("netcoreapp1.1")]
        [InlineData("nonexistingframeworkname")]
        public void WhenFrameworkSwitchIsNotMatchingAnyOfTargetedFrameworksItPrintsError(string framework)
        {
            var setup = Setup();
            var lib = new ProjDir(setup.LibDir);
            var net45lib = new ProjDir(Path.Combine(setup.TestRoot, "Net45Lib"));

            var csProjContent = lib.CsProjContent();
            var cmd = new AddP2PCommand()
                    .WithProject(lib.CsProjPath)
                    .Execute($"-f {framework} \"{net45lib.CsProjPath}\"");
            cmd.Should().Fail();
            cmd.StdErr.Should().MatchRegex(ProjectDoesNotTargetFrameworkErrorMessageRegEx);
            cmd.StdErr.Should().Contain($"`{framework}`");
            lib.CsProjContent().Should().BeEquivalentTo(csProjContent);
        }

        [Theory]
        [InlineData("")]
        [InlineData("-f net45")]
        public void WhenIncompatibleFrameworkDetectedItPrintsError(string frameworkArg)
        {
            var setup = Setup();
            var lib = new ProjDir(setup.LibDir);
            var net45lib = new ProjDir(Path.Combine(setup.TestRoot, "Net45Lib"));

            var csProjContent = net45lib.CsProjContent();
            var cmd = new AddP2PCommand()
                    .WithProject(net45lib.CsProjPath)
                    .Execute($"{frameworkArg} \"{lib.CsProjPath}\"");
            cmd.Should().Fail();
            cmd.StdErr.Should().MatchRegex(ProjectNotCompatibleErrorMessageRegEx);
            cmd.StdErr.Should().MatchRegex(" - net45");
            net45lib.CsProjContent().Should().BeEquivalentTo(csProjContent);
        }
    }
}
