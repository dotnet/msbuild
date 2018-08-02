// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the BuildFinishedEventArg class.
    /// </summary>
    public class BuildFinishedEventArgs_Tests
    {
        /// <summary>
        /// Default buildFinished event to use in tests.
        /// </summary>
        private BuildFinishedEventArgs _baseFinishedEvent = new BuildFinishedEventArgs("Message", "HelpKeyword", true);

        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            BuildFinishedEventArgs buildFinishedEvent = new BuildFinishedEventArgs2();
            buildFinishedEvent = new BuildFinishedEventArgs("Message", "HelpKeyword", true);
            buildFinishedEvent = new BuildFinishedEventArgs("Message", "HelpKeyword", true, new DateTime());
            buildFinishedEvent = new BuildFinishedEventArgs("{0}", "HelpKeyword", true, new DateTime(), "Message");
            buildFinishedEvent = new BuildFinishedEventArgs(null, null, true);
            buildFinishedEvent = new BuildFinishedEventArgs(null, null, true, new DateTime());
            buildFinishedEvent = new BuildFinishedEventArgs(null, null, true, new DateTime(), null);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and 
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private class BuildFinishedEventArgs2 : BuildFinishedEventArgs
        {
            /// <summary>
            /// Test constructor
            /// </summary>
            public BuildFinishedEventArgs2()
                : base()
            {
            }
        }
    }
}