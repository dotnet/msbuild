// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Framework;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the BuildMessageEventArgs class.
    /// </summary>
    public class BuildMessageEventArgs_Tests
    {
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            BuildMessageEventArgs bmea = new BuildMessageEventArgs2();
            bmea = new BuildMessageEventArgs("Message", "HelpKeyword", "Sender", MessageImportance.Low);
            bmea = new BuildMessageEventArgs("Message", "HelpKeyword", "Sender", MessageImportance.Low, DateTime.Now);
            bmea = new BuildMessageEventArgs("{0}", "HelpKeyword", "Sender", MessageImportance.Low, DateTime.Now, "Message");
            bmea = new BuildMessageEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "Sender", MessageImportance.Low);
            bmea = new BuildMessageEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "Sender", MessageImportance.Low, DateTime.Now);
            bmea = new BuildMessageEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "Sender", MessageImportance.Low, DateTime.Now, "Message");
            bmea = new BuildMessageEventArgs(null, null, null, MessageImportance.Low);
            bmea = new BuildMessageEventArgs(null, null, null, MessageImportance.Low, DateTime.Now);
            bmea = new BuildMessageEventArgs(null, null, null, MessageImportance.Low, DateTime.Now, null);
            bmea = new BuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null, MessageImportance.Low);
            bmea = new BuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null, MessageImportance.Low, DateTime.Now);
            bmea = new BuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null, MessageImportance.Low, DateTime.Now, null);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private sealed class BuildMessageEventArgs2 : BuildMessageEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public BuildMessageEventArgs2()
                : base()
            {
            }
        }
    }
}
