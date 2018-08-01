#!/usr/bin/env bash

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
REPOROOT="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$REPOROOT/scripts/common/_prettyprint.sh"
export DOTNET_VERSION=2.1.302
if [ -n "$DotNetCoreSdkDir" ]
then
    export DOTNET_VERSION=$( ${DotNetCoreSdkDir}/dotnet --version )
fi
export WebSdkRoot=$REPOROOT
export WebSdkReferences=$WebSdkRoot/references/
export WebSdkSource=$WebSdkRoot/src/
export WebSdkBuild=$WebSdkRoot/build/

[ -z "$BuildConfiguration" ] && export BuildConfiguration=Release

[ -z $NUGET_PACKAGES ] && export NUGET_PACKAGES="$REPOROOT/.nuget/packages"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

if [ -z "${DotNetCoreSdkDir}" ]
then
    # Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
    [ -z "$DOTNET_INSTALL_DIR" ] && export DOTNET_INSTALL_DIR=$REPOROOT/.dotnet
    [ -d "$DOTNET_INSTALL_DIR" ] || mkdir -p $DOTNET_INSTALL_DIR

    DOTNET_INSTALL_SCRIPT_URL="https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain/dotnet-install.sh"
    curl -sSL "$DOTNET_INSTALL_SCRIPT_URL" | bash /dev/stdin --verbose --version 2.1.300

    curl --retry 10 -s -SL -f --create-dirs -o $DOTNET_INSTALL_DIR/buildtools.tar.gz https://aspnetcore.blob.core.windows.net/buildtools/netfx/4.6.1/netfx.4.6.1.tar.gz
    [ -d "$DOTNET_INSTALL_DIR/buildtools/net461" ] || mkdir -p $DOTNET_INSTALL_DIR/buildtools/net461
    tar -zxf $DOTNET_INSTALL_DIR/buildtools.tar.gz -C $DOTNET_INSTALL_DIR/buildtools/net461

    # Put stage 0 on the PATH (for this shell only)
    PATH="$DOTNET_INSTALL_DIR:$PATH"
    export ReferenceAssemblyRoot=$DOTNET_INSTALL_DIR/buildtools/net461
else
    DOTNET_INSTALL_DIR=$DotNetCoreSdkDir
    PATH="$DotNetCoreSdkDir:$PATH"
fi


# Increases the file descriptors limit for this bash. It prevents an issue we were hitting during restore
FILE_DESCRIPTOR_LIMIT=$( ulimit -n )
if [ $FILE_DESCRIPTOR_LIMIT -lt 1024 ]
then
    echo "Increasing file description limit to 1024"
    ulimit -n 1024
fi

$DOTNET_INSTALL_DIR/dotnet msbuild "$REPOROOT/build/build.proj" /t:Build /p:Configuration=$BuildConfiguration /p:NETFrameworkSupported=false $*
