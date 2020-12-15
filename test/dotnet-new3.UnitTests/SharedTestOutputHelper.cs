using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace dotnet_new3.UnitTests
{
    /// <summary>
    /// This is so we can pass ITestOutputHelper to TestCommand constructor
    /// when calling from SharedHomeDirectory
    /// </summary>
    class SharedTestOutputHelper : ITestOutputHelper
    {
        private readonly IMessageSink sink;

        public SharedTestOutputHelper(IMessageSink sink)
        {
            this.sink = sink;
        }

        public void WriteLine(string message)
        {
            sink.OnMessage(new DiagnosticMessage(message));
        }

        public void WriteLine(string format, params object[] args)
        {
            sink.OnMessage(new DiagnosticMessage(format, args));
        }
    }
}
