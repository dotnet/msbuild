// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Server;
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

            using (OutOfProcServerNode.RedirectConsoleWriter writer = new(text => sb.Append(text)))
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
            OutOfProcServerNode.RedirectConsoleWriter writer = new(text =>
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
    }
}
