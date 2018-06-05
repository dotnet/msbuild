// Import the utility functionality.
import jobs.generation.*;

// The input project name
project = GithubProject

// The input branch name (e.g. master)
branch = GithubBranchName

// What this repo is using for its machine images at the current time
imageVersionMap = ['Windows_NT':'latest-dev15-5',
                    'OSX10.13':'latest-or-auto',
                    'Ubuntu16.04':'20170731',
                    'RHEL7.2' : 'latest']

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

[true, false].each { isPR ->
    ['Windows_NT', 'OSX10.13', 'Ubuntu16.04'].each {osName ->
        def runtimes = ['CoreCLR']

        if (osName == 'Windows_NT') {
            runtimes.add('Full')
        }

        // TODO: make this !windows once RHEL builds are working
        //if (osName.startsWith('Ubuntu') || osName.startsWith('OSX')) {
            //runtimes.add('Mono')
            //runtimes.add('MonoTest')
        //}

        def script = "NA"
        def machineAffinityOverride = null
        def shouldSkipTestsWhenResultsNotFound = false

        runtimes.each { runtime ->
            switch(osName) {
                case 'Windows_NT':

                    // Protect against VsDevCmd behaviour of changing the current working directory https://developercommunity.visualstudio.com/content/problem/26780/vsdevcmdbat-changes-the-current-working-directory.html
                    script = "set VSCMD_START_DIR=\"%CD%\" && call \"C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\Common7\\Tools\\VsDevCmd.bat\""

                    if (runtime == "Full") {
                        script += " && build\\cibuild.cmd"
                    }
                    else if (runtime == "CoreCLR") {
                        script += " && build\\cibuild.cmd -hostType Core"
                    }

                    // this agent has VS 15.7 on it, which is a min requirement for our repo now.
                    machineAffinityOverride = 'Windows.10.Amd64.ClientRS3.DevEx.Open'

                    break;
                case 'OSX10.13':
                    script = "./build/cibuild.sh"

                    if (runtime == "MonoTest" || runtime == "Mono") {
                        // default is to run tests!
                        script += " -hostType mono"
                    }

                    if (runtime == "Mono") {
                        // tests are failing on mono right now, so default to
                        // skipping tests
                        script += " -skipTests"
                        shouldSkipTestsWhenResultsNotFound = true
                    }

                    break;
                case { it.startsWith('Ubuntu') }:
                    script = "./build/cibuild.sh"

                    if (runtime == "MonoTest" || runtime == "Mono") {
                        // default is to run tests!
                        script += " -hostType mono"
                    }

                    if (runtime == "Mono") {
                        // tests are failing on mono right now, so default to
                        // skipping tests
                        script += " -skipTests"
                        shouldSkipTestsWhenResultsNotFound = true
                    }

                    break;
            }

            CreateJob(script, runtime, osName, isPR, machineAffinityOverride, shouldSkipTestsWhenResultsNotFound)
        }
    }
}

// sourcebuild simulation
CreateJob(
    "./build/build.sh build -dotnetBuildFromSource -bootstraponly -skiptests -pack -configuration Release",
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
