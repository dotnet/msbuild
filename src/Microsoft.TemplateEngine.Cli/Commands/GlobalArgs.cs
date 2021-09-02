// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class GlobalArgs
    {
        private static Option<bool> quietOption = new("--quiet", "sshhhhhhhhhhhh");

        public GlobalArgs(InvocationContext invocationContext)
        {
            Quiet = invocationContext.ParseResult.ValueForOption(quietOption);
        }

        internal bool Quiet { get; private set; }

        internal bool DebugAttach { get; private set; }

        internal bool DebugRebuildCache { get; private set; }

        internal bool DebugVirtualSettings { get; private set; }

        internal bool DebugReinit { get; private set; }

        internal bool DebugShowConfig { get; private set; }

        internal string? DebugSettingsLocation { get; private set; }

        internal static void AddGlobalsToCommand(Command command)
        {
            command.AddOption(quietOption);
        }
    }
}
