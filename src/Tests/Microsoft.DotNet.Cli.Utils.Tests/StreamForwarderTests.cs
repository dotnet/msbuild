// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace StreamForwarderTests
{
    public class StreamForwarderTests : SdkTest
    {
        public StreamForwarderTests(ITestOutputHelper log) : base(log)
        {
        }

        public static IEnumerable<object[]> ForwardingTheoryVariations
        {
            get
            {
                return new[]
                {
                    new object[] { "123", new string[]{"123"} },
                    new object[] { "123\n", new string[] {"123"} },
                    new object[] { "123\r\n", new string[] {"123"} },
                    new object[] { "1234\n5678", new string[] {"1234", "5678"} },
                    new object[] { "1234\r\n5678", new string[] {"1234", "5678"} },
                    new object[] { "1234\n5678\n", new string[] {"1234", "5678"} },
                    new object[] { "1234\r\n5678\r\n", new string[] {"1234", "5678"} },
                    new object[] { "1234\n5678\nabcdefghijklmnopqrstuvwxyz", new string[] {"1234", "5678", "abcdefghijklmnopqrstuvwxyz"} },
                    new object[] { "1234\r\n5678\r\nabcdefghijklmnopqrstuvwxyz", new string[] {"1234", "5678", "abcdefghijklmnopqrstuvwxyz"} },
                    new object[] { "1234\n5678\nabcdefghijklmnopqrstuvwxyz\n", new string[] {"1234", "5678", "abcdefghijklmnopqrstuvwxyz"} },
                    new object[] { "1234\r\n5678\r\nabcdefghijklmnopqrstuvwxyz\r\n", new string[] {"1234", "5678", "abcdefghijklmnopqrstuvwxyz"} }
                };
            }
        }

        [Theory]
        [InlineData("123")]
        [InlineData("123\n")]
        public void TestNoForwardingNoCapture(string inputStr)
        {
            TestCapturingAndForwardingHelper(ForwardOptions.None, inputStr, null, new string[0]);
        }

        [Theory]
        [MemberData(nameof(ForwardingTheoryVariations))]
        public void TestForwardingOnly(string inputStr, string[] expectedWrites)
        {
            for (int i = 0; i < expectedWrites.Length; ++i)
            {
                expectedWrites[i] += Environment.NewLine;
            }

            TestCapturingAndForwardingHelper(ForwardOptions.WriteLine, inputStr, null, expectedWrites);
        }

        [Theory]
        [MemberData(nameof(ForwardingTheoryVariations))]
        public void TestCaptureOnly(string inputStr, string[] expectedWrites)
        {
            for (int i = 0; i < expectedWrites.Length; ++i)
            {
                expectedWrites[i] += Environment.NewLine;
            }

            var expectedCaptured = string.Join("", expectedWrites);

            TestCapturingAndForwardingHelper(ForwardOptions.Capture, inputStr, expectedCaptured, new string[0]);
        }

        [Theory]
        [MemberData(nameof(ForwardingTheoryVariations))]
        public void TestCaptureAndForwardingTogether(string inputStr, string[] expectedWrites)
        {
            for (int i = 0; i < expectedWrites.Length; ++i)
            {
                expectedWrites[i] += Environment.NewLine;
            }

            var expectedCaptured = string.Join("", expectedWrites);

            TestCapturingAndForwardingHelper(ForwardOptions.WriteLine | ForwardOptions.Capture, inputStr, expectedCaptured, expectedWrites);
        }

        private enum ForwardOptions
        {
            None = 0x0,
            Capture = 0x1,
            WriteLine = 0x02,
        }

        private void TestCapturingAndForwardingHelper(ForwardOptions options, string str, string expectedCaptured, string[] expectedWrites)
        {
            var forwarder = new StreamForwarder();
            var writes = new List<string>();

            if ((options & ForwardOptions.WriteLine) != 0)
            {
                forwarder.ForwardTo(writeLine: s => writes.Add(s + Environment.NewLine));
            }
            if ((options & ForwardOptions.Capture) != 0)
            {
                forwarder.Capture();
            }

            forwarder.Read(new StringReader(str));
            Assert.Equal(expectedWrites, writes);

            var captured = forwarder.CapturedOutput;
            Assert.Equal(expectedCaptured, captured);
        }
    }
}
