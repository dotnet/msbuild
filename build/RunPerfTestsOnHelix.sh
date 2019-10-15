#!/bin/bash

if [ -z "$PERF_COMMAND_UPLOAD_TOKEN" ]; then
    echo EnvVar PERF_COMMAND_UPLOAD_TOKEN should be set; exiting...
    exit 1
fi
if [ -z "$HELIX_WORKITEM_ROOT" ]; then
    echo EnvVar HELIX_WORKITEM_ROOT should be set; exiting...
    exit 1
fi

configuration=$1
PerfIterations=$2
GIT_COMMIT=$3
GIT_BRANCH=$4
runType=$5
architecture=$6
OS=$7
HelixTargetQueues=$8
BuildNumber=$9

#  Since the transfer of the payload to the helix machine renders all scripts non-executable,
#    Add the executable bit to the appropriate scripts.
chmod +x "$HELIX_WORKITEM_ROOT/eng/common/build.sh" || { echo 'chmod of: build.sh failed...' ; }
chmod +x "$HELIX_WORKITEM_ROOT/.dotnet/dotnet" || { echo 'chmod of: dotnet failed...' ; }
chmod +x "$HELIX_WORKITEM_ROOT/build/uploadperfresult.sh" || { echo 'chmod of: uploadperfresult.sh failed...' ; }

#  Run the performance tests and collect performance data.
echo "Running the performance tests and collecting data"
"$HELIX_WORKITEM_ROOT/eng/common/build.sh" --configuration $configuration --restore --build --ci --performancetest /p:PerfIterations=$PerfIterations || { echo 'build.sh failed...' ; exit 1; }
echo "Performance tests completed"

#  Upload the performance data
export perfWorkingDirectory=$HELIX_WORKITEM_ROOT/artifacts/TestResults/$configuration/Performance
export GIT_COMMIT=$GIT_COMMIT
export GIT_BRANCH=$GIT_BRANCH
export configuration=$configuration
export runType=$runType
export architecture=$architecture
export OS=$OS
export HelixTargetQueues=$HelixTargetQueues
export BuildNumber=$BuildNumber

echo "Uploading data to: uploadperfresult.sh"
"$HELIX_WORKITEM_ROOT/build/uploadperfresult.sh" || { echo 'uploadperfresult.sh failed...' ; exit 1; }

exit 0
