using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class DotnetTestMessageScenario
    {
        private TestMessagesCollection _messages;
        private const string AssemblyUnderTest = "assembly.dll";
        private const string TestRunner = "testRunner";
        private const int Port = 1;

        public DotnetTest DotnetTestUnderTest { get; private set; }
        public Mock<ITestRunner> TestRunnerMock { get; private set; }
        public Mock<IReportingChannel> AdapterChannelMock { get; private set; }
        public Mock<IReportingChannel> TestRunnerChannelMock { get; private set; }

        public DotnetTestMessageScenario()
        {
            _messages = new TestMessagesCollection();
            DotnetTestUnderTest = new DotnetTest(_messages, AssemblyUnderTest);
            TestRunnerChannelMock = new Mock<IReportingChannel>();
            TestRunnerMock = new Mock<ITestRunner>();
            AdapterChannelMock = new Mock<IReportingChannel>();
        }

        public void Run()
        {
            var reportingChannelFactoryMock = new Mock<IReportingChannelFactory>();
            reportingChannelFactoryMock
                .Setup(r => r.CreateTestRunnerChannel())
                .Returns(TestRunnerChannelMock.Object)
                .Raises(
                    r => r.TestRunnerChannelCreated += null,
                    reportingChannelFactoryMock.Object, TestRunnerChannelMock.Object);

            var commandFactoryMock = new Mock<ICommandFactory>();

            var testRunnerFactoryMock = new Mock<ITestRunnerFactory>();
            testRunnerFactoryMock
                .Setup(t => t.CreateTestRunner(It.IsAny<DiscoverTestsArgumentsBuilder>()))
                .Returns(TestRunnerMock.Object);

            testRunnerFactoryMock
                .Setup(t => t.CreateTestRunner(It.IsAny<RunTestsArgumentsBuilder>()))
                .Returns(TestRunnerMock.Object);

            var reportingChannelFactory = reportingChannelFactoryMock.Object;
            var adapterChannel = AdapterChannelMock.Object;
            var commandFactory = commandFactoryMock.Object;
            var testRunnerFactory = testRunnerFactoryMock.Object;

            using (DotnetTestUnderTest)
            {
                DotnetTestUnderTest
                    .AddNonSpecificMessageHandlers(_messages, adapterChannel)
                    .AddTestDiscoveryMessageHandlers(adapterChannel, reportingChannelFactory, testRunnerFactory)
                    .AddTestRunMessageHandlers(adapterChannel, reportingChannelFactory, testRunnerFactory)
                    .AddTestRunnnersMessageHandlers(adapterChannel, reportingChannelFactory);

                DotnetTestUnderTest.StartListeningTo(adapterChannel);

                AdapterChannelMock.Raise(r => r.MessageReceived += null, DotnetTestUnderTest, new Message
                {
                    MessageType = TestMessageTypes.VersionCheck,
                    Payload = JToken.FromObject(new ProtocolVersionMessage { Version = 1 })
                });

                DotnetTestUnderTest.StartHandlingMessages();
            }

            AdapterChannelMock.Verify();
            TestRunnerMock.Verify();
        }
    }
}