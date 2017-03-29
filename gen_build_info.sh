#!/bin/sh

if [ $# -ne 1 ]; then
	echo "Usage: $0 <filename.cs>"
	exit
fi

version=
branch=
hash=

ABSOLUTE_PATH=$(cd `dirname "${BASH_SOURCE[0]}"` && pwd)/`basename "${BASH_SOURCE[0]}"`
REPO_ROOT=`dirname $ABSOLUTE_PATH`
if [ -d "$REPO_ROOT/.git" ]; then
	branch=`git branch | grep '^\*' | sed 's/(detached from .*/explicit/' | cut -d ' ' -f 2`
	hash=`git log --no-color --first-parent -n1 --pretty=format:%h`

	if [ ! -z "$branch" -a ! -z "$hash" ]; then
	    version="$branch/$hash"
	fi
fi

if [ -f $1 ]; then
    old_version=`grep BuildInfo $1 | sed -e 's,\(^.*BuildInfo *= *"\)\([^"]*\)\(.*\),\2,' | cut -d\  -f 1`
    test "$old_version" = "$version" && exit
fi

build_date=`date`
version="$version $build_date"
echo "class GitBuildInfoForMono { public static string BuildInfo = \"$version\"; }" > $1
