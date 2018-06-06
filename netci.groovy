// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Import the utility functionality.

import jobs.generation.ArchivalSettings;
import jobs.generation.Utilities;

def project = GithubProject
def branch = GithubBranchName
def isPR = true

def platformList = ['Linux:x64:Release', 'Debian8.2:x64:Debug', 'Ubuntu:x64:Release', 'Ubuntu16.04:x64:Debug', 'OSX10.12:x64:Release', 'Windows_NT:x64:Release', 'Windows_NT:x86:Debug', 'RHEL7.2:x64:Release', 'CentOS7.1:x64:Debug', 'ubuntu.18.04:x64:Debug', 'fedora.27:x64:Debug', 'opensuse.43.2:x64:Debug']

def static getBuildJobName(def configuration, def os, def architecture) {
    return configuration.toLowerCase() + '_' + os.toLowerCase() + '_' + architecture.toLowerCase()
}


platformList.each { platform ->
    // Calculate names
    def (os, architecture, configuration) = platform.tokenize(':')
    def osUsedForMachineAffinity = os;
    def osVersionUsedForMachineAffinity = 'latest-or-auto';

    // Calculate job name
    def jobName = getBuildJobName(configuration, os, architecture)
    def buildCommand = '';

    // Calculate the build command
    if (os == 'Windows_NT') {
        buildCommand = ".\\build.cmd -Configuration ${configuration} -Architecture ${architecture} -Targets Default"
    }
    else if (os == 'Windows_2016') {
        buildCommand = ".\\build.cmd -Configuration ${configuration} -Architecture ${architecture} -RunInstallerTestsInDocker -Targets Default"
    }
    else if (os == 'Ubuntu') {
        buildCommand = "./build.sh --skip-prereqs --configuration ${configuration} --docker ubuntu.14.04 --targets Default"
    }
    else if (os == 'Linux') {
        osUsedForMachineAffinity = 'Ubuntu16.04';
        buildCommand = "./build.sh --linux-portable --skip-prereqs --configuration ${configuration} --targets Default"
    }
    else if (os == 'ubuntu.18.04' || os == 'fedora.27' || os == 'opensuse.43.2') {
        osUsedForMachineAffinity = 'Ubuntu16.04'
        osVersionUsedForMachineAffinity = 'latest-docker'
        buildCommand = "./build.sh --linux-portable --skip-prereqs --configuration ${configuration} --docker ${os} --targets Default"
    }
    else {
        // Jenkins non-Ubuntu CI machines don't have docker
        buildCommand = "./build.sh --skip-prereqs --configuration ${configuration} --targets Default"
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

    Utilities.setMachineAffinity(newJob, osUsedForMachineAffinity, osVersionUsedForMachineAffinity)
    Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
    Utilities.addMSTestResults(newJob, '**/*.trx')
    Utilities.addGithubPRTriggerForBranch(newJob, branch, "${os} ${architecture} ${configuration} Build")

	def archiveSettings = new ArchivalSettings()
	archiveSettings.addFiles("test/**/*.trx")
	archiveSettings.setFailIfNothingArchived()
	archiveSettings.setArchiveOnFailure()
    Utilities.addArchival(newJob, archiveSettings)
}

// Make the call to generate the help job
Utilities.createHelperJob(this, project, branch,
    "Welcome to the ${project} Repository",  // This is prepended to the help message
    "Have a nice day!")  // This is appended to the help message.  You might put known issues here.

Utilities.addCROSSCheck(this, project, branch)
