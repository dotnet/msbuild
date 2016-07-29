// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Collections;
using System.IO;
using Microsoft.Build.Shared;
using System.Reflection;
using Xunit;
using Microsoft.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.SharedUtilities;

namespace Microsoft.Build.UnitTests
{
    public class TypeLoader_Tests
    {
        private static readonly string ProjectFilePath = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "portableTaskTest.proj");
        private static readonly string DLLFileName = "PortableTask.dll";
        private static readonly string OriginalDllPath = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, DLLFileName);

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
            string dllName = "NonExistent.dll";

            bool successfulExit;
            string output = RunnerUtilities.ExecMSBuild(ProjectFilePath + " /v:diag /p:AssemblyPath=" + dllName, out successfulExit);
            Assert.False(successfulExit);

            string dllPath = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, dllName);
            CheckIfCorrectAssemblyLoaded(output, dllPath, false);
        }

        [Fact]
        public void LoadInsideAsssembly()
        {
            bool successfulExit;
            string output = RunnerUtilities.ExecMSBuild(ProjectFilePath + " /v:diag", out successfulExit);
            Assert.True(successfulExit);

            CheckIfCorrectAssemblyLoaded(output, OriginalDllPath);
        }

        [Fact]
        public void LoadOutsideAssembly()
        {
            string movedDLLPath = MoveOrCopyDllToTempDir(copy : false);

            try
            {
                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(ProjectFilePath + " /v:diag /p:AssemblyPath=" + movedDLLPath, out successfulExit);
                Assert.True(successfulExit);

                CheckIfCorrectAssemblyLoaded(output, movedDLLPath);
            }
            finally
            {
                UndoDLLOperations(movedDLLPath, moveBack : true);
            }
        }

        [Fact (Skip = "https://github.com/Microsoft/msbuild/issues/325")]
        public void LoadInsideAssemblyWhenGivenOutsideAssemblyWithSameName()
        {
            string copiedDllPath = MoveOrCopyDllToTempDir(copy : true);

            try
            {
                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(ProjectFilePath + " /v:diag /p:AssemblyPath=" + copiedDllPath, out successfulExit);
                Assert.True(successfulExit);

                CheckIfCorrectAssemblyLoaded(output, OriginalDllPath);
            }
            finally
            {
                UndoDLLOperations(copiedDllPath, moveBack : false);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="copy"></param>
        /// <returns>Path to new DLL</returns>
        private string MoveOrCopyDllToTempDir(bool copy)
        {
            var temporaryDirectory = FileUtilities.GetTemporaryDirectory();
            var newDllPath = Path.Combine(temporaryDirectory, DLLFileName);

            Assert.True(File.Exists(OriginalDllPath));

            if (copy)
            {
                File.Copy(OriginalDllPath, newDllPath);

                Assert.True(File.Exists(newDllPath));
            }
            else
            {
                File.Move(OriginalDllPath, newDllPath);

                Assert.True(File.Exists(newDllPath));
                Assert.False(File.Exists(OriginalDllPath));
            }
            return newDllPath;
        }

        /// <summary>
        /// Move / Delete newDllPath and delete temp directory
        /// </summary>
        /// <param name="newDllPath"></param>
        /// <param name="moveBack">If true, move newDllPath back to bin. If false, delete it</param>
        private void UndoDLLOperations(string newDllPath, bool moveBack)
        {
            var tempDirectoryPath = Path.GetDirectoryName(newDllPath);

            if (moveBack)
            {
                File.Move(newDllPath, OriginalDllPath);
            }
            else
            {
                File.Delete(newDllPath);
            }

            Assert.True(File.Exists(OriginalDllPath));
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
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(forwardingLoggerLocation, StringComparison.OrdinalIgnoreCase));

            string fileLoggerLocation = typeof(Microsoft.Build.Logging.FileLogger).Assembly.Location;
            loader = new TypeLoader(IsLoggerClass);
            loadedType = loader.Load("FileLogger", AssemblyLoadInfo.Create(null, fileLoggerLocation));
            Assert.NotNull(loadedType);
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(fileLoggerLocation, StringComparison.OrdinalIgnoreCase));
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
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(forwardingLoggerLocation, StringComparison.OrdinalIgnoreCase));

            Type fileLoggerType = typeof(Microsoft.Build.Logging.FileLogger);
            string fileLoggerLocation = fileLoggerType.Assembly.Location;
            loader = new TypeLoader(IsLoggerClass);
            loadedType = loader.Load(fileLoggerType.FullName, AssemblyLoadInfo.Create(null, fileLoggerLocation));
            Assert.NotNull(loadedType);
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(fileLoggerLocation, StringComparison.OrdinalIgnoreCase));
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
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(forwardingLoggerAssemblyLocation, StringComparison.OrdinalIgnoreCase));
            Assert.True(loadedType.Type.Equals(firstPublicType));


            Type fileLoggerType = typeof(Microsoft.Build.Logging.FileLogger);
            string fileLoggerAssemblyLocation = forwardingLoggerType.Assembly.Location;
            Func<Type, object, bool> fileLoggerfilter = IsLoggerClass;
            firstPublicType = FirstPublicDesiredType(fileLoggerfilter, fileLoggerAssemblyLocation);

            loader = new TypeLoader(fileLoggerfilter);
            loadedType = loader.Load(String.Empty, AssemblyLoadInfo.Create(null, fileLoggerAssemblyLocation));
            Assert.NotNull(loadedType);
            Assert.True(loadedType.Assembly.AssemblyLocation.Equals(fileLoggerAssemblyLocation, StringComparison.OrdinalIgnoreCase));
            Assert.True(loadedType.Type.Equals(firstPublicType));
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
            return (type.IsClass &&
                !type.IsAbstract &&
                (type.GetInterface("ILogger") != null));
        }

        private static bool IsForwardingLoggerClass(Type type, object unused)
        {
            return (type.IsClass &&
                !type.IsAbstract &&
                (type.GetInterface("IForwardingLogger") != null));
        }
#endif
    }
}

