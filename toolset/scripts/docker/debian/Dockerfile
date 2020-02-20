#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Dockerfile that creates a container suitable to build dotnet-cli
FROM mcr.microsoft.com/dotnet-buildtools/prereqs:debian-jessie-debpkg-58e4974-20190328133153

# Misc Dependencies for build
RUN rm -rf /var/lib/apt/lists/* && \
    apt-get update && \
    apt-get -qqy install \
        sudo && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Setup User to match Host User, and give superuser permissions
ARG USER_ID=0
RUN useradd -m code_executor -u ${USER_ID} -g sudo
RUN echo 'code_executor ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers

# With the User Change, we need to change permissions on these directories
RUN chmod -R a+rwx /usr/local
RUN chmod -R a+rwx /home
RUN chmod -R 755 /usr/lib/sudo

# Set user to the one we just created
USER ${USER_ID}

# Set working directory
WORKDIR /opt/code
