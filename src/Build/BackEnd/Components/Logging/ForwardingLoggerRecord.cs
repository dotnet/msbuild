// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// This class descibes a central/forwarding logger pair used in multiproc logging.
    /// </summary>
    public class ForwardingLoggerRecord
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="centralLogger">The central logger</param>
        /// <param name="forwardingLoggerDescription">The description for the forwarding logger.</param>
        public ForwardingLoggerRecord(ILogger centralLogger, LoggerDescription forwardingLoggerDescription)
        {
            // The logging service allows a null central logger, so we don't check for it here.
            ErrorUtilities.VerifyThrowArgumentNull(forwardingLoggerDescription, nameof(forwardingLoggerDescription));

            this.CentralLogger = centralLogger;
            this.ForwardingLoggerDescription = forwardingLoggerDescription;
        }

        /// <summary>
        /// Retrieves the central logger.
        /// </summary>
        public ILogger CentralLogger
        {
            get;
            private set;
        }

        /// <summary>
        /// Retrieves the forwarding logger description.
        /// </summary>
        public LoggerDescription ForwardingLoggerDescription
        {
            get;
            private set;
        }
    }
}
