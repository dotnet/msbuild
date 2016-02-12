#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done

DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/../../common/_common.sh"

buildTestPackages() {
	mkdir -p "$TEST_PACKAGE_DIR"

	PROJECTS=$(loadTestPackageList)

	for project in $PROJECTS
	do
		dotnet pack "$REPOROOT/TestAssets/TestPackages/$project" --output "$TEST_PACKAGE_DIR"
	done
}

buildTestProjects() {
	testProjectsRoot="$REPOROOT/TestAssets/TestProjects"
    exclusionList=( "$testProjectsRoot/CompileFail/project.json" )
    testProjectsList=( $(find $testProjectsRoot -name "project.json") )

	for project in "${testProjectsList[@]}"
	do
		if [[ "${exclusionList[@]}" != "${project}" ]]; then
			dotnet build "$project" --framework dnxcore50
		fi
	done
}

buildTestPackages
buildTestProjects
