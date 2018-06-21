// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;

using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the ProjectStartedEventArgs class.
    /// </summary>
    public class ProjectStartedEventArgs_Tests
    {
        /// <summary>
        /// Default event to use in tests.
        /// </summary>
        private static ProjectStartedEventArgs s_baseProjectStartedEvent;

        /// <summary>
        /// Setup for text fixture, this is run ONCE for the entire test fixture
        /// </summary>
        public ProjectStartedEventArgs_Tests()
        {
            BuildEventContext parentBuildEventContext = new BuildEventContext(2, 3, 4, 5);
            s_baseProjectStartedEvent = new ProjectStartedEventArgs(1, "Message", "HelpKeyword", "ProjecFile", "TargetNames", null, null, parentBuildEventContext);
        }

        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            ProjectStartedEventArgs projectStartedEvent = new ProjectStartedEventArgs2();
            projectStartedEvent.ShouldNotBeNull();

            projectStartedEvent = new ProjectStartedEventArgs("Message", "HelpKeyword", "ProjecFile", "TargetNames", null, null);
            projectStartedEvent = new ProjectStartedEventArgs("Message", "HelpKeyword", "ProjecFile", "TargetNames", null, null, DateTime.Now);
            projectStartedEvent = new ProjectStartedEventArgs(1, "Message", "HelpKeyword", "ProjecFile", "TargetNames", null, null, null);
            projectStartedEvent = new ProjectStartedEventArgs(1, "Message", "HelpKeyword", "ProjecFile", "TargetNames", null, null, null, DateTime.Now);
            projectStartedEvent = new ProjectStartedEventArgs(null, null, null, null, null, null);
            projectStartedEvent = new ProjectStartedEventArgs(null, null, null, null, null, null, DateTime.Now);
            projectStartedEvent = new ProjectStartedEventArgs(1, null, null, null, null, null, null, null);
            projectStartedEvent = new ProjectStartedEventArgs(1, null, null, null, null, null, null, null, DateTime.Now);
        }

        /// <summary>
        /// Verify different Items and properties are not taken into account in the equals comparison. They should 
        /// not be considered as part of the equals evaluation
        /// </summary>
        [Fact]
        public void ItemsAndPropertiesDifferentEquals()
        {
            ArrayList itemsList = new ArrayList();
            ArrayList propertiesList = new ArrayList();
            ProjectStartedEventArgs differentItemsAndProperties = new ProjectStartedEventArgs
                (
                  s_baseProjectStartedEvent.ProjectId,
                  s_baseProjectStartedEvent.Message,
                  s_baseProjectStartedEvent.HelpKeyword,
                  s_baseProjectStartedEvent.ProjectFile,
                  s_baseProjectStartedEvent.TargetNames,
                  propertiesList,
                  itemsList,
                  s_baseProjectStartedEvent.ParentProjectBuildEventContext,
                  s_baseProjectStartedEvent.Timestamp
                );

            s_baseProjectStartedEvent.Properties.ShouldNotBe(propertiesList);
            s_baseProjectStartedEvent.Items.ShouldNotBe(itemsList);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and 
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private class ProjectStartedEventArgs2 : ProjectStartedEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public ProjectStartedEventArgs2()
                : base()
            {
            }
        }
    }
}