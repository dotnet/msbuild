// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests
{
    internal class ProjectIdLogger : Logger
    {
        private List<ProjectStartedEventArgs> projectStartedEvents = new List<ProjectStartedEventArgs>();
        private StringBuilder fullLog = new StringBuilder();

        internal List<ProjectStartedEventArgs> ProjectStartedEvents
        {
            get { return projectStartedEvents; }
        }

        public override void Initialize(IEventSource eventSource)
        {
            eventSource.ProjectStarted += new ProjectStartedEventHandler(ProjectStartedHandler);
            eventSource.AnyEventRaised += new AnyEventHandler(AnyEventHandler);
            eventSource.BuildFinished += new BuildFinishedEventHandler(BuildFinishedHandler);
        }

        void BuildFinishedHandler(object sender, BuildFinishedEventArgs e)
        {
            // Console.Write in the context of a unit test is very expensive.  A hundred
            // calls to Console.Write can easily take two seconds on a fast machine.  Therefore, only
            // do the Console.Write once at the end of the build.
            Console.Write(fullLog);
        }

        void AnyEventHandler(object sender, BuildEventArgs e)
        {
            fullLog.Append(e.Message);
            fullLog.Append("\r\n");
        }

        private void ProjectStartedHandler(object sender, ProjectStartedEventArgs e)
        {
            projectStartedEvents.Add(e);
        }
    }
}
