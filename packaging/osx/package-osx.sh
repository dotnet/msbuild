#!/bin/bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

help(){
    echo "Usage: $0 [--version <pkg version>] [--input <input directory>] [--output <output pkg>] [--help]"
    echo ""
    echo "Options:"
    echo "  --version <pkg version>          Specify a version for the package. Version format is 4 '.' separated numbers - <major>.<minor>.<patch>.<revision>"
    echo "  --input <input directory>        Package the entire contents of the directory tree."
    echo "  --output <output pkg>            The path to where the package will be written."
    exit 1
}

while [[ $# > 0 ]]; do
    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
        -v|--version)
            DOTNET_CLI_VERSION=$2
            shift
            ;;
        -o|--output)
            OUTPUT_PKG=$2
            shift
            ;;
        -i|--input)
            INPUT_DIR=$2
            shift
            ;;
        --help)
            help
            ;;
        *)
            break
            ;;
    esac
    shift
done

if [ -z "$DOTNET_CLI_VERSION" ]; then
    echo "Provide a version number. Missing option '--version'" && help
fi

if [ -z "$OUTPUT_PKG" ]; then
    echo "Provide an output pkg. Missing option '--output'" && help
fi

if [ -z "$INPUT_DIR" ]; then
    echo "Provide an input directory. Missing option '--input'" && help
fi

if [ ! -d "$INPUT_DIR" ]; then
    echo "'$INPUT_DIR' - is either missing or not a directory" 1>&2
    exit 1
fi

PACKAGE_DIR=$(dirname "${OUTPUT_PKG}")
[ -d "$PACKAGE_DIR" ] || mkdir -p $PACKAGE_DIR

PACKAGE_ID=$(basename "${OUTPUT_PKG}")

#chmod -R 755 $INPUT_DIR
pkgbuild --root $INPUT_DIR \
         --version $DOTNET_CLI_VERSION \
         --scripts $DIR/scripts \
         --identifier com.microsoft.dotnet.cli.pkg.dotnet-osx-x64 \
         --install-location /usr/local/share/dotnet \
         $DIR/$PACKAGE_ID

cat $DIR/Distribution-Template | sed "/{VERSION}/s//$DOTNET_CLI_VERSION/g" > $DIR/Dist

productbuild --version $DOTNET_CLI_VERSION --identifier com.microsoft.dotnet.cli --package-path $DIR --resources $DIR/resources --distribution $DIR/Dist $OUTPUT_PKG

#Clean temp files
rm $DIR/$PACKAGE_ID
rm $DIR/Dist
