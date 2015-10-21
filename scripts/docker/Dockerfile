# Dockerfile that creates a container suitable to build dotnet-cli
FROM microsoft/aspnet:1.0.0-beta8-coreclr

# Temporary: Install Mono, we use MCS to bootstrap
RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF 2>&1 && \
    echo "deb http://download.mono-project.com/repo/debian nightly main" > /etc/apt/sources.list.d/mono-nightly.list && \
    apt-get -qqy update && \
    apt-get -qqy install mono-complete

# Set working directory
WORKDIR /opt/code
