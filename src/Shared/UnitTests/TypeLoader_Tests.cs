// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class TypeLoader_Tests
    {
        private static readonly string ProjectFileFolder = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "PortableTask");
        private const string ProjectFileName = "portableTaskTest.proj";
        private const string DLLFileName = "PortableTask.dll";
        private static string PortableTaskFolderPath = Path.GetFullPath(
                    Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "..", "..", "..", "Samples", "PortableTask"));

        private readonly ITestOutputHelper _output;

        public TypeLoader_Tests(ITestOutputHelper testOutputHelper)
        {
            _output = testOutputHelper;
        }

        [Fact]
        public void Basic()
        {
            Assert.True(TypeLoader.IsPartialTypeNameMatch("Csc", "csc")); // ==> exact match
            Assert.True(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Microsoft.Build.Tasks.Csc")); // ==> exact match
            Assert.True(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Csc")); // ==> partial match
            Assert.True(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Tasks.Csc")); // ==> partial match
            Assert.True(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask+NestedTask", "NestedTask")); // ==> partial match
            Assert.True(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\\\+NestedTask", "NestedTask")); // ==> partial match
            Assert.False(TypeLoader.IsPartialTypeNameMatch("MyTasks.CscTask", "Csc")); // ==> no match
            Assert.False(TypeLoader.IsPartialTypeNameMatch("MyTasks.MyCsc", "Csc")); // ==> no match
            Assert.False(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\.Csc", "Csc")); // ==> no match
            Assert.False(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\\\\\.Csc", "Csc")); // ==> no match
        }

        [Fact]
        public void Regress_Mutation_TrailingPartMustMatch()
        {
            Assert.False(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Vbc"));
        }

        [Fact]
        public void Regress_Mutation_ParameterOrderDoesntMatter()
        {
            Assert.True(TypeLoader.IsPartialTypeNameMatch("Csc", "Microsoft.Build.Tasks.Csc"));
        }


        [Fact]
        public void LoadNonExistingAssembly()
        {
            using var dir = new FileUtilities.TempWorkingDirectory(ProjectFileFolder);

            string projectFilePath = Path.Combine(dir.Path, ProjectFileName);

            string dllName = "NonExistent.dll";

            bool successfulExit;
            string output = RunnerUtilities.ExecMSBuild(projectFilePath + " /v:diag /p:AssemblyPath=" + dllName, out successfulExit, _output);
            successfulExit.ShouldBeFalse();

            string dllPath = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, dllName);
            CheckIfCorrectAssemblyLoaded(output, dllPath, false);
        }

        [Fact]
        public void LoadInsideAsssembly()
        {
            using (var dir = new FileUtilities.TempWorkingDirectory(ProjectFileFolder))
            {
                string projectFilePath = Path.Combine(dir.Path, ProjectFileName);

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(projectFilePath + " /v:diag", out successfulExit, _output);
                Assert.True(successfulExit);

                string dllPath = Path.Combine(dir.Path, DLLFileName);

                CheckIfCorrectAssemblyLoaded(output, dllPath);
            }
        }

        [Fact]
        public void LoadTaskDependingOnMSBuild()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFolder folder = env.CreateFolder(createFolder: true);
                string currentAssembly = Assembly.GetExecutingAssembly().Location;
                string utilitiesName = "Microsoft.Build.Utilities.Core.dll";
                string newAssemblyLocation = Path.Combine(folder.Path, Path.GetFileName(currentAssembly));

                // The "first" directory is "Debug" or "Release"
                string portableTaskPath = Path.Combine(Directory.GetDirectories(PortableTaskFolderPath).First(), "netstandard2.0", "OldMSBuild");
                string utilities = Path.Combine(portableTaskPath, utilitiesName);
                File.Copy(utilities, Path.Combine(folder.Path, utilitiesName));
                File.Copy(currentAssembly, newAssemblyLocation);
                TypeLoader typeLoader = new(TaskLoader.IsTaskClass);

                // If we cannot accept MSBuild next to the task assembly we're loading, this will throw.
                typeLoader.Load("TypeLoader_Tests", AssemblyLoadInfo.Create(null, newAssemblyLocation), useTaskHost: true);
            }
        }

        [Fact]
        public void LoadOutsideAssembly()
        {
            using (var dir = new FileUtilities.TempWorkingDirectory(ProjectFileFolder))
            {
                string projectFilePath = Path.Combine(dir.Path, ProjectFileName);
                string originalDLLPath = Path.Combine(dir.Path, DLLFileName);

                string movedDLLPath = MoveOrCopyDllToTempDir(originalDLLPath, copy: false);

                try
                {
                    bool successfulExit;
                    string output = RunnerUtilities.ExecMSBuild(projectFilePath + " /v:diag /p:AssemblyPath=" + movedDLLPath, out successfulExit, _output);
                    Assert.True(successfulExit);

                    CheckIfCorrectAssemblyLoaded(output, movedDLLPath);
                }
                finally
                {
                    UndoDLLOperations(movedDLLPath);
                }
            }
        }

        [Fact(Skip = "https://github.com/dotnet/msbuild/issues/325")]
        public void LoadInsideAssemblyWhenGivenOutsideAssemblyWithSameName()
        {
            using (var dir = new FileUtilities.TempWorkingDirectory(ProjectFileFolder))
            {
                string projectFilePath = Path.Combine(dir.Path, ProjectFileName);
                string originalDLLPath = Path.Combine(dir.Path, DLLFileName);
                string copiedDllPath = MoveOrCopyDllToTempDir(originalDLLPath, copy: true);

                try
                {
                    bool successfulExit;
                    string output = RunnerUtilities.ExecMSBuild(projectFilePath + " /v:diag /p:AssemblyPath=" + copiedDllPath, out successfulExit, _output);
                    Assert.True(successfulExit);

                    CheckIfCorrectAssemblyLoaded(output, originalDLLPath);
                }
                finally
                {
                    UndoDLLOperations(copiedDllPath);
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="copy"></param>
        /// <returns>Path to new DLL</returns>
        private string MoveOrCopyDllToTempDir(string originalDllPath, bool copy)
        {
            var temporaryDirectory = FileUtilities.GetTemporaryDirectory();
            var newDllPath = Path.Combine(temporaryDirectory, DLLFileName);

            Assert.True(File.Exists(originalDllPath));

            if (copy)
            {
                File.Copy(originalDllPath, newDllPath);

                Assert.True(File.Exists(newDllPath));
            }
            else
            {
                File.Move(originalDllPath, newDllPath);

                Assert.True(File.Exists(newDllPath));
                Assert.False(File.Exists(originalDllPath));
            }
            return newDllPath;
        }

        /// <summary>
        /// Delete newDllPath and delete temp directory
        /// </summary>
        /// <param name="newDllPath"></param>
        /// <param name="moveBack">If true, move newDllPath back to bin. If false, delete it</param>
        private void UndoDLLOperations(string newDllPath)
        {
            var tempDirectoryPath = Path.GetDirectoryName(newDllPath);

            File.Delete(newDllPath);

            Assert.False(File.Exists(newDllPath));
            Assert.Empty(Directory.EnumerateFiles(tempDirectoryPath));

            Directory.Delete(tempDirectoryPath);
            Assert.False(Directory.Exists(tempDirectoryPath));
        }

        private void CheckIfCorrectAssemblyLoaded(string scriptOutput, string expectedAssemblyPath, bool expectedSuccess = true)
        {
            var successfulMessage = @"Using ""ShowItems"" task from assembly """ + expectedAssemblyPath + @""".";

            if (expectedSuccess)
            {
                Assert.Contains(successfulMessage, scriptOutput, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Assert.DoesNotContain(successfulMessage, scriptOutput, StringComparison.OrdinalIgnoreCase);
            }
        }

#if FEATURE_ASSEMBLY_LOCATION
        /// <summary>
        /// Make sure that when we load multiple types out of the same assembly with different type filters that both the fullyqualified name matching and the
        /// partial name matching still work.
        /// </summary>
        [Fact]
        public void Regress640476PartialName()
        {
            string forwardingLoggerLocation = typeof(Microsoft.Build.Logging.ConfigurableForwardingLogger).Assembly.Location;
            TypeLoader loader = new TypeLoader(IsForwardingLoggerClass);
            LoadedType loadedType = loader.Load("ConfigurableForwardingLogger", AssemblyLoadInfo.Create(null, forwardingLoggerLocation));
            Assert.NotNull(loadedType);
            Assert.Equal(forwardingLoggerLocation, loadedType.Assembly.AssemblyLocation);

            string fileLoggerLocation = typeof(Microsoft.Build.Logging.FileLogger).Assembly.Location;
            loader = new TypeLoader(IsLoggerClass);
            loadedType = loader.Load("FileLogger", AssemblyLoadInfo.Create(null, fileLoggerLocation));
            Assert.NotNull(loadedType);
            Assert.Equal(fileLoggerLocation, loadedType.Assembly.AssemblyLocation);
        }

        /// <summary>
        /// Make sure that when we load multiple types out of the same assembly with different type filters that both the fullyqualified name matching and the
        /// partial name matching still work.
        /// </summary>
        [Fact]
        public void Regress640476FullyQualifiedName()
        {
            Type forwardingLoggerType = typeof(Microsoft.Build.Logging.ConfigurableForwardingLogger);
            string forwardingLoggerLocation = forwardingLoggerType.Assembly.Location;
            TypeLoader loader = new TypeLoader(IsForwardingLoggerClass);
            LoadedType loadedType = loader.Load(forwardingLoggerType.FullName, AssemblyLoadInfo.Create(null, forwardingLoggerLocation));
            Assert.NotNull(loadedType);
            Assert.Equal(forwardingLoggerLocation, loadedType.Assembly.AssemblyLocation);

            Type fileLoggerType = typeof(Microsoft.Build.Logging.FileLogger);
            string fileLoggerLocation = fileLoggerType.Assembly.Location;
            loader = new TypeLoader(IsLoggerClass);
            loadedType = loader.Load(fileLoggerType.FullName, AssemblyLoadInfo.Create(null, fileLoggerLocation));
            Assert.NotNull(loadedType);
            Assert.Equal(fileLoggerLocation, loadedType.Assembly.AssemblyLocation);
        }


        /// <summary>
        /// Make sure if no typeName is passed in then pick the first type which matches the desired type filter.
        /// This has been in since whidbey but there has been no test for it and it was broken in the last refactoring of TypeLoader.
        /// This test is to prevent that from happening again.
        /// </summary>
        [Fact]
        public void NoTypeNamePicksFirstType()
        {
            Type forwardingLoggerType = typeof(Microsoft.Build.Logging.ConfigurableForwardingLogger);
            string forwardingLoggerAssemblyLocation = forwardingLoggerType.Assembly.Location;
            Func<Type, object, bool> forwardingLoggerfilter = IsForwardingLoggerClass;
            Type firstPublicType = FirstPublicDesiredType(forwardingLoggerfilter, forwardingLoggerAssemblyLocation);

            TypeLoader loader = new TypeLoader(forwardingLoggerfilter);
            LoadedType loadedType = loader.Load(String.Empty, AssemblyLoadInfo.Create(null, forwardingLoggerAssemblyLocation));
            Assert.NotNull(loadedType);
            Assert.Equal(forwardingLoggerAssemblyLocation, loadedType.Assembly.AssemblyLocation);
            Assert.Equal(firstPublicType, loadedType.Type);


            Type fileLoggerType = typeof(Microsoft.Build.Logging.FileLogger);
            string fileLoggerAssemblyLocation = forwardingLoggerType.Assembly.Location;
            Func<Type, object, bool> fileLoggerfilter = IsLoggerClass;
            firstPublicType = FirstPublicDesiredType(fileLoggerfilter, fileLoggerAssemblyLocation);

            loader = new TypeLoader(fileLoggerfilter);
            loadedType = loader.Load(String.Empty, AssemblyLoadInfo.Create(null, fileLoggerAssemblyLocation));
            Assert.NotNull(loadedType);
            Assert.Equal(fileLoggerAssemblyLocation, loadedType.Assembly.AssemblyLocation);
            Assert.Equal(firstPublicType, loadedType.Type);
        }


        private static Type FirstPublicDesiredType(Func<Type, object, bool> filter, string assemblyLocation)
        {
            Assembly loadedAssembly = Assembly.UnsafeLoadFrom(assemblyLocation);

            // only look at public types
            Type[] allPublicTypesInAssembly = loadedAssembly.GetExportedTypes();
            foreach (Type publicType in allPublicTypesInAssembly)
            {
                if (filter(publicType, null))
                {
                    return publicType;
                }
            }

            return null;
        }


        private static bool IsLoggerClass(Type type, object unused)
        {
            return type.IsClass &&
                !type.IsAbstract &&
                (type.GetInterface("ILogger") != null);
        }

        private static bool IsForwardingLoggerClass(Type type, object unused)
        {
            return type.IsClass &&
                !type.IsAbstract &&
                (type.GetInterface("IForwardingLogger") != null);
        }
#endif
    }
}
