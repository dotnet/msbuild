// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// BuildCheck data indicating end of the build.
/// This is the last event that can be received from the BuildCheck infrastructure.
/// </summary>
public class BuildFinishedCheckData() : CheckData(string.Empty, null)
{ }
