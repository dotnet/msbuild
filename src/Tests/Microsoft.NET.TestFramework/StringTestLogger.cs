// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework
{
    public class StringTestLogger : ITestOutputHelper
    {
        StringBuilder _stringBuilder = new StringBuilder();

        public void WriteLine(string message)
        {
            _stringBuilder.AppendLine(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            _stringBuilder.AppendLine(string.Format(format, args));
        }

        public override string ToString()
        {
            return _stringBuilder.ToString();
        }
    }
}
