// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    class MockProjectStartedLogger : ILogger
    {
        #region ILogger Members

        /// <summary>
        /// Logger verbosity
        /// </summary>
        public LoggerVerbosity Verbosity
        {
            get 
            { 
                return LoggerVerbosity.Diagnostic; 
            }
            set 
            { 
            }
        }

        /// <summary>
        /// Logger parameters
        /// </summary>
        public string Parameters
        {
            get 
            { 
                return null; 
            }
            set
            {
            }
        }
        
        /// <summary>
        /// Subscribing to the events
        /// </summary>
        /// <param name="eventSource"></param>
        public void Initialize(IEventSource eventSource)
        {
            eventSource.ProjectStarted += new ProjectStartedEventHandler(eventSource_ProjectStarted);
        }

        /// <summary>
        /// Handler for the ProjectStarted event. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void eventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            // Clone and store the properties so we can inspect the values later on
            foreach (DictionaryEntry property in e.Properties)
            {
                projectStartedProperties[(string) property.Key] = (string) property.Value;
            }
        }

        public void Shutdown()
        {
            // do nothing
        }

        #endregion

        // dictionary of properties sent to us with the ProjectStarted event
        private Dictionary<string, string> projectStartedProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> ProjectStartedProperties
        {
            get
            {
                return projectStartedProperties;
            }
        }
    }
}
