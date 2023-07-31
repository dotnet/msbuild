// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using IChannelTelemetry = Microsoft.ApplicationInsights.Channel.ITelemetry;

namespace Microsoft.DotNet.Cli.Telemetry.PersistenceChannel.Tests
{
    /// <summary>
    ///     Tests for Storage.
    /// </summary>
    /// <remarks>
    ///     To reduce complexity, there was a design decision to make Storage the file system abstraction layer.
    ///     That means that Storage knows about the file system types (e.g. IStorageFile or FileInfo).
    ///     Those types are not easy to mock (even IStorageFile is using extension methods that makes it very hard to mock).
    ///     Therefore those UnitTests just doesn't mock the file system. Every unit test in <see cref="StorageTests" />
    ///     reads and writes files to/from the disk.
    /// </remarks>
    public class StorageTests : SdkTest
    {
        public StorageTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void EnqueuedContentIsEqualToPeekedContent()
        {
            // Setup
            StorageService storage = new StorageService();
            storage.Init(GetTemporaryPath());
            Transmission transmissionToEnqueue = CreateTransmission(new TraceTelemetry("mock_item"));

            // Act
            storage.EnqueueAsync(transmissionToEnqueue).ConfigureAwait(false).GetAwaiter().GetResult();
            StorageTransmission peekedTransmission = storage.Peek();

            // Asserts
            string enqueuedContent =
                Encoding.UTF8.GetString(transmissionToEnqueue.Content, 0, transmissionToEnqueue.Content.Length);
            string peekedContent =
                Encoding.UTF8.GetString(peekedTransmission.Content, 0, peekedTransmission.Content.Length);
            enqueuedContent.Should().Be(peekedContent);
        }

        [Fact]
        public void DeletedItemIsNotReturnedInCallsToPeek()
        {
            // Setup - create a storage with one item
            StorageService storage = new StorageService();
            storage.Init(GetTemporaryPath());
            Transmission transmissionToEnqueue = CreateTransmissionAndEnqueueIt(storage);

            // Act
            StorageTransmission firstPeekedTransmission;

            // if item is not disposed,peek will not return it (regardless of the call to delete). 
            // So for this test to actually test something, using 'using' is required.  
            using (firstPeekedTransmission = storage.Peek())
            {
                storage.Delete(firstPeekedTransmission);
            }

            StorageTransmission secondPeekedTransmission = storage.Peek();

            // Asserts            
            firstPeekedTransmission.Should().NotBeNull();
            secondPeekedTransmission.Should().BeNull();
        }

        [Fact]
        public void PeekedItemIsOnlyReturnedOnce()
        {
            // Setup - create a storage with one item
            StorageService storage = new StorageService();
            storage.Init(GetTemporaryPath());

            Transmission transmissionToEnqueue = CreateTransmissionAndEnqueueIt(storage);

            // Act
            StorageTransmission firstPeekedTransmission = storage.Peek();
            StorageTransmission secondPeekedTransmission = storage.Peek();

            // Asserts            
            firstPeekedTransmission.Should().NotBeNull();
            secondPeekedTransmission.Should().BeNull();
        }

        [Fact]
        public void PeekedItemIsReturnedAgainAfterTheItemInTheFirstCallToPeekIsDisposed()
        {
            // Setup - create a storage with one item
            StorageService storage = new StorageService();
            storage.Init(GetTemporaryPath());

            Transmission transmissionToEnqueue = CreateTransmission(new TraceTelemetry("mock_item"));
            storage.EnqueueAsync(transmissionToEnqueue).ConfigureAwait(false).GetAwaiter().GetResult();

            // Act
            StorageTransmission firstPeekedTransmission;
            using (firstPeekedTransmission = storage.Peek())
            {
            }

            StorageTransmission secondPeekedTransmission = storage.Peek();

            // Asserts            
            firstPeekedTransmission.Should().NotBeNull();
            secondPeekedTransmission.Should().NotBeNull();
        }

        [Fact]
        public void WhenStorageHasTwoItemsThenTwoCallsToPeekReturns2DifferentItems()
        {
            // Setup - create a storage with 2 items
            StorageService storage = new StorageService();
            storage.Init(GetTemporaryPath());

            Transmission firstTransmission = CreateTransmissionAndEnqueueIt(storage);
            Transmission secondTransmission = CreateTransmissionAndEnqueueIt(storage);

            // Act
            StorageTransmission firstPeekedTransmission = storage.Peek();
            StorageTransmission secondPeekedTransmission = storage.Peek();

            // Asserts            
            firstPeekedTransmission.Should().NotBeNull();
            secondPeekedTransmission.Should().NotBeNull();

            string first = Encoding.UTF8.GetString(firstPeekedTransmission.Content, 0,
                firstPeekedTransmission.Content.Length);
            string second = Encoding.UTF8.GetString(secondPeekedTransmission.Content, 0,
                secondPeekedTransmission.Content.Length);
            first.Should().NotBe(second);
        }

        [Fact]
        public void WhenMaxFilesIsOneThenSecondTransmissionIsDropped()
        {
            // Setup
            StorageService storage = new StorageService();
            storage.Init(GetTemporaryPath());

            storage.MaxFiles = 1;

            // Act - Enqueue twice
            CreateTransmissionAndEnqueueIt(storage);
            CreateTransmissionAndEnqueueIt(storage);

            // Asserts - Second Peek should be null 
            storage.Peek().Should().NotBeNull();
            storage.Peek().Should().BeNull();
        }

        [Fact]
        public void WhenMaxSizeIsReachedThenEnqueuedTransmissionsAreDropped()
        {
            // Setup - create a storage with 2 items
            StorageService storage = new StorageService();
            storage.Init(GetTemporaryPath());

            storage.CapacityInBytes = 200; // Each file enqueued in CreateTransmissionAndEnqueueIt is ~300 bytes.

            // Act - Enqueue twice
            CreateTransmissionAndEnqueueIt(storage);
            CreateTransmissionAndEnqueueIt(storage);

            // Asserts - Second Peek should be null 
            storage.Peek().Should().NotBeNull();
            storage.Peek().Should().BeNull();
        }

        private static Transmission CreateTransmission(IChannelTelemetry telemetry)
        {
            byte[] data = JsonSerializer.Serialize(new[] {telemetry});
            Transmission transmission = new Transmission(
                new Uri(@"http://some.url"),
                data,
                "application/x-json-stream",
                JsonSerializer.CompressionType);

            return transmission;
        }

        private static Transmission CreateTransmissionAndEnqueueIt(StorageService storage)
        {
            Transmission firstTransmission = CreateTransmission(new TraceTelemetry(Guid.NewGuid().ToString()));
            storage.EnqueueAsync(firstTransmission).ConfigureAwait(false).GetAwaiter().GetResult();

            return firstTransmission;
        }

        private string GetTemporaryPath([CallerMemberName] string callingMethod = null)
        {
            return _testAssetsManager.CreateTestDirectory(callingMethod).Path;
        }
    }
}
