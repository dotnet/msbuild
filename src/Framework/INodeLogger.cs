// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface defines a "parallel aware logger" in the build system. A parallel aware logger
    /// will accept a cpu count and be aware that any cpu count greater than 1 means the events will
    /// be received from the logger from each cpu as the events are logged.
    /// </summary>
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
