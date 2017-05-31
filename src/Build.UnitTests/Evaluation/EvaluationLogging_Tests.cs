// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for evaluation logging</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation
{
    /// <summary>
    ///     Tests mainly for project evaluation logging
    /// </summary>
    public class EvaluationLogging_Tests : IDisposable
    {
        /// <summary>
        ///     Cleanup
        /// </summary>
        public EvaluationLogging_Tests()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        /// <summary>
        ///     Cleanup
        /// </summary>
        public void Dispose()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        private static void AssertLoggingEvents(Action<Project, MockLogger> loggingTest)
        {
            var projectImportContents =
                @"<Project>
                    <Target Name=`Foo`
                            AfterTargets=`X`
                            BeforeTargets=`Y`
                    >
                    </Target>

                    <PropertyGroup>
                      <P>Bar</P>
                      <P2>$(NonExisting)</P2>
                    </PropertyGroup>
                  </Project>".Cleanup();

            var projectContents =
                @"<Project>
                    <Target Name=`Foo`>
                    </Target>

                    <PropertyGroup>
                      <P>Foo</P>
                    </PropertyGroup>

                    <Import Project=`{0}`/>
                    <Import Project=`{0}`/>
                  </Project>".Cleanup();

            using (var env = TestEnvironment.Create())
            {
                var collection = env.CreateProjectCollection().Collection;

                var importFile = env.CreateFile().Path;
                File.WriteAllText(importFile, projectImportContents);

                projectContents = string.Format(projectContents, importFile);


                var projectFile = env.CreateFile().Path;
                File.WriteAllText(projectFile, projectContents);

                var logger = new MockLogger();
                collection.RegisterLogger(logger);

                var project = new Project(projectFile, null, null, collection);

                Assert.NotEmpty(logger.AllBuildEvents);

                loggingTest.Invoke(project, logger);
            }
        }

        [Fact]
        public void AllEvaluationEventsShouldHaveAnEvaluationId()
        {
            AssertLoggingEvents(
                (project, mockLogger) =>
                {
                    var evaluationId = project.LastEvaluationId;

                    Assert.NotEqual(BuildEventContext.InvalidEvaluationId, evaluationId);

                    foreach (var buildEvent in mockLogger.AllBuildEvents)
                    {
                        Assert.Equal(evaluationId, buildEvent.BuildEventContext.EvaluationId);
                    }
                });
        }

        [Fact]
        public void FirstAndLastEvaluationEventsShouldBeStartedAndEnded()
        {
            AssertLoggingEvents(
                (project, mockLogger) =>
                {
                    Assert.True(mockLogger.AllBuildEvents.Count >= 2);

                    Assert.StartsWith("Evaluation started", mockLogger.AllBuildEvents.First().Message);
                    Assert.StartsWith("Evaluation finished", mockLogger.AllBuildEvents.Last().Message);
                });
        }
    }
}
