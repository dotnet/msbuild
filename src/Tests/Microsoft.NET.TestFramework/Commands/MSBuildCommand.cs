// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework.Commands
{
    public class MSBuildCommand : TestCommand
    {
        public string Target { get; }

        private readonly string _projectRootPath;

        public string ProjectRootPath => _projectRootPath;

        public string ProjectFile { get; }

        public TestAsset TestAsset { get; }

        public string FullPathProjectFile => Path.Combine(ProjectRootPath, ProjectFile);

        public MSBuildCommand(ITestOutputHelper log, string target, string projectRootPath, string relativePathToProject = null)
            : base(log)
        {
            Target = target;

            _projectRootPath = projectRootPath;

            ProjectFile = FindProjectFile(ref _projectRootPath, relativePathToProject);
        }

        public MSBuildCommand(TestAsset testAsset, string target, string relativePathToProject = null)
            : this(testAsset.Log, target, testAsset.TestRoot, relativePathToProject ?? testAsset.TestProject?.Name)
        {
            TestAsset = testAsset;
        }

        internal static string FindProjectFile(ref string projectRootPath, string relativePathToProject)
        {
            if (File.Exists(projectRootPath) && string.IsNullOrEmpty(relativePathToProject))
            {
                return projectRootPath;
            }

            if (!string.IsNullOrEmpty(relativePathToProject))
            {
                string fullPathToProject = Path.Combine(projectRootPath, relativePathToProject);
                if (File.Exists(fullPathToProject))
                {
                    //  If a file exists at the specified relative path, it's the project file
                    return fullPathToProject;
                }
                else
                {
                    //  Otherwise, treat the relative path as the root path for the project and search for the project file under that path
                    projectRootPath = fullPathToProject;
                }
            }

            var buildProjectFiles = Directory.GetFiles(projectRootPath, "*.*proj");

            if (buildProjectFiles.Length != 1)
            {
                var errorMsg = $"Found {buildProjectFiles.Length} project files under {projectRootPath} instead of just 1.";
                throw new ArgumentException(errorMsg);
            }

            return buildProjectFiles[0];
        }

        public virtual DirectoryInfo GetOutputDirectory(string targetFramework = null, string configuration = "Debug", string runtimeIdentifier = null, string platform = null)
        {
            if (TestAsset != null)
            {
                return new DirectoryInfo(OutputPathCalculator.FromProject(ProjectFile, TestAsset).GetOutputDirectory(targetFramework, configuration, runtimeIdentifier, platform));
            }

            platform ??= string.Empty;
            targetFramework ??= string.Empty;
            configuration ??= string.Empty;
            runtimeIdentifier ??= string.Empty;

            string output = Path.Combine(ProjectRootPath, "bin", platform, configuration, targetFramework, runtimeIdentifier);
            return new DirectoryInfo(output);
        }

        public virtual DirectoryInfo GetIntermediateDirectory(string targetFramework = null, string configuration = "Debug", string runtimeIdentifier = null)
        {
            if (TestAsset != null)
            {
                return new DirectoryInfo(OutputPathCalculator.FromProject(ProjectFile, TestAsset).GetIntermediateDirectory(targetFramework, configuration, runtimeIdentifier));
            }

            targetFramework = targetFramework ?? string.Empty;
            configuration = configuration ?? string.Empty;
            runtimeIdentifier = runtimeIdentifier ?? string.Empty;

            string output = Path.Combine(ProjectRootPath, "obj", configuration, targetFramework, runtimeIdentifier);
            return new DirectoryInfo(output);
        }

        public DirectoryInfo GetPackageDirectory(string configuration = "Debug")
        {
            if (TestAsset != null)
            {
                return new DirectoryInfo(OutputPathCalculator.FromProject(ProjectFile, TestAsset).GetPackageDirectory(configuration));
            }

            string output = Path.Combine(ProjectRootPath, "bin", configuration);
            return new DirectoryInfo(output);
        }

        public virtual DirectoryInfo GetNonSDKOutputDirectory(string configuration = "Debug")
        {
            configuration = configuration ?? string.Empty;

            string output = Path.Combine(ProjectRootPath, "bin", configuration);
            return new DirectoryInfo(output);
        }

        public DirectoryInfo GetBaseIntermediateDirectory()
        {
            string output = Path.Combine(ProjectRootPath, "obj");
            return new DirectoryInfo(output);
        }

        protected virtual bool ExecuteWithRestoreByDefault => true;

        public override CommandResult Execute(IEnumerable<string> args)
        {
            if (ExecuteWithRestoreByDefault)
            {
                args = new[] { "/restore" }.Concat(args);
            }

            return base.Execute(args);
        }

        public CommandResult ExecuteWithoutRestore(IEnumerable<string> args)
        {
            return base.Execute(args);
        }

        public CommandResult ExecuteWithoutRestore(params string[] args)
        {
            IEnumerable<string> enumerableArgs = args;
            return ExecuteWithoutRestore(enumerableArgs);
        }

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var newArgs = args.ToList();
            newArgs.Insert(0, FullPathProjectFile);

            return TestContext.Current.ToolsetUnderTest.CreateCommandForTarget(Target, newArgs);
        }
    }
}
