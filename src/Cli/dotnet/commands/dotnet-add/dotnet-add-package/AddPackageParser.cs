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
        public static readonly Argument<string> CmdPackageArgument = new Argument<string>(LocalizableStrings.CmdPackage)
        {
            Description = LocalizableStrings.CmdPackageDescription
        }.AddSuggestions((parseResult, match) => QueryNuGet(match));

        public static readonly Option<string> VersionOption = new ForwardedOption<string>(new string[] { "-v", "--version" }, LocalizableStrings.CmdVersionDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdVersion
        }.ForwardAsSingle(o => $"--version {o}");

        public static readonly Option<string> FrameworkOption = new ForwardedOption<string>(new string[] { "-f", "--framework" }, LocalizableStrings.CmdFrameworkDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdFramework
        }.ForwardAsSingle(o => $"--framework {o}");

        public static readonly Option<bool> NoRestoreOption = new Option<bool>(new string[] { "-n", "--no-restore" }, LocalizableStrings.CmdNoRestoreDescription);

        public static readonly Option<string> SourceOption = new ForwardedOption<string>(new string[] { "-s", "--source" }, LocalizableStrings.CmdSourceDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdSource
        }.ForwardAsSingle(o => $"--source {o}");

        public static readonly Option<string> PackageDirOption = new ForwardedOption<string>("--package-directory", LocalizableStrings.CmdPackageDirectoryDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdPackageDirectory
        }.ForwardAsSingle(o => $"--package-directory {o}");

        public static readonly Option<bool> InteractiveOption = new ForwardedOption<bool>("--interactive", CommonLocalizableStrings.CommandInteractiveOptionDescription)
            .ForwardAs("--interactive");

        public static readonly Option<bool> PrereleaseOption = new ForwardedOption<bool>("--prerelease", CommonLocalizableStrings.CommandPrereleaseOptionDescription)
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
