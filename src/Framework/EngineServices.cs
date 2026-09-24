// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Exposes build engine functionality that was made available in newer versions of MSBuild.
    /// </summary>
    /// <remarks>
    /// Make all members virtual but not abstract, ensuring that implementations can override them and external implementations
    /// won't break when the class is extended with new members. This base implementation should be throwing <see cref="NotImplementedException"/>.
    /// </remarks>
    [Serializable]
    public abstract class EngineServices
    {
        /// <summary>
        /// Initial version with LogsMessagesOfImportance() and IsTaskInputLoggingEnabled as the only exposed members.
        /// </summary>
        public const int Version1 = 1;

        /// <summary>
        /// Version 2 with IsOutOfProcRarNodeEnabled().
        /// </summary>
        public const int Version2 = 2;

        /// <summary>
        /// Version 3 with task progress reporting.
        /// </summary>
        public const int Version3 = 3;

        /// <summary>
        /// Gets an explicit version of this class.
        /// </summary>
        /// <remarks>
        /// Must be incremented whenever new members are added. Derived classes should override
        /// the property to return the version actually being implemented.
        /// </remarks>
        public virtual int Version => Version3;

        /// <summary>
        /// Returns <see langword="true"/> if the given message importance is not guaranteed to be ignored by registered loggers.
        /// </summary>
        /// <param name="importance">The importance to check.</param>
        /// <returns>True if messages of the given importance should be logged, false if it's guaranteed that such messages would be ignored.</returns>
        /// <remarks>
        /// Example: If we know that no logger is interested in <see cref="MessageImportance.Low"/>, this method returns <see langword="true"/>
        /// for <see cref="MessageImportance.Normal"/> and <see cref="MessageImportance.High"/>, and returns <see langword="false"/>
        /// for <see cref="MessageImportance.Low"/>.
        /// </remarks>
        public virtual bool LogsMessagesOfImportance(MessageImportance importance) => throw new NotImplementedException();

        /// <summary>
        /// Returns <see langword="true"/> if the build is configured to log all task inputs.
        /// </summary>
        /// <remarks>
        /// This is a performance optimization allowing tasks to skip expensive double-logging.
        /// </remarks>
        public virtual bool IsTaskInputLoggingEnabled => throw new NotImplementedException();

        public virtual bool IsOutOfProcRarNodeEnabled => throw new NotImplementedException();

        /// <summary>
        /// Creates a reporter for a task operation.
        /// </summary>
        /// <param name="title">A short, stable description of the operation.</param>
        /// <param name="unit">The unit used by progress updates.</param>
        /// <returns>A progress reporter. Hosts that do not support progress return a no-op reporter.</returns>
        public virtual ITaskProgressReporter CreateTaskProgressReporter(
            string title,
            TaskProgressUnit unit = TaskProgressUnit.Unspecified)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            return NullTaskProgressReporter.Instance;
        }

        private sealed class NullTaskProgressReporter : ITaskProgressReporter
        {
            internal static readonly NullTaskProgressReporter Instance = new NullTaskProgressReporter();

            private NullTaskProgressReporter()
            {
            }

            public void Report(TaskProgressUpdate value)
            {
            }

            public void Complete(string? summary = null)
            {
            }

            public void Cancel(string? summary = null)
            {
            }

            public void Fail(string? summary = null)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
