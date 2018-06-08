#!/bin/bash

if [ "$1" ==  "-fromScript" ]; then
  otherScript=$2
  shift 2
  echo Executed from $otherScript with arguments: $*
fi

build=false
ci=false
configuration="Debug"
help=false
nolog=false
pack=false
prepareMachine=false
rebuild=false
norestore=false
sign=false
skipTests=false
bootstrapOnly=false
verbosity="minimal"
hostType="core"
properties=""
dotnetBuildFromSource=false
dotnetCoreSdkDir=""

function Help() {
  echo "Common settings:"
  echo "  -configuration <value>  Build configuration Debug, Release"
  echo "  -hostType <value>       core (default), mono"
  echo "  -verbosity <value>      Msbuild verbosity (q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic])"
  echo "  -help                   Print help and exit"
  echo ""
  echo "Actions:"
  echo "  -norestore              Don't automatically run restore"
  echo "  -build                  Build solution"
  echo "  -rebuild                Rebuild solution"
  echo "  -skipTests              Don't run tests"
  echo "  -bootstrapOnly          Don't run build again with bootstrapped MSBuild"
  echo "  -sign                   Sign build outputs"
  echo "  -pack                   Package build outputs into NuGet packages and Willow components"
  echo ""
  echo "Advanced settings:"
  echo "  -ci                     Set when running on CI server"
  echo "  -nolog                  Disable logging"
  echo "  -prepareMachine         Prepare machine for CI run"
  echo "  -useSystemMSBuild       [mono] Use system msbuild instead of downloading a copy for use with mono"
  echo ""
  echo "Command line arguments not listed above are passed through to MSBuild."
}

while [[ $# -gt 0 ]]; do
  lowerI="$(echo $1 | awk '{print tolower($0)}')"
  case $lowerI in
    build)
      build=true
      shift 1
      ;;
    -build)
      build=true
      shift 1
      ;;
    -ci)
      ci=true
      shift 1
      ;;
    -configuration)
      configuration=$2
      shift 2
      ;;
    -h | -help)
      Help
      exit 0
      ;;
    -hosttype)
      hostType=$2
      shift 2
      ;;
    -nolog)
      nolog=true
      shift 1
      ;;
    -pack)
      pack=true
      shift 1
      ;;
    -preparemachine)
      prepareMachine=true
      shift 1
      ;;
    -rebuild)
      rebuild=true
      shift 1
      ;;
    -norestore)
      norestore=true
      shift 1
      ;;
    -sign)
      sign=true
      shift 1
      ;;
    -skiptests)
      skipTests=true
      shift 1
      ;;
    -bootstraponly)
      bootstrapOnly=true
      shift 1
      ;;
    -usesystemmsbuild)
      useSystemMSBuild=true
      shift 1
      ;;
    -verbosity)
      verbosity=$2
      shift 2
      ;;
    -dotnetbuildfromsource)
      dotnetBuildFromSource=true
      shift 1
      ;;
    -dotnetcoresdkdir)
      dotnetCoreSdkDir=$2
      shift 2
      ;;
    *)
      properties="$properties $1"
      shift 1
      ;;
  esac
done

if [ "$hostType" = "mono" ]; then
  configuration="$configuration-MONO"
fi

function KillProcessWithName {
  echo "Killing processes containing \"$1\""
  kill $(ps ax | grep -i "$1" | awk '{ print $1 }')
}

function StopProcesses {
  echo "Killing running build processes..."
  KillProcessWithName "dotnet"
  KillProcessWithName "vbcscompiler"
}

function ExitIfError {
  if [ $1 != 0 ]
  then
    echo "$2"

    if [[ "$ci" != "true" && "$dotnetBuildFromSource" != "true" ]]; # kill command not permitted on CI machines or in source-build
    then
      StopProcesses
    fi

    exit $1
  fi
}

# copied from https://github.com/dotnet/metadata-tools/blob/0c2c58c79edc2085f97b208afb8980cfed33b857/eng/common/build.sh#L122-L138
# ReadJson [filename] [json key]
# Result: Sets 'readjsonvalue' to the value of the provided json key
# Note: this method may return unexpected results if there are duplicate
# keys in the json
function ReadJson {
  local unamestr="$(uname)"
  local sedextended='-r'
  if [[ "$unamestr" == 'Darwin' ]]; then
    sedextended='-E'
  fi;

  readjsonvalue="$(grep -m 1 "\"${2}\"" ${1} | sed ${sedextended} 's/^ *//;s/.*: *"//;s/",?//')"
  if [[ ! "$readjsonvalue" ]]; then
    ExitIfError 1 "Error: Cannot find \"${2}\" in ${1}"
  fi;
}

function CreateDirectory {
  if [ ! -d "$1" ]
  then
    mkdir -p "$1"
  fi
}

function QQ {
  echo '"'$*'"'
}

function GetLogCmd {
  if $ci || $log
  then
    CreateDirectory $LogDir

    local logCmd="/bl:$(QQ $LogDir/$1.binlog)"

    # When running under CI, also create a text log, so it can be viewed in the Jenkins UI
    if $ci
    then
      logCmd="$logCmd /fl /flp:Verbosity=diag\;LogFile=$(QQ $LogDir/$1.log)"
    fi
  else
    logCmd=""
  fi

  echo $logCmd
}

function downloadMSBuildForMono {
  if [[ -z $useSystemMSBuild && ! -e "$MONO_MSBUILD_DIR/MSBuild.dll" ]]
  then
    echo "** Downloading MSBUILD from $MSBUILD_DOWNLOAD_URL"
    curl -sL -o "$MSBUILD_ZIP" "$MSBUILD_DOWNLOAD_URL"

    unzip -q "$MSBUILD_ZIP" -d "$ArtifactsDir"
    # rename just to make it obvious when reading logs!
    mv $ArtifactsDir/msbuild $MONO_MSBUILD_DIR
    chmod +x $ArtifactsDir/mono-msbuild/MSBuild.dll
    rm "$MSBUILD_ZIP"
  fi
}

function CallMSBuild {
  local commandLine=""

  if [ -z "$msbuildHost" ]
  then
    commandLine="$msbuildToUse $*"
  else
    commandLine="$msbuildHost $msbuildToUse $*"
  fi

  echo ============= MSBuild command =============
  echo "$commandLine"
  echo ===========================================

  eval $commandLine

  ExitIfError $? "Failed to run MSBuild: $commandLine"
}

function GetVersionsPropsVersion {
  echo "$( awk -F'[<>]' "/<$1>/{print \$3}" "$VersionsProps" )"
}

function DownloadDotnetCli {
  DotNetCliVersion="$( GetVersionsPropsVersion DotNetCliVersion )"
  DotNetInstallVerbosity=""

  if [ -z "$DOTNET_INSTALL_DIR" ]
  then
    export DOTNET_INSTALL_DIR="$RepoRoot/artifacts/.dotnet/$DotNetCliVersion"
  fi

  DotNetRoot=$DOTNET_INSTALL_DIR
  DotNetInstallScript="$DotNetRoot/dotnet-install.sh"

  if [ ! -a "$DotNetInstallScript" ]
  then
    CreateDirectory "$DotNetRoot"
    curl "https://dot.net/v1/dotnet-install.sh" -sSL -o "$DotNetInstallScript"
  fi

  if [[ "$(echo $verbosity | awk '{print tolower($0)}')" == "diagnostic" ]]
  then
    DotNetInstallVerbosity="--verbose"
  fi

  # Install a stage 0
  SdkInstallDir="$DotNetRoot/sdk/$DotNetCliVersion"

  if [ ! -d "$SdkInstallDir" ]
  then
    bash "$DotNetInstallScript" --version $DotNetCliVersion $DotNetInstallVerbosity
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to install stage0"
      return $LASTEXITCODE
    fi
  fi
}

function InstallDotNetCli {
  if [ "$dotnetCoreSdkDir" = "" ]
  then
    DownloadDotnetCli
  else
    export DOTNET_INSTALL_DIR=$dotnetCoreSdkDir
  fi

  # don't double quote this otherwise the csc tooltask will fail with double double-quotting
  export DOTNET_HOST_PATH="$DOTNET_INSTALL_DIR/dotnet"

  # Put the stage 0 on the path
  export PATH="$DOTNET_INSTALL_DIR:$PATH"

  # Disable first run since we want to control all package sources
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Don't resolve runtime, shared framework, or SDK from other locations
  export DOTNET_MULTILEVEL_LOOKUP=0
}

function InstallRepoToolset {
  CreateDirectory "$RepoToolsetDir"
  ReadJson "$RepoRoot/global.json" "RoslynTools.RepoToolset"
  RepoToolsetVersion=$readjsonvalue
  RepoToolsetLocationFile="$RepoToolsetDir/$RepoToolsetVersion.txt"
  
  echo "Using repo tools version: $readjsonvalue"

  if [[ -a "$RepoToolsetLocationFile" ]]; then
    local path=`cat $RepoToolsetLocationFile`
    if [[ -a "$path" ]]; then
      RepoToolsetBuildProj=$path
      return
    fi
  fi
  if [[ "$restore" != true ]]; then
    ExitIfError 2 "Toolset version $RepoToolsetVersion has not been restored."
  fi

  local proj="$RepoToolsetDir/restore.proj"
  echo '<Project Sdk="RoslynTools.RepoToolset"/>' > $proj

  local logCmd=$(GetLogCmd Toolset)
  CallMSBuild $(QQ $proj) /t:__WriteToolsetLocation /m /nologo /clp:None /warnaserror $logCmd /p:__ToolsetLocationOutputFile=$RepoToolsetLocationFile
  local lastexitcode=$?

  ExitIfError $lastexitcode "Failed to restore toolset (exit code '$lastexitcode')."

  RepoToolsetBuildProj=`cat $RepoToolsetLocationFile`

  if [[ ! -a "$RepoToolsetBuildProj" ]]; then
    ExitIfError 3 "Invalid toolset path: $RepoToolsetBuildProj"
  fi
}

function ErrorHostType {
  echo "Unsupported hostType ($hostType)"
  exit 1
}

function Build {
  InstallDotNetCli

  echo "Using dotnet from: $DOTNET_INSTALL_DIR"
  if $prepareMachine
  then
    CreateDirectory "$NuGetPackageRoot"
    eval "$(QQ $DOTNET_HOST_PATH) nuget locals all --clear"

    ExitIfError $? "Failed to clear NuGet cache"
  fi

  if [ "$hostType" = "core" ]
  then
    msbuildHost=$(QQ $DOTNET_HOST_PATH)
  elif [ "$hostType" = "mono" ]
  then
    if [ -z $useSystemMSBuild ]; then
      downloadMSBuildForMono
      msbuildHost=""
      msbuildToUse="$MONO_MSBUILD_DIR/msbuild"
    else
      msbuildHost=""
      msbuildToUse="msbuild"
    fi
  else
    ErrorHostType
  fi

  InstallRepoToolset

  local logCmd=$(GetLogCmd Build)

  commonMSBuildArgs="/m /clp:Summary /v:$verbosity /p:Configuration=$configuration /p:RepoRoot=$(QQ $RepoRoot) /p:Projects=$(QQ $MSBuildSolution) /p:CIBuild=$ci /p:DisableNerdbankVersioning=$dotnetBuildFromSource"

  # Only enable warnaserror on CI runs.
  if $ci
  then
    commonMSBuildArgs="$commonMSBuildArgs /warnaserror"
  fi

  # Only test using stage 0 MSBuild if -bootstrapOnly is specified
  local testStage0=false
  if $bootstrapOnly
  then
    testStage0=$test
  fi

  CallMSBuild $(QQ $RepoToolsetBuildProj) $commonMSBuildArgs $logCmd /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Test=$testStage0 /p:Sign=$sign /p:Pack=$pack /p:CreateBootstrap=true $properties

  if ! $bootstrapOnly
  then
    bootstrapRoot="$ArtifactsConfigurationDir/bootstrap"

    if [ $hostType = "core" ]
    then
      msbuildToUse=$(QQ "$bootstrapRoot/netcoreapp2.1/MSBuild/MSBuild.dll")
    elif [ "$hostType" = "mono" ]
    then
      msbuildToUse="$bootstrapRoot/net461/MSBuild/15.0/Bin/MSBuild.dll"
      msbuildHost="mono"

      properties="$properties /p:MSBuildExtensionsPath=$bootstrapRoot/net461/MSBuild/"
    else
      ErrorHostType
    fi

    # forcing the slashes here or they get normalized and lost later
    export ArtifactsDir="$ArtifactsDir\\2\\"

    local logCmd=$(GetLogCmd BuildWithBootstrap)

      # When using bootstrapped MSBuild:
      # - Turn off node reuse (so that bootstrapped MSBuild processes don't stay running and lock files)
      # - Don't sign
      # - Don't pack
      # - Do run tests (if not skipped)
      # - Don't try to create a bootstrap deployment
    CallMSBuild $(QQ $RepoToolsetBuildProj) $commonMSBuildArgs /nr:false $logCmd /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Test=$test /p:Sign=false /p:Pack=false /p:CreateBootstrap=false $properties
  fi
}

function AssertNugetPackages {
  if $pack || $dotnetBuildFromSource
  then

    packageCount=$(find $PackagesDir -type f | wc -l)
    if [ $packageCount -ne 5 ]
    then
      ExitIfError 1 "Did not find 5 packages in $PackagesDir"
    fi

    echo "Ensured that 5 nuget packages were created"
  fi
}

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$ScriptRoot/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

RepoRoot="$ScriptRoot/.."
ArtifactsDir="$RepoRoot/artifacts"
ArtifactsConfigurationDir="$ArtifactsDir/$configuration"
PackagesDir="$ArtifactsConfigurationDir/packages"
LogDir="$ArtifactsConfigurationDir/log"
VersionsProps="$ScriptRoot/Versions.props"
RepoToolsetDir="$ArtifactsDir/toolset"

#https://github.com/dotnet/source-build
if $dotnetBuildFromSource
then
  MSBuildSolution="$RepoRoot/MSBuild.SourceBuild.sln"
else
  MSBuildSolution="$RepoRoot/MSBuild.sln"
fi

msbuildToUse="msbuild"
MONO_MSBUILD_DIR="$ArtifactsDir/mono-msbuild"
MSBUILD_DOWNLOAD_URL="https://github.com/mono/msbuild/releases/download/0.06/mono_msbuild_xplat-master-3c930fa8.zip"
MSBUILD_ZIP="$ArtifactsDir/msbuild.zip"

log=false
if ! $nolog
then
  log=true
fi

restore=false
if ! $norestore
then
  restore=true
fi

test=false
if ! $skipTests
then
  test=true
fi

if [ "$hostType" != "core" -a "$hostType" != "mono" ]; then
  ErrorHostType
fi

# HOME may not be defined in some scenarios, but it is required by NuGet
if [ -z $HOME ]
then
  export HOME="$RepoRoot/artifacts/.home/"
  CreateDirectory "$HOME"
fi

if $ci
then
  TempDir="$ArtifactsConfigurationDir/tmp"
  CreateDirectory "$TempDir"

  export TEMP="$TempDir"
  export TMP="$TempDir"
  export MSBUILDDEBUGPATH="$TempDir"
fi

if [ -z $NUGET_PACKAGES ]
then
  export NUGET_PACKAGES="$HOME/.nuget/packages"
fi

NuGetPackageRoot=$NUGET_PACKAGES

Build

ExitIfError $? "Build failed"

AssertNugetPackages

ExitIfError $? "AssertNugetPackages failed"
