# Dockerfile that creates a container suitable to build dotnet-cli
FROM debian:jessie

# This could become a "microsoft/coreclr" image, since it just installs the dependencies for CoreCLR (and stdlib)
# Install CoreCLR dependencies (and clean APT cache)
RUN apt-get update && \
    apt-get -qqy install unzip curl libicu-dev libunwind8 gettext libssl-dev libcurl3-gnutls zlib1g && \
    rm -rf /var/lib/apt/lists/*

# No longer need to install DNX since it is embedded (and soon will be gone!)

# Install Package Build Prereqs (and clean up APT cache after to save space in the image)
RUN echo "deb http://llvm.org/apt/trusty/ llvm-toolchain-trusty-3.6 main" | tee /etc/apt/sources.list.d/llvm.list && \
    curl http://llvm.org/apt/llvm-snapshot.gpg.key | apt-key add - && \
    apt-get update && \
    apt-get install -y debhelper build-essential devscripts git liblttng-ust-dev lldb-3.6-dev && \
    rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /opt/code
