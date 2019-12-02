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

source "$DIR/../common/_common.sh"

if [ ! -z "$CI_BUILD" ]; then
	# periodically clear out the package cache on the CI server
	PackageCacheFile="$NUGET_PACKAGES/packageCacheTime.txt"
	if [ ! -f $PackageCacheFile ]; then
		date > $PackageCacheFile
	else
		#$NUGET_PACKAGES_CACHE_TIME_LIMIT is in hours
		CacheTimeLimitInSeconds=$(($NUGET_PACKAGES_CACHE_TIME_LIMIT * 3600))
		CacheExpireTime=$(($(date +%s) - $CacheTimeLimitInSeconds))

		if [ $(date +%s -r $PackageCacheFile) -lt $CacheExpireTime ]; then
			header "Clearing package cache"

			rm -Rf $NUGET_PACKAGES
			mkdir -p $NUGET_PACKAGES
			date > $PackageCacheFile
		fi
	fi
fi
