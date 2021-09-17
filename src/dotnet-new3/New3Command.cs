// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Dotnet_new3
{
    internal class New3Command : Command
    {
        private static readonly Option<bool> _debugEmitTelemetryOption = new("--debug:emit-telemetry", "Enable telemetry")
        {
            IsHidden = true
        };

        private static readonly Option<bool> _debugDisableBuiltInTemplatesOption = new("--debug:disable-sdk-templates", "Disable built-in templates")
        {
            IsHidden = true
        };

        private static readonly Argument<string[]> _remainingTokensArgument = new Argument<string[]>()
        {
            Arity = new ArgumentArity(0, 999)
        };

        internal New3Command() : base("new3", "Pre-parse command for dotnet new3")
        {
            this.AddOption(DebugEmitTelemetryOption);
            this.AddOption(DebugDisableBuiltInTemplatesOption);
            this.AddArgument(RemainingTokensArgument);
        }

        internal static Option<bool> DebugEmitTelemetryOption => _debugEmitTelemetryOption;

        internal static Option<bool> DebugDisableBuiltInTemplatesOption => _debugDisableBuiltInTemplatesOption;

        internal static Argument<string[]> RemainingTokensArgument => _remainingTokensArgument;
    }
}
