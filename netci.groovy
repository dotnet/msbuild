// Import the utility functionality.
import jobs.generation.*;

// The input project name
project = GithubProject

// The input branch name (e.g. master)
branch = GithubBranchName

// What this repo is using for its machine images at the current time
imageVersionMap = ['RHEL7.2' : 'latest']

def CreateJob(script, runtime, osName, isPR, machineAffinityOverride = null, shouldSkipTestsWhenResultsNotFound = false, isSourceBuild = false) {
    def newJobName = Utilities.getFullJobName("innerloop_${osName}_${runtime}${isSourceBuild ? '_SourceBuild_' : ''}", isPR)

    // Create a new job with the specified name.  The brace opens a new closure
    // and calls made within that closure apply to the newly created job.
    def newJob = job(newJobName) {
        description('')
    }

    newJob.with{
        steps{
            if(osName.contains("Windows") || osName.contains("windows")) {
                batchFile(script)
            } else {
                shell(script)
            }
        }

        skipTestsWhenResultsNotFound = shouldSkipTestsWhenResultsNotFound
    }

    // Add xunit result archiving. Skip if no results found.
    Utilities.addXUnitDotNETResults(newJob, 'artifacts/**/TestResults/*.xml', skipTestsWhenResultsNotFound)

    if (machineAffinityOverride == null) {
        def imageVersion = imageVersionMap[osName];
        Utilities.setMachineAffinity(newJob, osName, imageVersion)
    }
    else {
        Utilities.setMachineAffinity(newJob, machineAffinityOverride)
    }

    Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
    // Add archiving of logs (even if the build failed)
    Utilities.addArchival(newJob,
                        'artifacts/**/log/*.binlog,artifacts/**/log/*.log,artifacts/**/TestResults/*,artifacts/**/MSBuild_*.failure.txt', /* filesToArchive */
                        '', /* filesToExclude */
                        false, /* doNotFailIfNothingArchived */
                        false, /* archiveOnlyIfSuccessful */)
    // Add trigger
    if (isPR) {
        TriggerBuilder prTrigger = TriggerBuilder.triggerOnPullRequest()

        if (runtime == "MonoTest") {
            // Until they're passing reliably, require opt in
            // for Mono tests
            prTrigger.setCustomTriggerPhrase("(?i).*test\\W+mono.*")
            prTrigger.triggerOnlyOnComment()
        }

        prTrigger.triggerForBranch(branch)
        // Set up what shows up in Github:
        prTrigger.setGithubContext("${osName} Build for ${runtime}")
        prTrigger.emitTrigger(newJob)
    } else {
        if (runtime != "Mono") {
            Utilities.addGithubPushTrigger(newJob)
        }
    }
}

// sourcebuild simulation
CreateJob(
    "./build/build.sh build -dotnetBuildFromSource -skiptests -pack -configuration Release",
    "CoreCLR",
    "RHEL7.2",
    true,
    null,
    true,
    true)

JobReport.Report.generateJobReport(out)

// Make the call to generate the help job
Utilities.createHelperJob(this, project, branch,
    "Welcome to the ${project} Repository",  // This is prepended to the help message
    "Have a nice day!")  // This is appended to the help message.  You might put known issues here.
