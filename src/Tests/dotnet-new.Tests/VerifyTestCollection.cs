// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
