// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface defines a logger that will receive information about number of logical execution
    /// nodes that will be executing the build requests and producing the build events.
    /// </summary>
    /// <remarks>
    /// Implementing loggers (same as loggers implementing ILogger) will be registered as so called 'central logger',
    /// which means that they will be receiving all events in the serialized order (either via locking or via delivery via single thread).
    /// This means that the implementation doesn't need to be thread safe.
    /// </remarks>
    [ComVisible(true)]
    public interface INodeLogger : ILogger
    {
        /// <summary>
        /// Initializes the current <see cref="INodeLogger"/> instance.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="nodeCount"></param>
        void Initialize(IEventSource eventSource, int nodeCount);
    }
}
