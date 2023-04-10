// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;
using Xunit;

namespace Microsoft.NET.Build.Containers.UnitTests
{
    public class DiagnosticMessageTests
    {
        [Fact]
        public void Error_ShouldCreateValidMessage()
        {
            string result = DiagnosticMessage.Error("code", "text");

            Assert.Equal("Containerize : error code : text", result);
        }

        [Fact]
        public void Warning_ShouldCreateValidMessage()
        {
            string result = DiagnosticMessage.Error("code", "text");

            Assert.Equal("Containerize : error code : text", result);
        }

        [Fact]
        public void Error_ShouldCreateValidMessageFromResource()
        {
            string result = DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings._Test), "param");

            Assert.Equal("Containerize : error CONTAINER0000: Value for unit test param", result);
        }

        [Fact]
        public void Warning_ShouldCreateValidMessageFromResource()
        {
            string result = DiagnosticMessage.WarningFromResourceWithCode(nameof(Strings._Test), "param");

            Assert.Equal("Containerize : warning CONTAINER0000: Value for unit test param", result);
        }
    }
}
