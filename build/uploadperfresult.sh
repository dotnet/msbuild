#!/bin/bash

# The intent of this script is upload produced performance results to BenchView in a CI context.
#    There is no support for running this script in a dev environment.

if [ -z "$perfWorkingDirectory" ]; then
    echo EnvVar perfWorkingDirectory should be set; exiting...
    exit 1
fi
if [ -z "$configuration" ]; then
    echo EnvVar configuration should be set; exiting...
    exit 1
fi
if [ -z "$architecture" ]; then
    echo EnvVar architecture should be set; exiting...
    exit 1
fi
if [ -z "$OS" ]; then
    echo EnvVar OS should be set; exiting...
    exit 1
fi
if [ "$runType" = "private" ]; then
    if [ -z "$TestRunCommitName" ]; then
        echo EnvVar TestRunCommitName should be set; exiting...
        exit 1
    fi
else
    if [ "$runType" = "rolling" ]; then
        if [ -z "$GIT_COMMIT" ]; then
            echo EnvVar GIT_COMMIT should be set; exiting...
            exit 1
        fi
    else
        echo EnvVar runType should be set; exiting...
        exit 1
    fi
fi
if [ -z "$GIT_BRANCH" ]; then
    echo EnvVar GIT_BRANCH should be set; exiting...
    exit 1
fi
if [ ! -d "$perfWorkingDirectory" ]; then
    echo "$perfWorkingDirectory" does not exist; exiting...
    exit 1
fi

# Do this here to remove the origin but at the front of the branch name
if [[ "$GIT_BRANCH" == "origin/"* ]]
then
    GIT_BRANCH_WITHOUT_ORIGIN=${GIT_BRANCH:7}
else
    GIT_BRANCH_WITHOUT_ORIGIN=$GIT_BRANCH
fi

TestRunName="SDK perf $OS $architecture $configuration $runType $GIT_BRANCH_WITHOUT_ORIGIN"
if [[ "$runType" == "private" ]]
then
    TestRunName="$TestRunName $TestRunCommitName"
fi
if [[ "$runType" == "rolling" ]]
then
    TestRunName="$TestRunName $GIT_COMMIT"
fi
export TestRunName=$TestRunName

echo TestRunName: "$TestRunName"

echo Creating and uploading: "$perfWorkingDirectory/submission.json"
"$HELIX_WORKITEM_ROOT/.dotnet/dotnet" run \
    --project $HELIX_WORKITEM_ROOT/src/Tests/PerformanceTestsResultUploader/PerformanceTestsResultUploader.csproj \
    --configuration $configuration -- \
    --output "$perfWorkingDirectory/submission.json" \
    --repository-root "$HELIX_WORKITEM_ROOT" \
    --sas "$PERF_COMMAND_UPLOAD_TOKEN" || { echo 'Generate and upload failed...' ; exit 1; }

exit 0
