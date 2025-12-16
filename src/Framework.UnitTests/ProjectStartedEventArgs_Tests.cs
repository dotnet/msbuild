// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the ProjectStartedEventArgs class.
    /// </summary>
    public class ProjectStartedEventArgs_Tests
    {
        /// <summary>
        /// Setup for text fixture, this is run ONCE for the entire test fixture
        /// </summary>
        public ProjectStartedEventArgs_Tests()
        {
        }

        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            ProjectStartedEventArgs projectStartedEvent = new ProjectStartedEventArgs2();
            projectStartedEvent.ShouldNotBeNull();

            projectStartedEvent = new ProjectStartedEventArgs("Message", "HelpKeyword", "ProjecFile", "TargetNames", null, null);
            projectStartedEvent = new ProjectStartedEventArgs("Message", "HelpKeyword", "ProjecFile", "TargetNames", null, null, DateTime.Now);
            projectStartedEvent = new ProjectStartedEventArgs(1, "Message", "HelpKeyword", "ProjecFile", "TargetNames", null, null, null);
            projectStartedEvent = new ProjectStartedEventArgs(1, "Message", "HelpKeyword", "ProjecFile", "TargetNames", null, null, null, DateTime.Now);
            projectStartedEvent = new ProjectStartedEventArgs(null, null, null, null, null, null);
            projectStartedEvent = new ProjectStartedEventArgs(null, null, null, null, null, null, DateTime.Now);
            projectStartedEvent = new ProjectStartedEventArgs(1, null, null, null, null, null, null, null);
            projectStartedEvent = new ProjectStartedEventArgs(1, null, null, null, null, null, null, null, DateTime.Now);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private sealed class ProjectStartedEventArgs2 : ProjectStartedEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public ProjectStartedEventArgs2()
                : base()
            {
            }
        }
    }
}
