// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using StringNodeState = Microsoft.Build.Evaluation.NodeStateStore.StringNodeState;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Supplies exclusively borrowed string-node state buffers from the shared array pool.
/// </summary>
internal static class StringNodeStateBuffer
{
    internal static StringNodeState[] Acquire(int minimumLength) =>
        ArrayPool<StringNodeState>.Shared.Rent(minimumLength);

    internal static void Release(StringNodeState[] buffer) =>
        ArrayPool<StringNodeState>.Shared.Return(buffer, clearArray: true);
}
