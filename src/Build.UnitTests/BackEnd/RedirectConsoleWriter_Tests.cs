// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental;
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

            using (TextWriter writer = OutOfProcServerNode.RedirectConsoleWriter.Create(text => sb.Append(text)))
            {
                writer.WriteLine("Line 1");
                await Task.Delay(80); // should be somehow bigger than `RedirectConsoleWriter` flush period - see its constructor
                writer.Write("Line 2");
            }

            sb.ToString().ShouldBe($"Line 1{Environment.NewLine}Line 2");
        }
    }
}
