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
    if [ -z "$BenchviewCommitName" ]; then
        echo EnvVar BenchviewCommitName should be set; exiting...
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


rm -r -f "$perfWorkingDirectory/Microsoft.BenchView.JSONFormat" > /dev/null 2>&1

if [ ! -d "$perfWorkingDirectory/Microsoft.BenchView.JSONFormat" ]; then
    # curl "http://benchviewtestfeed.azurewebsites.net/nuget/FindPackagesById()?id='Microsoft.BenchView.JSONFormat'" | grep "content type" | sed "$ s/.*src=\"\([^\"]*\)\".*/\1/;tx;d;:x" | xargs curl -o $perfWorkingDirectory/benchview.zip http://benchviewtestfeed.azurewebsites.net/api/v2/package/microsoft.benchview.jsonformat/0.1.0-pre001

    curl -o "$perfWorkingDirectory/benchview.zip" http://benchviewtestfeed.azurewebsites.net/api/v2/package/microsoft.benchview.jsonformat/0.1.0-pre024
    unzip -q -o "$perfWorkingDirectory/benchview.zip" -d "$perfWorkingDirectory/Microsoft.BenchView.JSONFormat"
fi
# nuget install Microsoft.BenchView.JSONFormat -Source http://benchviewtestfeed.azurewebsites.net/nuget -OutputDirectory "$perfWorkingDirectory" -Prerelease -ExcludeVersion || { echo Failed to install Microsoft.BenchView.JSONFormat NuPkg && exit 1 ; }

# Do this here to remove the origin but at the front of the branch name as this is a problem for BenchView
if [[ "$GIT_BRANCH" == "origin/"* ]]
then
    GIT_BRANCH_WITHOUT_ORIGIN=${GIT_BRANCH:7}
else
    GIT_BRANCH_WITHOUT_ORIGIN=$GIT_BRANCH
fi

timeStamp=`date --utc '+%Y-%m-%dT%H:%M:%SZ'`

benchViewName="SDK perf $OS $architecture $configuration $runType $GIT_BRANCH_WITHOUT_ORIGIN"
if [[ "$runType" == "private" ]]
then
    benchViewName="$benchViewName $BenchviewCommitName"
fi
if [[ "$runType" == "rolling" ]]
then
    benchViewName="$benchViewName $GIT_COMMIT"
fi
echo BenchViewName: "$benchViewName"

echo Creating: "$perfWorkingDirectory/submission-metadata.json"
python3.5 "$perfWorkingDirectory/Microsoft.BenchView.JSONFormat/tools/submission-metadata.py" --name "$benchViewName" --user-email "dotnet-bot@microsoft.com" \
                    -o "$perfWorkingDirectory/submission-metadata.json" || { echo Failed to create: "$perfWorkingDirectory/submission-metadata.json" && exit 1 ; }

echo Creating: "$perfWorkingDirectory/build.json"
python3.5 "$perfWorkingDirectory/Microsoft.BenchView.JSONFormat/tools/build.py" git --branch "$GIT_BRANCH_WITHOUT_ORIGIN" --type "$runType" --source-timestamp "$timeStamp" \
                   -o "$perfWorkingDirectory/build.json" || { echo Failed to create: "$perfWorkingDirectory/build.json" && exit 1 ; }

echo Creating: "$perfWorkingDirectory/machinedata.json"
python3.5 "$perfWorkingDirectory/Microsoft.BenchView.JSONFormat/tools/machinedata.py" \
                   -o "$perfWorkingDirectory/machinedata.json" || { echo Failed to create: "$perfWorkingDirectory/machinedata.json" && exit 1 ; }

echo Creating: "$perfWorkingDirectory/measurement.json"
find "$perfWorkingDirectory" -maxdepth 1 -type f -name "*.xml" -exec echo Processing: "{}" \;
find "$perfWorkingDirectory" -maxdepth 1 -type f -name "*.xml" -exec python3.5 "$perfWorkingDirectory/Microsoft.BenchView.JSONFormat/tools/measurement.py" xunitscenario "{}" --better desc --drop-first-value --append \
                       -o "$perfWorkingDirectory/measurement.json" \; || { echo Failed to create: "$perfWorkingDirectory/measurement.json" && exit 1 ; }

echo Creating: "$perfWorkingDirectory/submission.json"
python3.5 "$perfWorkingDirectory/Microsoft.BenchView.JSONFormat/tools/submission.py" "$perfWorkingDirectory/measurement.json" \
                    --build "$perfWorkingDirectory/build.json" \
                    --machine-data "$perfWorkingDirectory/machinedata.json" \
                    --metadata "$perfWorkingDirectory/submission-metadata.json" \
                    --group "SDK Perf Tests" \
                    --type "$runType" \
                    --config-name "$configuration" \
                    --config Configuration "$configuration" \
                    --config OS "$OS" \
                    --architecture "$architecture" \
                    --machinepool "perfsnake" \
                    -o "$perfWorkingDirectory/submission.json" || { echo Failed to create: "$perfWorkingDirectory/submission.json" && exit 1 ; }

echo Uploading: "$perfWorkingDirectory/submission.json"
python3.5 "$perfWorkingDirectory/Microsoft.BenchView.JSONFormat/tools/upload.py" "$perfWorkingDirectory/submission.json" --container coreclr || { echo Failed to upload: "$perfWorkingDirectory/submission.json" && exit 1 ; }

exit 0
