// Import the utility functionality.
import jobs.generation.Utilities;

// Defines a the new of the repo, used elsewhere in the file
def project = GithubProject

// Generate the builds for branches: xplat, master and PRs (which aren't branch specific)
['*/master', '*/xplat', 'pr'].each { branch ->
    ['Windows_NT', 'OSX', 'Ubuntu'].each {osName ->
        def isPR = false
        def newJobName = ''

        if (branch == 'pr') {
            isPR = true
            newJobName = Utilities.getFullJobName(project, "_${osName}", isPR)
        } else {
            newJobName = Utilities.getFullJobName(project, "innerloop_${branch.substring(2)}_${osName}", isPR)
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
                        batchFile("call \"C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat\" && RebuildWithLocalMSBuild.cmd")
                    }
                }
                break;
            case 'OSX':
                newJob.with{
                    steps{
                        shell("./cibuild.sh --scope Compile")
                    }
                }
                break;
            case 'Ubuntu':
                newJob.with{
                    steps{
                        shell("./cibuild.sh --scope Compile")
                    }
                }
                break;
        }
        
        Utilities.setMachineAffinity(newJob, osName)
        Utilities.standardJobSetup(newJob, project, isPR, branch)
        // Add xunit result archiving
        Utilities.addXUnitDotNETResults(newJob, 'bin/**/*_TestResults.xml')
        // Add archiving of logs
        Utilities.addArchival(newJob, 'msbuild.log')
        // Add trigger
        if (isPR) {
            Utilities.addGithubPRTrigger(newJob, "${osName} Build")
        } else {
            Utilities.addGithubPushTrigger(newJob)
        }
    }
    
}
