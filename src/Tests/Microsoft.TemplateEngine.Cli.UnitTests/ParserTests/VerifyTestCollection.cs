// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [CollectionDefinition("Verify Tests")]
    public class VerifyTestCollection : IClassFixture<VerifySettingsFixture>
    {
        //intentionally empty
        //defines test class collection to share the fixture
        //usage [Collection("Verify Tests")] on the test class.
    }
}
