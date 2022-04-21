// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class RedirectConsoleWriter_Tests
    {
        [Fact]
        public async Task EmitConsoleMessages()
        {
            StringBuilder sb = new StringBuilder();
            var writer = OutOfProcServerNode.RedirectConsoleWriter.Create(text => sb.Append(text));

            writer.WriteLine("Line 1");
            await Task.Delay(300);
            writer.Write("Line 2");
            writer.Dispose();

            Assert.Equal($"Line 1{Environment.NewLine}Line 2", sb.ToString());
        }
    }
}
