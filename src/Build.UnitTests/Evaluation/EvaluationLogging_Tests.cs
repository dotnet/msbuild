// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Shouldly;
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

        private static void AssertLoggingEvents(
            Action<Project, MockLogger> loggingTest = null,
            MockLogger firstEvaluationLogger = null,
            Func<Project, MockLogger> reevaluationLoggerFactory = null)
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

                firstEvaluationLogger = firstEvaluationLogger ?? new MockLogger();
                collection.RegisterLogger(firstEvaluationLogger);

                var project = new Project(projectFile, null, null, collection);

                firstEvaluationLogger.AllBuildEvents.ShouldNotBeEmpty();

                if (reevaluationLoggerFactory != null)
                {
                    var reevaluationLogger = reevaluationLoggerFactory.Invoke(project);
                    collection.RegisterLogger(reevaluationLogger);

                    project.SetProperty("aProperty", "Value");
                    project.ReevaluateIfNecessary();

                    reevaluationLogger.AllBuildEvents.ShouldNotBeEmpty();
                }

                loggingTest?.Invoke(project, firstEvaluationLogger);
            }
        }

        [Fact]
        public void AllEvaluationEventsShouldHaveAnEvaluationId()
        {
            AssertLoggingEvents(
                (project, firstEvaluationLogger) =>
                {
                    var evaluationId = project.LastEvaluationId;
                    evaluationId.ShouldNotBe(BuildEventContext.InvalidEvaluationId);

                    foreach (var buildEvent in firstEvaluationLogger.AllBuildEvents)
                    {
                        buildEvent.BuildEventContext.EvaluationId.ShouldBe(evaluationId);
                    }
                });
        }

        [Fact]
        public void GivenOneProjectThereShouldBeOneStartedAndOneEndedEvent()
        {
            AssertLoggingEvents(
                (project, firstEvaluationLogger) =>
                {
                    var allBuildEvents = firstEvaluationLogger.AllBuildEvents;

                    allBuildEvents.Count.ShouldBeGreaterThan(2);

                    for (var i = 0; i < allBuildEvents.Count; i++)
                    {
                        var buildEvent = allBuildEvents[i];

                        if (i == 0)
                        {
                            buildEvent.Message.ShouldStartWith("Evaluation started");
                        }
                        else if (i == allBuildEvents.Count - 1)
                        {
                            buildEvent.Message.ShouldStartWith("Evaluation finished");
                        }
                        else
                        {
                            buildEvent.Message.ShouldNotStartWith("Evaluation started");
                            buildEvent.Message.ShouldNotStartWith("Evaluation finished");
                        }
                    }
                });
        }

        [Fact]
        public void ProjectShouldHaveValidEvaluationIdDuringEvaluation()
        {
            AssertLoggingEvents(
                null,
                null,
                project => new MockLogger
                {
                    AdditionalHandlers = new List<Action<object, BuildEventArgs>>
                    {
                        (sender, args) =>
                        {
                            var eventEvaluationId = args.BuildEventContext.EvaluationId;

                            eventEvaluationId.ShouldNotBe(BuildEventContext.InvalidEvaluationId);
                            project.LastEvaluationId.ShouldBe(eventEvaluationId);
                        }
                    }
                });
        }

        [Fact]
        public void TurnOnProfileEvaluationFromLogger()
        {
            AssertLoggingEvents(
                (project, logger) =>
                {
                    foreach (var e in logger.AllBuildEvents.OfType<ProjectEvaluationFinishedEventArgs>())
                    {
                        e.ProfilerResult.ShouldNotBeNull();
                    }
                },
                new MockLogger(null, true));
        }
    }
}
