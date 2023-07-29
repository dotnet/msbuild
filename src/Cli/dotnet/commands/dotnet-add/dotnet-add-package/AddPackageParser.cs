// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.Text.Json;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Add.PackageReference;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.PackageReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddPackageParser
    {
        public static readonly CliArgument<string> CmdPackageArgument = new CliArgument<string>(LocalizableStrings.CmdPackage)
        {
            Description = LocalizableStrings.CmdPackageDescription
        }.AddCompletions((context) => QueryNuGet(context.WordToComplete).Select(match => new CompletionItem(match)));

        public static readonly CliOption<string> VersionOption = new ForwardedOption<string>("--version", "-v")
        {
            Description = LocalizableStrings.CmdVersionDescription,
            HelpName = LocalizableStrings.CmdVersion
        }.ForwardAsSingle(o => $"--version {o}");

        public static readonly CliOption<string> FrameworkOption = new ForwardedOption<string>("--framework", "-f")
        {
            Description = LocalizableStrings.CmdFrameworkDescription,
            HelpName = LocalizableStrings.CmdFramework
        }.ForwardAsSingle(o => $"--framework {o}");

        public static readonly CliOption<bool> NoRestoreOption = new("--no-restore", "-n")
        {
            Description = LocalizableStrings.CmdNoRestoreDescription
        };

        public static readonly CliOption<string> SourceOption = new ForwardedOption<string>("--source", "-s")
        {
            Description = LocalizableStrings.CmdSourceDescription,
            HelpName = LocalizableStrings.CmdSource
        }.ForwardAsSingle(o => $"--source {o}");

        public static readonly CliOption<string> PackageDirOption = new ForwardedOption<string>("--package-directory")
        {
            Description = LocalizableStrings.CmdPackageDirectoryDescription,
            HelpName = LocalizableStrings.CmdPackageDirectory
        }.ForwardAsSingle(o => $"--package-directory {o}");

        public static readonly CliOption<bool> InteractiveOption = new ForwardedOption<bool>("--interactive")
        {
            Description = CommonLocalizableStrings.CommandInteractiveOptionDescription,
        }.ForwardAs("--interactive");

        public static readonly CliOption<bool> PrereleaseOption = new ForwardedOption<bool>("--prerelease")
        {
            Description = CommonLocalizableStrings.CommandPrereleaseOptionDescription
        }.ForwardAs("--prerelease");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("package", LocalizableStrings.AppFullName);

            command.Arguments.Add(CmdPackageArgument);
            command.Options.Add(VersionOption);
            command.Options.Add(FrameworkOption);
            command.Options.Add(NoRestoreOption);
            command.Options.Add(SourceOption);
            command.Options.Add(PackageDirOption);
            command.Options.Add(InteractiveOption);
            command.Options.Add(PrereleaseOption);

            command.SetAction((parseResult) => new AddPackageReferenceCommand(parseResult).Execute());

            return command;
        }

        public static IEnumerable<string> QueryNuGet(string match)
        {
            var httpClient = new HttpClient();

            Stream result;

            try
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = httpClient.GetAsync($"https://api-v2v3search-0.nuget.org/autocomplete?q={match}&skip=0&take=100", cancellation.Token)
                                         .Result;

                result = response.Content.ReadAsStreamAsync().Result;
            }
            catch (Exception)
            {
                yield break;
            }

            foreach (var packageId in EnumerablePackageIdFromQueryResponse(result))
            {
                yield return packageId;
            }
        }

        internal static IEnumerable<string> EnumerablePackageIdFromQueryResponse(Stream result)
        {
            using (JsonDocument doc = JsonDocument.Parse(result))
            {
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("data", out var data))
                {
                    foreach (JsonElement packageIdElement in data.EnumerateArray())
                    {
                        yield return packageIdElement.GetString();
                    }
                }
            }
        }
    }
}
