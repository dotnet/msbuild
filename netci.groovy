// Import the utility functionality.
import jobs.generation.Utilities;

// Defines a the new of the repo, used elsewhere in the file
def project = GithubProject

// Generate the builds for branches: xplat, master and PRs (which aren't branch specific)
['*/master', '*/xplat', 'pr'].each { branch ->
    ['Windows_NT', 'OSX', 'Ubuntu14.04', 'Ubuntu16.04'].each {osName ->
        def runtimes = ['CoreCLR']

        if (osName == 'Windows_NT') {
            runtimes.add('Desktop')
        }

        // TODO: Mono

        runtimes.each { runtime ->
            def isPR = false
            def newJobName = ''
            def skipTestsWhenResultsNotFound = true

            if (branch == 'pr') {
                isPR = true
                newJobName = Utilities.getFullJobName(project, "_${osName}_${runtime}", isPR)
            } else {
                newJobName = Utilities.getFullJobName(project, "innerloop_${branch.substring(2)}_${osName}_${runtime}", isPR)
            }

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
                            def windowsScript = "call \"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\Common7\\Tools\\VsDevCmd.bat\" && cibuild.cmd --target ${runtime}"

                            // only Desktop support localized builds 
                            if (runtime == "Desktop") {
                                windowsScript += " --localized-build"
                            }

                            batchFile(windowsScript)
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
            Utilities.standardJobSetup(newJob, project, isPR, branch)
            // Add archiving of logs (even if the build failed)
            Utilities.addArchival(newJob,
                                  'init-tools.log,msbuild*.log,msbuild*.binlog,**/Microsoft.*.UnitTests.dll_*', /* filesToArchive */
                                  '', /* filesToExclude */
                                  false, /* doNotFailIfNothingArchived */
                                  false, /* archiveOnlyIfSuccessful */)
            // Add trigger
            if (isPR) {
                Utilities.addGithubPRTrigger(newJob, "${osName} Build for ${runtime}")
            } else {
                Utilities.addGithubPushTrigger(newJob)
            }
        }
    }
}
