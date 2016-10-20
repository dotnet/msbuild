#!/usr/bin/env bash

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

rm -rf bin obj
dotnet publish -o bin -f netcoreapp1.0 
cp -a "$DIR/bin/runtimes/any/native/." "$DIR/bin"

sdkRevision="cc1fc023e3375b3944dbedfdd4ba2b5d2cbd01f0"
sdkRoot="$DIR/bin/sdk"
(cd bin && \
  git clone https://github.com/dotnet/sdk.git && \
  cd sdk && \
  git reset --hard $sdkRevision)

dotnet "$DIR/bin/MigrationDefaultsConstructor.dll" "$sdkRoot"