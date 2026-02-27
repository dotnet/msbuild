// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// A strongly-typed task item interface that wraps a value of type <typeparamref name="T"/>
    /// and provides access to it along with standard task item functionality.
    /// </summary>
    /// <typeparam name="T">The type of the value. Can be a value type, FileInfo, or DirectoryInfo.</typeparam>
    /// <remarks>
    /// This interface allows tasks to receive strongly-typed parameters while still working with MSBuild's item system.
    /// The value is parsed from the item's identity (ItemSpec) using MSBuild's standard parsing conventions.
    /// Supported types include primitive value types (int, bool, etc.), AbsolutePath, FileInfo, and DirectoryInfo.
    /// </remarks>
    public interface ITaskItem<T> : ITaskItem2
    {
        /// <summary>
        /// Gets the strongly-typed value parsed from the item's identity.
        /// </summary>
        T Value { get; }
    }
}
