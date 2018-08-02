// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.UnitTests.TestResources;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    public sealed class VerifyFileHash_Tests
    {
        private readonly MockEngine _mockEngine;

        public VerifyFileHash_Tests(ITestOutputHelper output)
        {
            _mockEngine = new MockEngine(output);
        }

        [Fact]
        public void VerifyFileChecksum_FailsForUnknownHashEncoding()
        {
            new VerifyFileHash
            {
                File = Path.Combine(AppContext.BaseDirectory, "TestResources", "lorem.bin"),
                BuildEngine = _mockEngine,
                Algorithm = "SHA256",
                HashEncoding = "red",
            }
            .Execute()
            .ShouldBeFalse();

            _mockEngine.Log.ShouldContain("MSB3951");
        }

        [Fact]
        public void VerifyFileChecksum_FailsForUnknownAlgorithmName()
        {
            new VerifyFileHash
            {
                File = Path.Combine(AppContext.BaseDirectory, "TestResources", "lorem.bin"),
                BuildEngine = _mockEngine,
                Algorithm = "BANANA",
                Hash = "xyz",
            }
            .Execute()
            .ShouldBeFalse();

            _mockEngine.Log.ShouldContain("MSB3953");
        }

        [Fact]
        public void VerifyFileChecksum_FailsForFileNotFound()
        {
            new VerifyFileHash
            {
                File = Path.Combine(AppContext.BaseDirectory, "this_does_not_exist.txt"),
                BuildEngine = _mockEngine,
                Algorithm = "BANANA",
                Hash = "xyz",
            }
            .Execute()
            .ShouldBeFalse();

            _mockEngine.Log.ShouldContain("MSB3954");
        }

        [Theory]
        [InlineData("SHA256", "C442A45BB8D0938AFB2B5B0AA61C3ADA1B346F668A42879B1E653042433FAFCB")]
        [InlineData("SHA384", "F79223FF5E4A392AA01EC8BDF825C3B7F7941F9C5F7CF2A11BC61A8A5D0AF8182BAFC3FBFDACD83AE7A8A8EDF10B0255")]
        [InlineData("SHA512", "F923D2DA8F21B67FF4040FE9C5D00B0E891064E7B1DE47B54C9DA86DAAF215EFC64E282056027BEC2E75A83DE9FA6FFE6CA60F0141E19254B25CAE79C2694777")]
        public void VerifyFileChecksum_FailsForMismatch(string algoritm, string hash)
        {
            VerifyFileHash task = new VerifyFileHash
            {
                File = Path.Combine(AppContext.BaseDirectory, "TestResources", "lorem.bin"),
                BuildEngine = _mockEngine,
                Algorithm = algoritm,
                Hash = hash,
            };

            task.Execute().ShouldBeFalse(() => _mockEngine.Log);

            _mockEngine.Log.ShouldContain("MSB3952");
        }

        [Theory]
        [MemberData(nameof(TestBinary.GetLorem), MemberType = typeof(TestBinary))]
        public void VerifyFileChecksum_Pass(TestBinary testBinary)
        {
            VerifyFileHash task = new VerifyFileHash
            {
                File = testBinary.FilePath,
                BuildEngine = _mockEngine,
                Algorithm = testBinary.HashAlgorithm,
                Hash = testBinary.FileHash,
                HashEncoding = testBinary.HashEncoding,
            };

            task.Execute().ShouldBeTrue();
        }
    }
}
