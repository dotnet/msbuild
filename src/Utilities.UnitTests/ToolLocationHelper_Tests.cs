// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
#if FEATURE_CODETASKFACTORY
    private readonly ITestOutputHelper _output;
#endif

    public ToolLocationHelper_Tests(ITestOutputHelper output)
    {
#if FEATURE_CODETASKFACTORY
        _output = output;
#endif

        ToolLocationHelper.ClearStaticCaches();
    }
}
