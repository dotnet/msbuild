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
        error "\"$UPLOAD_FILE\" file does not exist"
        exit 1
    fi

    if [[ $UPLOAD_FILE == *.deb || $UPLOAD_FILE == *.pkg ]]; then
        upload_installers_to_blob_storage $UPLOAD_FILE
        result=$?
    elif [[ $UPLOAD_FILE == *.tar.gz ]]; then
        upload_binaries_to_blob_storage $UPLOAD_FILE
        result=$?
    fi

    exit $result
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
        warning "CHANNEL environment variable not set"
        ret=1
    fi

    if [[ -z "$CONNECTION_STRING" ]]; then
        warning "CONNECTION_STRING environment variable not set"
        ret=1
    fi

    return $ret
}

upload_file_to_blob_storage_azure_cli(){
    local blob=$1
    local file=$2

    banner "Uploading $file to blob storage"

    # use azure cli to upload to blob storage. We cannot use curl to do this becuase azure has a max limit of 64mb that can be uploaded using REST
    # statusCode=$(curl -s -w "%{http_code}" -L -H "x-ms-blob-type: BlockBlob" -H "x-ms-date: 2015-10-21" -H "x-ms-version: 2013-08-15" $upload_URL -T $file)
    azure storage blob upload --quiet --container $STORAGE_CONTAINER --blob $blob --blobtype block --connection-string "$CONNECTION_STRING" --file $file
    result=$?

    if [ "$result" -eq "0" ]; then
        info "successfully uploaded $filename to blob storage."
        return 0
    else
        error "uploading the $filename to blob storage - $statusCode"
        return 1
    fi
}

update_file_in_blob_storage(){
    local update_URL=$1
    local file=$2
    local filecontent=$3

    banner "Updating $file in blob storage"

    statusCode=$(curl -s -w "%{http_code}" -L -H "x-ms-blob-type: BlockBlob" -H "x-ms-date: 2015-10-21" -H "x-ms-version: 2013-08-15" $update_URL --data $filecontent --request PUT )

    if [ "$statusCode" -eq "201" ]; then
        info "successfully updated $file in blob storage."
        return 0
    else
        error "updating the $file in blob storage - $statusCode"
        return 1
    fi
}

upload_binaries_to_blob_storage(){
    local tarfile=$1
    local filename=$(basename $tarfile)
    local blob="$CHANNEL/Binaries/$DOTNET_BUILD_VERSION/$filename"

    if upload_file_to_blob_storage_azure_cli $blob $tarfile; then
        # update the index file
        local indexContent="Binaries/$DOTNET_BUILD_VERSION/$filename"
        local indexfile="latest.$OSNAME.index"
        local update_URL="https://$STORAGE_ACCOUNT.blob.core.windows.net/$STORAGE_CONTAINER/$CHANNEL/dnvm/$indexfile$SASTOKEN"
        update_file_in_blob_storage $update_URL $indexfile $indexContent
        return $?
    fi

    return 1
}

upload_installers_to_blob_storage(){
    local installfile=$1
    local filename=$(basename $installfile)
    local blob="$CHANNEL/Installers/$DOTNET_BUILD_VERSION/$filename"

    if ! upload_file_to_blob_storage_azure_cli $blob $installfile; then
        return 1
    fi

    # debain packages need to be uploaded to the PPA feed too
    if [[ $installfile == *.deb ]]; then
        DEB_FILE=$installfile
        generate_repoclient_json
        call_repo_client
    fi

    return 0
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
