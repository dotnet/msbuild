// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [CollectionDefinition("Verify Tests")]
    public class VerifyTestCollection : IClassFixture<VerifySettingsFixture>
    {
        //intentionally empty
        //defines test class collection to share the fixture
        //usage [Collection("Verify Tests")] on the test class.
    }
}
