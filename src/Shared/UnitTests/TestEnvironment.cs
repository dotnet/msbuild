using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests
{
    public class TestEnvironment : IDisposable
    {
        /// <summary>
        ///     List of test invariants to assert value does not change.
        /// </summary>
        private readonly List<TestInvariant> _invariants = new List<TestInvariant>();

        /// <summary>
        ///     List of test variants which need to be reverted when the test completes.
        /// </summary>
        private readonly List<TransientTestState> _variants = new List<TransientTestState>();

        private readonly ITestOutputHelper _output;

        public static TestEnvironment Create(ITestOutputHelper output = null, bool ignoreBuildErrorFiles = false)
        {
            var env = new TestEnvironment(output ?? new DefaultOutput());

            // In most cases, if MSBuild wrote an MSBuild_*.txt to the temp path something went wrong.
            if (!ignoreBuildErrorFiles)
                env.WithInvariant(new BuildFailureLogInvariant());

            env.SetEnvironmentVariable("MSBUILDRELOADTRAITSONEACHACCESS", "1");

            return env;
        }

        private TestEnvironment(ITestOutputHelper output)
        {
            _output = output;
            SetDefaultInvariant();
        }

        /// <summary>
        ///     Revert / cleanup variants and then assert invariants.
        /// </summary>
        public void Dispose()
        {
            // Reset test variants
            foreach (var variant in _variants)
                variant.Revert();

            // Assert invariants
            foreach (var item in _invariants)
                item.AssertInvariant(_output);
        }

        /// <summary>
        ///     Evaluate the test with the given invariant.
        /// </summary>
        /// <param name="invariant">Test invariant to assert unchanged on completion.</param>
        public T WithInvariant<T>(T invariant) where T : TestInvariant
        {
            _invariants.Add(invariant);
            return invariant;
        }

        /// <summary>
        ///     Evaluate the test with the given transient test state.
        /// </summary>
        /// <returns>Test state to revert on completion.</returns>
        public T WithTransientTestState<T>(T transientState) where T : TransientTestState
        {
            _variants.Add(transientState);
            return transientState;
        }

        /// <summary>
        ///     Clears all test invariants. This should only be used if there is a known
        ///     issue with a test!
        /// </summary>
        public void ClearTestInvariants()
        {
            _invariants.Clear();
        }

        #region Common test variants

        private void SetDefaultInvariant()
        {
            // Temp folder should not change before and after a test
            WithInvariant(new StringInvariant("Path.GetTempPath()", Path.GetTempPath));

            // Temp folder should not change before and after a test
            WithInvariant(new StringInvariant("Directory.GetCurrentDirectory", Directory.GetCurrentDirectory));

            // Common set of MSBuild environment variables that should remain the same before and after a test
            // runs. If these differ it likely indicates an issue with the test.
            WithEnvironmentVariableInvariant("MSBUILDNOINPROCNODE");
            WithEnvironmentVariableInvariant("MSBUILDENABLEALLPROPERTYFUNCTIONS");
            WithEnvironmentVariableInvariant("MSBuildForwardPropertiesFromChild");
            WithEnvironmentVariableInvariant("MsBuildForwardAllPropertiesFromChild");
            WithEnvironmentVariableInvariant("MSBUILDDEBUGFORCECACHING");
            WithEnvironmentVariableInvariant("MSBUILDRELOADTRAITSONEACHACCESS");
        }

        /// <summary>
        ///     Creates a test invariant that asserts an environment variable does not change during the test.
        /// </summary>
        /// <param name="environmentVariableName">Name of the environment variable.</param>
        public TestInvariant WithEnvironmentVariableInvariant(string environmentVariableName)
        {
            return WithInvariant(new StringInvariant(environmentVariableName,
                () => Environment.GetEnvironmentVariable(environmentVariableName)));
        }

        /// <summary>
        ///     Creates a string invariant that will assert the value is the same before and after the test.
        /// </summary>
        /// <param name="name">Name of the item to keep track of.</param>
        /// <param name="value">Delegate to get the value for the invariant.</param>
        public TestInvariant WithStringInvariant(string name, Func<string> value)
        {
            return WithInvariant(new StringInvariant(name, value));
        }

        /// <summary>
        ///     Creates a test variant that corresponds to a temporary file which will be deleted when the test completes.
        /// </summary>
        /// <param name="extension">Extensions of the file (defaults to '.tmp')</param>
        public TransientTestFile CreateFile(string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(extension));
        }

        /// <summary>
        ///     Creates a test variant that corresponds to a temporary file under a specific temporary folder. File will
        ///     be cleaned up when the test completes.
        /// </summary>
        /// <param name="transientTestFolder"></param>
        /// <param name="extension">Extension of the file (defaults to '.tmp')</param>
        public TransientTestFile CreateFile(TransientTestFolder transientTestFolder, string extension = ".tmp")
        {
            return WithTransientTestState(new TransientTestFile(transientTestFolder.FolderPath, extension));
        }

        /// <summary>
        ///     Creates a test variant used to add a unique temporary folder during a test. Will be deleted when the test
        ///     completes.
        /// </summary>
        public TransientTestFolder CreateFolder()
        {
            return WithTransientTestState(new TransientTestFolder());
        }

        /// <summary>
        ///     Creates a test variant that corresponds to a project collection which will have its projects unloaded,
        ///     loggers unregistered, toolsets removed and disposed when the test completes
        /// </summary>
        /// <returns></returns>
        public TransientProjectCollection CreateProjectCollection()
        {
            return WithTransientTestState(new TransientProjectCollection());
        }

        /// <summary>
        ///     Create an test variant used to change the value of an environment variable during a test. Original value
        ///     will be restored when complete.
        /// </summary>
        public TransientTestState SetEnvironmentVariable(string environmentVariableName, string newValue)
        {
            return WithTransientTestState(new TransientTestEnvironmentVariable(environmentVariableName, newValue));
        }

        public TransientTestState SetCurrentDirectory(string newWorkingDirectory)
        {
            return WithTransientTestState(new TransientWorkingDirectory(newWorkingDirectory));
        }

        /// <summary>
        ///     Creates a test variant representing a test project with files relative to the project root. All files
        ///     and the root will be cleaned up when the test completes.
        /// </summary>
        /// <param name="projectContents">Contents of the project file to be created.</param>
        /// <param name="files">Files to be created.</param>
        /// <param name="relativePathFromRootToProject">Path for the specified files to be created in relative to 
        /// the root of the project directory.</param>
        public TransientTestProjectWithFiles CreateTestProjectWithFiles(string projectContents, string[] files = null,
            string relativePathFromRootToProject = ".")
        {
            return WithTransientTestState(
                new TransientTestProjectWithFiles(projectContents, files, relativePathFromRootToProject));
        }

        public TransientSdkResolution CustomSdkResolution(Dictionary<string, string> sdkToFolderMapping)
        {
            return WithTransientTestState(new TransientSdkResolution(sdkToFolderMapping));
        }

        #endregion

        private class DefaultOutput : ITestOutputHelper
        {
            public void WriteLine(string message)
            {
                Console.WriteLine(message);
            }

            public void WriteLine(string format, params object[] args)
            {
                Console.WriteLine(format, args);
            }
        }
    }

    /// <summary>
    ///     Things that are expected not to change and should be asserted before and after running.
    /// </summary>
    public abstract class TestInvariant
    {
        public abstract void AssertInvariant(ITestOutputHelper output);
    }

    /// <summary>
    ///     Things that are expected to change and should be reverted after running.
    /// </summary>
    public abstract class TransientTestState
    {
        public abstract void Revert();
    }

    public class StringInvariant : TestInvariant
    {
        private readonly Func<string> _accessorFunc;
        private readonly string _name;
        private readonly string _originalValue;

        public StringInvariant(string name, Func<string> accessorFunc)
        {
            _name = name;
            _accessorFunc = accessorFunc;
            _originalValue = accessorFunc();
        }

        public override void AssertInvariant(ITestOutputHelper output)
        {
            Assert.Equal($"{_name}: {_originalValue}", $"{_name}: {_accessorFunc()}");
        }
    }

    public class BuildFailureLogInvariant : TestInvariant
    {
        private readonly string[] _originalFiles;

        public BuildFailureLogInvariant()
        {
            _originalFiles = Directory.GetFiles(Path.GetTempPath(), "MSBuild_*.txt");
        }

        public override void AssertInvariant(ITestOutputHelper output)
        {
            var newFiles = Directory.GetFiles(Path.GetTempPath(), "MSBuild_*.txt");

            int newFilesCount = newFiles.Length;
            if (newFilesCount > _originalFiles.Length)
            {
                foreach (var file in newFiles.Except(_originalFiles).Select(f => new FileInfo(f)))
                {
                    string contents = File.ReadAllText(file.FullName);

                    // Delete the file so we don't pollute the build machine
                    FileUtilities.DeleteNoThrow(file.FullName);

                    // Ignore clean shutdown trace logs.
                    if (Regex.IsMatch(file.Name, @"MSBuild_NodeShutdown_\d+\.txt") &&
                        Regex.IsMatch(contents, @"Node shutting down with reason BuildComplete and exception:\s*"))
                    {
                        newFilesCount--;
                        continue;
                    }

                    // Com trace file. This is probably fine, but output it as it was likely turned on
                    // for a reason.
                    if (Regex.IsMatch(file.Name, @"MSBuild_CommTrace_PID_\d+\.txt"))
                    {
                        output.WriteLine($"{file.Name}: {contents}");
                        newFilesCount--;
                        continue;
                    }

                    output.WriteLine($"Build Error File {file.Name}: {contents}");
                }
            }

            // Assert file count is equal minus any files that were OK
            Assert.Equal(_originalFiles.Length, newFilesCount);
        }
    }

    public class TransientTestFile : TransientTestState
    {
        public TransientTestFile(string extension)
        {
            Path = FileUtilities.GetTemporaryFile(extension);
        }

        public TransientTestFile(string rootPath, string extension)
        {
            Path = FileUtilities.GetTemporaryFile(rootPath, extension);
        }

        public string Path { get; }

        public override void Revert()
        {
            FileUtilities.DeleteNoThrow(Path);
        }
    }

    public class TransientTestFolder : TransientTestState
    {
        public TransientTestFolder()
        {
            FolderPath = FileUtilities.GetTemporaryDirectory();
        }

        public string FolderPath { get; }

        public override void Revert()
        {
            // Basic checks to make sure we're not deleting something very obviously wrong (e.g.
            // the entire temp drive).
            Assert.NotNull(FolderPath);
            Assert.NotEqual(string.Empty, FolderPath);
            Assert.NotEqual(@"\", FolderPath);
            Assert.NotEqual(@"/", FolderPath);
            Assert.NotEqual(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(FolderPath));
            Assert.True(Path.IsPathRooted(FolderPath));

            FileUtilities.DeleteDirectoryNoThrow(FolderPath, true);
        }
    }

    public class TransientTestEnvironmentVariable : TransientTestState
    {
        private readonly string _environmentVariableName;
        private readonly string _originalValue;

        public TransientTestEnvironmentVariable(string environmentVariableName, string newValue)
        {
            _environmentVariableName = environmentVariableName;
            _originalValue = Environment.GetEnvironmentVariable(environmentVariableName);

            Environment.SetEnvironmentVariable(environmentVariableName, newValue);
        }

        public override void Revert()
        {
            Environment.SetEnvironmentVariable(_environmentVariableName, _originalValue);
        }
    }

    public class TransientWorkingDirectory : TransientTestState
    {
        private readonly string _originalValue;

        public TransientWorkingDirectory(string newWorkingDirectory)
        {
            _originalValue = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(newWorkingDirectory);
        }

        public override void Revert()
        {
            Directory.SetCurrentDirectory(_originalValue);
        }
    }

    public class TransientTestProjectWithFiles : TransientTestState
    {
        private readonly TransientTestFolder _folder;

        public string TestRoot => _folder.FolderPath;

        public string[] CreatedFiles { get; }

        public string ProjectFile { get; }

        public TransientTestProjectWithFiles(string projectContents, string[] files,
            string relativePathFromRootToProject = ".")
        {
            _folder = new TransientTestFolder();

            var projectDir = Path.Combine(TestRoot, relativePathFromRootToProject);
            Directory.CreateDirectory(projectDir);

            ProjectFile = Path.Combine(projectDir, "build.proj");
            File.WriteAllText(ProjectFile, ObjectModelHelpers.CleanupFileContents(projectContents));

            CreatedFiles = Helpers.CreateFilesInDirectory(TestRoot, files);
        }

        internal MockLogger BuildProjectExpectFailure(IDictionary<string, string> globalProperties = null, string toolsVersion = null)
        {
            MockLogger logger;

            BuildProject(globalProperties, toolsVersion, out logger).ShouldBeFalse();

            return logger;
        }

        internal MockLogger BuildProjectExpectSuccess(IDictionary<string, string> globalProperties = null, string toolsVersion = null)
        {
            MockLogger logger;

            BuildProject(globalProperties, toolsVersion, out logger).ShouldBeTrue();

            return logger;
        }

        public override void Revert()
        {
            _folder.Revert();
        }

        private bool BuildProject(IDictionary<string, string> globalProperties, string toolsVersion, out MockLogger logger)
        {
            logger = new MockLogger();

            using (ProjectCollection projectCollection = new ProjectCollection())
            {
                Project project = new Project(ProjectFile, globalProperties, toolsVersion, projectCollection);

                return project.Build(logger);
            }
        }
    }

    /// <summary>
    /// Represents custom SDK resolution in the context of this test.
    /// </summary>
    public class TransientSdkResolution : TransientTestState
    {
        private readonly Dictionary<string, string> _mapping;

        public TransientSdkResolution(Dictionary<string, string> mapping)
        {
            _mapping = mapping;
            
            CallResetForTests(new List<SdkResolver> { new TestSdkResolver(_mapping) });
        }

        public override void Revert()
        {
            CallResetForTests(null);
        }

        /// <summary>
        /// SdkResolution is internal (by design) and not all UnitTest projects are allowed to have
        /// InternalsVisibleTo.
        /// </summary>
        /// <param name="resolvers"></param>
        private static void CallResetForTests(IList<SdkResolver> resolvers)
        {
            Type sdkResolverServiceType = typeof(ProjectCollection).GetTypeInfo().Assembly.GetType("Microsoft.Build.BackEnd.SdkResolution.SdkResolverService");

            PropertyInfo instancePropertyInfo = sdkResolverServiceType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

            object sdkResolverService = instancePropertyInfo.GetValue(null);

            MethodInfo initializeForTestsMethodInfo = sdkResolverServiceType.GetMethod("InitializeForTests", BindingFlags.NonPublic | BindingFlags.Instance);

            initializeForTestsMethodInfo.Invoke(sdkResolverService, new object[] { null, resolvers });
        }

        private class TestSdkResolver : SdkResolver
        {
            private readonly Dictionary<string, string> _mapping;

            public TestSdkResolver(Dictionary<string, string> mapping)
            {
                _mapping = mapping;
            }
            public override string Name => "TestSdkResolver";
            public override int Priority => int.MinValue;

            public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
            {
                resolverContext.Logger.LogMessage($"{nameof(resolverContext.ProjectFilePath)} = {resolverContext.ProjectFilePath}", MessageImportance.High);
                resolverContext.Logger.LogMessage($"{nameof(resolverContext.SolutionFilePath)} = {resolverContext.SolutionFilePath}", MessageImportance.High);
                resolverContext.Logger.LogMessage($"{nameof(resolverContext.MSBuildVersion)} = {resolverContext.MSBuildVersion}", MessageImportance.High);

                return _mapping.ContainsKey(sdkReference.Name)
                    ? factory.IndicateSuccess(_mapping[sdkReference.Name], null)
                    : factory.IndicateFailure(new[] {$"Not in {nameof(_mapping)}"});
            }
        }
    }

    public class TransientProjectCollection : TransientTestState
    {
        public ProjectCollection Collection { get; }

        public TransientProjectCollection()
        {
            Collection = new ProjectCollection();
        }

        public override void Revert()
        {
            Collection.UnloadAllProjects();
            Collection.UnregisterAllLoggers();
            Collection.RemoveAllToolsets();
            Collection.Dispose();
        }
    }
}
