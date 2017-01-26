// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Import the utility functionality.

import jobs.generation.Utilities;
import jobs.generation.InternalUtilities;

def project = GithubProject
def branch = GithubBranchName
def isPR = true

def osList = ['Windows_NT', 'Windows_NT_FullFramework', 'Ubuntu14.04']
def configList = ['Release', 'Debug']

def static getBuildJobName(def configuration, def os) {
    return configuration.toLowerCase() + '_' + os.toLowerCase()
}

osList.each { os ->
    configList.each { config ->
        // Calculate job name
        def jobName = getBuildJobName(config, os)
        def buildCommand = '';

        def osBase = os

        // Calculate the build command
        if (os == 'Windows_NT') {
            buildCommand = ".\\build.cmd -Configuration $config"
        } else if (os == 'Windows_NT_FullFramework') {
            buildCommand = ".\\build.cmd -Configuration $config -FullMSBuild"
            osBase = 'Windows_NT'
        } else {
            // Jenkins non-Ubuntu CI machines don't have docker
            buildCommand = "./build.sh --configuration $config"
        }

        def newJob = job(Utilities.getFullJobName(project, jobName, isPR)) {
            // Set the label.
            steps {
                if (osBase == 'Windows_NT') {
                    // Batch
                    batchFile(buildCommand)
                }
                else {
                    // Shell
                    shell(buildCommand)
                }
            }
        }

        Utilities.setMachineAffinity(newJob, osBase, 'latest-or-auto-internal')
        InternalUtilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
        Utilities.addXUnitDotNETResults(newJob, "bin/$config/Tests/TestResults.xml", false)
        Utilities.addGithubPRTriggerForBranch(newJob, branch, "$os $config")
    }
}
