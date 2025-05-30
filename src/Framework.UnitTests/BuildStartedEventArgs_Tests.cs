// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the BuildStartedEventArgs class.
    /// </summary>
    public class BuildStartedEventArgs_Tests
    {
        /// <summary>
        /// Default event to use in tests.
        /// </summary>
        private BuildStartedEventArgs _baseStartedEvent = new BuildStartedEventArgs("Message", "HelpKeyword");

        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            BuildStartedEventArgs bsea = new BuildStartedEventArgs2();
            bsea = new BuildStartedEventArgs("Message", "HelpKeyword");
            bsea = new BuildStartedEventArgs("Message", "HelpKeyword", DateTime.Now);
            bsea = new BuildStartedEventArgs("{0}", "HelpKeyword", DateTime.Now, "Message");
            bsea = new BuildStartedEventArgs(null, null);
            bsea = new BuildStartedEventArgs(null, null, DateTime.Now);
            bsea = new BuildStartedEventArgs(null, null, DateTime.Now, null);
        }

        /// <summary>
        /// Trivially exercise getHashCode.
        /// </summary>
        [Fact]
        public void TestGetHashCode()
        {
            _baseStartedEvent.GetHashCode();
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private sealed class BuildStartedEventArgs2 : BuildStartedEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public BuildStartedEventArgs2()
                : base()
            {
            }
        }
    }
}
