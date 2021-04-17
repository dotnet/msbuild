// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    // This parser is used for completion and telemetry.
    // See https://github.com/NuGet/NuGet.Client for the actual implementation.
    internal static class NuGetCommandParser
    {
        public static Command GetCommand()
        {
            var command = new Command("nuget");

            command.AddOption(new Option<bool>("--version"));
            command.AddOption(new Option<string>(new string[] { "-v", "--verbosity" }));
            command.AddArgument(new Argument() { IsHidden = true });

            command.AddCommand(GetDeleteCommand());
            command.AddCommand(GetLocalsCommand());
            command.AddCommand(GetPushCommand());
            command.AddCommand(GetVerifyCommand());

            return command;
        }

        private static Command GetDeleteCommand()
        {
            var deleteCommand = new Command("delete");
            deleteCommand.AddArgument(new Argument<IEnumerable<string>>() { Arity = ArgumentArity.OneOrMore });
            deleteCommand.AddOption(new Option<bool>("--force-english-output"));
            deleteCommand.AddOption(new Option<string>(new string[] { "-s", "--source" }));
            deleteCommand.AddOption(new Option<bool>("--non-interactive"));
            deleteCommand.AddOption(new Option<string>(new string[] { "-k", "--api-key" }));
            deleteCommand.AddOption(new Option<bool>("--no-service-endpoint"));
            deleteCommand.AddOption(new Option<bool>("--interactive"));

            return deleteCommand;
        }

        private static Command GetLocalsCommand()
        {
            var localsCommand = new Command("locals");

            localsCommand.AddArgument(new Argument<string>()
                .FromAmong(new string[] { "all", "http-cache", "global-packages", "plugins-cache", "temp" }));

            localsCommand.AddOption(new Option<bool>("--force-english-output"));
            localsCommand.AddOption(new Option<bool>(new string[] { "-c", "--clear" }));
            localsCommand.AddOption(new Option<bool>(new string[] { "-l", "--list" }));

            return localsCommand;
        }

        private static Command GetPushCommand()
        {
            var pushCommand = new Command("push");

            pushCommand.AddArgument(new Argument<IEnumerable<string>>() { Arity = ArgumentArity.OneOrMore });

            pushCommand.AddOption(new Option<bool>("--force-english-output"));
            pushCommand.AddOption(new Option<string>(new string[] { "-s", "--source" }));
            pushCommand.AddOption(new Option<string>(new string[] { "-ss", "--symbol-source" }));
            pushCommand.AddOption(new Option<string>(new string[] { "-t", "--timeout" }));
            pushCommand.AddOption(new Option<string>(new string[] { "-k", "--api-key" }));
            pushCommand.AddOption(new Option<string>(new string[] { "-sk", "--symbol-api-key" }));
            pushCommand.AddOption(new Option<bool>(new string[] { "-d", "--disable-buffering" }));
            pushCommand.AddOption(new Option<bool>(new string[] { "-n", "--no-symbols" }));
            pushCommand.AddOption(new Option<bool>("--no-service-endpoint"));
            pushCommand.AddOption(new Option<bool>("--interactive"));
            pushCommand.AddOption(new Option<bool>("--skip-duplicate"));

            return pushCommand;
        }

        private static Command GetVerifyCommand()
        {
            const string fingerprint = "--certificate-fingerprint";
            var verifyCommand = new Command("verify");

            verifyCommand.AddArgument(new Argument<IEnumerable<string>>() { Arity = ArgumentArity.OneOrMore });

            verifyCommand.AddOption(new Option<bool>("--all"));
            verifyCommand.AddOption(new ForwardedOption<IEnumerable<string>>(fingerprint)
                .ForwardAsManyArgumentsEachPrefixedByOption(fingerprint)
                .AllowSingleArgPerToken());
            verifyCommand.AddOption(CommonOptions.VerbosityOption());

            return verifyCommand;
        }
    }
}
