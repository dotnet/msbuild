// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.IO;
using System.Linq;
using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class TypeLoader_Tests
    {
        private static readonly string ProjectFileFolder = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "PortableTask");
        private const string ProjectFileName = "portableTaskTest.proj";
        private const string DLLFileName = "PortableTask.dll";
        private static string PortableTaskFolderPath = Path.GetFullPath(
                    Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "..", "..", "..", "Samples", "PortableTask"));

        private readonly TestContext _output;

        public TypeLoader_Tests(TestContext testOutputHelper)
        {
            _output = testOutputHelper;
        }

        [MSBuildTestMethod]
        public void Basic()
        {
            Assert.IsTrue(TypeLoader.IsPartialTypeNameMatch("Csc", "csc")); // ==> exact match
            Assert.IsTrue(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Microsoft.Build.Tasks.Csc")); // ==> exact match
            Assert.IsTrue(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Csc")); // ==> partial match
            Assert.IsTrue(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Tasks.Csc")); // ==> partial match
            Assert.IsTrue(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask+NestedTask", "NestedTask")); // ==> partial match
            Assert.IsTrue(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\\\+NestedTask", "NestedTask")); // ==> partial match
            Assert.IsFalse(TypeLoader.IsPartialTypeNameMatch("MyTasks.CscTask", "Csc")); // ==> no match
            Assert.IsFalse(TypeLoader.IsPartialTypeNameMatch("MyTasks.MyCsc", "Csc")); // ==> no match
            Assert.IsFalse(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\.Csc", "Csc")); // ==> no match
            Assert.IsFalse(TypeLoader.IsPartialTypeNameMatch("MyTasks.ATask\\\\\\.Csc", "Csc")); // ==> no match
        }

        [MSBuildTestMethod]
        public void Regress_Mutation_TrailingPartMustMatch()
        {
            Assert.IsFalse(TypeLoader.IsPartialTypeNameMatch("Microsoft.Build.Tasks.Csc", "Vbc"));
        }

        [MSBuildTestMethod]
        public void Regress_Mutation_ParameterOrderDoesntMatter()
        {
            Assert.IsTrue(TypeLoader.IsPartialTypeNameMatch("Csc", "Microsoft.Build.Tasks.Csc"));
        }


        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
        public void LoadInsideAsssembly()
        {
            using (var dir = new FileUtilities.TempWorkingDirectory(ProjectFileFolder))
            {
                string projectFilePath = Path.Combine(dir.Path, ProjectFileName);

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(projectFilePath + " /v:diag", out successfulExit, _output);
                Assert.IsTrue(successfulExit);

                string dllPath = Path.Combine(dir.Path, DLLFileName);

                CheckIfCorrectAssemblyLoaded(output, dllPath);
            }
        }

        [MSBuildTestMethod]
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
                TypeLoader typeLoader = TypeLoader.Create<ITask>();

                // If we cannot accept MSBuild next to the task assembly we're loading, this will throw.
                typeLoader.Load("TypeLoader_Tests", AssemblyLoadInfo.Create(null, newAssemblyLocation), logWarning: (format, args) => { }, useTaskHost: true);
            }
        }

        [MSBuildTestMethod]
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
                    Assert.IsTrue(successfulExit);

                    CheckIfCorrectAssemblyLoaded(output, movedDLLPath);
                }
                finally
                {
                    UndoDLLOperations(movedDLLPath);
                }
            }
        }

        [MSBuildTestMethod]
        [Ignore("https://github.com/dotnet/msbuild/issues/325")]
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
                    Assert.IsTrue(successfulExit);

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

            Assert.IsTrue(File.Exists(originalDllPath));

            if (copy)
            {
                File.Copy(originalDllPath, newDllPath);

                Assert.IsTrue(File.Exists(newDllPath));
            }
            else
            {
                File.Move(originalDllPath, newDllPath);

                Assert.IsTrue(File.Exists(newDllPath));
                Assert.IsFalse(File.Exists(originalDllPath));
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

            Assert.IsFalse(File.Exists(newDllPath));
            Assert.IsEmpty(Directory.EnumerateFiles(tempDirectoryPath));

            Directory.Delete(tempDirectoryPath);
            Assert.IsFalse(Directory.Exists(tempDirectoryPath));
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

        /// <summary>
        /// Make sure that when we load multiple types out of the same assembly with different type filters that both the fullyqualified name matching and the
        /// partial name matching still work.
        /// </summary>
        [MSBuildTestMethod]
        public void Regress640476PartialName()
        {
            string forwardingLoggerLocation = typeof(Microsoft.Build.Logging.ConfigurableForwardingLogger).Assembly.Location;
            TypeLoader loader = TypeLoader.Create<IForwardingLogger>();
            LoadedType loadedType = loader.Load("ConfigurableForwardingLogger", AssemblyLoadInfo.Create(null, forwardingLoggerLocation), logWarning: (format, args) => { });
            Assert.IsNotNull(loadedType);
            Assert.AreEqual(forwardingLoggerLocation, loadedType.Assembly.AssemblyLocation);

            string fileLoggerLocation = typeof(Microsoft.Build.Logging.FileLogger).Assembly.Location;
            loader = TypeLoader.Create<ILogger>();
            loadedType = loader.Load("FileLogger", AssemblyLoadInfo.Create(null, fileLoggerLocation), logWarning: (format, args) => { });
            Assert.IsNotNull(loadedType);
            Assert.AreEqual(fileLoggerLocation, loadedType.Assembly.AssemblyLocation);
        }

        /// <summary>
        /// Make sure that when we load multiple types out of the same assembly with different type filters that both the fullyqualified name matching and the
        /// partial name matching still work.
        /// </summary>
        [MSBuildTestMethod]
        public void Regress640476FullyQualifiedName()
        {
            Type forwardingLoggerType = typeof(Microsoft.Build.Logging.ConfigurableForwardingLogger);
            string forwardingLoggerLocation = forwardingLoggerType.Assembly.Location;
            TypeLoader loader = TypeLoader.Create<IForwardingLogger>();
            LoadedType loadedType = loader.Load(forwardingLoggerType.FullName, AssemblyLoadInfo.Create(null, forwardingLoggerLocation), logWarning: (format, args) => { });
            Assert.IsNotNull(loadedType);
            Assert.AreEqual(forwardingLoggerLocation, loadedType.Assembly.AssemblyLocation);

            Type fileLoggerType = typeof(Microsoft.Build.Logging.FileLogger);
            string fileLoggerLocation = fileLoggerType.Assembly.Location;
            loader = TypeLoader.Create<ILogger>();
            loadedType = loader.Load(fileLoggerType.FullName, AssemblyLoadInfo.Create(null, fileLoggerLocation), logWarning: (format, args) => { });
            Assert.IsNotNull(loadedType);
            Assert.AreEqual(fileLoggerLocation, loadedType.Assembly.AssemblyLocation);
        }


        /// <summary>
        /// Make sure if no typeName is passed in then pick the first type which matches the desired type filter.
        /// This has been in since whidbey but there has been no test for it and it was broken in the last refactoring of TypeLoader.
        /// This test is to prevent that from happening again.
        /// </summary>
        [MSBuildTestMethod]
        public void NoTypeNamePicksFirstType()
        {
            Type forwardingLoggerType = typeof(Microsoft.Build.Logging.ConfigurableForwardingLogger);
            string forwardingLoggerAssemblyLocation = forwardingLoggerType.Assembly.Location;
            Func<Type, object, bool> forwardingLoggerfilter = IsForwardingLoggerClass;
            Type firstPublicType = FirstPublicDesiredType(forwardingLoggerfilter, forwardingLoggerAssemblyLocation);

            TypeLoader loader = TypeLoader.Create<IForwardingLogger>();
            LoadedType loadedType = loader.Load(String.Empty, AssemblyLoadInfo.Create(null, forwardingLoggerAssemblyLocation), logWarning: (format, args) => { });
            Assert.IsNotNull(loadedType);
            Assert.AreEqual(forwardingLoggerAssemblyLocation, loadedType.Assembly.AssemblyLocation);
#if NET
            Assert.AreEqual(AssemblyLoadContext.GetLoadContext(firstPublicType.Assembly), AssemblyLoadContext.GetLoadContext(loadedType.Type.Assembly));
#endif
            Assert.AreEqual(firstPublicType, loadedType.Type);


            Type fileLoggerType = typeof(Microsoft.Build.Logging.FileLogger);
            string fileLoggerAssemblyLocation = forwardingLoggerType.Assembly.Location;
            Func<Type, object, bool> fileLoggerfilter = IsLoggerClass;
            firstPublicType = FirstPublicDesiredType(fileLoggerfilter, fileLoggerAssemblyLocation);

            loader = TypeLoader.Create<ILogger>();
            loadedType = loader.Load(String.Empty, AssemblyLoadInfo.Create(null, fileLoggerAssemblyLocation), logWarning: (format, args) => { });
            Assert.IsNotNull(loadedType);
            Assert.AreEqual(fileLoggerAssemblyLocation, loadedType.Assembly.AssemblyLocation);
            Assert.AreEqual(firstPublicType, loadedType.Type);
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
                type.GetInterface("ILogger") != null;
        }

        private static bool IsForwardingLoggerClass(Type type, object unused)
        {
            return type.IsClass &&
                !type.IsAbstract &&
                type.GetInterface("IForwardingLogger") != null;
        }
    }
}
