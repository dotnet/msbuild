// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.CommandLine;
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
  -d|--diagnostics      {LocalizableStrings.DiagnosticsDefinition}
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
  clean         {LocalizableStrings.CleanDefinition}
  sln           {LocalizableStrings.SlnDefinition}

Project modification commands:
  add           Add items to the project
  remove        Remove items from the project
  list          List items in the project

{LocalizableStrings.AdvancedCommands}:
  nuget         {LocalizableStrings.NugetDefinition}
  msbuild       {LocalizableStrings.MsBuildDefinition}
  vstest        {LocalizableStrings.VsTestDefinition}";

        public static int Run(string[] args)
        {

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet help";
            app.FullName = LocalizableStrings.AppFullName;
            app.Description = LocalizableStrings.AppDescription;

            CommandArgument commandNameArgument = app.Argument($"<{LocalizableStrings.CommandArgumentName}>", LocalizableStrings.CommandArgumentDescription);

            app.OnExecute(() => 
            {
                Cli.BuiltInCommandMetadata builtIn;
                if (Cli.BuiltInCommandsCatalog.Commands.TryGetValue(commandNameArgument.Value, out builtIn))
                {
                    // var p = Process.Start(GetProcessStartInfo(builtIn));
                    var process = ConfigureProcess(builtIn.DocLink.ToString());
                    process.Start();
                    process.WaitForExit();
                }
                else
                {
                    Reporter.Error.WriteLine(String.Format(LocalizableStrings.CommandDoesNotExist, commandNameArgument.Value));
                    return 1;
                }
                return 0;
            });
            
            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }
            else
            {
                return app.Execute(args);
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

        public static Process ConfigureProcess(string docUrl)
        {
            ProcessStartInfo psInfo;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                psInfo = new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c start {docUrl}"
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                psInfo = new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = docUrl
                };
            }
            else
            {
                psInfo = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = docUrl
                };
            }
            
            return new Process
            {
                StartInfo = psInfo
            };
        }
    }
}
