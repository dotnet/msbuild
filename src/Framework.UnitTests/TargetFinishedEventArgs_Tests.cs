// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Xunit;

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
        private class TargetFinishedEventArgs2 : TargetFinishedEventArgs
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
