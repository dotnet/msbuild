// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the ProjectFinishedEventArgs class.
    /// </summary>
    public class ProjectFinishedEventArgs_Tests
    {
        /// <summary>
        /// Default event to use in tests.
        /// </summary>
        private ProjectFinishedEventArgs _baseProjectFinishedEvent = new ProjectFinishedEventArgs("Message", "HelpKeyword", "ProjectFile", true);
        
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            ProjectFinishedEventArgs projectFinishedEvent = new ProjectFinishedEventArgs2();
            projectFinishedEvent = new ProjectFinishedEventArgs("Message", "HelpKeyword", "ProjectFile", true);
            projectFinishedEvent = new ProjectFinishedEventArgs("Message", "HelpKeyword", "ProjectFile", true, DateTime.Now);
            projectFinishedEvent = new ProjectFinishedEventArgs(null, null, null, true);
            projectFinishedEvent = new ProjectFinishedEventArgs(null, null, null, true, DateTime.Now);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and 
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private class ProjectFinishedEventArgs2 : ProjectFinishedEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public ProjectFinishedEventArgs2()
                : base()
            {
            }
        }
    }
}
