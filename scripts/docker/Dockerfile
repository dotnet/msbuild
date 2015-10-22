# Dockerfile that creates a container suitable to build dotnet-cli
FROM microsoft/aspnet:1.0.0-beta8-coreclr

RUN apt-get update
RUN apt-get install -y wget

# Install Package Build Prereqs
RUN echo "deb http://llvm.org/apt/trusty/ llvm-toolchain-trusty-3.6 main" | tee /etc/apt/sources.list.d/llvm.list
RUN wget -O - http://llvm.org/apt/llvm-snapshot.gpg.key | apt-key add -
RUN apt-get update
RUN apt-get install -y debhelper build-essential devscripts git liblttng-ust-dev lldb-3.6-dev

# Set working directory
WORKDIR /opt/code
