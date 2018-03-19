using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Shouldly;

namespace Microsoft.Build.UnitTests
{
    public partial class TestEnvironment
    {
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
        ///     Creates a test variant representing a test project with files relative to the project root. All files
        ///     and the root will be cleaned up when the test completes.
        /// </summary>
        /// <param name="projectContents">Contents of the project file to be created.</param>
        /// <param name="files">Files to be created.</param>
        /// <param name="relativePathFromRootToProject">Path for the specified files to be created in relative to 
        /// the root of the project directory.</param>
        public TransientTestProjectWithFiles CreateTestProjectWithFiles(string projectContents, string[] files = null, string relativePathFromRootToProject = ".")
        {
            return WithTransientTestState(
                new TransientTestProjectWithFiles(projectContents, files, relativePathFromRootToProject));
        }

        public TransientSdkResolution CustomSdkResolution(Dictionary<string, string> sdkToFolderMapping)
        {
            return WithTransientTestState(new TransientSdkResolution(sdkToFolderMapping));
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
                    : factory.IndicateFailure(new[] { $"Not in {nameof(_mapping)}" });
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
