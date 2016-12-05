// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Help
{
    public class HelpCommand
    {
        private static readonly string UsageText = $@"{LocalizableStrings.Usage}: dotnet [host-options] [command] [arguments] [common-options]

{LocalizableStrings.Arguments}:
  [command]             {LocalizableStrings.CommandDefinition}
  [arguments]           {LocalizableStrings.ArgumentsDefinition}
  [host-options]        {LocalizableStrings.HostOptionsDefinition}
  [common-options]      {LocalizableStrings.OptionsDescription}

{LocalizableStrings.CommonOptions}:
  -v|--verbose          {LocalizableStrings.VerboseDefinition}
  -h|--help             {LocalizableStrings.HelpDefinition} 

{LocalizableStrings.HostOptions}:
  -v|--verbose          {LocalizableStrings.VerboseDefinition}
  --version             {LocalizableStrings.VersionDescription}
  --info                {LocalizableStrings.InfoDescription}

{LocalizableStrings.Commands}:
  new           {LocalizableStrings.NewDefinition}
  restore       {LocalizableStrings.RestoreDefinition}
  build         {LocalizableStrings.BuildDefinition}
  publish       {LocalizableStrings.PublishDefinition}
  run           {LocalizableStrings.RunDefinition}
  test          {LocalizableStrings.TestDefinition}
  pack          {LocalizableStrings.PackDefinition}
  migrate       {LocalizableStrings.MigrateDefinition}

Project modification commands:
  add           Add items to the project
  remove        Remove items from the project

{LocalizableStrings.AdvancedCommands}:
  nuget         {LocalizableStrings.NugetDefinition}
  msbuild       {LocalizableStrings.MsBuildDefinition}
  vstest        {LocalizableStrings.VsTestDefinition}";

        public static int Run(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }
            else
            {
                return Cli.Program.Main(new[] { args[0], "--help" });
            }
        }

        public static void PrintHelp()
        {
            PrintVersionHeader();
            Reporter.Output.WriteLine(UsageText);
        }

        public static void PrintVersionHeader()
        {
            var versionString = string.IsNullOrEmpty(Product.Version) ?
                string.Empty :
                $" ({Product.Version})";
            Reporter.Output.WriteLine(Product.LongName + versionString);
        }
    }
}
