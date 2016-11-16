// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.Cli.Add.P2P.Tests
{
    public class GivenDotnetAddP2P : TestBase
    {
        const string FrameworkNet451Arg = "-f net451";
        const string ConditionFrameworkNet451 = "== 'net451'";
        const string FrameworkNetCoreApp10Arg = "-f netcoreapp1.0";
        const string ConditionFrameworkNetCoreApp10 = "== 'netcoreapp1.0'";
        private static readonly string ValidRef = "ValidRef";
        private static readonly string ValidRefCsproj = $"{ValidRef}.csproj";
        private static readonly string ValidRefPath = Path.Combine("..", ValidRef, ValidRefCsproj);

        private static readonly string LibRef = "Lib";
        private static readonly string LibRefCsproj = $"{LibRef}.csproj";
        private static readonly string LibRefPath = Path.Combine("..", LibRef, LibRefCsproj);

        private static readonly string AppPath = Path.Combine("App", "App.csproj");
        private static readonly string Lib1Path = Path.Combine("Lib1", "Lib1.csproj");
        private static readonly string Lib2Path = Path.Combine("Lib2", "Lib2.csproj");
        private static readonly string Lib3Path = Path.Combine("Lib3", "Lib3.csproj");
        private static readonly string Lib4Path = Path.Combine("Lib4", "Lib4.csproj");

        private string Setup([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(Setup), string identifier = "")
        {
            const string TestGroup = "NonRestoredTestProjects";
            const string ProjectName = "DotnetAddP2PProjects";
            return GetTestGroupTestAssetsManager(TestGroup).CreateTestInstance(ProjectName, callingMethod: callingMethod, identifier: identifier).Path;
        }

        private ProjDir NewDir([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            return new ProjDir(TestAssetsManager, callingMethod, identifier: identifier);
        }

        private ProjDir NewLib([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var dir = new ProjDir(TestAssetsManager, callingMethod, identifier: identifier);

            try
            {
                new NewCommand()
                    .WithWorkingDirectory(dir.Path)
                    .ExecuteWithCapturedOutput("-t Lib")
                .Should().Pass();
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new Exception($"DIDIDIDIDOIDIR: {dir.Path}\n{e}");
            }

            return dir;
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

        [Theory]
        [InlineData("idontexist.csproj")]
        [InlineData("ihave?inv@lid/char\\acters")]
        public void WhenNonExistingProjectIsPassedItPrintsErrorAndUsage(string projName)
        {
            string testRoot = NewDir().Path;
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(testRoot)
                    .WithProject(projName)
                    .Execute($"\"{ValidRefPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("Could not find");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]

        public void WhenBrokenProjectIsPassedItPrintsErrorAndUsage()
        {
            string projName = "Broken/Broken.csproj";
            string testRoot = Setup();
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(testRoot)
                    .WithProject(projName)
                    .Execute($"\"{ValidRefPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("Invalid project");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]
        public void WhenMoreThanOneProjectExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            string testRoot = Setup();
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(Path.Combine(testRoot, "MoreThanOne"))
                    .Execute($"\"{ValidRefPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("more than one");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]
        public void WhenNoProjectsExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            string testRoot = Setup();
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(testRoot)
                    .Execute($"\"{ValidRefPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Contain("not find any");
            cmd.StdOut.Should().Contain("Usage");
        }

        [Fact]
        public void ItAddsRefWithoutCondAndPrintsStatus()
        {
            var lib = NewLib();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"\"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(ValidRefCsproj).Should().Be(1);
        }

        [Fact]
        public void ItAddsRefWithCondAndPrintsStatus()
        {
            var lib = NewLib();

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"{FrameworkNet451Arg} \"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(ValidRefCsproj, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithoutCondIsPresentItAddsDifferentRefWithoutCond()
        {
            var lib = NewLib();

            new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"\"{LibRefPath}\"")
                .Should().Pass();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"\"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(ValidRefCsproj).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondIsPresentItAddsDifferentRefWithCond()
        {
            var lib = NewLib();

            new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"{FrameworkNet451Arg} \"{LibRefPath}\"")
                .Should().Pass();

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"{FrameworkNet451Arg} \"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(ValidRefCsproj, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondIsPresentItAddsRefWithDifferentCond()
        {
            var lib = NewLib();

            new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"{FrameworkNetCoreApp10Arg} \"{ValidRefPath}\"")
                .Should().Pass();

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"{FrameworkNet451Arg} \"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(ValidRefCsproj, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithConditionIsPresentItAddsDifferentRefWithoutCond()
        {
            var lib = NewLib();

            new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"{FrameworkNet451Arg} \"{LibRefPath}\"")
                .Should().Pass();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"\"{ValidRefPath}\"");
            cmd.Should().Pass();

            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(ValidRefCsproj).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithNoCondAlreadyExistsItDoesntDuplicate()
        {
            var lib = NewLib();

            new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"\"{ValidRefPath}\"")
                .Should().Pass();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"\"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");

            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(ValidRefCsproj).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondOnItemAlreadyExistsItDoesntDuplicate()
        {
            string testRoot = Setup();

            string projDir = Path.Combine(testRoot, "WithExistingRefCondOnItem");
            string projName = Path.Combine(projDir, "WithExistingRefCondOnItem.csproj");
            string contentBefore = File.ReadAllText(projName);
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(projDir)
                    .WithProject(projName)
                    .Execute($"{FrameworkNet451Arg} \"{LibRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");
            File.ReadAllText(projName).Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithCondOnItemGroupAlreadyExistsItDoesntDuplicate()
        {
            var lib = NewLib();

            new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"{FrameworkNet451Arg} \"{ValidRefPath}\"")
                .Should().Pass();

            var csprojContentBefore = lib.CsProjContent();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"{FrameworkNet451Arg} \"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");
            lib.CsProjContent().Should().BeEquivalentTo(csprojContentBefore);
        }

        [Fact]
        public void WhenRefWithCondWithWhitespaceOnItemGroupExistsItDoesntDuplicate()
        {
            string testRoot = Setup();

            string projDir = Path.Combine(testRoot, "WithExistingRefCondWhitespaces");
            string projName = Path.Combine(projDir, "WithExistingRefCondWhitespaces.csproj");
            string contentBefore = File.ReadAllText(projName);
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(projDir)
                    .WithProject(projName)
                    .Execute($"{FrameworkNet451Arg} \"{LibRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");
            File.ReadAllText(projName).Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithoutCondAlreadyExistsInNonUniformItemGroupItDoesntDuplicate()
        {
            string testRoot = Setup();

            string projDir = Path.Combine(testRoot, "WithRefNoCondNonUniform");
            string projName = Path.Combine(projDir, "WithRefNoCondNonUniform.csproj");
            string contentBefore = File.ReadAllText(projName);
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(projDir)
                    .WithProject(projName)
                    .Execute($"\"{LibRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");
            File.ReadAllText(projName).Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithoutCondAlreadyExistsInNonUniformItemGroupItAddsDifferentRefInDifferentGroup()
        {
            string testRoot = Setup();

            var proj = new ProjDir(Path.Combine(testRoot, "WithRefNoCondNonUniform"));

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(proj.Path)
                    .WithProject(proj.CsProjPath)
                    .Execute($"\"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(ValidRefPath).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondAlreadyExistsInNonUniformItemGroupItDoesntDuplicate()
        {
            string testRoot = Setup();

            string projDir = Path.Combine(testRoot, "WithRefCondNonUniform");
            string projName = Path.Combine(projDir, "WithRefCondNonUniform.csproj");
            string contentBefore = File.ReadAllText(projName);
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(projDir)
                    .WithProject(projName)
                    .Execute($"{FrameworkNet451Arg} \"{LibRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("already has a reference");
            File.ReadAllText(projName).Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithCondAlreadyExistsInNonUniformItemGroupItAddsDifferentRefInDifferentGroup()
        {
            string testRoot = Setup();

            var proj = new ProjDir(Path.Combine(testRoot, "WithRefCondNonUniform"));

            int condBefore = proj.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(proj.Path)
                    .WithProject(proj.CsProjPath)
                    .Execute($"{FrameworkNet451Arg} \"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(ValidRefPath, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenEmptyItemGroupPresentItAddsRefInIt()
        {
            string testRoot = Setup();

            var proj = new ProjDir(Path.Combine(testRoot, "EmptyItemGroup"));

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                    .WithWorkingDirectory(proj.Path)
                    .WithProject(proj.CsProjPath)
                    .Execute($"\"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project");
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(ValidRefPath).Should().Be(1);
        }

        [Fact]
        public void ItAddsMultipleRefsNoCondToTheSameItemGroup()
        {
            var lib = NewLib();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"\"{LibRefPath}\" \"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project").And.NotContain("already has a reference");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(ValidRefCsproj).Should().Be(1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(LibRefPath).Should().Be(1);
        }

        [Fact]
        public void ItAddsMultipleRefsWithCondToTheSameItemGroup()
        {
            var lib = NewLib();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new AddP2PCommand()
                .WithWorkingDirectory(lib.Path)
                .WithProject(lib.CsProjName)
                .Execute($"{FrameworkNet451Arg}  \"{LibRefPath}\" \"{ValidRefPath}\"");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("added to the project").And.NotContain("already has a reference");
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(ValidRefCsproj, ConditionFrameworkNet451).Should().Be(1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(LibRefPath, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact(Skip = "Not finished")]
        public void WhenProjectNameIsNotPassedItFindsItAndAddsReference()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not finished")]
        public void ItAddsRefBeforeImports()
        {
            throw new NotImplementedException();
        }
    }
}