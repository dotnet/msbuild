// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
    public ToolLocationHelper_Tests()
    {
        ToolLocationHelper.ClearStaticCaches();
    }
}
