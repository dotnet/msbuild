using Microsoft.DotNet.Tools.Help;

internal static class HelpUsageText
{
    public static readonly string UsageText =
$@"{LocalizableStrings.Usage}: dotnet [host-options] [command] [arguments] [common-options]

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

{LocalizableStrings.ProjectModificationCommands}:
  add           {LocalizableStrings.AddDefinition}
  remove        {LocalizableStrings.RemoveDefinition}
  list          {LocalizableStrings.ListDefinition}

{LocalizableStrings.AdvancedCommands}:
  nuget         {LocalizableStrings.NugetDefinition}
  msbuild       {LocalizableStrings.MsBuildDefinition}
  vstest        {LocalizableStrings.VsTestDefinition}";
}

