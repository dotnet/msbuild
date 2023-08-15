// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Cli
{
    // This parser is used for completion and telemetry.
    // See https://github.com/NuGet/NuGet.Client for the actual implementation.
    internal static class NuGetCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-nuget";

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new DocumentedCommand("nuget", DocsLink);

            // some subcommands are not defined here and just forwarded to NuGet app
            command.TreatUnmatchedTokensAsErrors = false;

            command.Options.Add(new CliOption<bool>("--version"));
            command.Options.Add(new CliOption<string>("--verbosity", "-v"));

            command.Subcommands.Add(GetDeleteCommand());
            command.Subcommands.Add(GetLocalsCommand());
            command.Subcommands.Add(GetPushCommand());
            command.Subcommands.Add(GetVerifyCommand());
            command.Subcommands.Add(GetTrustCommand());
            command.Subcommands.Add(GetSignCommand());

            command.SetAction(NuGetCommand.Run);

            return command;
        }

        private static CliCommand GetDeleteCommand()
        {
            CliCommand deleteCommand = new("delete");
            deleteCommand.Arguments.Add(new CliArgument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });
            deleteCommand.Options.Add(new CliOption<bool>("--force-english-output"));
            deleteCommand.Options.Add(new CliOption<string>("--source", "-s"));
            deleteCommand.Options.Add(new CliOption<bool>("--non-interactive"));
            deleteCommand.Options.Add(new CliOption<string>("--api-key", "-k"));
            deleteCommand.Options.Add(new CliOption<bool>("--no-service-endpoint"));
            deleteCommand.Options.Add(new CliOption<bool>("--interactive"));

            deleteCommand.SetAction(NuGetCommand.Run);

            return deleteCommand;
        }

        private static CliCommand GetLocalsCommand()
        {
            CliCommand localsCommand = new("locals");

            CliArgument<string> foldersArgument = new CliArgument<string>("folders");
            foldersArgument.AcceptOnlyFromAmong(new string[] { "all", "http-cache", "global-packages", "plugins-cache", "temp" });

            localsCommand.Arguments.Add(foldersArgument);

            localsCommand.Options.Add(new CliOption<bool>("--force-english-output"));
            localsCommand.Options.Add(new CliOption<bool>("--clear", "-c"));
            localsCommand.Options.Add(new CliOption<bool>("--list", "-l"));

            localsCommand.SetAction(NuGetCommand.Run);

            return localsCommand;
        }

        private static CliCommand GetPushCommand()
        {
            CliCommand pushCommand = new("push");

            pushCommand.Arguments.Add(new CliArgument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });

            pushCommand.Options.Add(new CliOption<bool>("--force-english-output"));
            pushCommand.Options.Add(new CliOption<string>("--source", "-s"));
            pushCommand.Options.Add(new CliOption<string>("--symbol-source", "-ss"));
            pushCommand.Options.Add(new CliOption<string>("--timeout", "-t"));
            pushCommand.Options.Add(new CliOption<string>("--api-key", "-k"));
            pushCommand.Options.Add(new CliOption<string>("--symbol-api-key", "-sk"));
            pushCommand.Options.Add(new CliOption<bool>("--disable-buffering", "-d"));
            pushCommand.Options.Add(new CliOption<bool>("--no-symbols", "-n"));
            pushCommand.Options.Add(new CliOption<bool>("--no-service-endpoint"));
            pushCommand.Options.Add(new CliOption<bool>("--interactive"));
            pushCommand.Options.Add(new CliOption<bool>("--skip-duplicate"));

            pushCommand.SetAction(NuGetCommand.Run);

            return pushCommand;
        }

        private static CliCommand GetVerifyCommand()
        {
            const string fingerprint = "--certificate-fingerprint";
            CliCommand verifyCommand = new("verify");

            verifyCommand.Arguments.Add(new CliArgument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });

            verifyCommand.Options.Add(new CliOption<bool>("--all"));
            verifyCommand.Options.Add(new ForwardedOption<IEnumerable<string>>(fingerprint)
                .ForwardAsManyArgumentsEachPrefixedByOption(fingerprint)
                .AllowSingleArgPerToken());
            verifyCommand.Options.Add(CommonOptions.VerbosityOption);

            verifyCommand.SetAction(NuGetCommand.Run);

            return verifyCommand;
        }

        private static CliCommand GetTrustCommand()
        {
            CliCommand trustCommand = new("trust");

            CliOption<bool> allowUntrustedRoot = new("--allow-untrusted-root");
            CliOption<string> owners = new("--owners");

            trustCommand.Subcommands.Add (new CliCommand("list"));
            trustCommand.Subcommands.Add (AuthorCommand());
            trustCommand.Subcommands.Add (RepositoryCommand());
            trustCommand.Subcommands.Add (SourceCommand());
            trustCommand.Subcommands.Add (CertificateCommand());
            trustCommand.Subcommands.Add (RemoveCommand());
            trustCommand.Subcommands.Add (SyncCommand());

            CliOption<string> configFile = new("--configfile");

            // now set global options for all nuget commands: configfile, verbosity
            // as well as the standard NugetCommand.Run handler

            trustCommand.Options.Add(configFile);
            trustCommand.Options.Add(CommonOptions.VerbosityOption);
            trustCommand.SetAction(NuGetCommand.Run);

            foreach (var command in trustCommand.Subcommands)
            {
                command.Options.Add(configFile);
                command.Options.Add(CommonOptions.VerbosityOption);
                command.SetAction(NuGetCommand.Run);
            }

            CliCommand AuthorCommand() => new CliCommand("author") {
                new CliArgument<string>("NAME"),
                new CliArgument<string>("PACKAGE"),
                allowUntrustedRoot,
            };

            CliCommand RepositoryCommand() => new CliCommand("repository") {
                new CliArgument<string>("NAME"),
                new CliArgument<string>("PACKAGE"),
                allowUntrustedRoot,
                owners
            };

            CliCommand SourceCommand() => new CliCommand("source") {
                new CliArgument<string>("NAME"),
                owners,
                new CliOption<string>("--source-url"),
            };

            CliCommand CertificateCommand() {
                CliOption<string> algorithm = new("--algorithm");
                algorithm.DefaultValueFactory = (_argResult) => "SHA256";
                algorithm.AcceptOnlyFromAmong("SHA256", "SHA384", "SHA512");

                return new CliCommand("certificate") {
                    new CliArgument<string>("NAME"),
                    new CliArgument<string>("FINGERPRINT"),
                    allowUntrustedRoot,
                    algorithm
                };
            };

            CliCommand RemoveCommand() => new CliCommand("remove") {
                new CliArgument<string>("NAME"),
            };

            CliCommand SyncCommand() => new CliCommand("sync") {
                new CliArgument<string>("NAME"),
            };

            return trustCommand;
        }

        private static CliCommand GetSignCommand()
        {
            CliCommand signCommand = new("sign");

            signCommand.Arguments.Add(new CliArgument<IEnumerable<string>>("package-paths") { Arity = ArgumentArity.OneOrMore });

            signCommand.Options.Add(new CliOption<string>("--output", "-o"));
            signCommand.Options.Add(new CliOption<string>("--certificate-path"));
            signCommand.Options.Add(new CliOption<string>("--certificate-store-name"));
            signCommand.Options.Add(new CliOption<string>("--certificate-store-location"));
            signCommand.Options.Add(new CliOption<string>("--certificate-subject-name"));
            signCommand.Options.Add(new CliOption<string>("--certificate-fingerprint"));
            signCommand.Options.Add(new CliOption<string>("--certificate-password"));
            signCommand.Options.Add(new CliOption<string>("--hash-algorithm"));
            signCommand.Options.Add(new CliOption<string>("--timestamper"));
            signCommand.Options.Add(new CliOption<string>("--timestamp-hash-algorithm"));
            signCommand.Options.Add(new CliOption<bool>("--overwrite"));
            signCommand.Options.Add(CommonOptions.VerbosityOption);

            signCommand.SetAction(NuGetCommand.Run);

            return signCommand;
        }
    }
}
