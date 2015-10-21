#!/bin/bash
# This is a simple script to push the deb package to our private corpnet feed
#
# Usage: publish_package.sh [deb file]
# Requires: Azure Cli installed (for uploading to blob storage)
# 
# Environment Dependencies:
#     $STORAGE_CONTAINER_NAME
#     $STORAGE_ACCOUNT
#     $STORAGE_KEY
#     $REPO_ID

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

DEB_FILE=$1
UPLOAD_JSON_FILE="package_upload.json"

execute(){
    if ! validate_inputs; then
        exit 1
    fi

    upload_deb_to_blob_storage
    generate_repoclient_json
    call_repo_client
}

validate_inputs(){
    local ret=0
    if [[ ! -f "$DEB_FILE" ]]; then
        echo "Error: .deb file does not exist"
        ret=1
    fi
    if [[ -z "$STORAGE_CONTAINER" ]]; then
        echo "Error: STORAGE_CONTAINER environment variable not set"
        ret=1
    fi

    if [[ -z "$STORAGE_ACCOUNT" ]]; then
        echo "Error: STORAGE_ACCOUNT environment variable not set"
        ret=1
    fi

    if [[ -z "$STORAGE_KEY" ]]; then
        echo "Error: STORAGE_KEY environment variable not set"
        ret=1
    fi

    return $ret
}

upload_deb_to_blob_storage(){
    local deb_filename=$(basename $DEB_FILE)
    azure storage blob upload $DEB_FILE $STORAGE_CONTAINER $deb_filename -a $STORAGE_ACCOUNT -k $STORAGE_KEY

    UPLOAD_URL="http://$STORAGE_ACCOUNT.blob.core.windows.net/$STORAGE_CONTAINER/$deb_filename"
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