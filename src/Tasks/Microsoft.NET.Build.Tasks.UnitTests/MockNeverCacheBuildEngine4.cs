// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            RegisteredTaskObjects.TryGetValue(key, out object obj);
            return obj;
        }
    }
}
