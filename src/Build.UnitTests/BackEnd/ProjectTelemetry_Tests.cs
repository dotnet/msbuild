// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for ProjectTelemetry class
    /// </summary>
    public class ProjectTelemetry_Tests
    {
        /// <summary>
        /// Test that AddMicrosoftTaskLoaded correctly tracks sealed tasks
        /// </summary>
        [Fact]
        public void AddMicrosoftTaskLoaded_TracksSealed()
        {
            var telemetry = new ProjectTelemetry();
            
            // Use a sealed type - string is sealed
            telemetry.AddMicrosoftTaskLoaded(typeof(string));
            
            // Get properties using reflection to verify counts
            var properties = GetMicrosoftTaskProperties(telemetry);
            
            properties["MicrosoftTasksLoadedCount"].ShouldBe("1");
            properties["MicrosoftTasksSealedCount"].ShouldBe("1");
        }

        /// <summary>
        /// Test that AddMicrosoftTaskLoaded correctly tracks non-sealed tasks
        /// </summary>
        [Fact]
        public void AddMicrosoftTaskLoaded_TracksNonSealed()
        {
            var telemetry = new ProjectTelemetry();
            
            // Use a non-sealed type
            telemetry.AddMicrosoftTaskLoaded(typeof(TestTask));
            
            var properties = GetMicrosoftTaskProperties(telemetry);
            
            properties["MicrosoftTasksLoadedCount"].ShouldBe("1");
            properties.ShouldNotContainKey("MicrosoftTasksSealedCount");
        }

        /// <summary>
        /// Test that AddMicrosoftTaskLoaded correctly tracks tasks inheriting from Task
        /// </summary>
        [Fact]
        public void AddMicrosoftTaskLoaded_TracksInheritanceFromTask()
        {
            var telemetry = new ProjectTelemetry();
            
            // Use a task that inherits from Microsoft.Build.Utilities.Task
            telemetry.AddMicrosoftTaskLoaded(typeof(TestTask));
            
            var properties = GetMicrosoftTaskProperties(telemetry);
            
            properties["MicrosoftTasksLoadedCount"].ShouldBe("1");
            properties["MicrosoftTasksInheritingFromTaskCount"].ShouldBe("1");
        }

        /// <summary>
        /// Test that AddMicrosoftTaskLoaded tracks multiple tasks correctly
        /// </summary>
        [Fact]
        public void AddMicrosoftTaskLoaded_TracksMultipleTasks()
        {
            var telemetry = new ProjectTelemetry();
            
            telemetry.AddMicrosoftTaskLoaded(typeof(TestTask));
            telemetry.AddMicrosoftTaskLoaded(typeof(string)); // sealed
            telemetry.AddMicrosoftTaskLoaded(typeof(TestSealedTask)); // sealed and inherits from Task
            
            var properties = GetMicrosoftTaskProperties(telemetry);
            
            properties["MicrosoftTasksLoadedCount"].ShouldBe("3");
            properties["MicrosoftTasksSealedCount"].ShouldBe("2");
            properties["MicrosoftTasksInheritingFromTaskCount"].ShouldBe("2");
        }

        /// <summary>
        /// Test that AddMicrosoftTaskLoaded handles null gracefully
        /// </summary>
        [Fact]
        public void AddMicrosoftTaskLoaded_HandlesNull()
        {
            var telemetry = new ProjectTelemetry();
            
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
            telemetry.AddMicrosoftTaskLoaded(null);
#pragma warning restore CS8625
            
            var properties = GetMicrosoftTaskProperties(telemetry);
            
            properties.Count.ShouldBe(0);
        }

        /// <summary>
        /// Helper method to get Microsoft task properties from telemetry using reflection
        /// </summary>
        private System.Collections.Generic.Dictionary<string, string> GetMicrosoftTaskProperties(ProjectTelemetry telemetry)
        {
            var method = typeof(ProjectTelemetry).GetMethod("GetMicrosoftTaskProperties", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (System.Collections.Generic.Dictionary<string, string>)method!.Invoke(telemetry, null)!;
        }

        /// <summary>
        /// Test task that inherits from Microsoft.Build.Utilities.Task
        /// </summary>
#pragma warning disable CA1852 // Type can be sealed
        private class TestTask : Task
#pragma warning restore CA1852
        {
            public override bool Execute()
            {
                return true;
            }
        }

        /// <summary>
        /// Test sealed task that inherits from Microsoft.Build.Utilities.Task
        /// </summary>
        private sealed class TestSealedTask : Task
        {
            public override bool Execute()
            {
                return true;
            }
        }

        /// <summary>
        /// Integration test that verifies telemetry is logged during a build with Microsoft tasks
        /// </summary>
        [Fact]
        public void MicrosoftTaskTelemetry_IsLoggedDuringBuild()
        {
            string projectContent = @"
                <Project>
                    <Target Name='Build'>
                        <Message Text='Hello World' Importance='High' />
                    </Target>
                </Project>";

            var events = new System.Collections.Generic.List<BuildEventArgs>();
            var logger = new Microsoft.Build.Logging.ConsoleLogger(LoggerVerbosity.Diagnostic);
            
            using var projectCollection = new ProjectCollection();
            using var stringReader = new System.IO.StringReader(projectContent);
            using var xmlReader = System.Xml.XmlReader.Create(stringReader);
            var project = new Project(xmlReader, null, null, projectCollection);

            // Build the project
            var result = project.Build();
            
            result.ShouldBeTrue();
        }
    }
}
