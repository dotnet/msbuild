#!/bin/sh

printf "\n\nThis is master branch. Crossplatform is not supported\n\n"

printf "Stub log to avoid non windows CI failures on master and pull requests targetting master due to missing msbuild.log." > msbuild.log

exit 0
