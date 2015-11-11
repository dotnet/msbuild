#!/usr/bin/env sh

#setup some colors to use. These need to work in fairly limited shells, like the Ubuntu Docker container where there are only 8 colors.
#See if stdout is a terminal
if [ -t 1 ]; then
    # see if it supports colors
    ncolors=$(tput colors)
    if [ -n "$ncolors" ] && [ $ncolors -ge 8 ]; then
        bold="$(tput bold)"
        normal="$(tput sgr0)"
        black="$(tput setaf 0)"
        red="$(tput setaf 1)"
        green="$(tput setaf 2)"
        yellow="$(tput setaf 3)"
        blue="$(tput setaf 4)"
        magenta="$(tput setaf 5)"
        cyan="$(tput setaf 6)"
        white="$(tput setaf 7)"
    fi
fi

#Standardise OS name to what is put into filenames of the tarballs.
__current_os()
{
    local uname=$(uname)
    if [ "$uname" = "Darwin" ]; then
        echo "osx"
    else
        echo "linux"
    fi
}

__machine_has() {
    type "$1" > /dev/null 2>&1
    return $?
}

#Not 100% sure at the moment that these checks are enough. We might need to take version into account or do something
#more complicated. This seemed like a good beginning though as it should catch the default "clean" machine case and give
#people an appropriate hint.
__check_pre_reqs() {
    local os=$(__current_os)
    local _failing=false;
    if [ "$os" = "linux" ]; then
        [ -z "$(ldconfig -p | grep libunwind)" ] && say_err "Unable to locate libunwind. Install libunwind to continue" && _failing=true
        [ -z "$(ldconfig -p | grep libssl)" ] && say_err "Unable to locate libssl. Install libssl to continue" && _failing=true
        [ -z "$(ldconfig -p | grep libcurl)" ] && say_err "Unable to locate libcurl. Install libcurl to continue" && _failing=true
        [ -z "$(ldconfig -p | grep libicu)" ] && say_err "Unable to locate libicu. Install libicu to continue" && _failing=true
        [ -z "$(ldconfig -p | grep gettext)" ] && say_err "Unable to locate gettext. Install gettext to continue" && _failing=true
    fi
    
    if [ "$_failing" = true ]; then
       return 1
    fi
}

say_err() {
    printf "%b\n" "${red}dotnet_install: Error: $1${normal}" >&2
}

say() {
    printf "%b\n" "dotnet_install: $1"
}

_install_dotnet()
{
    if ! __machine_has "curl"; then
        printf "%b\n" "${red}curl is required to download dotnet. Install curl to proceed. ${normal}" >&2;
        return 1
    fi

    if [ -e "/usr/local/share/dotnet/cli/dotnet" ] && [ ! -w "/usr/local/share/dotnet/cli/dotnet" ]; then
        say_err "dotnet cli is already installed and not writeable. Use 'curl -sSL <url> | sudo sh' to force install."
        say_err "If you have previously installed the cli using a pkg then that is why it is write protected, and you need to run sudo to install the new version."
        say_err "Alternatively, removing the '/usr/local/share/dotnet' directory completely before running the script will also resolve the issue."
        return 1
    fi

    __check_pre_reqs
    if [ $? != 0 ]; then
        say_err "Ending install due to missing pre-reqs"
        return 1;
    fi

    local installLocation="/usr/local/share/dotnet"
    local dotnet_url="https://dotnetcli.blob.core.windows.net/dotnet/dev/Binaries/Latest"
    local dotnet_filename="dotnet-$(__current_os)-x64.latest.tar.gz"

    #This should noop if the directory already exists.
    mkdir -p $installLocation

    say "Downloading $dotnet_filename from $dotnet_url"

    #Download file and check status code, error and return if we cannot download a cli tar.
    local httpResult=$(curl -L -D - "$dotnet_url/$dotnet_filename" -o "$installLocation/$dotnet_filename" -# | grep "^HTTP/1.1" | head -n 1 | sed "s/HTTP.1.1 \([0-9]*\).*/\1/")
    [ $httpResult -ne "302" ] && [ $httpResult -ne "200" ] && echo "${Red}HTTP Error $httpResult fetching the dotnet cli from $dotnet_url ${RCol}" && return 1

    say "Extracting tarball"
    #Any of these could fail for various reasons so we will check each one and end the script there if it fails.
    rm -rf "$installLocation/cli_new"
    mkdir "$installLocation/cli_new"
    [ $? != 0 ] && say_err "failed to clean and create temporary cli directory to extract into" && return 1
    
    tar -xzf "$installLocation/$dotnet_filename" -C "$installLocation/cli_new"
    [ $? != 0 ] && say_err "failed to extract tar" && return 1
    
    say "Moving new CLI into install location and symlinking"

    rm -rf "$installLocation/cli"
    [ $? != 0 ] && say_err "Failed to clean current dotnet install" && return 1

    mv "$installLocation/cli_new" "$installLocation/cli"

    for f in $(find "$installLocation/cli" -regex ".*/dotnet[a-z\-]*$")
    do
        local baseFile=$(basename $f)
        if [ -e "/usr/local/bin/$baseFile" ]; then
            say "${yellow}$baseFile already exists in /usr/local/bin. Skipping symlink...${normal}"
        else
            say "linking $baseFile"
            ln -s $f /usr/local/bin/
        fi
    done

    say "Cleaning $dotnet_filename"
    rm "$installLocation/$dotnet_filename"
    [ $? != 0 ] && say_err "Failed to delete tar after extracting." && return 1
}

_install_dotnet