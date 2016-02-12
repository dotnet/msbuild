// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Import the utility functionality.

import jobs.generation.Utilities;

def project = GithubProject
def branch = GithubBranchName

def osList = ['Ubuntu', 'OSX', 'Windows_NT', 'CentOS7.1']

def static getBuildJobName(def configuration, def os) {
    return configuration.toLowerCase() + '_' + os.toLowerCase()
}

[true, false].each { isPR ->
    ['Debug', 'Release'].each { configuration ->
        osList.each { os ->
            // Calculate names
            def lowerConfiguration = configuration.toLowerCase()

            // Calculate job name
            def jobName = getBuildJobName(configuration, os)
            def buildCommand = '';

            // Calculate the build command
            if (os == 'Windows_NT') {
                buildCommand = ".\\scripts\\ci_build.cmd ${lowerConfiguration}"
            }
            else {
                buildCommand = "./scripts/ci_build.sh ${lowerConfiguration}"
            }

            def newJob = job(Utilities.getFullJobName(project, jobName, isPR)) {
                // Set the label.
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

            Utilities.setMachineAffinity(newJob, os, 'latest-or-auto')
            Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
            Utilities.addXUnitDotNETResults(newJob, '**/*-testResults.xml')
            if (isPR) {
                Utilities.addGithubPRTriggerForBranch(newJob, branch, "${os} ${configuration} Build")
            }
            else {
                Utilities.addGithubPushTrigger(newJob)
            }
        }
    }
}