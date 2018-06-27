using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.Cli.Build.UploadToLinuxPackageRepository;
using Xunit;

namespace dotnet_cli_build.Tests
{
    public class GivenActionAndRetryTimes
    {
        public static IEnumerable<Task> NoWaitTimer()
        {
            while (true)
            {
                yield return Task.CompletedTask;
            }
        }

        [Fact]
        public void ExponentialRetryShouldProvideIntervalSequence()
        {
            ExponentialRetry.Intervals.First().Should().Be(TimeSpan.FromSeconds(5));
            ExponentialRetry.Intervals.Skip(1).First().Should().Be(TimeSpan.FromSeconds(10));
            ExponentialRetry.Intervals.Skip(2).First().Should().Be(TimeSpan.FromSeconds(20));
            ExponentialRetry.Intervals.Skip(3).First().Should().Be(TimeSpan.FromSeconds(40));
            ExponentialRetry.Intervals.Skip(4).First().Should().Be(TimeSpan.FromSeconds(80));
        }

        [Fact]
        public void ExponentialShouldNotRetryAfterFirstSucceess()
        {
            var fakeAction = new FakeAction(0);
            ExponentialRetry.ExecuteWithRetry(
                fakeAction.Run,
                s => s == "success",
                10,
                NoWaitTimer).Wait();
            fakeAction.Count.Should().Be(0);
        }

        [Fact]
        public void ExponentialShouldRetryUntilSuccess()
        {
            var fakeAction = new FakeAction(5);
            ExponentialRetry.ExecuteWithRetry(
                fakeAction.Run,
                s => s == "success",
                10,
                NoWaitTimer).Wait();
            fakeAction.Count.Should().Be(5);
        }

        [Fact]
        public void ExponentialShouldThrowAfterMaximumAmountReached()
        {
            var fakeAction = new FakeAction(10);
            Action a = () => ExponentialRetry.ExecuteWithRetry(
                fakeAction.Run,
                s => s == "success",
                5,
                NoWaitTimer,
                "testing retry").Wait();
            a.ShouldThrow<RetryFailedException>()
                .WithMessage("Retry failed for testing retry after 5 times with result: fail");
        }
    }

    public class FakeAction
    {
        private readonly int _successAfter;

        public FakeAction(int successAfter)
        {
            _successAfter = successAfter;
        }

        public int Count { get; private set; }

        public Task<string> Run()
        {
            if (_successAfter == Count)
            {
                return Task.FromResult("success");
            }

            Count++;
            return Task.FromResult("fail");
        }
    }
}