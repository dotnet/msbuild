import jobs.generation.Utilities
import jobs.generation.InternalUtilities

def project = GithubProject
def branch = GithubBranchName

// Generate a PR/nonPR job for debug (test only), which just does testing.
[true, false].each { isPR ->
    ['Debug', 'Release'].each { config ->
        def lowerCaseConfig = config.toLowerCase()

        def newJobName = InternalUtilities.getFullJobName(project, "windows_$lowerCaseConfig", isPR)

        def newJob = job(newJobName) {
            steps {
                batchFile("build.cmd -Configuration $config")
            }
        }

        // TODO: For when we actually have unit tests in this repo
        // Utilities.addXUnitDotNETResults(myJob, '**/xUnitResults/*.xml')

        Utilities.setMachineAffinity(newJob, 'Windows_NT', 'latest-or-auto-internal')
        InternalUtilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
        Utilities.addHtmlPublisher(newJob, "TestResults", "Unit Test Results", "index.html")

        if (isPR) {
            Utilities.addGithubPRTriggerForBranch(newJob, branch, "Windows $config")
        } else {
            Utilities.addGithubPushTrigger(newJob)
        }
    }
}