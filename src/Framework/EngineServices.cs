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
        /// Version 3 with TryGetItemSourceLocation().
        /// </summary>
        public const int Version3 = 3;

        /// <summary>
        /// Gets an explicit version of this class.
        /// </summary>
        /// <remarks>
        /// Must be incremented whenever new members are added. Derived classes should override
        /// the property to return the version actually being implemented.
        /// </remarks>
        public virtual int Version => Version2;

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
        /// Attempts to resolve the source location (file, line, column) of the XML item element that
        /// declared the given item in the project currently being built.
        /// </summary>
        /// <param name="itemType">The item type (e.g. <c>PackageReference</c>).</param>
        /// <param name="itemSpec">The evaluated item include to match (e.g. a package id).</param>
        /// <param name="file">When this method returns <see langword="true"/>, the file containing the item element's <c>Include</c> attribute.</param>
        /// <param name="lineNumber">When this method returns <see langword="true"/>, the 1-based line of the item element's <c>Include</c> attribute.</param>
        /// <param name="columnNumber">When this method returns <see langword="true"/>, the 1-based column of the item element's <c>Include</c> attribute.</param>
        /// <returns>
        /// <see langword="true"/> if a single declaring XML item element was found; otherwise <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// This is a best-effort, on-demand lookup intended for diagnostics — for example mapping a NuGet
        /// restore warning back to the <c>&lt;PackageReference&gt;</c> that triggered it — so that the build
        /// process does not have to carry XML location on every item instance. It returns <see langword="false"/>
        /// when the item cannot be traced to a single literal XML item element: for instance when the element's
        /// <c>Include</c> uses properties, item references, or wildcards; when the element (or an ancestor) is
        /// conditioned; when the item was produced inside a target; or when more than one literal declaration matches.
        /// Resolution is only attempted on the in-proc entry node; tasks running on out-of-proc worker nodes always
        /// receive <see langword="false"/> because the project XML and its import closure are not available there.
        /// Only call this when <see cref="Version"/> is at least <see cref="Version3"/>.
        /// </remarks>
        public virtual bool TryGetItemSourceLocation(string itemType, string itemSpec, out string file, out int lineNumber, out int columnNumber) => throw new NotImplementedException();
    }
}
