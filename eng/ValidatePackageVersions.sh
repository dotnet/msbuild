#! /bin/bash

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$ScriptRoot/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

RepoRoot="$ScriptRoot/.."
Packages="$RepoRoot/eng/Packages.props"
AppConfig="$RepoRoot/src/MSBuild/app.config"

NextHasVersion="f"
while IFS='' read -r line; do
if [ $line =~ PackageReference Update=[^\ ]* Version=[^>]*>$ ]; then
PackageName=$line | cut -d'"' -f 2
PackageVersion=$line | cut -d'"' -f 4
while IFS='' read -r line2; do
if [ NextHasVersion = "t" ]; then
NextHasVersion="f"
AppVersion=$line2 | cut -d'"' -f 4
# Check match between PackageVersion and AppVersion
fi
if [ $line2 =~ assemblyIdentity ]; then
if [ $PackageName = $line2 | cut -d'"' -f 2 ]; then
NextHasVersion="t"
fi
fi
done < $AppConfig
fi
done < $Packages
