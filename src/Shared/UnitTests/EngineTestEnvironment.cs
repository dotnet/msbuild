// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Shouldly;

namespace Microsoft.Build.UnitTests
{
    public partial class TestEnvironment
    {
        // reset the default build manager and the state it might have accumulated from other tests
        private object _resetBuildManager = new ResetDefaultBuildManager();

        private class ResetDefaultBuildManager
        {
            protected internal static FieldInfo SingletonField;

            public ResetDefaultBuildManager()
            {
                if (DefaultBuildManagerIsInstantiated())
                {
                    DisposeDefaultBuildManager();
                }
            }

            private static void DisposeDefaultBuildManager()
            {
                try
                {
                    BuildManager.DefaultBuildManager.BeginBuild(
                        new BuildParameters()
                        {
                            EnableNodeReuse = false,
                            ShutdownInProcNodeOnBuildFinish = true
                        });
                }
                finally
                {
                    BuildManager.DefaultBuildManager.EndBuild();
                    BuildManager.DefaultBuildManager.Dispose();
                }
            }

            private static bool DefaultBuildManagerIsInstantiated()
            {
                if (SingletonField == null)
                {
                    SingletonField = typeof(BuildManager).GetField("s_singletonInstance", BindingFlags.Static | BindingFlags.NonPublic);
                }

                SingletonField.ShouldNotBeNull();

                return SingletonField.GetValue(null) != null;
            }
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
        ///     Creates a test variant representing a test project with files relative to the project root. All files
        ///     and the root will be cleaned up when the test completes.
        /// </summary>
        /// <param name="projectFileName">Name of the project file with extension to be created.</param>
        /// <param name="projectContents">Contents of the project file to be created.</param>
        /// <param name="files">Files to be created.</param>
        /// <param name="relativePathFromRootToProject">Path for the specified files to be created in relative to 
        /// the root of the project directory.</param>
        public TransientTestProjectWithFiles CreateTestProjectWithFiles(string projectFileName, string projectContents, string[] files = null, string relativePathFromRootToProject = ".")
            => WithTransientTestState(new TransientTestProjectWithFiles(projectFileName, projectContents, files, relativePathFromRootToProject));

        /// <summary>
        ///     Creates a test variant representing a test project with files relative to the project root. All files
        ///     and the root will be cleaned up when the test completes.
        /// </summary>
        /// <param name="projectContents">Contents of the project file to be created.</param>
        /// <param name="files">Files to be created.</param>
        /// <param name="relativePathFromRootToProject">Path for the specified files to be created in relative to 
        /// the root of the project directory.</param>
        public TransientTestProjectWithFiles CreateTestProjectWithFiles(string projectContents, string[] files = null, string relativePathFromRootToProject = ".")
            => CreateTestProjectWithFiles("build.proj", projectContents, files, relativePathFromRootToProject);
    }

    public class TransientTestProjectWithFiles : TransientTestState
    {
        private readonly TransientTestFolder _folder;

        public string TestRoot => _folder.Path;

        public string[] CreatedFiles { get; }

        public string ProjectFile { get; }

        public TransientTestProjectWithFiles(
            string projectFileName,
            string projectContents,
            string[] files,
            string relativePathFromRootToProject = ".")
        {
            _folder = new TransientTestFolder();

            var projectDir = Path.GetFullPath(Path.Combine(TestRoot, relativePathFromRootToProject));
            Directory.CreateDirectory(projectDir);

            ProjectFile = Path.GetFullPath(Path.Combine(projectDir, projectFileName));
            File.WriteAllText(ProjectFile, ObjectModelHelpers.CleanupFileContents(projectContents));

            CreatedFiles = Helpers.CreateFilesInDirectory(TestRoot, files);
        }

        internal MockLogger BuildProjectExpectFailure(IDictionary<string, string> globalProperties = null, string toolsVersion = null, bool validateLoggerRoundtrip = true)
        {
            BuildProject(globalProperties, toolsVersion, out MockLogger logger, validateLoggerRoundtrip).ShouldBeFalse();
            return logger;
        }

        internal MockLogger BuildProjectExpectSuccess(IDictionary<string, string> globalProperties = null, string toolsVersion = null, bool validateLoggerRoundtrip = true)
        {
            BuildProject(globalProperties, toolsVersion, out MockLogger logger, validateLoggerRoundtrip).ShouldBeTrue();
            return logger;
        }

        public override void Revert()
        {
            _folder.Revert();
        }

        private IEnumerable<(ILogger logger, Func<string> textGetter)> GetLoggers()
        {
            var result = new List<(ILogger logger, Func<string> textGetter)>();

            result.Add(GetMockLogger());
            result.Add(GetBinaryLogger());

#if MICROSOFT_BUILD_ENGINE_UNITTESTS
            result.Add(GetSerialLogger());
            result.Add(GetParallelLogger());
#endif

            return result;
        }

        private (ILogger logger, Func<string> textGetter) GetMockLogger()
        {
            var logger = new MockLogger();
            return (logger, () => logger.FullLog);
        }

#if MICROSOFT_BUILD_ENGINE_UNITTESTS

        private (ILogger, Func<string>) GetSerialLogger()
        {
            var sb = new StringBuilder();
            var serialFromBuild = new SerialConsoleLogger(LoggerVerbosity.Diagnostic, t => sb.Append(t), colorSet: null, colorReset: null);
            serialFromBuild.Parameters = "NOPERFORMANCESUMMARY";
            return (serialFromBuild, () => sb.ToString());
        }

        private (ILogger, Func<string>) GetParallelLogger()
        {
            var sb = new StringBuilder();
            var parallelFromBuild = new ParallelConsoleLogger(LoggerVerbosity.Diagnostic, t => sb.Append(t), colorSet: null, colorReset: null);
            parallelFromBuild.Parameters = "NOPERFORMANCESUMMARY";
            return (parallelFromBuild, () => sb.ToString());
        }

#endif

        private (ILogger, Func<string>) GetBinaryLogger()
        {
            var binaryLogger = new BinaryLogger();
            string binaryLoggerFilePath = Path.GetFullPath(Path.Combine(TestRoot, Guid.NewGuid().ToString() + ".binlog"));
            binaryLogger.CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.None;
            binaryLogger.Parameters = binaryLoggerFilePath;
            return (binaryLogger, null);
        }

        private bool BuildProject(
            IDictionary<string, string> globalProperties,
            string toolsVersion,
            out MockLogger mockLogger,
            bool validateLoggerRoundtrip = true)
        {
            var expectedLoggerPairs = validateLoggerRoundtrip ? GetLoggers() : new[] { GetMockLogger() };
            var expectedLoggers = expectedLoggerPairs.Select(l => l.logger).ToArray();
            mockLogger = expectedLoggers.OfType<MockLogger>().First();
            var binaryLogger = expectedLoggers.OfType<BinaryLogger>().FirstOrDefault();

            try
            {
                using (ProjectCollection projectCollection = new ProjectCollection())
                {
                    Project project = new Project(ProjectFile, globalProperties, toolsVersion, projectCollection);
                    return project.Build(expectedLoggers);
                }
            }
            finally
            {
                if (binaryLogger != null)
                {
                    string binaryLoggerFilePath = binaryLogger.Parameters;

                    var actualLoggerPairs = GetLoggers().Where(l => l.logger is not BinaryLogger).ToArray();
                    expectedLoggerPairs = expectedLoggerPairs.Where(l => l.logger is not BinaryLogger).ToArray();

                    PlaybackBinlog(binaryLoggerFilePath, actualLoggerPairs.Select(k => k.logger).ToArray());
                    FileUtilities.DeleteNoThrow(binaryLoggerFilePath);

                    var pairs = expectedLoggerPairs.Zip(actualLoggerPairs, (expected, actual) => (expected, actual));

                    foreach (var pair in pairs)
                    {
                        var expectedText = pair.expected.textGetter();
                        var actualText = pair.actual.textGetter();
                        actualText.ShouldContainWithoutWhitespace(expectedText);
                    }
                }
            }
        }

        private static void PlaybackBinlog(string binlogFilePath, params ILogger[] loggers)
        {
            var replayEventSource = new BinaryLogReplayEventSource();

            foreach (var logger in loggers)
            {
                if (logger is INodeLogger nodeLogger)
                {
                    nodeLogger.Initialize(replayEventSource, 1);
                }
                else
                {
                    logger.Initialize(replayEventSource);
                }
            }

            replayEventSource.Replay(binlogFilePath);

            foreach (var logger in loggers)
            {
                logger.Shutdown();
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
