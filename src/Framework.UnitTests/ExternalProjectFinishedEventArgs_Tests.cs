// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Framework;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the ExternalProjectFinishedEventArgs class.
    /// </summary>
    public class ExternalProjectFinishedEventArgs_Tests
    {
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            ExternalProjectFinishedEventArgs externalProjectFinishedEvent = new ExternalProjectFinishedEventArgs2();
            externalProjectFinishedEvent = new ExternalProjectFinishedEventArgs("Message", "HelpKeyword", "Sender", "ProjectFile", true);
            externalProjectFinishedEvent = new ExternalProjectFinishedEventArgs("Message", "HelpKeyword", "Sender", "ProjectFile", true, DateTime.Now);
            externalProjectFinishedEvent = new ExternalProjectFinishedEventArgs(null, null, null, null, true);
            externalProjectFinishedEvent = new ExternalProjectFinishedEventArgs(null, null, null, null, true, DateTime.Now);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private sealed class ExternalProjectFinishedEventArgs2 : ExternalProjectFinishedEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public ExternalProjectFinishedEventArgs2()
                : base()
            {
            }
        }
    }
}
