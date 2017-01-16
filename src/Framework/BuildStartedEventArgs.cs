// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for build started events.
    /// </summary>
    /// <remarks>
    /// WARNING: marking a type [Serializable] without implementing
    /// ISerializable imposes a serialization contract -- it is a
    /// promise to never change the type's fields i.e. the type is
    /// immutable; adding new fields in the next version of the type
    /// without following certain special FX guidelines, can break both
    /// forward and backward compatibility
    /// </remarks>
    [Serializable]
    public class BuildStartedEventArgs : BuildStatusEventArgs
    {
        private IDictionary<string, string> environmentOnBuildStart;

        /// <summary>
        /// Default constructor
        /// </summary>
        protected BuildStartedEventArgs()
            : base()
        {
            // do nothing
        }

        /// <summary>
        /// Constructor to initialize all parameters.
        /// Sender field cannot be set here and is assumed to be "MSBuild"
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        public BuildStartedEventArgs
        (
            string message,
            string helpKeyword
        )
            : this(message, helpKeyword, DateTime.UtcNow)
        {
            // do nothing
        }

        /// <summary>
        /// Constructor to initialize all parameters.
        /// Sender field cannot be set here and is assumed to be "MSBuild"
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="environmentOfBuild">A dictionary which lists the environment of the build when the build is started.</param>
        public BuildStartedEventArgs
        (
            string message,
            string helpKeyword,
            IDictionary<string, string> environmentOfBuild
        )
            : this(message, helpKeyword, DateTime.UtcNow)
        {
            environmentOnBuildStart = environmentOfBuild;
        }

        /// <summary>
        /// Constructor to allow timestamp to be set
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        public BuildStartedEventArgs
        (
            string message,
            string helpKeyword,
            DateTime eventTimestamp
        )
            : this(message, helpKeyword, eventTimestamp, null)
        {
            // do nothing
        }

        /// <summary>
        /// Constructor to allow timestamp to be set
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        /// <param name="messageArgs">message args</param>
        public BuildStartedEventArgs
        (
            string message,
            string helpKeyword,
            DateTime eventTimestamp,
            params object[] messageArgs
        )
            : base(message, helpKeyword, "MSBuild", eventTimestamp, messageArgs)
        {
            // do nothing
        }

        /// <summary>
        /// The environment which is used at the start of the build
        /// </summary>
        public IDictionary<string, string> BuildEnvironment
        {
            get { return environmentOnBuildStart; }
        }
    }
}
