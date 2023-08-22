// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework.Utilities
{
    public class BufferedReporter : IReporter
    {
        public List<string> Lines { get; private set; } = new List<string>();

        private bool AddLine = true;

        public void WriteLine(string message)
        {
            if (AddLine)
            {
                Lines.Add(message);
            }
            else
            {
                AddLine = true;
                Lines[Lines.Count - 1] = Lines[Lines.Count - 1] + message;
            }

        }

        public void WriteLine()
        {
            if (AddLine)
            {
                Lines.Add("");
            }
            else
            {
                AddLine = true;
            }
        }

        public void Write(string message)
        {
            if (AddLine)
            {
                AddLine = false;
                Lines.Add(message);
            }
            else
            {
                Lines[Lines.Count - 1] = Lines[Lines.Count - 1] + message;
            }
        }

        public void Clear()
        {
            Lines.Clear();
        }

        public void WriteLine(string format, params object?[] args) => WriteLine(string.Format(format, args));
    }
}
