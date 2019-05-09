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
        public void ParsesSingleStringAsString()
        {
            var resxWithSingleString = new MSBuildResXReader(
                ResXHelper.SurroundWithBoilerplate(
                    @"<data name=""StringResource"" xml:space=""preserve"">
    <value>StringValue</value>
    <comment>Comment</comment>
  </data>"));

            resxWithSingleString.Resources.ShouldBe(new[] { new StringResource("StringResource", "StringValue", null) });
        }

        [Fact]
        public void LoadsMultipleStringsPreservingOrder()
        {
            var resxWithTwoStrings = new MSBuildResXReader(
    ResXHelper.SurroundWithBoilerplate(
        @"<data name=""StringResource"" xml:space=""preserve"">
    <value>StringValue</value>
    <comment>Comment</comment>
  </data>
  <data name=""2StringResource2"" xml:space=""preserve"">
    <value>2StringValue2</value>
  </data>"));

            resxWithTwoStrings.Resources.ShouldBe(
                new[] {
                    new StringResource("StringResource", "StringValue", null),
                    new StringResource("2StringResource2", "2StringValue2", null),
                });
        }

        [Fact]
        public void LoadsStringFromFileRefAsString()
        {
            File.Exists(@"ResourceHandling\TextFile1.txt").ShouldBeTrue("Test deployment is missing None files");

            var resxWithLinkedString = new MSBuildResXReader(
                ResXHelper.SurroundWithBoilerplate(
@"  <assembly alias=""System.Windows.Forms"" name=""System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" />
  <data name=""TextFile1"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
    <value>ResourceHandling\TextFile1.txt;System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089;utf-8</value>
  </data>"));

            resxWithLinkedString.Resources.ShouldBe(new[] { new StringResource("TextFile1", "Contents of TextFile1", null) });
        }

        [Fact]
        public void LoadsStringFromFileRefAsStringWithShiftJISEncoding()
        {
            File.Exists(@"ResourceHandling\TextFileInShiftJIS.txt").ShouldBeTrue("Test deployment is missing None files");

            var resxWithLinkedString = new MSBuildResXReader(
                ResXHelper.SurroundWithBoilerplate(
@"  <assembly alias=""System.Windows.Forms"" name=""System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" />
  <data name=""TextFile1"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
    <value>ResourceHandling\TextFileInShiftJIS.txt;System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089;shift_jis</value>
  </data>"));

            resxWithLinkedString.Resources.ShouldBe(new[] { new StringResource("TextFile1", "ハローワールド！", null) });
        }

        // TODO: invalid resx xml

        // TODO: valid xml, but invalid resx-specific data
    }
}
