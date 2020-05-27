### Issue Description
[Brief description of the issue]

### Steps to Reproduce
Include one of the following if possible:

1. A sample project. See below.
2. Attach Your zipped project.
3. Provide IDE / CLI steps to create the project and repro the behaviour.

Example of a project sample:

### Expected Behavior
[What **should** be happening]

### Actual Behavior
[What is **actually** happening]

### MSBuild Version
This removes the guesswork of finding what commit your version of MSBuild is at.

There are multiple ways to do this:
1. The easiest way is using a Visual Studio Developer Command Prompt. Run `msbuild -version` and attach all of the output.
2. Find your `msbuild.exe` generally located at `C:\Program Files (x86)\Microsoft Visual Studio\<year>\<version>\MSBuild\Current\Bin`, right click it, and click `Properties`. In the new window, go to the details tab and either screenshot or copy over the `File Version` and `Product Version`.


### Attach a binlog
See details on the process [here](https://gist.github.com/dsplaisted/b256d7804672a6f6375476a5f5658b7b)

