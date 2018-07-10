#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Dockerfile that creates a container suitable to build dotnet-cli
FROM microsoft/dotnet-buildtools-prereqs:alpine-3.6-3148f11-20171119021156

RUN apk update && apk upgrade && apk add --no-cache curl ncurses

# This Dockerfile doesn't use the USER_ID, but the parameter needs to be declared to prevent docker
# from issuing a warning
ARG USER_ID=0

# Set working directory
WORKDIR /opt/code
