using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Microsoft.Extensions.Testing.Abstractions
{
    public class StreamingTestDiscoverySink : StreamingTestSink, ITestDiscoverySink
    {
        public StreamingTestDiscoverySink(Stream stream) : base(stream)
        {
        }

        public void SendTestFound(Test test)
        {
            if (test == null)
            {
                throw new ArgumentNullException(nameof(test));
            }

            Stream.Send(new Message
            {
                MessageType = "TestDiscovery.TestFound",
                Payload = JToken.FromObject(test),
            });
        }
    }
}