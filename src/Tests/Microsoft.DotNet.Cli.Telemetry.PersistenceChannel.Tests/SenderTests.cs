using System;
using System.IO;
using System.Net;
using System.Threading;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Telemetry.PersistenceChannel.Tests
{
    public class SenderTests : SdkTest
    {
        private int _deleteCount;

        private Mock<StorageTransmission> TransmissionMock { get; }

        private Mock<BaseStorageService> StorageBaseMock { get; }

        private SenderUnderTest Sender { get; }

        public SenderTests(ITestOutputHelper log) : base(log)
        {
            StorageBaseMock = new Mock<BaseStorageService>();
            TransmissionMock = new Mock<StorageTransmission>(string.Empty, new Uri("http://some/url"), new byte[] { },
                string.Empty, string.Empty);
            StorageService storageService = CreateStorageService();
            PersistenceTransmitter transmitter = new PersistenceTransmitter(storageService, 0);
            Sender = new SenderUnderTest(StorageBaseMock.Object, transmitter);
            _deleteCount = 0;
            StorageBaseMock.Setup(storage => storage.Delete(It.IsAny<StorageTransmission>()))
                .Callback(() => _deleteCount++);
        }

        [Fact]
        public void WhenServerReturn503TransmissionWillBeRetried()
        {
            int peekCounts = 0;

            // Setup transmission.SendAsync() to throw WebException that has 503 status Code
            TransmissionMock.Setup(transmission => transmission.SendAsync())
                .Throws(GenerateWebException((HttpStatusCode)503));

            // Setup Storage.Peek() to return the mocked transmission, and stop the loop after 10 peeks.
            StorageBaseMock.Setup(storage => storage.Peek())
                .Returns(TransmissionMock.Object)
                .Callback(() =>
                {
                    if (peekCounts++ == 10)
                    {
                        Sender.StopAsync();
                    }
                });

            // Act 
            Sender.SendLoop();
            _deleteCount.Should().Be(0,
                "delete is not expected to be called on 503, request is expected to be send forever.");
        }

        [Fact]
        public void WhenServerReturn400IntervalWillBe10Seconds()
        {
            int peekCounts = 0;

            // Setup transmission.SendAsync() to throw WebException that has 400 status Code
            TransmissionMock.Setup(transmission => transmission.SendAsync())
                .Throws(GenerateWebException((HttpStatusCode)400));

            // Setup Storage.Peek() to return the mocked transmission, and stop the loop after 10 peeks.
            StorageBaseMock.Setup(storage => storage.Peek())
                .Returns(TransmissionMock.Object)
                .Callback(() =>
                {
                    if (peekCounts++ == 10)
                    {
                        Sender.StopAsync();
                    }
                });

            // Cache the interval (it is a parameter passed to the Send method).
            TimeSpan intervalOnSixIteration = TimeSpan.Zero;
            Sender.OnSend = interval => intervalOnSixIteration = interval;

            // Act 
            Sender.SendLoop();

            intervalOnSixIteration.TotalSeconds.Should().Be(5);
            _deleteCount.Should().Be(10, "400 should not be retried so delete should always be called.");
        }

        [Fact]
        public void DisposeDoesNotThrow()
        {
            new Sender(StorageBaseMock.Object,
                    new PersistenceTransmitter(
                        CreateStorageService(),
                        3))
                .Dispose();
        }

        [Fact]
        public void WhenServerReturnDnsErrorRequestWillBeRetried()
        {
            int peekCounts = 0;

            // Setup transmission.SendAsync() to throw WebException with ProxyNameResolutionFailure failure
            WebException webException = new WebException(
                string.Empty,
                new Exception(),
                WebExceptionStatus.ProxyNameResolutionFailure,
                null);
            TransmissionMock.Setup(transmission => transmission.SendAsync()).Throws(webException);

            // Setup Storage.Peek() to return the mocked transmission, and stop the loop after 10 peeks.
            StorageBaseMock.Setup(storage => storage.Peek())
                .Returns(TransmissionMock.Object)
                .Callback(() =>
                {
                    if (peekCounts++ == 10)
                    {
                        Sender.StopAsync();
                    }
                });

            // Act 
            Sender.SendLoop();

            _deleteCount.Should().Be(0,
                "delete is not expected to be called on Dns errors since it , request is expected to be retried forever.");
        }

        private WebException GenerateWebException(HttpStatusCode httpStatusCode)
        {
            Mock<HttpWebResponse> httpWebResponse = new Mock<HttpWebResponse>();
            httpWebResponse.SetupGet(webResponse => webResponse.StatusCode).Returns(httpStatusCode);

            WebException webException = new WebException(string.Empty, new Exception(), WebExceptionStatus.SendFailure,
                httpWebResponse.Object);

            return webException;
        }

        /// <summary>
        ///     A class that inherits from Sender, to expose its protected methods.
        /// </summary>
        internal class SenderUnderTest : Sender
        {
            internal Action<TimeSpan> OnSend = nextSendInterval => { };

            internal SenderUnderTest(BaseStorageService storage, PersistenceTransmitter transmitter)
                : base(storage, transmitter, false)
            {
            }

            internal AutoResetEvent IntervalAutoResetEvent => DelayHandler;

            internal new void SendLoop()
            {
                base.SendLoop();
            }

            protected override bool Send(StorageTransmission transmission, ref TimeSpan nextSendInterval)
            {
                OnSend(nextSendInterval);
                DelayHandler.Set();
                return base.Send(transmission, ref nextSendInterval);
            }
        }

        private StorageService CreateStorageService()
        {
            string tempPath = Path.Combine(_testAssetsManager.CreateTestDirectory("TestStorageService").Path, Path.GetTempFileName());
            StorageService storageService = new StorageService();
            storageService.Init(tempPath);
            return storageService;
        }
    }
}
