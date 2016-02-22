// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.Extensions.Testing.Abstractions
{
    public abstract class StreamingTestSink : ITestSink
    {
        protected LineDelimitedJsonStream Stream { get; }

        protected StreamingTestSink(Stream stream)
        {
            Stream = new LineDelimitedJsonStream(stream);
        }

        public void SendTestCompleted()
        {
            Stream.Send(new Message
            {
                MessageType = "TestRunner.TestCompleted"
            });
        }
    }
}
