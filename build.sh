#!/usr/bin/env bash
#
# $1 is passed to package to enable deb or pkg packaging

./scripts/bootstrap.sh

./scripts/package.sh $1
