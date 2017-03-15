// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.DotNet.Cli.CommandLine;
using Newtonsoft.Json.Linq;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.PackageReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddCommandParser
    {
        public static Command Add() =>
            Create.Command(
                "add",
                ".NET Add Command",
                Accept.ExactlyOneArgument()
                      .ExistingFilesOnly()
                      .DefaultToCurrentDirectory()
                      .With(name: "PROJECT",
                            description: "The project file to operate on. If a file is not specified, the command will search the current directory for one."),
                Create.Command(
                    "package",
                    ".NET Add Package reference Command",
                    Accept.ExactlyOneArgument(errorMessage: o => LocalizableStrings.SpecifyExactlyOnePackageReference)
                          .WithSuggestionsFrom(QueryNuGet)
                          .With(name: "PACKAGE_NAME",
                                description: "Package references to add"),
                    CommonOptions.HelpOption(),
                    Create.Option("-v|--version",
                                  "Version for the package to be added.",
                                  Accept.ExactlyOneArgument()
                                        .With(name: "VERSION")
                                        .ForwardAs(o => $"--version {o.Arguments.Single()}")),
                    Create.Option("-f|--framework",
                                  "Add reference only when targeting a specific framework",
                                  Accept.ExactlyOneArgument()
                                        .With(name: "FRAMEWORK")
                                        .ForwardAs(o => $"--framework {o.Arguments.Single()}")),
                    Create.Option("-n|--no-restore ",
                                  "Add reference without performing restore preview and compatibility check."),
                    Create.Option("-s|--source",
                                  "Use specific NuGet package sources to use during the restore.",
                                  Accept.ExactlyOneArgument()
                                        .With(name: "SOURCE")
                                        .ForwardAs(o => $"--source {o.Arguments.Single()}")),
                    Create.Option("--package-directory",
                                  "Restore the packages to this Directory .",
                                  Accept.ExactlyOneArgument()
                                        .With(name: "PACKAGE_DIRECTORY")
                                        .ForwardAs(o => $"--package-directory {o.Arguments.Single()}"))),
                Create.Command(
                    "reference",
                    Tools.Add.ProjectToProjectReference.LocalizableStrings.AppFullName,
                    Accept.OneOrMoreArguments()
                          .With(name: "args",
                                description: Tools.Add.ProjectToProjectReference.LocalizableStrings.AppHelpText),
                    CommonOptions.HelpOption(),
                    Create.Option("-f|--framework",
                                  "Add reference only when targeting a specific framework",
                                  Accept.AnyOneOf(Suggest.TargetFrameworksFromProjectFile)
                                        .With(name: "FRAMEWORK"))),
                CommonOptions.HelpOption());

        public static IEnumerable<string> QueryNuGet(string match)
        {
            var httpClient = new HttpClient();

            string result;

            try
            {
                var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = httpClient.GetAsync($"https://api-v2v3search-0.nuget.org/query?q={match}&skip=0&take=100&prerelease=true", cancellation.Token)
                                         .Result;

                result = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception)
            {
                yield break;
            }

            var json = JObject.Parse(result);

            foreach (var id in json["data"])
            {
                yield return id["id"].Value<string>();
            }
        }
    }
}