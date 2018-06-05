#!/bin/bash

install=false
uninstall=false
run=false
# Default / LKG package version
packageVersion=2.1.400-preview-63001-03
tests=Build,Clean,Pack,Perf,Publish,Rebuild,Restore,ToolPack
additionalargs=()

packageSource=https://dotnet.myget.org/F/dotnet-cli/api/v3/index.json

while (($# > 0)); do
  lowerI="$(echo "$1" | awk '{print tolower($0)}')"
  case $lowerI in
    --install)
      install=true
      shift 1
      ;;
    --uninstall)
      uninstall=true
      shift 1
      ;;
    --run)
      run=true
      shift 1
      ;;
    --packageversion)
      packageVersion=$2
      shift 2
      ;;
    --tests)
      tests=$2
      shift 2
      ;;
    *)
      additionalargs+=("$1")
      shift 1
      ;;
    esac
done

IFS=',' read -ra testsArray <<< "$tests"

if [ "$uninstall" = true ]; then
    for name in "${testsArray[@]}"
    do :
        echo Uninstalling "$name"
        dotnet tool uninstall -g "testSdk$name"
    done
fi

if [ "$install" = true ]; then
    for name in "${testsArray[@]}"
    do :
        echo Installing "$name"
        dotnet tool install -g "testSdk$name" --version "$packageVersion" --add-source "$packageSource"
    done
fi

if [ "$run" = true ]; then
    failedTests=''
    passed=true
    for name in "${testsArray[@]}"
    do :
        echo Running "$name"
        cmd="testSdk$name"
        resultsFile="$name"
        resultsFile+="results.xml"

        set -- "${additionalargs[@]}" # restore positional parameters
        "$cmd" -xml "$resultsFile" "$@"

        lastexitcode=$?
        if [[ $lastexitcode != 0 ]]; then
            passed=false
            if [[ -z "$failedTests" ]]; then
                failedTests=$name
            else
                failedTests="$failedTests, $name"
            fi
        fi
    done

    if [ "$passed" = true ]; then
        echo "Tests passed"
    else
        echo "Tests failed: $failedTests"
        exit 1
    fi
fi