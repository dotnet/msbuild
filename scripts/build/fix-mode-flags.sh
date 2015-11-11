#!/usr/bin/env bash

# Managed code doesn't need 'x'
find . -type f -name "*.dll" | xargs chmod 644
find . -type f -name "*.exe" | xargs chmod 644

# Generally, dylibs and sos have 'x' (no idea if it's required ;))
if [ "$(uname)" == "Darwin" ]; then
    find . -type f -name "*.dylib" | xargs chmod 755
else
    find . -type f -name "*.so" | xargs chmod 755
fi

# Executables (those without dots) are executable :)
find . -type f ! -name "*.*" | xargs chmod 755
