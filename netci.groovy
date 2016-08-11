// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Import the utility functionality.

import jobs.generation.Utilities;

def project = GithubProject
def branch = GithubBranchName
def isPR = true

def platformList = ['Debian8.2:Debug', 'Ubuntu:Release', 'Ubuntu16.04:Debug', 'OSX:Release', 'Windows_NT:Release', 'Windows_NT:Debug', 'RHEL7.2:Release', 'CentOS7.1:Debug', 'Fedora23:Debug', 'OpenSUSE13.2:Debug']

def static getBuildJobName(def configuration, def os) {
    return configuration.toLowerCase() + '_' + os.toLowerCase()
}


platformList.each { platform ->
    // Calculate names
    def (os, config) = platform.tokenize(':')

    // Calculate job name
    def jobName = getBuildJobName(configuration, os)
    def buildCommand = '';

    // Calculate the build command
    if (os == 'Windows_NT' || os == 'Windows_2016') {
        buildCommand = ".\\build.cmd -Configuration $config"
    } else {
        // Jenkins non-Ubuntu CI machines don't have docker
        buildCommand = "./build.sh --configuration $config"
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

    Utilities.setMachineAffinity(newJob, os, 'latest-or-auto-internal')
    InternalUtilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
    Utilities.addXUnitDotNETResults(newJob, "bin/$config/Tests/TestResults.xml", false)
    Utilities.addGithubPRTriggerForBranch(newJob, branch, "$os $config")
}


