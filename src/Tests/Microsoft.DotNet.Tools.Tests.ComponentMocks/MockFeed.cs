// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    public class MockFeed
    {
        public MockFeedType Type;
        public string Uri;
        public List<MockFeedPackage> Packages;
    }
}
