#!/usr/bin/env bash

STAGE0_DIR=$1
DNVM_PATH=$2

export DOTNET_USER_HOME=$STAGE0_DIR

source $DNVM_PATH

dnvm upgrade -a dotnet_stage0

VER=`dnvm alias dotnet_stage0`
mv $STAGE0_DIR/sdks/$VER $STAGE0_DIR/bin
