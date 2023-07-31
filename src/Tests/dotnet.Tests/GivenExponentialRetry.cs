// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

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
