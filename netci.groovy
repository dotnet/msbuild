// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Import the utility functionality.

import jobs.generation.Utilities;
import jobs.generation.InternalUtilities;

def project = 'dotnet/cli'

def osList = ['Ubuntu', 'OSX', 'Windows_NT']

def machineLabelMap = ['Ubuntu':'ubuntu-doc',
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
        def postBuildCommand = '';

        // Calculate the build command
        if (os == 'Windows_NT') {
            buildCommand = ".\\scripts\\ci_build.cmd ${lowerConfiguration}"
        }
        else {
            buildCommand = "./scripts/ci_build.sh ${lowerConfiguration}"
        }

        // Create the new job
        def newCommitJob = job(InternalUtilities.getFullJobName(project, jobName, false)) {
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


        def newPRJob = job(InternalUtilities.getFullJobName(project, jobName, true)) {
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

                    // Post Build Cleanup
                    publishers {
                        postBuildScripts {
                            steps {
                                shell(postBuildCommand)
                            }
                            onlyIfBuildSucceeds(false)
                        }
                    }

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
