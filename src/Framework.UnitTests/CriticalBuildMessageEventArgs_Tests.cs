// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the CriticalBuildMessageEventArgs class.
    /// </summary>
    public class CriticalBuildMessageEventArgs_Tests
    {
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            CriticalBuildMessageEventArgs cbmea = new CriticalBuildMessageEventArgs2();
            cbmea = new CriticalBuildMessageEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "Sender");
            cbmea = new CriticalBuildMessageEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "Sender", DateTime.Now);
            cbmea = new CriticalBuildMessageEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "Sender", DateTime.Now, "Message");
            cbmea = new CriticalBuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null);
            cbmea = new CriticalBuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null, DateTime.Now);
            cbmea = new CriticalBuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null, DateTime.Now, null);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and 
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private class CriticalBuildMessageEventArgs2 : CriticalBuildMessageEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public CriticalBuildMessageEventArgs2()
                : base()
            {
            }
        }
    }
}
