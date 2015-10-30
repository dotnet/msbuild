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
    apt-get install -y debhelper build-essential devscripts git liblttng-ust-dev lldb-3.6-dev lldb-3.6 clang

# Install latest cmake
RUN mkdir -p /opt/src/cmake && \
    cd /opt/src/cmake && \
    curl -O https://cmake.org/files/v3.4/cmake-3.4.0-rc2-Linux-x86_64.tar.gz && \
    tar xzf cmake-3.4.0-rc2-Linux-x86_64.tar.gz && \
    cd cmake-3.4.0-rc2-Linux-x86_64 && \
    ln -s /opt/src/cmake/cmake-3.4.0-rc2-Linux-x86_64/bin/cmake /usr/local/bin/cmake

# Use clang as c++ compiler
RUN update-alternatives --set c++ /usr/bin/clang++

# Set working directory
WORKDIR /opt/code
