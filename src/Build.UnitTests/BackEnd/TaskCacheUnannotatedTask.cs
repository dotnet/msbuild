// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class UnannotatedValueTask : ITask
{
    public IBuildEngine BuildEngine { get; set; } = null!;
    public ITaskHost HostObject { get; set; } = null!;
    public float Value { get; set; }

    [Output]
    public float Result => Value;

    public bool Execute() => true;
}
