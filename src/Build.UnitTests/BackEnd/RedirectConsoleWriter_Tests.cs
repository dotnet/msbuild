// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class RedirectConsoleWriter_Tests
    {
        [Fact]
        public async Task EmitConsoleMessages()
        {
            StringBuilder sb = new StringBuilder();

            using (RedirectConsoleWriter writer = new(text => sb.Append(text)))
            {
                writer.WriteLine("Line 1");
                await Task.Delay(80); // should be somehow bigger than `RedirectConsoleWriter` flush period - see its constructor
                writer.Write("Line 2");
            }

            sb.ToString().ShouldBe($"Line 1{Environment.NewLine}Line 2");
        }

        [Fact]
        public void WriteAfterDispose_IsDiscardedWithoutThrowingOrInvokingCallback()
        {
            StringBuilder output = new();
            int callbackCount = 0;
            RedirectConsoleWriter writer = new(text =>
            {
                callbackCount++;
                output.Append(text);
            });

            writer.Write("before dispose");
            writer.Dispose();
            output.ToString().ShouldBe("before dispose");
            int callbackCountAfterDispose = callbackCount;

            Should.NotThrow(() => writer.WriteLine("after dispose"));
            writer.Flush();

            output.ToString().ShouldBe("before dispose");
            callbackCount.ShouldBe(callbackCountAfterDispose);
        }

        [Fact]
        public void FormattedWriteDoesNotAppendNewLine()
        {
            StringBuilder output = new();

            using (RedirectConsoleWriter writer = new(text => output.Append(text)))
            {
                writer.Write("{0}{1}{2}{3}", "a", "b", "c", "d");
            }

            output.ToString().ShouldBe("abcd");
        }

        [Fact]
        public void EmptyFlushDoesNotInvokeCallback()
        {
            int callbackCount = 0;

            using (RedirectConsoleWriter writer = new(_ => callbackCount++))
            {
                writer.Flush();
            }

            callbackCount.ShouldBe(0);
        }
    }
}
