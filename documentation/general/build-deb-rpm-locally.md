# Build Deb or Rpm locally

We do not need to build Debian or RPM locally too often. However, if we need, it is not productive to check in and wait for a build since the feedback loop could be 2 hours for a typo. The following guide is a quick solution if you don't have an existing environment setup.

## Use a VM

It is good to have an editor on the machine. So you can edit and run in the same environment. Setting up git credentials might be an hassle you could edit on the host machine, push to github and the pull from the VM. Prefer VM if you know it would take a while to build and test the feature. 

### For Deb

The build script has not been updated since Ubuntu 16.04. Due to dependencies like Python 2 on the path, you cannot easily build on later versions. 

- Download and install ubuntu 16.04 ios file on a hypervisor
- install all apt-get dependency following https://github.com/dotnet/installer/blob/main/eng/docker/ubuntu.16.04/Dockerfile. debhelper and build-essential are must have, since the build script would not build deb if the depending softwares are not present on the machine.
- Once all installed, run `./build.sh --pack` to create deb.

### For Rpm

Unfortunately, old Fedoras are out of support, and you cannot easily download RHEL images. You could try Fedora 24. https://github.com/dotnet/cli/blob/release/2.2.4xx/scripts/docker/fedora.24/Dockerfile

## User docker

If you are on Windows, try to run docker in a Linux VM. Do not mount drive because you cannot build sdk successfully if you build on an mounted Windows drive due to a file permission issue.

- cd to docker folder locally for example https://github.com/dotnet/installer/tree/main/eng/docker
- Use and editor remove everything under `ARG USER_ID=0 ` locally in the DockerFile. For example `~/Documents/installer/eng/docker/rhel/DockerFile`. The lines to be deleted are for special tests (Install sudo and create a user account, so that it can test package installation). Without deleting the lines, you cannot build the docker file locally.
- Say you want to test Rpm, `docker build rhel/ -t image`
- `docker run -v /home/HOSTMOUNTFOLDER:/home/installer -it --name image2 image /bin/bash`
- After `git pull` the new build, run `./build.sh --pack -bl`. You should be able to inspect files on the host mounted drive easily. More importantly, it is easy for you to copy binlog out to a Windows machine to read.
