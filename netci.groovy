// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Import the utility functionality.

import jobs.generation.ArchivalSettings;
import jobs.generation.Utilities;

def project = GithubProject
def branch = GithubBranchName
def isPR = true

def platformList = [
  'CentOS7.1:x64:Debug',
  'Debian8.2:x64:Debug',
  'fedora.27:x64:Debug',
  'Linux:arm:Debug',
  'Linux:arm64:Debug',
  'Linux-musl:x64:Debug',
  'Linux:x64:Release',
  'Linux_NoSuffix:arm:Release',
  'Linux_NoSuffix:x64:Release',
  'opensuse.42.3:x64:Debug',
  'OSX10.12:x64:Release',
  'RHEL6:x64:Debug',
  'RHEL7.2:x64:Release',
  'Ubuntu:x64:Release',
  'Ubuntu16.04:x64:Debug',
  'ubuntu.18.04:x64:Debug',
  'Windows_NT:x64:Release',
  'Windows_NT:x86:Debug',
  'Windows_NT_ES:x64:Debug',
  'Windows_NT_NoSuffix:x64:Release'
]

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
    def baseBatchBuildCommand = ".\\build.cmd -Configuration ${configuration} -Architecture ${architecture} -Targets Default";
    def baseShellBuildCommand = "./build.sh --skip-prereqs --configuration ${configuration} --targets Default";

    // Calculate the build command
    if (os.startsWith("Windows_NT")) {
        osUsedForMachineAffinity = 'Windows_NT'
        buildCommand = "${baseBatchBuildCommand}"
        if (os == 'Windows_NT_ES') {
            buildCommand = """
set DOTNET_CLI_UI_LANGUAGE=es
${buildCommand}
"""
        }
        else if (os == 'Windows_NT_NoSuffix') {
            buildCommand = "${buildCommand} /p:DropSuffix=true"
        }
    }
    else if (os == 'Windows_2016') {
        buildCommand = "${baseBatchBuildCommand} -RunInstallerTestsInDocker"
    }
    else if (os.startsWith("Linux")) {
        osUsedForMachineAffinity = 'Ubuntu16.04';
        if (os == 'Linux-musl') {
            buildCommand = "${buildCommand} --runtime-id linux-musl-x64 --docker alpine.3.6"
        }
        else
        {
            buildCommand = "${baseShellBuildCommand} --linux-portable"
            if ((architecture == 'arm') || (architecture == 'arm64')) {
                buildCommand = "${buildCommand} --architecture ${architecture} /p:CLIBUILD_SKIP_TESTS=true"
            }
            if (os == 'Linux_NoSuffix') {
                buildCommand = "${buildCommand} /p:DropSuffix=true"
            }
        }
    }
    else if (os == 'Ubuntu') {
        buildCommand = "${baseShellBuildCommand} --docker ubuntu.14.04"
    }
    else if (os == 'RHEL6') {
        osUsedForMachineAffinity = 'Ubuntu16.04';
        buildCommand = "${baseShellBuildCommand} --runtime-id rhel.6-x64 --docker rhel.6"
    }
    else if (os == 'ubuntu.18.04' || os == 'fedora.27' || os == 'opensuse.42.3') {
        osUsedForMachineAffinity = 'Ubuntu16.04'
        osVersionUsedForMachineAffinity = 'latest-docker'
        buildCommand = "${baseShellBuildCommand} --docker ${os} --linux-portable"
    }
    else {
        buildCommand = "${baseShellBuildCommand}"
    }

    def newJob = job(Utilities.getFullJobName(project, jobName, isPR)) {
        // Set the label.
        steps {
            if (osUsedForMachineAffinity == 'Windows_NT' || osUsedForMachineAffinity == 'Windows_2016') {
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
    // ARM CI runs are build only.
    if ((architecture != 'arm') && (architecture != 'arm64')) {
        Utilities.addMSTestResults(newJob, '**/*.trx')
    }
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
