#!/usr/bin/env sh
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

set -euo pipefail

MANPAGE_TOOL_DIR=$(cd "$(dirname "$0")" || exit; pwd)

cd "$MANPAGE_TOOL_DIR"/../sdk || exit

echo "Downloading dotnet/docs main"

if [ -d docs-main ]; then
  cd docs-main
  git clean -xdf
  git checkout -- .
  git checkout main
  git pull
  cd ..
elif command -v git > /dev/null 2>&1; then
  git clone https://github.com/dotnet/docs --single-branch --branch main --depth 1 docs-main
elif command -v curl > /dev/null 2>&1; then
  curl -sSL https://github.com/dotnet/docs/archive/main.tar.gz | tar -xvz > /dev/null
elif command -v wget > /dev/null 2>&1; then
  wget -qO- https://github.com/dotnet/docs/archive/main.tar.gz | tar -xvz > /dev/null
else
  echo "Install git, curl or wget to proceed"
  exit 1
fi

ls docs-main/docs/core/tools/dotnet*.md | while read -r file;
  do
    echo "Working on $file"
    "$MANPAGE_TOOL_DIR"/remove-metadata-and-embed-includes.py "$file"
    command_name=$(basename "${file%.md}")
    man_page_section=1
    if [[ "$command_name" == "dotnet-install-script" ]]; then
        continue;
    fi
    if [[ "$command_name" == "dotnet-environment-variables" ]] || \
       [[ "$command_name" == "dotnet-new-sdk-templates" ]]; then
        "$MANPAGE_TOOL_DIR"/handle-missing-name.py "$file"
        man_page_section=7
    fi
    file_modification_date=$(git -C docs-main/ log -1 --format='%cs' "${file#docs-main/}")
    pandoc --standalone -t man \
      -V title="$command_name" \
      -V section="$man_page_section" \
      -V header=".NET Documentation" \
      -V date="$file_modification_date" \
      --column=500 \
      --filter "$MANPAGE_TOOL_DIR"/man-pandoc-filter.py \
      "$file" -o "${command_name}"."${man_page_section}"
done

# rm -rf docs-main
