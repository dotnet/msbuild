#!/usr/bin/env sh
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

MANPAGE_TOOL_DIR=$(cd $(dirname $0); pwd)

cd $MANPAGE_TOOL_DIR/sdk
pandocVersion=2.1.3
pandocVersionedName=pandoc-$pandocVersion-1-amd64

if [ ! -x "$(command -v pandoc)" ]; then
  echo "pandoc $pandocVersion not found, installing"
  wget -q https://github.com/jgm/pandoc/releases/download/$pandocVersion/$pandocVersionedName.deb > /dev/null
  apt install libgmp10 -y
  dpkg -i $pandocVersionedName.deb
  rm $pandocVersionedName.deb*
fi

if ! $(python -c "import pandocfilters" &> /dev/null); then
  echo "pandocfilters package for python not found, installing v1.4.2"
  wget -q https://github.com/jgm/pandocfilters/archive/1.4.2.zip -O pandocfilters-1.4.2.zip > /dev/null
  unzip -o pandocfilters-1.4.2.zip > /dev/null
  cd pandocfilters-1.4.2
  python setup.py install
  cd ..
  rm -rf pandocfilters-1.4.2*
fi

echo "Downloading dotnet/docs master"
wget -q https://github.com/dotnet/docs/archive/master.zip > /dev/null
unzip -o master.zip > /dev/null
rm master.zip*

ls docs-master/docs/core/tools/dotnet*.md | while read -r line;
  do
    echo "Working on $line"
    pandoc -s -t man -V section=1 -V header=".NET Core" --column=500 --filter $MANPAGE_TOOL_DIR/man-pandoc-filter.py "$line" -o "$(basename ${line%.md}.1)"
done

rm -rf docs-master
