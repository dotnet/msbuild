using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Help;

internal static class HelpUsageText
{
    public static readonly string UsageText =
$@"{LocalizableStrings.Usage}: dotnet [runtime-options] [path-to-application]
{LocalizableStrings.Usage}: dotnet [command] [arguments] [command-options]

path-to-application:
  {LocalizableStrings.PathToApplicationDefinition}

{LocalizableStrings.Commands}:
  new              {LocalizableStrings.NewDefinition}
  restore          {LocalizableStrings.RestoreDefinition}
  run              {LocalizableStrings.RunDefinition}
  build            {LocalizableStrings.BuildDefinition}
  publish          {LocalizableStrings.PublishDefinition}
  test             {LocalizableStrings.TestDefinition}
  pack             {LocalizableStrings.PackDefinition}
  migrate          {LocalizableStrings.MigrateDefinition}
  clean            {LocalizableStrings.CleanDefinition}
  sln              {LocalizableStrings.SlnDefinition}
  add              {LocalizableStrings.AddDefinition}
  remove           {LocalizableStrings.RemoveDefinition}
  list             {LocalizableStrings.ListDefinition}
  nuget            {LocalizableStrings.NugetDefinition}
  msbuild          {LocalizableStrings.MsBuildDefinition}
  vstest           {LocalizableStrings.VsTestDefinition}
  -v|--version     {LocalizableStrings.SDKVersionCommandDefinition}
  -i|--info        {LocalizableStrings.SDKInfoCommandDefinition}
  -d|--diagnostics {LocalizableStrings.SDKDiagnosticsCommandDefinition}

{LocalizableStrings.CommonOptions}:
  -v|--verbosity        {CommonLocalizableStrings.VerbosityOptionDescription}
  -h|--help             {LocalizableStrings.HelpDefinition}

{LocalizableStrings.RunDotnetCommandHelpForMore}

runtime-options:
  --additionalprobingpath <path>    {LocalizableStrings.AdditionalprobingpathDefinition}
  --depsfile <path>                 {LocalizableStrings.DepsfilDefinition}
  --runtimeconfig <path>            {LocalizableStrings.RuntimeconfigDefinition}
  --fx-version <version>            {LocalizableStrings.FxVersionDefinition}
  --roll-forward-on-no-candidate-fx {LocalizableStrings.RollForwardOnNoCandidateFxDefinition}
  --additional-deps <path>          {LocalizableStrings.AdditionalDeps}
";
}
