TODO: Ankit will fill this file with steps how to update our fork when a new msbuild version appears upstream


mono/build/install.proj:

    We keep two canonical lists:

        mono/build/all_files.canon.txt:
                - all the files that install.proj copies to $(MonoInstallPrefix)

        mono/build/remaining_files.canon.txt:
                - files that were *not* copied from the bin dir in artifacts

    If files get added/removed, then handle them and update these lists.

mono/create_bootstrap.sh:

    Create a bootstrap msbuild zip, for use with the initial build.

    This uses the msbuild in $PATH to build this bootstrap. This is mainly
    to ensure that we get the corresponding Roslyn binaries too.

    To create a bootstrap from the current build, install that, add to $PATH
    and run the script.

Build notes:

- We download a bootstrap msbuild and use that for the initial build. `csc` used here is from the bootstrap msbuild.
- The subsequent build uses the freshly built msbuild. And `csc` used is whatever was referenced, so same as upstream.

- The installation process does not install any roslyn, instead depending on the mono prefix to have that available. And just
  sets up symlinks for them.
