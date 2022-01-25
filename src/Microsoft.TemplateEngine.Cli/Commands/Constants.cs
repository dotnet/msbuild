// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal static class Constants
    {
        internal static string[] KnownHelpAliases { get;  } = new[] { "-h", "/h", "--help", "-?", "/?" };
    }
}
