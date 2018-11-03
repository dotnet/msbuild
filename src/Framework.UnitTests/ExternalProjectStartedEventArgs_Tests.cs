// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the ExternalProjectStartedEventArgs class.
    /// </summary>
    public class ExternalProjectStartedEventArgs_Tests
    {
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            ExternalProjectStartedEventArgs externalProjectStartedEvent = new ExternalProjectStartedEventArgs2();
            externalProjectStartedEvent = new ExternalProjectStartedEventArgs("Message", "HelpKeyword", "Sender", "ProjectFile", "TargetNames");
            externalProjectStartedEvent = new ExternalProjectStartedEventArgs("Message", "HelpKeyword", "Sender", "ProjectFile", "TargetNames", DateTime.Now);
            externalProjectStartedEvent = new ExternalProjectStartedEventArgs(null, null, null, null, null);
            externalProjectStartedEvent = new ExternalProjectStartedEventArgs(null, null, null, null, null, DateTime.Now);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and 
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private class ExternalProjectStartedEventArgs2 : ExternalProjectStartedEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public ExternalProjectStartedEventArgs2() : base()
            {
            }
        }
    }
}
