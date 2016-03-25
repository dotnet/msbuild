// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class CommandContext
    {
        public static class Variables
        {
            private static readonly string Prefix = "DOTNET_CLI_CONTEXT_";
            public static readonly string Verbose = Prefix + "VERBOSE";
            public static readonly string AnsiPassThru = Prefix + "ANSI_PASS_THRU";
        }

        private static Lazy<bool> _verbose = new Lazy<bool>(() => Env.GetBool(Variables.Verbose));
        private static Lazy<bool> _ansiPassThru = new Lazy<bool>(() => Env.GetBool(Variables.AnsiPassThru));

        public static bool IsVerbose()
        {
            return _verbose.Value;
        }

        public static bool ShouldPassAnsiCodesThrough()
        {
            return _ansiPassThru.Value;
        }        
    }
}
