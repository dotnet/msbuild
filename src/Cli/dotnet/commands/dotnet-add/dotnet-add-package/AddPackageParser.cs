// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.PackageReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddPackageParser
    {
        public static readonly Argument CmdPackageArgument = new Argument(LocalizableStrings.CmdPackage)
        {
            Description = LocalizableStrings.CmdPackageDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option VersionOption = new Option(new string[] { "-v", "--version" },
                              LocalizableStrings.CmdVersionDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdVersion)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"--version {o}");

        public static readonly Option FrameworkOption = new Option(new string[] { "-f", "--framework" },
                              LocalizableStrings.CmdFrameworkDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdFramework)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"--framework {o}");

        public static readonly Option NoRestoreOption = new Option(new string[] { "-n", "--no-restore" }, LocalizableStrings.CmdNoRestoreDescription);

        public static readonly Option SourceOption = new Option(new string[] { "-s", "--source" },
                              LocalizableStrings.CmdSourceDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdSource)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"--source {o}");


        public static readonly Option PackageDirOption = new Option("--package-directory",
                              LocalizableStrings.CmdPackageDirectoryDescription)
        {
            Argument = new Argument(LocalizableStrings.CmdPackageDirectory)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        }.ForwardAsSingle<string>(o => $"--package-directory {o}");

        public static readonly Option InteractiveOption = new Option("--interactive", CommonLocalizableStrings.CommandInteractiveOptionDescription)
            .ForwardAs("--interactive");

        public static readonly Option PrereleaseOption = new Option("--prerelease", CommonLocalizableStrings.CommandPrereleaseOptionDescription)
            .ForwardAs("--prerelease");

        public static Command GetCommand()
        {
            var command = new Command("package", LocalizableStrings.AppFullName);

            command.AddArgument(CmdPackageArgument);
            command.AddOption(VersionOption);
            command.AddOption(FrameworkOption);
            command.AddOption(NoRestoreOption);
            command.AddOption(SourceOption);
            command.AddOption(PackageDirOption);
            command.AddOption(InteractiveOption);
            command.AddOption(PrereleaseOption);

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
