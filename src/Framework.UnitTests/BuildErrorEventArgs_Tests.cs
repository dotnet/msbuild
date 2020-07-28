// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the BuildErrorEventArg class.
    /// </summary>
    public class BuildErrorEventArgs_Tests
    {
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            BuildErrorEventArgs beea = new BuildErrorEventArgs2();
            beea = new BuildErrorEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "sender");
            beea = new BuildErrorEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "sender", DateTime.Now);
            beea = new BuildErrorEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "sender", DateTime.Now, "Message");
            beea = new BuildErrorEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "sender", "HelpLink", DateTime.Now, "Message");
            beea = new BuildErrorEventArgs(null, null, null, 1, 2, 3, 4, null, null, null);
            beea = new BuildErrorEventArgs(null, null, null, 1, 2, 3, 4, null, null, null, DateTime.Now);
            beea = new BuildErrorEventArgs(null, null, null, 1, 2, 3, 4, null, null, null, null, DateTime.Now, null);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and
        /// verify this code path.
        /// </summary>
        private class BuildErrorEventArgs2 : BuildErrorEventArgs
        {
            /// <summary>
            /// Test Constructor
            /// </summary>
            public BuildErrorEventArgs2() : base()
            {
            }
        }
    }
}
