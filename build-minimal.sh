#!/usr/bin/env bash

# ============================================================================
# build-minimal.sh - Fast build script for minimal MSBuild assemblies only
# 
# This script builds only the essential MSBuild runtime without tests, samples,
# or package projects. It is significantly faster than a full build.
#
# Usage:
#   ./build-minimal.sh                  - Build with bootstrap (default)
#   ./build-minimal.sh --nobootstrap    - Build without bootstrap (fastest)
#   ./build-minimal.sh --release        - Build release configuration
#   ./build-minimal.sh --rebuild        - Force rebuild
#   ./build-minimal.sh --help           - Show help
# ============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR"

# Default values
CONFIGURATION="Debug"
CREATE_BOOTSTRAP="true"
REBUILD=""
BUILD="true"
VERBOSITY="minimal"
EXTRA_ARGS=""

show_help() {
    echo ""
    echo "MSBuild Minimal Build Script - Fast build for development"
    echo ""
    echo "Usage: ./build-minimal.sh [options]"
    echo ""
    echo "Options:"
    echo "  --nobootstrap    Skip creating the bootstrap folder (fastest builds)"
    echo "  --release        Build in Release configuration (default: Debug)"
    echo "  --debug          Build in Debug configuration"
    echo "  --rebuild        Force a rebuild (clean + build)"
    echo "  -v <level>       Verbosity: q[uiet], m[inimal], n[ormal], d[etailed]"
    echo "  --help           Show this help"
    echo ""
    echo "Examples:"
    echo "  ./build-minimal.sh                     Minimal build with bootstrap"
    echo "  ./build-minimal.sh --nobootstrap       Fast incremental build"
    echo "  ./build-minimal.sh --release           Release build"
    echo ""
    echo "For full builds including tests, use: ./build.sh"
    echo ""
    exit 0
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        --help|-h)
            show_help
            ;;
        --nobootstrap|-nobootstrap)
            CREATE_BOOTSTRAP="false"
            shift
            ;;
        --release|-release)
            CONFIGURATION="Release"
            shift
            ;;
        --debug|-debug)
            CONFIGURATION="Debug"
            shift
            ;;
        --rebuild|-rebuild)
            REBUILD="--rebuild"
            shift
            ;;
        -v|--verbosity)
            VERBOSITY="$2"
            shift 2
            ;;
        *)
            EXTRA_ARGS="$EXTRA_ARGS $1"
            shift
            ;;
    esac
done

# Build arguments
BUILD_ARGS="--restore --configuration $CONFIGURATION -v $VERBOSITY"
if [[ -n "$BUILD" ]]; then
    BUILD_ARGS="$BUILD_ARGS --build"
fi
if [[ -n "$REBUILD" ]]; then
    BUILD_ARGS="$BUILD_ARGS $REBUILD"
fi
BUILD_ARGS="$BUILD_ARGS /p:CreateBootstrap=$CREATE_BOOTSTRAP"

# Use solution filter for minimal projects only
BUILD_ARGS="$BUILD_ARGS /p:Projects=$REPO_ROOT/MSBuild.Minimal.slnf"

# Disable IBC optimization for minimal builds (requires VSSetup which we don't build)
BUILD_ARGS="$BUILD_ARGS /p:UsingToolIbcOptimization=false /p:UsingToolVisualStudioIbcTraining=false"

echo ""
echo "============================================================"
echo " MSBuild Minimal Build"
echo "============================================================"
echo " Configuration:    $CONFIGURATION"
echo " Create Bootstrap: $CREATE_BOOTSTRAP"
echo " Verbosity:        $VERBOSITY"
echo "============================================================"
echo ""

# Run the build
"$REPO_ROOT/eng/common/build.sh" $BUILD_ARGS $EXTRA_ARGS
EXIT_CODE=$?

if [[ $EXIT_CODE -eq 0 ]]; then
    echo ""
    echo "============================================================"
    echo " Build succeeded!"
    if [[ "$CREATE_BOOTSTRAP" == "true" ]]; then
        echo ""
        echo " To use the bootstrapped MSBuild, run:"
        echo "   source artifacts/sdk-build-env.sh"
        echo ""
        echo " Then use 'dotnet build' with your locally-built MSBuild."
    fi
    echo "============================================================"
else
    echo ""
    echo "Build failed with exit code $EXIT_CODE. Check errors above."
fi

exit $EXIT_CODE
