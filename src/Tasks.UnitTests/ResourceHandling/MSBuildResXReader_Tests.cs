// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Tasks.ResourceHandling;
using Microsoft.Build.Tasks.UnitTests.ResourceHandling;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests.GenerateResource
{
    public class MSBuildResXReader_Tests
    {
        private readonly ITestOutputHelper _output;

        public MSBuildResXReader_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void StringResourcesAreReadAsStrings()
        {
            var resxWithSingleString = new MSBuildResXReader(
                ResXHelper.SurroundWithBoilerplate(
                    @"<data name=""StringResource"" xml:space=""preserve"">
    <value>StringValue</value>
    <comment>Comment</comment>
  </data>"));

            resxWithSingleString.Resources.ShouldHaveSingleItem();
            resxWithSingleString.Resources[0].ShouldBeOfType<StringResource>();

            var loadedResource = (StringResource)resxWithSingleString.Resources[0];

            loadedResource.Name.ShouldBe("StringResource");
            loadedResource.Value.ShouldBe("StringValue");
        }
    }
}
