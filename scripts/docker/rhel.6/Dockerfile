#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Dockerfile that creates a container suitable to build dotnet-cli
FROM microsoft/dotnet-buildtools-prereqs:centos-6-376e1a3-20174311014331

# yum doesn't work with the special curl version we have in the base docker image,
# so we remove /usr/local/lib from the library path for this command
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

# Set library path to make CURL and ICU libraries that are in /usr/local/lib visible
ENV LD_LIBRARY_PATH /usr/local/lib

# Set working directory
WORKDIR /opt/code

