// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;

namespace StreamForwarderTests
{
    public class StreamForwarderTests
    {
        public static void Main()
        {
            Console.WriteLine("Dummy Entrypoint");
        }

        [Fact]
        public void Unbuffered()
        {
            Forward(4, true, "");
            Forward(4, true, "123", "123");
            Forward(4, true, "1234", "1234");
            Forward(3, true, "123456789", "123", "456", "789");
            Forward(4, true, "\r\n", "\n");
            Forward(4, true, "\r\n34", "\n", "34");
            Forward(4, true, "1\r\n4", "1\n", "4");
            Forward(4, true, "12\r\n", "12\n");
            Forward(4, true, "123\r\n", "123\n");
            Forward(4, true, "1234\r\n", "1234", "\n");
            Forward(3, true, "\r\n3456\r\n9", "\n", "3456", "\n", "9");
            Forward(4, true, "\n", "\n");
            Forward(4, true, "\n234", "\n", "234");
            Forward(4, true, "1\n34", "1\n", "34");
            Forward(4, true, "12\n4", "12\n", "4");
            Forward(4, true, "123\n", "123\n");
            Forward(4, true, "1234\n", "1234", "\n");
            Forward(3, true, "\n23456\n89", "\n", "23456", "\n", "89");
        }

        [Fact]
        public void LineBuffered()
        {
            Forward(4, false, "");
            Forward(4, false, "123", "123\n");
            Forward(4, false, "1234", "1234\n");
            Forward(3, false, "123456789", "123456789\n");
            Forward(4, false, "\r\n", "\n");
            Forward(4, false, "\r\n34", "\n", "34\n");
            Forward(4, false, "1\r\n4", "1\n", "4\n");
            Forward(4, false, "12\r\n", "12\n");
            Forward(4, false, "123\r\n", "123\n");
            Forward(4, false, "1234\r\n", "1234\n");
            Forward(3, false, "\r\n3456\r\n9", "\n", "3456\n", "9\n");
            Forward(4, false, "\n", "\n");
            Forward(4, false, "\n234", "\n", "234\n");
            Forward(4, false, "1\n34", "1\n", "34\n");
            Forward(4, false, "12\n4", "12\n", "4\n");
            Forward(4, false, "123\n", "123\n");
            Forward(4, false, "1234\n", "1234\n");
            Forward(3, false, "\n23456\n89", "\n", "23456\n", "89\n");
        }

        private static void Forward(int bufferSize, bool unbuffered, string str, params string[] expectedWrites)
        {
            var expectedCaptured = str.Replace("\r", "").Replace("\n", Environment.NewLine);

            // No forwarding.
            Forward(bufferSize, ForwardOptions.None, str, null, new string[0]);

            // Capture only.
            Forward(bufferSize, ForwardOptions.Capture, str, expectedCaptured, new string[0]);

            var writeOptions = unbuffered ?
                ForwardOptions.Write | ForwardOptions.WriteLine :
                ForwardOptions.WriteLine;

            // Forward.
            Forward(bufferSize, writeOptions, str, null, expectedWrites);

            // Forward and capture.
            Forward(bufferSize, writeOptions | ForwardOptions.Capture, str, expectedCaptured, expectedWrites);
        }

        private enum ForwardOptions
        {
            None = 0x0,
            Capture = 0x1,
            Write = 0x02,
            WriteLine = 0x04,
        }

        private static void Forward(int bufferSize, ForwardOptions options, string str, string expectedCaptured, string[] expectedWrites)
        {
            var forwarder = new StreamForwarder(bufferSize);
            var writes = new List<string>();
            if ((options & ForwardOptions.WriteLine) != 0)
            {
                forwarder.ForwardTo(
                    write: (options & ForwardOptions.Write) == 0 ? (Action<string>)null : writes.Add,
                    writeLine: s => writes.Add(s + "\n"));
            }
            if ((options & ForwardOptions.Capture) != 0)
            {
                forwarder.Capture();
            }
            forwarder.Read(new StringReader(str));
            Assert.Equal(expectedWrites, writes);
            var captured = forwarder.GetCapturedOutput();
            Assert.Equal(expectedCaptured, captured);
        }
    }
}
