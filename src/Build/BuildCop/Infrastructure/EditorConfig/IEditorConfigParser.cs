// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.BuildCop.Infrastructure.EditorConfig
{
    internal interface IEditorConfigParser
    {
        public Dictionary<string, string> Parse(string filePath);
    }
}
