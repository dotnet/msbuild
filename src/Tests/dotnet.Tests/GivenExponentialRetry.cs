// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests
{
    public class GivenExponentialRetry : SdkTest
    {
        public GivenExponentialRetry(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItReturnsOnSuccess()
        {
            var retryCount = 0;
            Func<Task<string>> action = () => {
                retryCount++;
                return Task.FromResult("done");
            };
            var res = ExponentialRetry.ExecuteWithRetryOnFailure<string>(action).Result;

            retryCount.Should().Be(1);
        }

        [Fact(Skip = "Don't want to retry on exceptions")]
        public void ItRetriesOnError()
        {
            var retryCount = 0;
            Func<Task<string>> action = () => {
                retryCount++;
                throw new Exception();
            };
            Assert.Throws<AggregateException>(() => ExponentialRetry.ExecuteWithRetryOnFailure<string>(action, 2, timer: () => ExponentialRetry.Timer(ExponentialRetry.TestingIntervals)).Result);

            retryCount.Should().Be(2);
        }
    }
}
