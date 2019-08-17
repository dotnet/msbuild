// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This class is used by tasks to log their command lines. This class extends
    /// <see cref="BuildMessageEventArgs"/> so that command lines can be logged as
    /// messages. Logging a command line is only relevant for tasks that wrap an
    /// underlying executable/tool, or emulate a shell command. Tasks that have
    /// no command line equivalent should not raise this extended message event.
    /// </summary>
    /// <remarks>
    /// WARNING: marking a type [Serializable] without implementing ISerializable
    /// imposes a serialization contract -- it is a promise to never change the
    /// type's fields i.e. the type is immutable; adding new fields in the next
    /// version of the type without following certain special FX guidelines, can
    /// break both forward and backward compatibility
    /// </remarks>
    [Serializable]
    public class TaskCommandLineEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Default (family) constructor.
        /// </summary>
        protected TaskCommandLineEventArgs()
            : base()
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this class for the given task command line.
        /// </summary>
        /// <param name="commandLine">The command line used by a task to launch
        /// its underlying tool/executable.</param>
        /// <param name="taskName">The name of the task raising this event.</param>
        /// <param name="importance">Importance of command line -- controls whether
        /// the command line will be displayed by less verbose loggers.</param>
        public TaskCommandLineEventArgs
        (
            string commandLine,
            string taskName,
            MessageImportance importance
        )
            : this(commandLine, taskName, importance, DateTime.UtcNow)
        {
            // do nothing
        }


        /// <summary>
        /// Creates an instance of this class for the given task command line. This constructor allows the timestamp to be set
        /// </summary>
        /// <param name="commandLine">The command line used by a task to launch
        /// its underlying tool/executable.</param>
        /// <param name="taskName">The name of the task raising this event.</param>
        /// <param name="importance">Importance of command line -- controls whether
        /// the command line will be displayed by less verbose loggers.</param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        public TaskCommandLineEventArgs
        (
            string commandLine,
            string taskName,
            MessageImportance importance,
            DateTime eventTimestamp
        )
            : base(commandLine, null, taskName, importance, eventTimestamp)
        {
            // do nothing
        }

        /// <summary>
        /// Gets the task command line associated with this event.
        /// </summary>
        public string CommandLine
        {
            get
            {
                return Message;
            }
        }

        /// <summary>
        /// Gets the name of the task that raised this event.
        /// </summary>
        public string TaskName
        {
            get
            {
                return SenderName;
            }
        }
    }
}
