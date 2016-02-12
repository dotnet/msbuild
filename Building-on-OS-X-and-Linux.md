MSBuild can be successfully built on Windows, OS X, and Ubuntu from the [`xplat`](https://github.com/Microsoft/msbuild/tree/xplat) branch.

# Required packages

The initial build process is done with a Mono-hosted version of MSBuild. Until we migrate our build to use MSBuild on the CoreCLR, we require that `mono` be installed and on your PATH.

# Build process

```sh
./cibuild.sh
```

# Tests

Tests are currently disabled on platforms other than Windows. To enable them, ...