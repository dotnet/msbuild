#!/usr/bin/env sh
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

MANPAGE_TOOL_DIR=$(cd $(dirname $0); pwd)

cd $MANPAGE_TOOL_DIR/../sdk

echo "Downloading dotnet/docs master"

if [ -x "$(command -v curl)" ]; then
  curl -sSLO https://github.com/dotnet/docs/archive/master.zip > /dev/null
elif [ -x "$(command -v wget)" ]; then
  wget -q https://github.com/dotnet/docs/archive/master.zip > /dev/null
else
  echo "Install curl or wget to proceed"
  exit 1
fi

echo "Extracting master.zip"
unzip -o master.zip > /dev/null

echo "Removing master.zip"
rm master.zip*

ls docs-master/docs/core/tools/dotnet*.md | while read -r line;
  do
    echo "Working on $line"
    pandoc -s -t man -V section=1 -V header=".NET Core" --column=500 --filter $MANPAGE_TOOL_DIR/man-pandoc-filter.py "$line" -o "$(basename ${line%.md}.1)"
done

rm -rf docs-master
