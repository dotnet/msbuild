#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Dockerfile that creates a container suitable to build dotnet-cli
FROM debian:jessie

# This could become a "microsoft/coreclr" image, since it just installs the dependencies for CoreCLR (and stdlib)
# Install CoreCLR dependencies
RUN apt-get update && \
    apt-get -qqy install unzip curl libicu-dev libunwind8 gettext libssl-dev libcurl3-gnutls zlib1g

# Install Build Prereqs
RUN echo "deb http://llvm.org/apt/trusty/ llvm-toolchain-trusty-3.6 main" | tee /etc/apt/sources.list.d/llvm.list && \
    curl http://llvm.org/apt/llvm-snapshot.gpg.key | apt-key add - && \
    apt-get update && \
    apt-get install -y debhelper build-essential devscripts git liblttng-ust-dev lldb-3.6-dev lldb-3.6 clang cmake

# Use clang as c++ compiler
RUN update-alternatives --set c++ /usr/bin/clang++

# Install azure cli. We need this to publish artifacts.
RUN apt-get -y install nodejs-legacy && \
    apt-get -y install npm && \
    npm install -g azure-cli


RUN apt-get install -qqy sudo

# Setup User to match Host User, and give superuser permissions
ARG USER_ID=0
RUN useradd code_executor -u ${USER_ID} -g sudo
RUN echo 'code_executor ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers

# With the User Change, we need to change permssions on these directories
RUN chmod -R a+rwx /usr/local
RUN chmod -R a+rwx /home
RUN chmod -R 755 /usr/lib/sudo

# Set user to the one we just created
USER ${USER_ID}

# Set working directory
WORKDIR /opt/code
