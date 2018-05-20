// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddCommandParser
    {
        public static Command Add() =>
            Create.Command(
                "add",
                LocalizableStrings.NetAddCommand,
                Accept.ExactlyOneArgument()
                      .DefaultToCurrentDirectory()
                      .With(name: CommonLocalizableStrings.ProjectArgumentName,
                            description: CommonLocalizableStrings.ProjectArgumentDescription),
                AddPackageParser.AddPackage(),
                AddProjectToProjectReferenceParser.AddProjectReference(),
                CommonOptions.HelpOption());
    }
}