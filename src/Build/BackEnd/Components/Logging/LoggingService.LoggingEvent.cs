// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Logging;

internal partial class LoggingService
{
    /// <summary>
    ///  Represents a build event to process, optionally targeted at a specific logging sink.
    /// </summary>
    /// <param name="buildEvent">The <see cref="BuildEventArgs"/> to process.</param>
    /// <param name="sinkId">The identifier of the logging sink that should receive the event.</param>
    protected internal readonly struct LoggingEvent(BuildEventArgs buildEvent, int sinkId)
    {
        /// <summary>
        ///  Represents the absence of a specific logging sink.
        /// </summary>
        private const int InvalidSinkId = -1;

        /// <summary>
        ///  Initializes a logging event that is not targeted at a specific logging sink.
        /// </summary>
        /// <param name="buildEvent">The <see cref="BuildEventArgs"/> to process.</param>
        public LoggingEvent(BuildEventArgs buildEvent)
            : this(buildEvent, InvalidSinkId)
        {
        }

        /// <summary>
        ///  Gets the build event to process.
        /// </summary>
        /// <value>
        ///  The <see cref="BuildEventArgs"/> to process.
        /// </value>
        public BuildEventArgs BuildEvent => buildEvent;

        /// <summary>
        ///  Gets the identifier of the logging sink that should receive the event.
        /// </summary>
        /// <value>
        ///  The logging sink identifier, or <see cref="InvalidSinkId"/> when the event is not targeted at a specific sink.
        /// </value>
        public int SinkId => sinkId;

        /// <summary>
        ///  Gets a value indicating whether the event is targeted at a specific logging sink.
        /// </summary>
        /// <value>
        ///  <see langword="true"/> when <see cref="SinkId"/> identifies a logging sink; otherwise, <see langword="false"/>.
        /// </value>
        public bool HasSinkId => sinkId != InvalidSinkId;

        /// <summary>
        ///  Converts a <see cref="BuildEventArgs"/> to a logging event that is not targeted at a specific logging sink.
        /// </summary>
        /// <param name="buildEvent">The <see cref="BuildEventArgs"/> to process.</param>
        /// <returns>
        ///  A logging event containing <paramref name="buildEvent"/>.
        /// </returns>
        public static implicit operator LoggingEvent(BuildEventArgs buildEvent)
            => new(buildEvent);
    }
}
