Running Performance Tests
=========================

Pre-Requisites
--------------

* Python 2.7+ or 3.5+
* msbuild.exe (must be on `PATH`)

Single Perf Run
---------------

1. Build the CLI repo to get dotnet.exe, or otherwise source the CLI. For
   meaningful perf results, be sure to use release mode.

2. `cd <cli_repo_root>/test/Performance`

3. `python run-perftests.py <dotnet_bin> --name <unique_run_name>
   --xunit-perf-path <x_repo_path>`  
   where:
    * `<dotnet_bin>` is the path to the dotnet binary whose perf you want to
      measure.
    * `<x_repo_path>` should point either to an non-existent directory, or to
      the root of a local clone of xunit-performance. If a non-existent
      directory is specified, the repo will automatically be cloned.
        - NOTE: You can also set the environment variable
          `XUNIT_PERFORMANCE_PATH` to avoid having to pass this variable every
          time.

4. View the `*.csv` / `*.xml` results in the current directory.

Comparison Run
--------------

In general, follow the same steps as for a single perf run. The following
additional steps are required:

1. In addition to the dotnet.exe that you're testing, be sure to also build or
   otherwise source the baseline dotnet.exe. This could be the "stage0" exe, or
   the exe from the last nightly build, or the exe built from sources prior to
   changes you made, etc.

2. When invoking `run-perftests.py`, add an additional parameter: `--base
   <base_bin>`, which points to the baseline dotnet.exe mentioned in step 1.

3. View the `*.html` file generated for the perf comparison analysis.

Debugging Issues
----------------

The output of commands invoked by `run-perftests` is hidden by default. You can
see the output after an error by looking in the `logs/run-perftests` directory.
Alternatively, you can rerun `run-perftests` with `--verbose`, which will print
all output to the console instead of piping it to log files.
