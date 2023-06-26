// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Help;

internal static class HelpUsageText
{
    public static readonly string UsageText =
$@"{LocalizableStrings.Usage}: dotnet [runtime-options] [path-to-application] [arguments]

{LocalizableStrings.ExecutionUsageDescription}

runtime-options:
  --additionalprobingpath <path>   {LocalizableStrings.AdditionalProbingPathDefinition}
  --additional-deps <path>         {LocalizableStrings.AdditionalDeps}
  --depsfile                       {LocalizableStrings.DepsFileDefinition}
  --fx-version <version>           {LocalizableStrings.FxVersionDefinition}
  --roll-forward <setting>         {LocalizableStrings.RollForwardDefinition}
  --runtimeconfig                  {LocalizableStrings.RuntimeConfigDefinition}

path-to-application:
  {LocalizableStrings.PathToApplicationDefinition}

{LocalizableStrings.Usage}: dotnet [sdk-options] [command] [command-options] [arguments]

{LocalizableStrings.SDKCommandUsageDescription}

sdk-options:
  -d|--diagnostics  {LocalizableStrings.SDKDiagnosticsCommandDefinition}
  -h|--help         {LocalizableStrings.HelpDefinition}
  --info            {LocalizableStrings.SDKInfoCommandDefinition}
  --list-runtimes   {LocalizableStrings.SDKListRuntimesCommandDefinition}
  --list-sdks       {LocalizableStrings.SDKListSdksCommandDefinition}
  --version         {LocalizableStrings.SDKVersionCommandDefinition}

{LocalizableStrings.Commands}:
  add               {LocalizableStrings.AddDefinition}
  build             {LocalizableStrings.BuildDefinition}
  build-server      {LocalizableStrings.BuildServerDefinition}
  clean             {LocalizableStrings.CleanDefinition}
  format            {LocalizableStrings.FormatDefinition}
  help              {LocalizableStrings.HelpDefinition}
  list              {LocalizableStrings.ListDefinition}
  msbuild           {LocalizableStrings.MsBuildDefinition}
  new               {LocalizableStrings.NewDefinition}
  nuget             {LocalizableStrings.NugetDefinition}
  pack              {LocalizableStrings.PackDefinition}
  publish           {LocalizableStrings.PublishDefinition}
  remove            {LocalizableStrings.RemoveDefinition}
  restore           {LocalizableStrings.RestoreDefinition}
  run               {LocalizableStrings.RunDefinition}
  sdk               {LocalizableStrings.SdkDefinition}
  sln               {LocalizableStrings.SlnDefinition}
  store             {LocalizableStrings.StoreDefinition}
  test              {LocalizableStrings.TestDefinition}
  tool              {LocalizableStrings.ToolDefinition}
  vstest            {LocalizableStrings.VsTestDefinition}
  workload          {LocalizableStrings.WorkloadDefinition}

{LocalizableStrings.AdditionalTools}
  dev-certs         {LocalizableStrings.DevCertsDefinition}
  fsi               {LocalizableStrings.FsiDefinition}
  user-jwts         {LocalizableStrings.UserJwtsDefinition}
  user-secrets      {LocalizableStrings.UserSecretsDefinition}
  watch             {LocalizableStrings.WatchDefinition}

{LocalizableStrings.RunDotnetCommandHelpForMore}";
}
