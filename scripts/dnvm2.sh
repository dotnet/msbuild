# dnvm.sh
# Source this file from your .bash-profile or script to use

# "Constants"
_DNVM_BUILDNUMBER="{{BUILD_VERSION}}"
_DNVM_AUTHORS="{{AUTHORS}}"
_DNVM_RUNTIME_PACKAGE_NAME="dotnet"
_DNVM_RUNTIME_FRIENDLY_NAME=".NET CLI"
_DNVM_RUNTIME_SHORT_NAME="dotnet"
_DNVM_RUNTIME_FOLDER_NAME=".dotnet"
_DNVM_COMMAND_NAME="dnvm"
_DNVM_PACKAGE_MANAGER_NAME="dnu"
_DNVM_VERSION_MANAGER_NAME=".NET Version Manager"
_DNVM_DEFAULT_FEED="https://dotnetcli.blob.core.windows.net/dotnet"
_DNVM_DEFAULT_CHANNEL="dev"
_DNVM_DEFAULT_UNSTABLE_CHANNEL="dev"
_DNVM_UPDATE_LOCATION="https://raw.githubusercontent.com/aspnet/Home/dev/dnvm.sh"
_DNVM_INSTALL_SUBFOLDER="sdks"

if [ "$NO_COLOR" != "1" ]; then
    # ANSI Colors
    RCol='\e[0m'    # Text Reset

    # Regular           Bold                Underline           High Intensity      BoldHigh Intens     Background          High Intensity Backgrounds
    Bla='\e[0;30m';     BBla='\e[1;30m';    UBla='\e[4;30m';    IBla='\e[0;90m';    BIBla='\e[1;90m';   On_Bla='\e[40m';    On_IBla='\e[0;100m';
    Red='\e[0;31m';     BRed='\e[1;31m';    URed='\e[4;31m';    IRed='\e[0;91m';    BIRed='\e[1;91m';   On_Red='\e[41m';    On_IRed='\e[0;101m';
    Gre='\e[0;32m';     BGre='\e[1;32m';    UGre='\e[4;32m';    IGre='\e[0;92m';    BIGre='\e[1;92m';   On_Gre='\e[42m';    On_IGre='\e[0;102m';
    Yel='\e[0;33m';     BYel='\e[1;33m';    UYel='\e[4;33m';    IYel='\e[0;93m';    BIYel='\e[1;93m';   On_Yel='\e[43m';    On_IYel='\e[0;103m';
    Blu='\e[0;34m';     BBlu='\e[1;34m';    UBlu='\e[4;34m';    IBlu='\e[0;94m';    BIBlu='\e[1;94m';   On_Blu='\e[44m';    On_IBlu='\e[0;104m';
    Pur='\e[0;35m';     BPur='\e[1;35m';    UPur='\e[4;35m';    IPur='\e[0;95m';    BIPur='\e[1;95m';   On_Pur='\e[45m';    On_IPur='\e[0;105m';
    Cya='\e[0;36m';     BCya='\e[1;36m';    UCya='\e[4;36m';    ICya='\e[0;96m';    BICya='\e[1;96m';   On_Cya='\e[46m';    On_ICya='\e[0;106m';
    Whi='\e[0;37m';     BWhi='\e[1;37m';    UWhi='\e[4;37m';    IWhi='\e[0;97m';    BIWhi='\e[1;97m';   On_Whi='\e[47m';    On_IWhi='\e[0;107m';
fi


[[ "$_DNVM_BUILDNUMBER" = {{* ]] && _DNVM_BUILDNUMBER="HEAD"

__dnvm_has() {
    type "$1" > /dev/null 2>&1
    return $?
}

if __dnvm_has "unsetopt"; then
    unsetopt nomatch 2>/dev/null
fi

if [ -z "$DOTNET_USER_HOME" ]; then
    eval DOTNET_USER_HOME="~/$_DNVM_RUNTIME_FOLDER_NAME"
fi

if [ -z "$DOTNET_GLOBAL_HOME" ]; then
    eval DOTNET_GLOBAL_HOME="/usr/local/lib/dotnet"
fi

if [ -z "$DOTNET_HOME" ]; then
    # Set to the user home value
    eval DOTNET_HOME="$DOTNET_USER_HOME:$DOTNET_GLOBAL_HOME"
elif [[ $DOTNET_HOME != *"$DOTNET_GLOBAL_HOME"* ]]; then
    eval DOTNET_HOME="$DOTNET_HOME:$DOTNET_GLOBAL_HOME"
fi

_DNVM_USER_PACKAGES="$DOTNET_USER_HOME/$_DNVM_INSTALL_SUBFOLDER"
_DNVM_GLOBAL_PACKAGES="$DOTNET_GLOBAL_HOME/$_DNVM_INSTALL_SUBFOLDER"
_DNVM_ALIAS_DIR="$DOTNET_USER_HOME/alias"
_DNVM_DNVM_DIR="$DOTNET_USER_HOME/dnvm"

DNX_ACTIVE_FEED=""
DNX_ACTIVE_CHANNEL=""

__dnvm_current_os()
{
    local uname=$(uname)
    if [[ $uname == "Darwin" ]]; then
        echo "osx"
    else
        echo "linux"
    fi
}

__dnvm_find_latest() {
    local arch=$1
    local os=$2

    if ! __dnvm_has "curl"; then
        printf "%b\n" "${Red}$_DNVM_COMMAND_NAME needs curl to proceed. ${RCol}" >&2;
        return 1
    fi

    #dnx-coreclr-linux-x64
    local packageId="$_DNVM_RUNTIME_PACKAGE_NAME-$os-$arch"

    local url="$DNX_ACTIVE_FEED/$DNX_ACTIVE_CHANNEL/dnvm/latest.$os.index"
    local index="$(curl $url 2>/dev/null)"

    local version="$(export IFS=; echo $index | sed -n "s/^.*$packageId\.\(.*\)\.tar\.gz$/\1/p")"

    local downloadUrl="$DNX_ACTIVE_FEED/$DNX_ACTIVE_CHANNEL/$index"

    echo $version $downloadUrl
}

__dnvm_find_package() {
    local arch=$1
    local os=$2
    local version=$3

    #dnx-coreclr-linux-x64
    local packageId="$_DNVM_RUNTIME_PACKAGE_NAME-$os-$arch"

    local url="$DNX_ACTIVE_FEED/$DNX_ACTIVE_CHANNEL/dnvm/index"
    local index="$(curl $url 2>/dev/null)"

    local filename="$(export IFS=; echo $index | sed -n "s/Filename: \(.*$packageId.$version\)/\1/p")"
    local packageUrl="$DNX_ACTIVE_FEED/$DNX_ACTIVE_CHANNEL/$filename"
    echo $packageUrl
}

__dnvm_strip_path() {
    echo "$1" | sed -e "s#$_DNVM_USER_PACKAGES/[^/]*$2[^:]*:##g" -e "s#:$_DNVM_USER_PACKAGES/[^/]*$2[^:]*##g" -e "s#$_DNVM_USER_PACKAGES/[^/]*$2[^:]*##g" | sed -e "s#$_DNVM_GLOBAL_PACKAGES/[^/]*$2[^:]*:##g" -e "s#:$_DNVM_GLOBAL_PACKAGES/[^/]*$2[^:]*##g" -e "s#$_DNVM_GLOBAL_PACKAGES/[^/]*$2[^:]*##g"
}

__dnvm_prepend_path() {
    if [ -z "$1" ]; then
        echo "$2"
    else
        echo "$2:$1"
    fi
}

__dnvm_package_version() {
    local runtimeFullName="$1"
    echo "$runtimeFullName" | sed "s/[^.]*.\(.*\)/\1/"
}

__dnvm_package_name() {
    local runtimeFullName="$1"
    echo "$runtimeFullName" | sed "s/\([^.]*\).*/\1/"
}

__dnvm_package_arch() {
    local runtimeFullName="$1"
    echo "$runtimeFullName" | sed "s/$_DNVM_RUNTIME_PACKAGE_NAME-[^-.]*-\([^-.]*\)\..*/\1/"
}

__dnvm_package_os() {
    local runtimeFullName="$1"
    echo "$runtimeFullName" | sed "s/$_DNVM_RUNTIME_PACKAGE_NAME-\([^.-]*\).*/\1/"
}

__dnvm_update_self() {
    local dnvmFileLocation="$_DNVM_DNVM_DIR/dnvm.sh"
    if [ ! -e $dnvmFileLocation ]; then
        local formattedDnvmFileLocation=`(echo $dnvmFileLocation | sed s=$HOME=~=g)`
        local formattedDnvmHome=`(echo $_DNVM_DNVM_DIR | sed s=$HOME=~=g)`
        local bashSourceLocation=${BASH_SOURCE}
        local scriptLocation=$bashSourceLocation
        if [ -z "${bashSourceLocation}" ]; then
          local scriptLocation=${(%):-%x}
        fi
        printf "%b\n" "${Red}$formattedDnvmFileLocation doesn't exist. This command assumes you have installed dnvm in the usual location and are trying to update it. If you want to use update-self then dnvm.sh should be sourced from $formattedDnvmHome. dnvm is currently sourced from $scriptLocation ${RCol}"
        return 1
    fi
    printf "%b\n" "${Cya}Downloading dnvm.sh from $_DNVM_UPDATE_LOCATION ${RCol}"
    local httpResult=$(curl -L -D - "$_DNVM_UPDATE_LOCATION" -o "$dnvmFileLocation" -# | grep "^HTTP/1.1" | head -n 1 | sed "s/HTTP.1.1 \([0-9]*\).*/\1/")

    [[ $httpResult == "404" ]] &&printf "%b\n" "${Red}404. Unable to download DNVM from $_DNVM_UPDATE_LOCATION ${RCol}" && return 1
    [[ $httpResult != "302" && $httpResult != "200" ]] && echo "${Red}HTTP Error $httpResult fetching DNVM from $_DNVM_UPDATE_LOCATION ${RCol}" && return 1

    source "$dnvmFileLocation"
}

__dnvm_promptSudo() {
    local acceptSudo="$1"

    local answer=
    if [ "$acceptSudo" == "0" ]; then
        echo "In order to install dnx globally, dnvm will have to temporarily run as root."
        read -p "You may be prompted for your password via 'sudo' during this process. Is this Ok? (y/N) " answer
    else
        answer="y"
    fi
    if echo $answer | grep -iq "^y" ; then
        return 1
    else
        return 0
    fi
}

__dnvm_download() {
    local runtimeFullName="$1"
    local downloadUrl="$2"
    local runtimeFolder="$3"
    local acceptSudo="$4"

    #todo: This will need to change to be whatever the filename in the index is.    
    local runtimeFile="$runtimeFolder/$runtimeFullName.tar.gz"

    if [ -e "$runtimeFolder" ]; then
       printf "%b\n" "${Gre}$runtimeFullName already installed. ${RCol}"
        return 0
    fi

    if ! __dnvm_has "curl"; then
       printf "%b\n" "${Red}$_DNVM_COMMAND_NAME needs curl to proceed. ${RCol}" >&2;
        return 1
    fi

    local useSudo=
    mkdir -p "$runtimeFolder" > /dev/null 2>&1
    if [ ! -d $runtimeFolder ]; then
        if ! __dnvm_promptSudo $acceptSudo ; then
            useSudo=sudo
            sudo mkdir -p "$runtimeFolder" > /dev/null 2>&1 || return 1
        else
            return 1
        fi
    fi
    echo "Downloading $runtimeFullName from $DNX_ACTIVE_FEED"
    echo "Download: $downloadUrl to $runtimeFile"

    local httpResult=$($useSudo curl -L -D - "$downloadUrl" -o "$runtimeFile" -# | grep "^HTTP/1.1" | head -n 1 | sed "s/HTTP.1.1 \([0-9]*\).*/\1/")

    if [[ $httpResult == "404" ]]; then
        printf "%b\n" "${Red}$runtimeFullName was not found in repository $DNX_ACTIVE_FEED ${RCol}"
        printf "%b\n" "${Cya}This is most likely caused by the feed not having the version that you typed. Check that you typed the right version and try again. Other possible causes are the feed doesn't have a $_DNVM_RUNTIME_SHORT_NAME of the right name format or some other error caused a 404 on the server.${RCol}"
        return 1
    fi
    [[ $httpResult != "302" && $httpResult != "200" ]] && echo "${Red}HTTP Error $httpResult fetching $runtimeFullName from $DNX_ACTIVE_FEED ${RCol}" && return 1

    __dnvm_unpack $runtimeFile $runtimeFolder $useSudo
    return $?
}

__dnvm_unpack() {
    local runtimeFile="$1"
    local runtimeFolder="$2"
    local useSudo=$3

    echo "Installing to $runtimeFolder"

    if ! __dnvm_has "unzip"; then
        echo "$_DNVM_COMMAND_NAME needs unzip to proceed." >&2;
        return 1
    fi

    $useSudo tar -xzf $runtimeFile -C $runtimeFolder > /dev/null 2>&1
}

__dnvm_requested_version_or_alias() {
    local versionOrAlias="$1"
    local arch="$2"
    local os="$3"
    local runtimeBin=$(__dnvm_locate_runtime_bin_from_full_name "$versionOrAlias")

    # If the name specified is an existing package, just use it as is
    if [ -n "$runtimeBin" ]; then
        echo "$versionOrAlias"
    else
        if [ -e "$_DNVM_ALIAS_DIR/$versionOrAlias.alias" ]; then
            local runtimeFullName=$(cat "$_DNVM_ALIAS_DIR/$versionOrAlias.alias")
            if [[ ! -n "$arch" ]]; then
                echo "$runtimeFullName"
                return
            fi
            local pkgVersion=$(__dnvm_package_version "$runtimeFullName")
        fi

        if [[ ! -n "$pkgVersion" ]]; then
            local pkgVersion=$versionOrAlias
        fi
        local pkgArchitecture="x64"
        local pkgSystem=$os

        if [ "$arch" != "" ]; then
            local pkgArchitecture="$arch"
        fi
        if [ "$os" == "" ]; then
            local pkgSystem=$(__dnvm_current_os)
        fi

        echo "$_DNVM_RUNTIME_PACKAGE_NAME-$pkgSystem-$pkgArchitecture.$pkgVersion"
    fi
}

# This will be more relevant if we support global installs
__dnvm_locate_runtime_bin_from_full_name() {
    local runtimeFullName=$1
    for v in `echo $DOTNET_HOME | tr ":" "\n"`; do
        if [ -e "$v/$_DNVM_INSTALL_SUBFOLDER/$runtimeFullName" ]; then
            echo "$v/$_DNVM_INSTALL_SUBFOLDER/$runtimeFullName" && return
        fi
    done
}

__echo_art() {
  printf "%b" "${Cya}"
    echo "    ___  _  ___   ____  ___"
    echo "   / _ \/ |/ / | / /  |/  /"
    echo "  / // /    /| |/ / /|_/ / "
    echo " /____/_/|_/ |___/_/  /_/  "
   printf "%b" "${RCol}"
}

__dnvm_description() {
    __echo_art
    echo ""
    echo "$_DNVM_VERSION_MANAGER_NAME - Version 1.0.0-$_DNVM_BUILDNUMBER"
    [[ "$_DNVM_AUTHORS" != {{* ]] && echo "By $_DNVM_AUTHORS"
    echo ""
    echo "DNVM can be used to download versions of the $_DNVM_RUNTIME_FRIENDLY_NAME and manage which version you are using."
    echo "You can control the URL of the stable and unstable channel by setting the DOTNET_FEED and DNX_UNSTABLE_FEED variables."
    echo ""
   printf "%b\n" "${Yel}Current feed settings:${RCol}"
   printf "%b\n" "${Cya}Feed:${Yel} $_DNVM_DEFAULT_FEED"
   printf "%b\n" "${Cya}Default Channel:${Yel} $_DNVM_DEFAULT_CHANNEL"
   printf "%b\n" "${Cya}Unstable Channel:${Yel} $_DNVM_DEFAULT_UNSTABLE_CHANNEL"

   local dnxStableOverride="<none>"
   [[ -n $DOTNET_FEED ]] && dnxStableOverride="$DOTNET_FEED"

   printf "%b\n" "${Cya}Current Override Feed:${Yel} $dnxStableOverride"

   local dnxUnstableOverride="<none>"
   [[ -n $DNX_UNSTABLE_FEED ]] && dnxUnstableOverride="$DNX_UNSTABLE_FEED"

   #printf "%b\n" "${Cya}Current Unstable Override:${Yel} $dnxUnstableOverride${RCol}"
    echo ""

}

__dnvm_version() {
   echo "1.0.0-$_DNVM_BUILDNUMBER"
}

__dnvm_help() {
    __dnvm_description
   printf "%b\n" "${Cya}USAGE:${Yel} $_DNVM_COMMAND_NAME <command> [options] ${RCol}"
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME upgrade [-f|-force] [-u|-unstable] [-g|-global] [-y]${RCol}"
    echo "  install latest $_DNVM_RUNTIME_SHORT_NAME from feed"
    echo "  adds $_DNVM_RUNTIME_SHORT_NAME bin to path of current command line"
    echo "  set installed version as default"
    echo "  -f|forces         force upgrade. Overwrite existing version of $_DNVM_RUNTIME_SHORT_NAME if already installed"
    echo "  -u|unstable       use unstable feed. Installs the $_DNVM_RUNTIME_SHORT_NAME from the unstable feed"
    echo "  -g|global         Installs the latest $_DNVM_RUNTIME_SHORT_NAME in the configured global $_DNVM_RUNTIME_SHORT_NAME  file location (default: /usr/local/lib/dnx current: $DOTNET_GLOBAL_HOME)"
    echo "  -y                Assume Yes to all queries and do not prompt"
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME install <semver>|<alias>|<nupkg>|latest [-OS <OS>] [-a|-alias <alias>] [-p|-persistent] [-f|-force] [-u|-unstable] [-g|-global] [-y]${RCol}"
    echo "  <semver>|<alias>  install requested $_DNVM_RUNTIME_SHORT_NAME from feed"
    echo "  <nupkg>           install requested $_DNVM_RUNTIME_SHORT_NAME from local package on filesystem"
    echo "  latest            install latest version of $_DNVM_RUNTIME_SHORT_NAME from feed"
    echo "  -OS               the operating system that the runtime targets (default:$(__dnvm_current_os)"
    echo "  -a|-alias <alias> set alias <alias> for requested $_DNVM_RUNTIME_SHORT_NAME on install"
    echo "  -p|-persistent    set installed version as default"
    echo "  -f|force          force install. Overwrite existing version of $_DNVM_RUNTIME_SHORT_NAME if already installed"
    echo "  -u|unstable       use unstable feed. Installs the $_DNVM_RUNTIME_SHORT_NAME from the unstable feed"
    echo "  -g|global         Installs to the configured global $_DNVM_RUNTIME_SHORT_NAME file location (default: /usr/local/lib/dnx current: $DOTNET_GLOBAL_HOME)"
    echo "  -y                Assume Yes to all queries and do not prompt"
    echo ""
    echo "  adds $_DNVM_RUNTIME_SHORT_NAME bin to path of current command line"
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME use <semver>|<alias>|<package>|none [-p|-persistent] [-a|-arch <architecture>] ${RCol}"
    echo "  <semver>|<alias>|<package>  add $_DNVM_RUNTIME_SHORT_NAME bin to path of current command line   "
    echo "  none                        remove $_DNVM_RUNTIME_SHORT_NAME bin from path of current command line"
    echo "  -p|-persistent              set selected version as default"
    echo "  -a|-arch                    architecture to use (x64)"
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME run <semver>|<alias> <args...> ${RCol}"
    echo "  <semver>|<alias>            the version or alias to run"
    echo "  <args...>                   arguments to be passed to $_DNVM_RUNTIME_SHORT_NAME"
    echo ""
    echo "  runs the $_DNVM_RUNTIME_SHORT_NAME command from the specified version of the runtime without affecting the current PATH"
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME exec <semver>|<alias> <command> <args...> ${RCol}"
    echo "  <semver>|<alias>            the version or alias to execute in"
    echo "  <command>                   the command to run"
    echo "  <args...>                   arguments to be passed to the command"
    echo ""
    echo "  runs the specified command in the context of the specified version of the runtime without affecting the current PATH"
    echo "  example: $_DNVM_COMMAND_NAME exec 1.0.0-beta4 $_DNVM_PACKAGE_MANAGER_NAME build"
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME list [-detailed]${RCol}"
    echo "  -detailed                   display more detailed information on each runtime"
    echo ""
    echo "  list $_DNVM_RUNTIME_SHORT_NAME versions installed "
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME alias ${RCol}"
    echo "  list $_DNVM_RUNTIME_SHORT_NAME aliases which have been defined"
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME alias <alias> ${RCol}"
    echo "  display value of the specified alias"
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME alias <alias> <semver>|<alias>|<package> ${RCol}"
    echo "  <alias>                      the name of the alias to set"
    echo "  <semver>|<alias>|<package>   the $_DNVM_RUNTIME_SHORT_NAME version to set the alias to. Alternatively use the version of the specified alias"
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME unalias <alias> ${RCol}"
    echo "  remove the specified alias"
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME [help|-h|-help|--help] ${RCol}"
    echo "  displays this help text."
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME [version|-v|-version|--version] ${RCol}"
    echo "  print the dnvm version."
    echo ""
   printf "%b\n" "${Yel}$_DNVM_COMMAND_NAME update-self ${RCol}"
    echo "  updates dnvm itself."
}

dnvm()
{
    if [ $# -lt 1 ]; then
        __dnvm_description

        printf "%b\n" "Use ${Yel}$_DNVM_COMMAND_NAME [help|-h|-help|--help] ${RCol} to display help text."
        echo ""
        return
    fi

    case $1 in
        "help"|"-h"|"-help"|"--help" )
            __dnvm_help
        ;;

        "version"|"-v"|"-version"|"--version" )
            __dnvm_version
        ;;

        "update-self" )
            __dnvm_update_self
        ;;

        "upgrade" )
            shift
            $_DNVM_COMMAND_NAME install latest -p $@
        ;;

        "install" )
            [ $# -lt 2 ] && __dnvm_help && return
            shift
            local persistent=
            local versionOrAlias=
            local alias=
            local force=
            local unstable=
            local os=
            local arch=
            local global=0
            local acceptSudo=0
            while [ $# -ne 0 ]
            do
                if [[ $1 == "-p" || $1 == "-persistent" ]]; then
                    local persistent="-p"
                elif [[ $1 == "-a" || $1 == "-alias" ]]; then
                    local alias=$2
                    shift
                elif [[ $1 == "-f" || $1 == "-force" ]]; then
                    local force="-f"
                elif [[ $1 == "-u" || $1 == "-unstable" ]]; then
                    local unstable="-u"
                elif [[ $1 == "-OS" ]]; then
                    local os=$2
                    shift
                elif [[ $1 == "-y" ]]; then
                    local acceptSudo=1
                elif [[ $1 == "-arch" ]]; then
                    local arch=$2
                    shift

                    if [[ $arch != "x86" && $arch != "x64" ]]; then
                        printf "%b\n" "${Red}Architecture must be x86 or x64.${RCol}"
                        return 1
                    fi
                elif [[ $1 == "-g" || $1 == "-global" ]]; then
                    local global=1
                elif [[ -n $1 ]]; then
                    [[ -n $versionOrAlias ]] && echo "Invalid option $1" && __dnvm_help && return 1
                    local versionOrAlias=$1
                fi
                shift
            done

            if [[ $arch == "x86" ]]; then
                printf "%b\n" "${Red}$_DNVM_RUNTIME_FRIENDLY_NAME doesn't currently have a 32 bit build. You must use x64.${RCol}"
                return 1
            fi

            #This will be temporary whilst a more first class channels feature is added. There needs to be 3 values.
            if [ -z $unstable ]; then
                DNX_ACTIVE_CHANNEL="$_DNVM_DEFAULT_CHANNEL"
            else
                DNX_ACTIVE_CHANNEL="$_DNVM_DEFAULT_UNSTABLE_CHANNEL"
            fi

            echo "Using Channel: $DNX_ACTIVE_CHANNEL"

            DNX_ACTIVE_FEED="$DOTNET_FEED"
            if [ -z "$DNX_ACTIVE_FEED" ]; then
                DNX_ACTIVE_FEED="$_DNVM_DEFAULT_FEED"
            else
                printf "%b\n" "${Yel}Default feed ($_DNVM_DEFAULT_FEED) is being overridden by the value of the DOTNET_FEED variable ($DOTNET_FEED). ${RCol}"
            fi

            if [[ -z $os ]]; then
                os=$(__dnvm_current_os)
            fi
            if [[ $os == "darwin" ]]; then
                os="osx"
            fi

            if [[ -z $arch ]]; then
                arch="x64"
            fi

            local runtimeDir=$_DNVM_USER_PACKAGES
            if [ $global == 1 ]; then
                runtimeDir=$_DNVM_GLOBAL_PACKAGES
            fi

            if [[ "$versionOrAlias" != *.nupkg ]]; then
                if [[ "$versionOrAlias" == "latest" ]]; then
                   echo "Determining latest version"
                   read versionOrAlias downloadUrl < <(__dnvm_find_latest "$arch" "$os")
                   echo "DownloadURL: $downloadUrl"
                   [[ $? == 1 ]] && echo "Error: Could not find latest version from feed $DNX_ACTIVE_FEED" && return 1
                   printf "%b\n" "Latest version is ${Cya}$versionOrAlias located at $downloadUrl${RCol}"
                else
                    local runtimeFullName=$(__dnvm_requested_version_or_alias "$versionOrAlias" "$arch" "$os")
                    local runtimeVersion=$(__dnvm_package_version "$runtimeFullName")
                    local versionOrAlias=$runtimeVersion
                    local downloadUrl=$(__dnvm_find_package "$arch" "$os" "$runtimeVersion")
                    echo "DownloadURL: $downloadUrl"
                    [[ $? == 1 ]] && echo "Error: Could not find version $runtimeVersion in feed $DNX_ACTIVE_FEED" && return 1
                fi
                local runtimeFullName=$(__dnvm_requested_version_or_alias "$versionOrAlias" "$arch" "$os")
                local runtimeFolder="$runtimeDir/$runtimeFullName"

                #check all runtime homes for the runtime being installed.
                #we can force install a DNX if it exists in the user or global folder, but not anywhere else.
                #The installed dnx could be global, so if we are forcing it here we will remove it since we have the
                #global path handy.
                local exist=0
                for folder in `echo $DOTNET_HOME | tr ":" "\n"`; do
                    if [ -e "$folder/$_DNVM_INSTALL_SUBFOLDER/$runtimeFullName" ]; then
                        local useSudo=
                        if [[ ("$folder" == "$DOTNET_USER_HOME" || "$folder" == "$DOTNET_GLOBAL_HOME") && -n "$force" ]]; then
                            if [[ "$folder" == "$DOTNET_GLOBAL_HOME" ]]; then
                                useSudo=sudo
                            fi
                            printf "%b\n" "${Yel}Forcing download by deleting $folder/$_DNVM_INSTALL_SUBFOLDER/$runtimeFullName directory ${RCol}"
                            $useSudo rm -rf "$folder/$_DNVM_INSTALL_SUBFOLDER/$runtimeFullName"
                            continue
                        fi
                        echo "$runtimeFullName already installed in $folder $DNVM_USER_HOME"
                        exist=1
                    fi
                done

                if [[ $exist != 1 ]]; then
                    __dnvm_download "$runtimeFullName" "$downloadUrl" "$runtimeFolder" "$acceptSudo"
                fi
                [[ $? == 1 ]] && return 1
                if [[ "$os" == $(__dnvm_current_os) ]]; then
                    $_DNVM_COMMAND_NAME use "$versionOrAlias" "$persistent" "-arch" "$arch"
                    [[ -n $alias ]] && $_DNVM_COMMAND_NAME alias "$alias" "$versionOrAlias"
                fi
            else
                local runtimeFullName=$(basename $versionOrAlias | sed "s/\(.*\)\.nupkg/\1/")
                local runtimeVersion=$(__dnvm_package_version "$runtimeFullName")
                local runtimeFolder="$runtimeDir/$runtimeFullName"
                local runtimeFile="$runtimeFolder/$runtimeFullName.nupkg"
                local runtimeClr=$(__dnvm_package_runtime "$runtimeFullName")

                if [ -n "$force" ]; then
                   printf "%b\n" "${Yel}Forcing download by deleting $runtimeFolder directory ${RCol}"
                   rm -rf "$runtimeFolder"
                fi

                if [ -e "$runtimeFolder" ]; then
                  echo "$runtimeFullName already installed"
                else
                  local useSudo=
                  mkdir -p "$runtimeFolder" > /dev/null 2>&1
                  if [ ! -d $runtimeFolder ]; then
                     if ! __dnvm_promptSudo $acceptSudo ; then
                         useSudo=sudo
                         sudo mkdir -p "$runtimeFolder" > /dev/null 2>&1 || return 1
                     else
                         return 1
                     fi
                  fi
                  cp -a "$versionOrAlias" "$runtimeFile"
                  __dnvm_unpack "$runtimeFile" "$runtimeFolder" $useSudo
                  [[ $? == 1 ]] && return 1
                fi
                $_DNVM_COMMAND_NAME use "$runtimeVersion" "$persistent"
                [[ -n $alias ]] && $_DNVM_COMMAND_NAME alias "$alias" "$runtimeVersion"
            fi
        ;;

        "use"|"run"|"exec" )
            [[ $1 == "use" && $# -lt 2 ]] && __dnvm_help && return

            local cmd=$1
            local persistent=
            local arch=
            local versionOrAlias=

            shift
            if [ $cmd == "use" ]; then
                while [ $# -ne 0 ]
                do
                    if [[ $1 == "-p" || $1 == "-persistent" ]]; then
                        local persistent="true"
                    elif [[ $1 == "-a" || $1 == "-arch" ]]; then
                        local arch=$2
                        shift
                    elif [[ $1 == -* ]]; then
                        echo "Invalid option $1" && __dnvm_help && return 1
                    elif [[ -n $1 ]]; then
                        [[ -n $versionOrAlias ]] && echo "Invalid option $1" && __dnvm_help && return 1
                        local versionOrAlias=$1
                    fi
                    shift
                done
            else
                while [ $# -ne 0 ]
                do
                    if [[ $1 == "-a" || $1 == "-arch" ]]; then
                        local arch=$2
                        shift
                    elif [[ -n $1 ]]; then
                        [[ -n $versionOrAlias ]] && break
                        local versionOrAlias=$1
                    fi
                    shift
                done
            fi

            if [[ $cmd == "use" && $versionOrAlias == "none" ]]; then
                echo "Removing $_DNVM_RUNTIME_SHORT_NAME from process PATH"
                # Strip other version from PATH
                PATH=$(__dnvm_strip_path "$PATH")

                if [[ -n $persistent && -e "$_DNVM_ALIAS_DIR/default.alias" ]]; then
                    echo "Setting default $_DNVM_RUNTIME_SHORT_NAME to none"
                    rm "$_DNVM_ALIAS_DIR/default.alias"
                fi
                return 0
            fi

            local runtimeFullName=$(__dnvm_requested_version_or_alias "$versionOrAlias" "$arch" "$(__dnvm_current_os)")
            local runtimeBin=$(__dnvm_locate_runtime_bin_from_full_name "$runtimeFullName")

            if [[ -z $runtimeBin ]]; then
                echo "Cannot find $runtimeFullName, do you need to run '$_DNVM_COMMAND_NAME install $versionOrAlias'?"
                return 1
            fi

            case $cmd in
                "run")
                    local hostpath="$runtimeBin/dotnet"
                    if [[ -e $hostpath ]]; then
                        $hostpath $@
                        return $?
                    else
                        echo "Cannot find $_DNVM_RUNTIME_SHORT_NAME in $runtimeBin. It may have been corrupted. Use '$_DNVM_COMMAND_NAME install $versionOrAlias -f' to attempt to reinstall it"
                    fi
                ;;
                "exec")
                    (
                        PATH=$(__dnvm_strip_path "$PATH")
                        PATH=$(__dnvm_prepend_path "$PATH" "$runtimeBin")
                        $@
                    )
                    return $?
                ;;
                "use")
                    echo "Adding" $runtimeBin "to process PATH"

                    PATH=$(__dnvm_strip_path "$PATH")
                    PATH=$(__dnvm_prepend_path "$PATH" "$runtimeBin")

                    if [[ -n $persistent ]]; then
                        local runtimeVersion=$(__dnvm_package_version "$runtimeFullName")
                        $_DNVM_COMMAND_NAME alias default "$runtimeVersion"
                    fi
                ;;
            esac
        ;;

        "alias" )
            [[ $# -gt 7 ]] && __dnvm_help && return

            [[ ! -e "$_DNVM_ALIAS_DIR/" ]] && mkdir "$_DNVM_ALIAS_DIR/" > /dev/null

            if [[ $# == 1 ]]; then
                echo ""
                local format="%-25s %s\n"
                printf "$format" "Alias" "Name"
                printf "$format" "-----" "----"
                if [ -d "$_DNVM_ALIAS_DIR" ]; then
                    for __dnvm_file in $(find "$_DNVM_ALIAS_DIR" -name *.alias); do
                        local alias="$(basename $__dnvm_file | sed 's/\.alias//')"
                        local name="$(cat $__dnvm_file)"
                        printf "$format" "$alias" "$name"
                    done
                fi
                echo ""
                return
            fi
            shift
            local name="$1"

            if [[ $# == 1 ]]; then
                [[ ! -e "$_DNVM_ALIAS_DIR/$name.alias" ]] && echo "There is no alias called '$name'" && return 1
                cat "$_DNVM_ALIAS_DIR/$name.alias"
                echo ""
                return
            fi

            shift
            local versionOrAlias="$1"
            shift
            while [ $# -ne 0 ]
                do
                    if [[ $1 == "-a" || $1 == "-arch" ]]; then
                        local arch=$2
                        shift
                    elif [[ $1 == "-OS" ]]; then
                        local os=$2
                        shift
                    fi
                    shift
             done

            local runtimeFullName=$(__dnvm_requested_version_or_alias "$versionOrAlias" "$arch" "$os")

            ([[ ! -d "$_DNVM_USER_PACKAGES/$runtimeFullName" ]] && [[ ! -d "$_DNVM_GLOBAL_PACKAGES/$runtimeFullName" ]]) && echo "$runtimeFullName is not an installed $_DNVM_RUNTIME_SHORT_NAME version" && return 1

            local action="Setting"
            [[ -e "$_DNVM_ALIAS_DIR/$name.alias" ]] && action="Updating"
            echo "$action alias '$name' to '$runtimeFullName'"
            echo "$runtimeFullName" >| "$_DNVM_ALIAS_DIR/$name.alias"
        ;;

        "unalias" )
            [[ $# -ne 2 ]] && __dnvm_help && return

            local name=$2
            local aliasPath="$_DNVM_ALIAS_DIR/$name.alias"
            [[ ! -e  "$aliasPath" ]] && echo "Cannot remove alias, '$name' is not a valid alias name" && return 1
            echo "Removing alias $name"
            rm "$aliasPath" >> /dev/null 2>&1
        ;;

        "list" )
            [[ $# -gt 2 ]] && __dnvm_help && return

            [[ ! -d $_DNVM_USER_PACKAGES ]] && echo "$_DNVM_RUNTIME_FRIENDLY_NAME is not installed." && return 1

            local searchGlob="$_DNVM_RUNTIME_PACKAGE_NAME-*"

            local runtimes=""
            for location in `echo $DOTNET_HOME | tr ":" "\n"`; do
                location+="/$_DNVM_INSTALL_SUBFOLDER"
                if [ -d "$location" ]; then
                    local oruntimes="$(find $location -name "$searchGlob" \( -type d -or -type l \) -prune -exec basename {} \;)"
                    for v in `echo $oruntimes | tr "\n" " "`; do
                        runtimes+="$v:$location"$'\n'
                    done
                fi
            done

            [[ -z $runtimes ]] && echo 'No runtimes installed. You can run `dnvm install latest` or `dnvm upgrade` to install a runtime.' && return

            echo ""

            # Separate empty array declaration from initialization
            # to avoid potential ZSH error: local:217: maximum nested function level reached
            local arr
            arr=()

            # Z shell array-index starts at one.
            local i=1
            local format="%-20s %s\n"
            if [ -d "$_DNVM_ALIAS_DIR" ]; then
                for __dnvm_file in $(find "$_DNVM_ALIAS_DIR" -name *.alias); do
                    if [ ! -d "$_DNVM_USER_PACKAGES/$(cat $__dnvm_file)" ] && [ ! -d "$_DNVM_GLOBAL_PACKAGES/$(cat $__dnvm_file)" ]; then
                        arr[$i]="$(basename $__dnvm_file | sed 's/\.alias//')/missing/$(cat $__dnvm_file)"
                        runtimes="$runtimes $(cat $__dnvm_file)"
                    else
                        arr[$i]="$(basename $__dnvm_file | sed 's/\.alias//')/$(cat $__dnvm_file)"
                    fi
                    let i+=1
                done
            fi

            if [[ $2 == "-detailed" ]]; then
                # Calculate widest alias
                local widestAlias=5
                for f in `echo $runtimes`; do
                    local pkgName=$(__dnvm_package_name "$f")
                    local pkgVersion=$(__dnvm_package_version "$f")
                    local alias=""
                    local delim=""
                    for i in "${arr[@]}"; do
                        if [[ ${i##*/} == "$pkgName.$pkgVersion" ]]; then
                            alias+="$delim${i%%/*}"
                            delim=", "
                            if [[ "${i%/*}" =~ \/missing$ ]]; then
                                alias+=" (missing)"
                            fi
                        fi
                    done
                    if [ "${#alias}" -gt "$widestAlias" ]; then
                        widestAlias=${#alias}
                    fi
                done
                local formatString="%-6s %-20s %-7s %-12s %-15s %-${widestAlias}s %s\n"
                printf "$formatString" "Active" "Version" "Alias" "Location"
                printf "$formatString" "------" "-------" "-----" "--------"
            else
                local formatString="%-6s %-20s %-7s %-12s %-15s %s\n"
                printf "$formatString" "Active" "Version" "Alias"
                printf "$formatString" "------" "-------" "-----"
            fi

            for f in `echo -e "$runtimes" | sort -t. -k2 -k3 -k4 -k1`; do
                local location=`echo $f | sed 's/.*\([:]\)//'`
                f=`echo $f | sed 's/\([:]\).*//'`
                local formattedHome=`(echo $location | sed s=$HOME=~=g)`
                local active=""
                [[ $PATH == *"$location/$f"* ]] && local active="  *"
                local pkgName=$(__dnvm_package_name "$f")
                local pkgVersion=$(__dnvm_package_version "$f")

                local alias=""
                local delim=""
                for i in "${arr[@]}"; do
                    if [[ ${i##*/} == "$pkgName.$pkgVersion" ]]; then
                        alias+="$delim${i%%/*}"
                        delim=", "
                        if [[ "${i%/*}" =~ \/missing$ ]]; then
                            alias+=" (missing)"
                            formattedHome=""
                        fi
                    fi
                done

                if [[ $2 == "-detailed" ]]; then
                    printf "$formatString" "$active" "$pkgVersion" "$alias" "$formattedHome"
                else
                    printf "$formatString" "$active" "$pkgVersion" "$alias"
                fi
            done

            echo ""
        ;;

        *)
            echo "Unknown command $1"
            return 1
    esac

    return 0
}

# Add the home location's bin directory to the path if it doesn't exist
[[ ":$PATH:" != *":$DOTNET_USER_HOME:"* ]] && export PATH="$DOTNET_USER_HOME:$PATH"

# Generate the command function using the constant defined above.
$_DNVM_COMMAND_NAME alias default >/dev/null && $_DNVM_COMMAND_NAME use default >/dev/null || true
