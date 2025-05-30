// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Framework;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the TaskCommandLineEventArgs class.
    /// </summary>
    public class TaskCommandLineEventArgs_Tests
    {
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            TaskCommandLineEventArgs taskCommandLineEvent = new TaskCommandLineEventArgs2();
            taskCommandLineEvent = new TaskCommandLineEventArgs("Commandline", "taskName", MessageImportance.High);
            taskCommandLineEvent = new TaskCommandLineEventArgs("Commandline", "taskName", MessageImportance.High, DateTime.Now);
            taskCommandLineEvent = new TaskCommandLineEventArgs(null, null, MessageImportance.High);
            taskCommandLineEvent = new TaskCommandLineEventArgs(null, null, MessageImportance.High, DateTime.Now);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private sealed class TaskCommandLineEventArgs2 : TaskCommandLineEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public TaskCommandLineEventArgs2() : base()
            {
            }
        }
    }
}
