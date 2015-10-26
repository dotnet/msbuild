#!/usr/bin/env bash
#
# Prerequisites:
#   Git Bash (http://www.git-scm.com/downloads)
#   Docker Toolbox (https://www.docker.com/docker-toolbox)
#   Ensure Hyper-V is disabled!

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

# This function is necessary to bypass POSIX Path Conversion in Git Bash
# http://www.mingw.org/wiki/Posix_path_conversion
_convert_path(){
    local path=$1
    path=$( echo "$path" | sed -r 's/[\/]+/\\/g')
    path=${path#\\}
    path=//$path

    echo $path
}

# Bypass Msys path conversion
REPO_ROOT=$(readlink -f $DIR/..)
REPO_ROOT=$(_convert_path $REPO_ROOT)

VM_NAME="dotnet"
VM_CODE_DIR="/home/docker/code"

RESULTS_DIR="$REPO_ROOT/artifacts"

execute(){
    check_prereqs

    echo "Setting up VM..."
    create_or_start_vm

    echo "Copying code from Host to VM"
    eval $(docker-machine env --shell bash $VM_NAME)
    copy_code_to_vm

    echo "Running Build in Docker Container"
    run_build

    echo "Copying Results from VM to Hosts..."
    copy_results_from_vm
}

check_prereqs(){
    if ! which docker; then
        echo "Error: Install docker toolbox (https://www.docker.com/docker-toolbox)"
        exit 1
    fi 

    if ! which docker-machine; then
        echo "Error: Install docker toolbox (https://www.docker.com/docker-toolbox)"
        exit 1
    fi

}

create_or_start_vm(){

    if [[ $(docker-machine ls | grep $VM_NAME) == "" ]]; then
        docker-machine create -d virtualbox $VM_NAME
    else
        docker-machine start $VM_NAME
    fi

}

copy_code_to_vm(){
    docker-machine ssh $VM_NAME "sudo rm -rf $VM_CODE_DIR"
    docker-machine scp -r $REPO_ROOT $VM_NAME:$VM_CODE_DIR >> /dev/null 2>&1
}


run_build(){
    # These are env variables for dockerbuild.sh
    export DOCKER_HOST_SHARE_DIR="$(_convert_path $VM_CODE_DIR)"
    export BUILD_COMMAND="//opt\\code\\build.sh"

    $DIR/dockerbuild.sh debian
}

# This will duplicate the entire repo + any side effects from
# the operations in the docker container
copy_results_from_vm(){
    T_RESULTS_DIR=$( echo "$RESULTS_DIR" | sed -r 's/[\\]+/\//g')
    T_RESULTS_DIR=${T_RESULTS_DIR#/}

    mkdir $T_RESULTS_DIR
    docker-machine ssh $VM_NAME "sudo chmod -R a+rx $VM_CODE_DIR"
    docker-machine scp -r $VM_NAME:$VM_CODE_DIR/artifacts $REPO_ROOT >> /dev/null 2>&1
}

execute

