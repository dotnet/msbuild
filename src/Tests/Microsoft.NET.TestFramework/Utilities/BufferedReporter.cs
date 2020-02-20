using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework.Utilities
{
    public class BufferedReporter : IReporter
    {
        public List<string> Lines { get; private set; } = new List<string>();

        public void WriteLine(string message)
        {
            Lines.Add(message);
        }

        public void WriteLine()
        {
            Lines.Add("");
        }

        public void Write(string message)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            Lines.Clear();
        }
    }
}
