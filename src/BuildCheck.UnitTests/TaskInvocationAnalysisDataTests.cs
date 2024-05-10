// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using static Microsoft.Build.Experimental.BuildCheck.Infrastructure.BuildCheckManagerProvider;

namespace Microsoft.Build.BuildCheck.UnitTests
{
    public class TaskInvocationAnalysisDataTests : IDisposable
    {
        internal sealed class TestAnalyzer : BuildAnalyzer
        {
            #region BuildAnalyzer initialization

            public static BuildAnalyzerRule SupportedRule = new BuildAnalyzerRule("BC0000", "TestRule", "TestDescription", "TestMessage",
                new BuildAnalyzerConfiguration() { Severity = BuildAnalyzerResultSeverity.Warning, IsEnabled = true });

            public override string FriendlyName => "MSBuild.TestAnalyzer";

            public override IReadOnlyList<BuildAnalyzerRule> SupportedRules { get; } = [SupportedRule];

            public override void Initialize(ConfigurationContext configurationContext)
            { }

            public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
            {
                registrationContext.RegisterTaskInvocationAction(TaskInvocationAction);
            }

            #endregion

            /// <summary>
            /// Stores all TaskInvocationAnalysisData reported during the build.
            /// </summary>
            public List<TaskInvocationAnalysisData> AnalysisData = new();

            private void TaskInvocationAction(BuildCheckDataContext<TaskInvocationAnalysisData> context)
            {
                AnalysisData.Add(context.Data);
            }
        }

        private static TestAnalyzer? s_testAnalyzer;

        public TaskInvocationAnalysisDataTests()
        {
            BuildCheckManager.s_testFactoriesPerDataSource =
            [
                // BuildCheckDataSource.EventArgs
                [
                    ([TestAnalyzer.SupportedRule.Id], true, () => (s_testAnalyzer = new TestAnalyzer())),
                ],
                // BuildCheckDataSource.Execution
                [],
            ];

            s_testAnalyzer?.AnalysisData.Clear();
        }

        public void Dispose()
        {
            BuildCheckManager.s_testFactoriesPerDataSource = null;
        }

        private void BuildProject(string taskInvocation)
        {
            using (var env = TestEnvironment.Create())
            {
                var testProject = env.CreateTestProjectWithFiles($"<Project><Target Name=\"Build\">{taskInvocation}</Target></Project>");

                using (var buildManager = new BuildManager())
                {
                    var request = new BuildRequestData(testProject.ProjectFile, new Dictionary<string, string>(), MSBuildConstants.CurrentToolsVersion, [], null, BuildRequestDataFlags.None);
                    var parameters = new BuildParameters
                    {
                        LogTaskInputs = true,
                        IsBuildCheckEnabled = true,
                        ShutdownInProcNodeOnBuildFinish = true,
                    };

                    var result = buildManager.Build(parameters, request);

                    result.OverallResult.ShouldBe(BuildResultCode.Success);
                }

                foreach (var data in s_testAnalyzer!.AnalysisData)
                {
                    data.ProjectFilePath.ShouldBe(testProject.ProjectFile);
                    data.LineNumber.ShouldBeGreaterThan(0);
                    data.ColumnNumber.ShouldBeGreaterThan(0);
                }
            }
        }

        [Fact]
        public void ReportsSimpleTaskParameters()
        {
            BuildProject("<Message Text='Hello'/>");

            s_testAnalyzer!.AnalysisData.Count.ShouldBe(1);
            var data = s_testAnalyzer.AnalysisData[0];
            data.TaskName.ShouldBe("Message");
            data.Parameters.Count.ShouldBe(1);
            data.Parameters["Text"].IsOutput.ShouldBe(false);
            data.Parameters["Text"].Value.ShouldBe("Hello");
        }

        [Fact]
        public void ReportsComplexTaskParameters()
        {
            BuildProject("""
                <ItemGroup>
                  <TestItem Include='item1;item2'/>
                </ItemGroup>
                <CombinePath BasePath='base' Paths='@(TestItem)'>
                    <Output TaskParameter='CombinedPaths' ItemName='OutputDirectories' />
                </CombinePath>
            """);

            s_testAnalyzer!.AnalysisData.Count.ShouldBe(1);
            var data = s_testAnalyzer.AnalysisData[0];
            data.TaskName.ShouldBe("CombinePath");
            data.Parameters.Count.ShouldBe(3);

            data.Parameters["Paths"].IsOutput.ShouldBe(false);
            data.Parameters["Paths"].Value.ShouldBeAssignableTo(typeof(IList));
            IList listValue = (IList)data.Parameters["Paths"].Value!;
            listValue.Count.ShouldBe(2);
            listValue[0]!.ShouldBeAssignableTo(typeof(ITaskItem));
            listValue[1]!.ShouldBeAssignableTo(typeof(ITaskItem));
            ((ITaskItem)listValue[0]!).ItemSpec.ShouldBe("item1");
            ((ITaskItem)listValue[1]!).ItemSpec.ShouldBe("item2");

            // The name of the parameter would ideally be "CombinedPaths" but we don't seem to be currently logging it.
            data.Parameters["OutputDirectories"].IsOutput.ShouldBe(true);
        }
    }
}
