// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    internal class MockNeverCacheBuildEngine4 : MockBuildEngine
    {
        public override object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            return null;
        }
    }
}
