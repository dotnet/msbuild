TODO: Ankit will fill this file with steps how to update our fork when a new msbuild version appears upstream


mono/build/install.proj:

    We keep two canonical lists:

        mono/build/all_files.canon.txt:
                - all the files that install.proj copies to $(MonoInstallPrefix)

        mono/build/remaining_files.canon.txt:
                - files that were *not* copied from the bin dir in artifacts

    If files get added/removed, then handle them and update these lists.
