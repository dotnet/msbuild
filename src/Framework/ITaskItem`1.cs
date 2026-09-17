// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework;

/// <summary>
/// A strongly-typed task item interface that wraps a value of type <typeparamref name="T"/>
/// and provides access to it along with standard task item functionality.
/// </summary>
/// <typeparam name="T">The type of the value. Supported types are <see cref="AbsolutePath"/>, <see cref="System.IO.FileInfo"/>, and <see cref="System.IO.DirectoryInfo"/>.</typeparam>
/// <remarks>
/// This interface allows tasks to receive strongly-typed item parameters while still working with MSBuild's item system.
/// The value is parsed from the item's identity (ItemSpec) using MSBuild's standard parsing conventions.
/// </remarks>
public interface ITaskItem<T> : ITaskItem2
{
    /// <summary>
    /// Gets the strongly-typed value parsed from the item's identity.
    /// </summary>
    T Value { get; }
}
