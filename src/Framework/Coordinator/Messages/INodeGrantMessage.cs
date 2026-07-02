// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Common contract for coordinator node grant messages.
/// </summary>
internal interface INodeGrantMessage
{
    Guid GrantId { get; }

    int GrantedNodes { get; }
}
