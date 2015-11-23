// Import the utility functionality.
import jobs.generation.Utilities;

// Defines a the new of the repo, used elsewhere in the file
def project = GithubProject

// Generate the builds for branches: xplat, master and PRs (which aren't branch specific)
['*/master', '*/xplat', 'pr'].each { branch ->
    def isPR = false
    def newJobName = ''
    if (branch == 'pr') {
        isPR = true
        newJobName = Utilities.getFullJobName(project, '', isPR)
    } else {
        newJobName = Utilities.getFullJobName(project, "innerloop_${branch.substring(2)}", isPR)
    }
        
    // Define build string.
    def buildString = "call \"C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat\" && RebuildWithLocalMSBuild.cmd"

    // Create a new job with the specified name.  The brace opens a new closure
    // and calls made within that closure apply to the newly created job.
    def newJob = job(newJobName) {    
        // This opens the set of build steps that will be run.
        steps {
            // Indicates that a batch script should be run with the build string (see above)
            batchFile(buildString)
        }
    }
        
    Utilities.setMachineAffinity(newJob, 'Windows_NT')
    Utilities.standardJobSetup(newJob, project, isPR, branch)
    // Add xunit result archiving
    Utilities.addXUnitDotNETResults(newJob, 'bin/**/*_TestResults.xml')
    // Add archiving of logs
    Utilities.addArchival(newJob, 'msbuild.log')
    // Add trigger
    if (isPR) {
        Utilities.addGithubPRTrigger(newJob, 'Windows Build')
    } else {
        Utilities.addGithubPushTrigger(newJob)
    }
}