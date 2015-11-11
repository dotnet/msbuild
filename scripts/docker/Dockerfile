# Dockerfile that creates a container suitable to build dotnet-cli
FROM debian:jessie

# This could become a "microsoft/coreclr" image, since it just installs the dependencies for CoreCLR (and stdlib)
# Install CoreCLR dependencies
RUN apt-get update && \
    apt-get -qqy install unzip curl libicu-dev libunwind8 gettext libssl-dev libcurl3-gnutls zlib1g

# No longer need to install DNX since it is embedded (and soon will be gone!)

# Install Build Prereqs
RUN echo "deb http://llvm.org/apt/trusty/ llvm-toolchain-trusty-3.6 main" | tee /etc/apt/sources.list.d/llvm.list && \
    curl http://llvm.org/apt/llvm-snapshot.gpg.key | apt-key add - && \
    apt-get update && \
    apt-get install -y debhelper build-essential devscripts git liblttng-ust-dev lldb-3.6-dev lldb-3.6 clang cmake

# Use clang as c++ compiler
RUN update-alternatives --set c++ /usr/bin/clang++

# Install azure cli. We need this to publish atrifacts.
RUN apt-get -y install nodejs-legacy && \
    apt-get -y install npm && \
    npm install -g azure-cli

# Set working directory
WORKDIR /opt/code
