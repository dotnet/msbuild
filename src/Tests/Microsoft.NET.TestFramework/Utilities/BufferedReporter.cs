// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
    }
}
