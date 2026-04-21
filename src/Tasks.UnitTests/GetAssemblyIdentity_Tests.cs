// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class GetAssemblyIdentity_Tests
    {
        private readonly ITestOutputHelper _output;

        public GetAssemblyIdentity_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        private GetAssemblyIdentity CreateTaskUnderTest(MockEngine engine = null)
        {
            return new GetAssemblyIdentity
            {
                BuildEngine = engine ?? new MockEngine(_output),
            };
        }

        [Fact]
        public void AbsolutePathToExistingAssembly_ProducesCorrectIdentity()
        {
            // Use this test assembly as the input — it's guaranteed to exist.
            string assemblyPath = typeof(GetAssemblyIdentity_Tests).Assembly.Location;
            AssemblyName expectedName = AssemblyName.GetAssemblyName(assemblyPath);

            GetAssemblyIdentity task = CreateTaskUnderTest();
            task.AssemblyFiles = [new TaskItem(assemblyPath)];

            task.Execute().ShouldBeTrue();

            task.Assemblies.Length.ShouldBe(1);
            task.Assemblies[0].ItemSpec.ShouldBe(expectedName.FullName);
            task.Assemblies[0].GetMetadata("Name").ShouldBe(expectedName.Name);
            task.Assemblies[0].GetMetadata("Version").ShouldBe(expectedName.Version.ToString());
        }

        [Fact]
        public void NonExistentFile_LogsErrorAndReturnsFalse()
        {
            var engine = new MockEngine(_output);
            GetAssemblyIdentity task = CreateTaskUnderTest(engine);
            task.AssemblyFiles = [new TaskItem("does-not-exist.dll")];

            task.Execute().ShouldBeFalse();

            AssertCouldNotGetAssemblyName(engine);
            engine.Log.ShouldContain("does-not-exist.dll");
        }

        [Fact]
        public void NonAssemblyFile_LogsErrorAndReturnsFalse()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFile textFile = env.CreateFile("notanassembly.txt", "This is not an assembly.");

            var engine = new MockEngine(_output);
            GetAssemblyIdentity task = CreateTaskUnderTest(engine);
            task.AssemblyFiles = [new TaskItem(textFile.Path)];

            task.Execute().ShouldBeFalse();

            AssertCouldNotGetAssemblyName(engine);
        }

        [Fact]
        public void MixedBatch_GoodAndBadItems()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFile textFile = env.CreateFile("bad.txt", "not an assembly");

            string goodAssemblyPath = typeof(GetAssemblyIdentity_Tests).Assembly.Location;
            AssemblyName expectedName = AssemblyName.GetAssemblyName(goodAssemblyPath);

            var engine = new MockEngine(_output);
            GetAssemblyIdentity task = CreateTaskUnderTest(engine);
            task.AssemblyFiles =
            [
                new TaskItem(goodAssemblyPath),
                new TaskItem(textFile.Path),
            ];

            // Execute returns false because one item failed.
            task.Execute().ShouldBeFalse();

            // The good item should still appear in the output.
            task.Assemblies.Length.ShouldBe(1);
            task.Assemblies[0].ItemSpec.ShouldBe(expectedName.FullName);

            // The bad item should have produced an error.
            engine.Errors.ShouldBe(1);
        }

        [Fact]
        public void EmptyItemSpec_LogsErrorAndReturnsFalse()
        {
            var engine = new MockEngine(_output);
            GetAssemblyIdentity task = CreateTaskUnderTest(engine);
            task.AssemblyFiles = [new TaskItem(string.Empty)];

            task.Execute().ShouldBeFalse();

            AssertCouldNotGetAssemblyName(engine);
        }

        [Fact]
        public void RelativePath_ResolvesAgainstProjectDirectory()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFolder projectDir = env.CreateFolder();

            string sourceAssembly = typeof(GetAssemblyIdentity_Tests).Assembly.Location;
            AssemblyName expectedName = AssemblyName.GetAssemblyName(sourceAssembly);

            string relativeFileName = "TestAssembly.dll";
            File.Copy(sourceAssembly, Path.Combine(projectDir.Path, relativeFileName));

            var engine = new MockEngine(_output);
            GetAssemblyIdentity task = CreateTaskUnderTest(engine);
            task.TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir.Path);
            task.AssemblyFiles = [new TaskItem(relativeFileName)];

            task.Execute().ShouldBeTrue();

            task.Assemblies.Length.ShouldBe(1);
            task.Assemblies[0].ItemSpec.ShouldBe(expectedName.FullName);
        }

        [Fact]
        public void OutputItem_DoesNotContainAbsolutizedPaths()
        {
            string assemblyPath = typeof(GetAssemblyIdentity_Tests).Assembly.Location;
            AssemblyName expectedName = AssemblyName.GetAssemblyName(assemblyPath);

            GetAssemblyIdentity task = CreateTaskUnderTest();
            task.AssemblyFiles = [new TaskItem(assemblyPath)];

            task.Execute().ShouldBeTrue();

            task.Assemblies.Length.ShouldBe(1);

            // ItemSpec should be the assembly identity string, not a path.
            task.Assemblies[0].ItemSpec.ShouldBe(expectedName.FullName);
        }

        [Fact]
        public void OutputItem_CopiesInputMetadataVerbatim()
        {
            string assemblyPath = typeof(GetAssemblyIdentity_Tests).Assembly.Location;

            var inputItem = new TaskItem(assemblyPath);
            inputItem.SetMetadata("CustomMeta", "CustomValue");

            GetAssemblyIdentity task = CreateTaskUnderTest();
            task.AssemblyFiles = [inputItem];

            task.Execute().ShouldBeTrue();

            task.Assemblies.Length.ShouldBe(1);
            task.Assemblies[0].GetMetadata("CustomMeta").ShouldBe("CustomValue");
        }

        private void AssertCouldNotGetAssemblyName(MockEngine engine)
        {
            engine.Errors.ShouldBe(1);
            engine.Log.ShouldContain("MSB3441");
        }
    }
}
