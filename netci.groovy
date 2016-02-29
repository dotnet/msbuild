// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Import the utility functionality.

import jobs.generation.Utilities;

def project = GithubProject
def branch = GithubBranchName

def osList = ['Ubuntu', 'OSX', 'Windows_NT', 'Windows_2016', 'CentOS7.1']

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
                buildCommand = ".\\build.cmd -Configuration ${lowerConfiguration} -Targets Default"
            }
            else if (os == 'Windows_2016') {
                buildCommand = ".\\build.cmd -Configuration ${lowerConfiguration} -RunInstallerTestsInDocker -Targets Default"
            }
            else if (os == 'Ubuntu') {
                buildCommand = "./build.sh --skip-prereqs --configuration ${lowerConfiguration} --docker ubuntu --targets Default"
            }
            else {
                // Jenkins non-Ubuntu CI machines don't have docker
                buildCommand = "./build.sh --skip-prereqs --configuration ${lowerConfiguration} --targets Default"
            }

            def newJob = job(Utilities.getFullJobName(project, jobName, isPR)) {
                // Set the label.
                steps {
                    if (os == 'Windows_NT' || os == 'Windows_2016') {
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
