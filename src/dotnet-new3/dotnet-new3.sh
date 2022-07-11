#!/bin/bash
self=$(readlink "$0")
resolved=$(dirname "$self")
dotnet $resolved/dotnet-new3.dll "$@"
