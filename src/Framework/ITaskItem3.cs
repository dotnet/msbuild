// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Build.Framework;

/// <summary>
/// Extends <see cref="ITaskItem2"/> with the source location of the XML element that produced the item.
/// </summary>
[ComVisible(false)]
public interface ITaskItem3 : ITaskItem2
{
    /// <summary>
    /// Gets the source location of the XML element that produced this item, or <see langword="null"/> when unavailable.
    /// </summary>
    TaskItemLocation? Location { get; }
}
