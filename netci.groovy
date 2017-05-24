// Import the utility functionality.
import jobs.generation.Utilities;
import jobs.generation.JobReport;

// The input project name
def project = GithubProject
// The input branch name (e.g. master)
def branch = GithubBranchName

[true, false].each { isPR ->
    ['Windows_NT', 'OSX', 'Ubuntu14.04', 'Ubuntu16.04'].each {osName ->
        def runtimes = ['CoreCLR']

        if (osName == 'Windows_NT') {
            runtimes.add('Full')
        }

        // TODO: Mono

        runtimes.each { runtime ->
            def newJobName = Utilities.getFullJobName("innerloop_${osName}_${runtime}", isPR)
            def skipTestsWhenResultsNotFound = true

            // Create a new job with the specified name.  The brace opens a new closure
            // and calls made within that closure apply to the newly created job.
            def newJob = job(newJobName) {
                description('')
            }

            // Define job.
            switch(osName) {
                case 'Windows_NT':
                    newJob.with{
                        steps{
                            // all windows builds do a full framework localized build to produce satellite assemblies
                            def script = "call \"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\Common7\\Tools\\VsDevCmd.bat\""

                            if (runtime == "Full") {
                                script += " && cibuild.cmd --target Full --scope Test"
                            }
                            // .net core builds are localized (they need the satellites from the full framework build), run tests, and also build the nuget packages
                            else if (runtime == "CoreCLR") {
                                script += " && cibuild.cmd --windows-core-localized-job"
                            }

                            batchFile(script)
                        }

                        skipTestsWhenResultsNotFound = false
                    }
                    Utilities.setMachineAffinity(newJob, 'Windows_NT', 'latest-or-auto-dev15-rc')

                    break;
                case 'OSX':
                    newJob.with{
                        steps{
                            shell("./cibuild.sh --scope Test --target ${runtime}")
                        }
                    }
                    Utilities.setMachineAffinity(newJob, osName, 'latest-or-auto')

                    break;
                case { it.startsWith('Ubuntu') }:
                    newJob.with{
                        steps{
                            shell("./cibuild.sh --scope Test --target ${runtime}")
                        }
                    }
                    Utilities.setMachineAffinity(newJob, osName, 'latest-or-auto')

                    break;
            }

            // Add xunit result archiving. Skip if no results found.
            Utilities.addXUnitDotNETResults(newJob, 'bin/**/*_TestResults.xml', skipTestsWhenResultsNotFound)
            Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
            // Add archiving of logs (even if the build failed)
            Utilities.addArchival(newJob,
                                  'init-tools.log,msbuild*.log,msbuild*.binlog,**/Microsoft.*.UnitTests.dll_*', /* filesToArchive */
                                  '', /* filesToExclude */
                                  false, /* doNotFailIfNothingArchived */
                                  false, /* archiveOnlyIfSuccessful */)
            // Add trigger
            if (isPR) {
                Utilities.addGithubPRTriggerForBranch(newJob, branch, "${osName} Build for ${runtime}")
            } else {
                Utilities.addGithubPushTrigger(newJob)
            }
        }
    }
}

JobReport.Report.generateJobReport(out)

// Make the call to generate the help job
Utilities.createHelperJob(this, project, branch,
    "Welcome to the ${project} Repository",  // This is prepended to the help message
    "Have a nice day!")  // This is appended to the help message.  You might put known issues here.