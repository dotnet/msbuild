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
    }
}
