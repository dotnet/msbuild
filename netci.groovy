// Import the utility functionality.

import jobs.generation.Utilities;
import jobs.generation.InternalUtilities;

def project = 'dotnet/cli'

def osList = ['Ubuntu', 'OSX', 'Windows_NT']

def machineLabelMap = ['Ubuntu':'ubuntu',
                       'OSX':'mac',
                       'Windows_NT':'windows']

def static getBuildJobName(def configuration, def os) {
    return configuration.toLowerCase() + '_' + os.toLowerCase()
}


['Debug', 'Release'].each { configuration ->
    osList.each { os ->
        // Calculate names
        def lowerConfiguration = configuration.toLowerCase()

        // Calculate job name
        def jobName = getBuildJobName(configuration, os)
        def buildCommand = '';

        // Calculate the build command
        if (os == 'Windows_NT') {
            // On Windows we build the mscorlibs too.
            buildCommand = ".\\scripts\\ci_build.cmd ${lowerConfiguration}"
        }
        else {
            // On other OS's we skipmscorlib but run the pal tests
            buildCommand = "./scripts/ci_build.sh ${lowerConfiguration}"
        }

        // Create the new job
        def newCommitJob = job(Utilities.getFullJobName(project, jobName, false)) {
            // Set the label.
            label(machineLabelMap[os])
            steps {
                if (os == 'Windows_NT') {
                    // Batch
                    batchFile(buildCommand)
                }
                else {
                    // Shell
                    shell(buildCommand)
                }
            }
        }

        InternalUtilities.addPrivatePermissions(newCommitJob)
        InternalUtilities.addPrivateScm(newCommitJob, project)
        Utilities.addStandardOptions(newCommitJob)
        Utilities.addStandardNonPRParameters(newCommitJob)
        Utilities.addGithubPushTrigger(newCommitJob)


        def newPRJob = job(Utilities.getFullJobName(project, jobName, true)) {
            // Set the label.
            label(machineLabelMap[os])
            steps {
                if (os == 'Windows_NT') {
                    // Batch
                    batchFile(buildCommand)
                }
                else {
                    // Shell
                    shell(buildCommand)
                }
            }
        }


        InternalUtilities.addPrivatePermissions(newPRJob)
        InternalUtilities.addPrivatePRTestSCM(newPRJob, project)
        Utilities.addStandardOptions(newPRJob)
        Utilities.addStandardPRParameters(newPRJob, project)
        Utilities.addGithubPRTrigger(newPRJob, "${os} ${configuration} Build")
	}
}
