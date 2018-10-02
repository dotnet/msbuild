// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Xunit;

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
        private class BuildStartedEventArgs2 : BuildStartedEventArgs
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