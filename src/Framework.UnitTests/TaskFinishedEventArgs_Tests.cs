// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Framework;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the TaskFinishedEventArgs class.
    /// </summary>
    public class TaskFinishedEventArgs_Tests
    {
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            TaskFinishedEventArgs targetFinishedEvent = new TaskFinishedEventArgs2();
            targetFinishedEvent = new TaskFinishedEventArgs("Message", "HelpKeyword", "ProjectFile", "TaskFile", "TaskName", true);
            targetFinishedEvent = new TaskFinishedEventArgs("Message", "HelpKeyword", "ProjectFile", "TaskFile", "TaskName", true, DateTime.Now);
            targetFinishedEvent = new TaskFinishedEventArgs(null, null, null, null, null, true);
            targetFinishedEvent = new TaskFinishedEventArgs(null, null, null, null, null, true, DateTime.Now);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private sealed class TaskFinishedEventArgs2 : TaskFinishedEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public TaskFinishedEventArgs2()
                : base()
            {
            }
        }
    }
}
