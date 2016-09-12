// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

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
