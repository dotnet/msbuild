// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    public class MockFeed
    {
        public MockFeedType Type;
        public string Uri;
        public List<MockFeedPackage> Packages;
    }
}
