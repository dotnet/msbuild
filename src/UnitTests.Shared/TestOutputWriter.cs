// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public interface ITestOutputWriter
    {
        void WriteLine(string message);
    }

    internal sealed class TestOutputWriter : ITestOutputWriter
    {
        private static readonly Type[] s_writeLineParameterTypes = new[] { typeof(string) };
        private readonly object _output;
        private readonly MethodInfo _writeLine;

        private TestOutputWriter(object output)
        {
            _output = output;
            _writeLine = output.GetType().GetMethod(nameof(WriteLine), s_writeLineParameterTypes)
                ?? throw new ArgumentException($"Output object '{output.GetType()}' does not expose WriteLine(string).", nameof(output));
        }

        public static ITestOutputWriter Create(object output)
        {
            return output switch
            {
                null => null,
                ITestOutputWriter writer => writer,
                TestContext testContext => new MSTestOutputWriter(testContext),
                _ => new TestOutputWriter(output),
            };
        }

        public void WriteLine(string message)
            => _writeLine.Invoke(_output, new object[] { message });
    }

    public sealed class MSTestOutputWriter : ITestOutputWriter
    {
        private readonly TestContext _testContext;

        public MSTestOutputWriter(TestContext testContext)
            => _testContext = testContext;

        public void WriteLine(string message)
            => _testContext.WriteLine(message);
    }
}
