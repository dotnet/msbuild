// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Logging;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This class is a container class used to pass around information about distributed logger
    /// </summary>
    internal class DistributedLoggerRecord
    {
        #region Constructors
        /// <summary>
        /// Initialize the container class with the given centralLogger and forwardingLoggerDescription
        /// </summary>
        internal DistributedLoggerRecord(ILogger centralLogger, LoggerDescription forwardingLoggerDescription)
        {
            _centralLogger = centralLogger;
            _forwardingLoggerDescription = forwardingLoggerDescription;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Fully initialized central logger
        /// </summary>
        internal ILogger CentralLogger
        {
            get
            {
                return _centralLogger;
            }
        }

        /// <summary>
        /// Description of the forwarding class
        /// </summary>
        internal LoggerDescription ForwardingLoggerDescription
        {
            get
            {
                return _forwardingLoggerDescription;
            }
        }
        #endregion

        #region Data
        // Central logger
        private ILogger _centralLogger;
        // Description of the forwarding logger
        private LoggerDescription _forwardingLoggerDescription;
        #endregion
    }
}
