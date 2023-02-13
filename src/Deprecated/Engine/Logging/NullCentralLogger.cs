// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class will throw an exception when it recieves any event except for the build started or build finished event
    /// this logger is good to use if a distributed logger is attached but does not want to forward any events
    /// </summary>
    internal class NullCentralLogger : INodeLogger
    {
        #region Data
        private string parameters;
        private LoggerVerbosity verbosity;
        #endregion

        #region Properties
        public LoggerVerbosity Verbosity
        {
            get
            {
                return verbosity;
            }
            set
            {
                verbosity = value;
            }
        }

        public string Parameters
        {
            get
            {
                return parameters;
            }
            set
            {
                parameters = value;
            }
        }
        #endregion

        #region Methods
        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            eventSource.AnyEventRaised += AnyEventRaisedHandler;
        }

        public void AnyEventRaisedHandler(object sender, BuildEventArgs e)
        {
            if (!(e is BuildStartedEventArgs) && !(e is BuildFinishedEventArgs))
            {
                ErrorUtilities.VerifyThrowInvalidOperation(false, "Should not recieve any events other than build started or finished");
            }
        }

        public void Initialize(IEventSource eventSource)
        {
            Initialize(eventSource, 1);
        }

        public void Shutdown()
        {
            // do nothing
        }
        #endregion
    }
}
