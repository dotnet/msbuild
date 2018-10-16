#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Dockerfile that creates a container suitable to build dotnet-cli
FROM centos:7.1.1503

# Swap the "fakesystemd" package with the real "systemd" package, because fakesystemd conflicts with openssl-devel.
# The CentOS Docker image uses fakesystemd instead of systemd to reduce disk space.
RUN yum -q -y swap -- remove fakesystemd -- install systemd systemd-libs

RUN yum -q -y install tar git

RUN yum -q -y install sudo

# Setup User to match Host User, and give superuser permissions
ARG USER_ID=0
RUN useradd -m code_executor -u ${USER_ID} -g root
RUN echo 'code_executor ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers

# With the User Change, we need to change permssions on these directories
RUN chmod -R a+rwx /usr/local
RUN chmod -R a+rwx /home
RUN chmod -R 755 /usr/bin/sudo

# Set user to the one we just created
USER ${USER_ID}

# Set working directory
WORKDIR /opt/code
