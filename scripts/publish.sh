#!/bin/bash
#
# Usage: publish.sh [file to be uploaded]
# 
# Environment Dependencies:
#     $STORAGE_CONTAINER
#     $STORAGE_ACCOUNT
#     $SASTOKEN
#     $REPO_ID

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
SCRIPT_DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$SCRIPT_DIR/_common.sh"

UPLOAD_FILE=$1
UPLOAD_JSON_FILE="package_upload.json"

banner "Publishing package"

execute(){
    if ! validate_env_variables; then
        # fail silently if the required variables are not available for publishing the file.
        exit 0
    fi

    if [[ ! -f "$UPLOAD_FILE" ]]; then
        echo "Error: \"$UPLOAD_FILE\" file does not exist"
        exit 1
    fi

    if ! upload_file_to_blob_storage; then
        exit 1
    fi

    # debain packages need to be uploaded to the PPA feed too
    if [[ $UPLOAD_FILE == *.deb ]]; then
        DEB_FILE=$UPLOAD_FILE
        generate_repoclient_json
        call_repo_client
    fi
}

validate_env_variables(){
    local ret=0

    if [[ -z "$DOTNET_BUILD_VERSION" ]]; then
        warning "DOTNET_BUILD_VERSION environment variable not set"
        ret=1
    fi

    if [[ -z "$SASTOKEN" ]]; then
        warning "SASTOKEN environment variable not set"
        ret=1
    fi

    if [[ -z "$STORAGE_ACCOUNT" ]]; then
        warning "STORAGE_ACCOUNT environment variable not set"
        ret=1
    fi

    if [[ -z "$STORAGE_CONTAINER" ]]; then
        warning "STORAGE_CONTAINER environment variable not set"
        ret=1
    fi

     if [[ -z "$CHANNEL" ]]; then
        echo "CHANNEL environment variable not set"
        ret=1
    fi

    return $ret
}

upload_file_to_blob_storage(){
    local filename=$(basename $UPLOAD_FILE)

    banner "Uploading $filename to blob storage"

    if [[ $filename == *.deb || $filename == *.pkg ]]; then
        FOLDER="Installers"
    elif [[ $filename == *.tar.gz ]]; then
        FOLDER="Binaries"
    fi

    UPLOAD_URL="https://$STORAGE_ACCOUNT.blob.core.windows.net/$STORAGE_CONTAINER/$CHANNEL/$FOLDER/$DOTNET_BUILD_VERSION/$filename$SASTOKEN"

    curl -L -H "x-ms-blob-type: BlockBlob" -H "x-ms-date: 2015-10-21" -H "x-ms-version: 2013-08-15" $UPLOAD_URL -T $UPLOAD_FILE
    result=$?

    if [ "$result" -gt "0" ]; then
        error "uploading the $filename to blob storage - $result"
    else
        info "successfully uploaded $filename to blob storage."
    fi

    return $result
}

generate_repoclient_json(){
    # Clean any existing json file
    rm -f $SCRIPT_DIR/$UPLOAD_JSON_FILE

    echo "{"                                            >> "$SCRIPT_DIR/$UPLOAD_JSON_FILE"
    echo "  \"name\":\"$(_get_package_name)\","         >> "$SCRIPT_DIR/$UPLOAD_JSON_FILE"
    echo "  \"version\":\"$(_get_package_version)\","   >> "$SCRIPT_DIR/$UPLOAD_JSON_FILE"
    echo "  \"repositoryId\":\"$REPO_ID\","             >> "$SCRIPT_DIR/$UPLOAD_JSON_FILE"
    echo "  \"sourceUrl\":\"$UPLOAD_URL\""              >> "$SCRIPT_DIR/$UPLOAD_JSON_FILE"
    echo "}"                                            >> "$SCRIPT_DIR/$UPLOAD_JSON_FILE"
}

call_repo_client(){
    $SCRIPT_DIR/repoapi_client.sh -addpkg $SCRIPT_DIR/$UPLOAD_JSON_FILE
}

# Extract the package name from the .deb filename
_get_package_name(){
    local deb_filename=$(basename $DEB_FILE)
    local package_name=${deb_filename%%_*}

    echo $package_name
}

# Extract the package version from the .deb filename
_get_package_version(){
    local deb_filename=$(basename $DEB_FILE)
    local package_version=${deb_filename#*_}
    package_version=${package_version%-*}

    echo $package_version
}

execute
