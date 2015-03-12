// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface is used to forward events to another loggers
    /// </summary>
    public interface IEventRedirector
    {
        /// <summary>
        /// This method is called by the node loggers to forward the events to central logger
        /// </summary>
        void ForwardEvent(BuildEventArgs buildEvent);
    }
}
