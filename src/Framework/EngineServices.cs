// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

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

#nullable enable
        /// <summary>
        /// Gets the import edges discovered during project evaluation, representing the graph of
        /// <c>&lt;Import&gt;</c> relationships between project files.
        /// </summary>
        /// <value>
        /// A read-only list of <see cref="ProjectImportEdge"/> values describing each import relationship,
        /// or <see langword="null"/> if the import graph is not available on this node.
        /// </value>
        /// <remarks>
        /// <para>
        /// The import graph is always available when the task runs on the in-process node.
        /// For out-of-process nodes, set the MSBuild property <c>MSBuildProvideImportGraph</c> to <c>true</c>
        /// in your project to opt in to serializing import graph data across nodes.
        /// </para>
        /// <para>
        /// Tasks can use pattern matching to access this property:
        /// <code>
        /// if (BuildEngine is IBuildEngine10 { EngineServices.ImportEdges: IReadOnlyList&lt;ProjectImportEdge&gt; edges })
        /// {
        ///     // use edges
        /// }
        /// </code>
        /// The pattern naturally handles older engines (where the property is absent) and
        /// out-of-proc nodes where the data was not serialized (where the property returns <see langword="null"/>).
        /// </para>
        /// </remarks>
        public virtual IReadOnlyList<ProjectImportEdge>? ImportEdges => null;
#nullable restore
    }
}
