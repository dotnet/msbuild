// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class GetAssembliesMetadata_Tests
    {
        private static string TestAssembliesPaths { get; } = Path.Combine(AppContext.BaseDirectory, "TestResources", "Projects");

        private readonly ITestOutputHelper _testOutput;

        public GetAssembliesMetadata_Tests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Fact]
        public void CheckPresenceOfCustomCOMAssemblyAttributes()
        {
            string testSolutionPath = Path.Combine(TestAssembliesPaths, "Custom_COM");
            RunnerUtilities.ExecMSBuild(testSolutionPath, out bool success, _testOutput);
            string assemblyPath = Path.Combine(testSolutionPath, "Custom_COM", "bin", "Debug", "Custom_COM.dll");
            GetAssembliesMetadata t = new() { AssemblyPaths = new[] { assemblyPath } };

            bool isSuccess = t.Execute();

            success.ShouldBeTrue();
            isSuccess.ShouldBeTrue();
            t.AssembliesMetadata[0].ItemSpec.ShouldBe(assemblyPath);
            t.AssembliesMetadata[0].GetMetadata("AssemblyName").ShouldBe("Custom_COM");
            t.AssembliesMetadata[0].GetMetadata("IsImportedFromTypeLib").ShouldBe("False");
            t.AssembliesMetadata[0].GetMetadata("RevisionNumber").ShouldBe("4");
            t.AssembliesMetadata[0].GetMetadata("IsAssembly").ShouldBe("True");
            t.AssembliesMetadata[0].GetMetadata("RuntimeVersion").ShouldBe("v4.0.30319");
            t.AssembliesMetadata[0].GetMetadata("MajorVersion").ShouldBe("1");
            t.AssembliesMetadata[0].GetMetadata("MinorVersion").ShouldBe("2");
            t.AssembliesMetadata[0].GetMetadata("PeKind").ShouldBe("1");
            t.AssembliesMetadata[0].GetMetadata("BuildNumber").ShouldBe("3");
            t.AssembliesMetadata[0].GetMetadata("Description").ShouldBe("description for com");
            t.AssembliesMetadata[0].GetMetadata("Culture").ShouldBeEmpty();
            t.AssembliesMetadata[0].GetMetadata("TargetFrameworkMoniker").ShouldBe(".NETFramework,Version=v4.7.2");
            t.AssembliesMetadata[0].GetMetadata("DefaultAlias").ShouldBe("Custom_COM");
            t.AssembliesMetadata[0].GetMetadata("PublicHexKey").ShouldBeEmpty();
        }

        [Fact]
        public void CheckPresenceOfCOMAssemblyAttributes()
        {
            string pathToWinFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string assemblyPath = Path.Combine(pathToWinFolder, "Microsoft.NET", "Framework", "v4.0.30319", "mscorlib.dll");
            GetAssembliesMetadata t = new() { AssemblyPaths = new[] { assemblyPath } };

            bool isSuccess = t.Execute();

            isSuccess.ShouldBeTrue();
            t.AssembliesMetadata[0].ItemSpec.ShouldBe(assemblyPath);
            t.AssembliesMetadata[0].GetMetadata("AssemblyName").ShouldBe("mscorlib");
            t.AssembliesMetadata[0].GetMetadata("IsImportedFromTypeLib").ShouldBe("False");
            t.AssembliesMetadata[0].GetMetadata("RevisionNumber").ShouldBe("0");
            t.AssembliesMetadata[0].GetMetadata("IsAssembly").ShouldBe("True");
            t.AssembliesMetadata[0].GetMetadata("RuntimeVersion").ShouldBe("v4.0.30319");
            t.AssembliesMetadata[0].GetMetadata("MajorVersion").ShouldBe("4");
            t.AssembliesMetadata[0].GetMetadata("MinorVersion").ShouldBe("0");
            t.AssembliesMetadata[0].GetMetadata("PeKind").ShouldBe("3");
            t.AssembliesMetadata[0].GetMetadata("BuildNumber").ShouldBe("0");
            t.AssembliesMetadata[0].GetMetadata("Description").ShouldBe("mscorlib.dll");
            t.AssembliesMetadata[0].GetMetadata("Culture").ShouldBeEmpty();
            t.AssembliesMetadata[0].GetMetadata("TargetFrameworkMoniker").ShouldBeEmpty();
            t.AssembliesMetadata[0].GetMetadata("DefaultAlias").ShouldBe("mscorlib");
            t.AssembliesMetadata[0].GetMetadata("PublicHexKey").ShouldBe("00000000000000000400000000000000");
        }
    }

    /// <summary>
    /// Isolated because <see cref="AssemblyPathIsResolvedAgainstProjectDirectoryNotCurrentDirectory"/> mutates the
    /// process-wide current directory, which cannot be done safely while other collections run concurrently.
    /// Only this test needs the serialization, so it lives apart from <see cref="GetAssembliesMetadata_Tests"/>.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class GetAssembliesMetadataCurrentDirectoryCollection
    {
        public const string Name = nameof(GetAssembliesMetadataCurrentDirectoryCollection);
    }

    [Collection(GetAssembliesMetadataCurrentDirectoryCollection.Name)]
    public class GetAssembliesMetadata_CurrentDirectory_Tests
    {
        private readonly ITestOutputHelper _testOutput;

        public GetAssembliesMetadata_CurrentDirectory_Tests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        /// <summary>
        /// Regression test for the multithreaded migration: a relative assembly path must resolve against the task's
        /// <see cref="TaskEnvironment"/> project directory, not the process current directory. The current directory is
        /// pointed at a decoy folder that contains no assembly at all, so the task can only succeed if it absolutizes
        /// the input against the project directory.
        /// </summary>
        [Fact]
        public void AssemblyPathIsResolvedAgainstProjectDirectoryNotCurrentDirectory()
        {
            using TestEnvironment env = TestEnvironment.Create(_testOutput);
            TransientTestFolder projectFolder = env.CreateFolder();
            TransientTestFolder decoyFolder = env.CreateFolder();

            string pathToWinFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sourceAssemblyPath = Path.Combine(pathToWinFolder, "Microsoft.NET", "Framework", "v4.0.30319", "mscorlib.dll");
            Directory.CreateDirectory(Path.Combine(projectFolder.Path, "libs"));
            File.Copy(sourceAssemblyPath, Path.Combine(projectFolder.Path, "libs", "mscorlib.dll"));

            // The decoy is deliberately empty: if the task resolved against the current directory it would find nothing.
            env.SetCurrentDirectory(decoyFolder.Path);

            GetAssembliesMetadata t = new()
            {
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectFolder.Path),
                AssemblyPaths = [@"libs\mscorlib.dll"],
            };

            bool isSuccess = t.Execute();

            isSuccess.ShouldBeTrue();
            t.AssembliesMetadata.Length.ShouldBe(1);

            // The ItemSpec must stay the exact user-supplied relative path, not the absolutized form used to read metadata.
            t.AssembliesMetadata[0].ItemSpec.ShouldBe(@"libs\mscorlib.dll");
            t.AssembliesMetadata[0].GetMetadata("AssemblyName").ShouldBe("mscorlib");
            t.AssembliesMetadata[0].GetMetadata("IsAssembly").ShouldBe("True");
            t.AssembliesMetadata[0].GetMetadata("RuntimeVersion").ShouldBe("v4.0.30319");
            t.AssembliesMetadata[0].GetMetadata("PublicHexKey").ShouldBe("00000000000000000400000000000000");
        }
    }
}
#endif
