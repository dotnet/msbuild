// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends the ILogger interface to provide a property which can be used to forward events
    /// to a logger running in a different process. It can also be used to create filtering loggers.
    /// </summary>
    public interface IForwardingLogger : INodeLogger
    {
        /// <summary>
        /// This property is set by the build engine to allow a node loggers to forward messages to the
        /// central logger
        /// </summary>
        IEventRedirector BuildEventRedirector
        {
            get;

            set;
        }

        /// <summary>
        /// This property is set by the build engine or node to inform the forwarding logger which node it is running on
        /// </summary>
        int NodeId
        {
            get;

            set;
        }
    }
}
