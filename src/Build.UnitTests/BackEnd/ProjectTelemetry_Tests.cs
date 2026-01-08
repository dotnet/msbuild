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
        /// Test that TrackTaskSubclassing tracks sealed tasks that derive from Microsoft tasks
        /// </summary>
        [Fact]
        public void TrackTaskSubclassing_TracksSealedTasks()
        {
            var telemetry = new ProjectTelemetry();
            
            // Sealed task should be tracked if it derives from Microsoft task
            telemetry.TrackTaskSubclassing(typeof(TestSealedTask), isMicrosoftOwned: false);
            
            var properties = GetMSBuildTaskSubclassProperties(telemetry);
            
            // Should track sealed tasks that inherit from Microsoft tasks
            properties.Count.ShouldBe(1);
            properties.ShouldContainKey("Microsoft_Build_Utilities_Task");
            properties["Microsoft_Build_Utilities_Task"].ShouldBe("1");
        }

        /// <summary>
        /// Test that TrackTaskSubclassing tracks subclasses of Microsoft tasks
        /// </summary>
        [Fact]
        public void TrackTaskSubclassing_TracksSubclass()
        {
            var telemetry = new ProjectTelemetry();
            
            // User task inheriting from Microsoft.Build.Utilities.Task
            telemetry.TrackTaskSubclassing(typeof(UserTask), isMicrosoftOwned: false);
            
            var properties = GetMSBuildTaskSubclassProperties(telemetry);
            
            // Should track the Microsoft.Build.Utilities.Task base class
            properties.Count.ShouldBe(1);
            properties.ShouldContainKey("Microsoft_Build_Utilities_Task");
            properties["Microsoft_Build_Utilities_Task"].ShouldBe("1");
        }

        /// <summary>
        /// Test that TrackTaskSubclassing does not track Microsoft-owned tasks
        /// </summary>
        [Fact]
        public void TrackTaskSubclassing_IgnoresMicrosoftOwnedTasks()
        {
            var telemetry = new ProjectTelemetry();
            
            // Microsoft-owned task should not be tracked even if non-sealed
            telemetry.TrackTaskSubclassing(typeof(UserTask), isMicrosoftOwned: true);
            
            var properties = GetMSBuildTaskSubclassProperties(telemetry);
            
            // Should not track Microsoft-owned tasks
            properties.Count.ShouldBe(0);
        }

        /// <summary>
        /// Test that TrackTaskSubclassing tracks multiple subclasses
        /// </summary>
        [Fact]
        public void TrackTaskSubclassing_TracksMultipleSubclasses()
        {
            var telemetry = new ProjectTelemetry();
            
            // Track multiple user tasks
            telemetry.TrackTaskSubclassing(typeof(UserTask), isMicrosoftOwned: false);
            telemetry.TrackTaskSubclassing(typeof(AnotherUserTask), isMicrosoftOwned: false);
            
            var properties = GetMSBuildTaskSubclassProperties(telemetry);
            
            // Should aggregate counts for the same base class
            properties.Count.ShouldBe(1);
            properties["Microsoft_Build_Utilities_Task"].ShouldBe("2");
        }

        /// <summary>
        /// Test that TrackTaskSubclassing handles null gracefully
        /// </summary>
        [Fact]
        public void TrackTaskSubclassing_HandlesNull()
        {
            var telemetry = new ProjectTelemetry();
            
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
            telemetry.TrackTaskSubclassing(null, isMicrosoftOwned: false);
#pragma warning restore CS8625
            
            var properties = GetMSBuildTaskSubclassProperties(telemetry);
            
            properties.Count.ShouldBe(0);
        }

        /// <summary>
        /// Helper method to get MSBuild task subclass properties from telemetry using reflection
        /// </summary>
        private System.Collections.Generic.Dictionary<string, string> GetMSBuildTaskSubclassProperties(ProjectTelemetry telemetry)
        {
            var method = typeof(ProjectTelemetry).GetMethod("GetMSBuildTaskSubclassProperties", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (System.Collections.Generic.Dictionary<string, string>)method!.Invoke(telemetry, null)!;
        }

        /// <summary>
        /// Helper method to get custom task factory properties from telemetry using reflection
        /// </summary>
        private System.Collections.Generic.Dictionary<string, string> GetCustomTaskFactoryProperties(ProjectTelemetry telemetry)
        {
            var method = typeof(ProjectTelemetry).GetMethod("GetCustomTaskFactoryProperties", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (System.Collections.Generic.Dictionary<string, string>)method!.Invoke(telemetry, null)!;
        }

        /// <summary>
        /// Non-sealed user task that inherits from Microsoft.Build.Utilities.Task
        /// </summary>
#pragma warning disable CA1852 // Type can be sealed
        private class UserTask : Task
#pragma warning restore CA1852
        {
            public override bool Execute()
            {
                return true;
            }
        }

        /// <summary>
        /// Another non-sealed user task that inherits from Microsoft.Build.Utilities.Task
        /// </summary>
#pragma warning disable CA1852 // Type can be sealed
        private class AnotherUserTask : Task
#pragma warning restore CA1852
        {
            public override bool Execute()
            {
                return true;
            }
        }

        /// <summary>
        /// Sealed task that inherits from Microsoft.Build.Utilities.Task
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
        public void MSBuildTaskTelemetry_IsLoggedDuringBuild()
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

        /// <summary>
        /// Test that AddTaskExecution tracks custom task factory usage individually
        /// </summary>
        [Fact]
        public void AddTaskExecution_TracksCustomTaskFactoryIndividually()
        {
            var telemetry = new ProjectTelemetry();
            
            // Add executions from custom task factories
            telemetry.AddTaskExecution("CustomFactory.MyTaskFactory", isTaskHost: false);
            telemetry.AddTaskExecution("AnotherCustomFactory", isTaskHost: false);
            telemetry.AddTaskExecution("CustomFactory.MyTaskFactory", isTaskHost: false);
            
            var properties = GetCustomTaskFactoryProperties(telemetry);
            
            // Should track each custom factory separately
            properties.Count.ShouldBe(2);
            properties.ShouldContainKey("CustomFactory_MyTaskFactory");
            properties["CustomFactory_MyTaskFactory"].ShouldBe("2");
            properties.ShouldContainKey("AnotherCustomFactory");
            properties["AnotherCustomFactory"].ShouldBe("1");
        }

        /// <summary>
        /// Test that AddTaskExecution does not track built-in factories as custom
        /// </summary>
        [Fact]
        public void AddTaskExecution_DoesNotTrackBuiltInFactoriesAsCustom()
        {
            var telemetry = new ProjectTelemetry();
            
            // Add executions from built-in task factories
            telemetry.AddTaskExecution("Microsoft.Build.BackEnd.AssemblyTaskFactory", isTaskHost: false);
            telemetry.AddTaskExecution("Microsoft.Build.Tasks.CodeTaskFactory", isTaskHost: false);
            telemetry.AddTaskExecution("Microsoft.Build.Tasks.RoslynCodeTaskFactory", isTaskHost: false);
            
            var properties = GetCustomTaskFactoryProperties(telemetry);
            
            // Should not track any custom factories
            properties.Count.ShouldBe(0);
        }

        /// <summary>
        /// Test that AddTaskExecution tracks CodeTaskFactory separately
        /// </summary>
        [Fact]
        public void AddTaskExecution_TracksCodeTaskFactorySeparately()
        {
            var telemetry = new ProjectTelemetry();
            
            // Add execution from CodeTaskFactory
            telemetry.AddTaskExecution("Microsoft.Build.Tasks.CodeTaskFactory", isTaskHost: false);
            telemetry.AddTaskExecution("Microsoft.Build.Tasks.CodeTaskFactory", isTaskHost: false);
            
            // Use reflection to get task factory properties
            var method = typeof(ProjectTelemetry).GetMethod("GetTaskFactoryProperties", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var properties = (System.Collections.Generic.Dictionary<string, string>)method!.Invoke(telemetry, null)!;
            
            // Should track CodeTaskFactory separately from custom factories
            properties.ShouldContainKey("CodeTaskFactoryTasksExecutedCount");
            properties["CodeTaskFactoryTasksExecutedCount"].ShouldBe("2");
            
            // Should not be in custom factory properties
            var customProperties = GetCustomTaskFactoryProperties(telemetry);
            customProperties.Count.ShouldBe(0);
        }

        /// <summary>
        /// Test that AddTaskExecution handles null or empty factory names gracefully
        /// </summary>
        [Fact]
        public void AddTaskExecution_HandlesNullFactoryNameGracefully()
        {
            var telemetry = new ProjectTelemetry();
            
            // Add execution with null or empty factory name
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
            telemetry.AddTaskExecution(null, isTaskHost: false);
#pragma warning restore CS8625
            telemetry.AddTaskExecution(string.Empty, isTaskHost: false);
            
            var properties = GetCustomTaskFactoryProperties(telemetry);
            
            // Should handle null/empty gracefully without adding entries
            properties.Count.ShouldBe(0);
        }

        /// <summary>
        /// Test that custom task factory names with special characters are properly sanitized
        /// </summary>
        [Fact]
        public void AddTaskExecution_SanitizesCustomTaskFactoryNames()
        {
            var telemetry = new ProjectTelemetry();
            
            // Add executions from custom task factories with special characters
            telemetry.AddTaskExecution("My.Custom-Factory Task", isTaskHost: false);
            telemetry.AddTaskExecution("Another.Factory", isTaskHost: false);
            
            var properties = GetCustomTaskFactoryProperties(telemetry);
            
            // Should sanitize special characters to underscores
            properties.Count.ShouldBe(2);
            properties.ShouldContainKey("My_Custom_Factory_Task");
            properties["My_Custom_Factory_Task"].ShouldBe("1");
            properties.ShouldContainKey("Another_Factory");
            properties["Another_Factory"].ShouldBe("1");
        }

        /// <summary>
        /// Test that sanitization handles edge cases correctly
        /// </summary>
        [Fact]
        public void SanitizePropertyName_HandlesEdgeCases()
        {
            // Use reflection to access the private SanitizePropertyName method
            var method = typeof(ProjectTelemetry).GetMethod("SanitizePropertyName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            // Test null input
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type
            var resultNull = (string)method!.Invoke(null, [null])!;
#pragma warning restore CS8625
            resultNull.ShouldBe(string.Empty);
            
            // Test empty string
            var resultEmpty = (string)method.Invoke(null, [string.Empty])!;
            resultEmpty.ShouldBe(string.Empty);
            
            // Test string with only special characters
            var resultSpecial = (string)method.Invoke(null, ["...---   "])!;
            resultSpecial.ShouldBe("_________");
        }
    }
}
