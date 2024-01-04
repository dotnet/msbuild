// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the TargetFinishedEventArgs class.
    /// </summary>
    public class TargetFinishedEventArgs_Tests
    {
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            List<ITaskItem> outputs = new List<ITaskItem>();
            TargetFinishedEventArgs targetFinishedEvent = new TargetFinishedEventArgs2();
            targetFinishedEvent = new TargetFinishedEventArgs("Message", "HelpKeyword", "TargetName", "ProjectFile", "TargetFile", true);
            targetFinishedEvent = new TargetFinishedEventArgs("Message", "HelpKeyword", "TargetName", "ProjectFile", "TargetFile", true, DateTime.Now, outputs);
            targetFinishedEvent = new TargetFinishedEventArgs(null, null, null, null, null, true);
            targetFinishedEvent = new TargetFinishedEventArgs(null, null, null, null, null, true, DateTime.Now, null);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private sealed class TargetFinishedEventArgs2 : TargetFinishedEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public TargetFinishedEventArgs2()
                : base()
            {
            }
        }
    }
}
