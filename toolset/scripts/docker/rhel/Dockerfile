#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Dockerfile that creates a container suitable to build dotnet-cli
FROM microsoft/dotnet-buildtools-prereqs:rhel-7-rpmpkg-e1b4a89-20175311035359

# Install repository configuration
RUN curl https://packages.microsoft.com/config/rhel/7/prod.repo > ./microsoft-prod.repo
RUN cp ./microsoft-prod.repo /etc/yum.repos.d/

# Install Microsoft's GPG public key
RUN curl https://packages.microsoft.com/keys/microsoft.asc > ./microsoft.asc
RUN rpm --import ./microsoft.asc

# Setup User to match Host User, and give superuser permissions
ARG USER_ID=0
RUN useradd -m code_executor -u ${USER_ID} -g root
RUN echo 'code_executor ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers

# With the User Change, we need to change permssions on these directories
RUN chmod -R a+rwx /usr/local
RUN chmod -R a+rwx /home
RUN chown root:root /usr/bin/sudo && chmod 4755 /usr/bin/sudo

# Set user to the one we just created
USER ${USER_ID}

# Set working directory
WORKDIR /opt/code
