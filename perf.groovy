// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Import the utility functionality.

import jobs.generation.ArchivalSettings;
import jobs.generation.Utilities;
import jobs.generation.TriggerBuilder;

def project = GithubProject
def branch = GithubBranchName

def static getBuildJobName(def configuration, def os) {
    return configuration.toLowerCase() + '_' + os.toLowerCase()
}

// Setup SDK performance tests runs
[true, false].each { isPR ->
    ['Windows_NT'].each { os ->
      ['Release'].each { config ->
        ['x64', 'x86'].each { arch ->
            def jobName = "SDK_Perf_${os}_${arch}"
            def newJob = job(Utilities.getFullJobName(project, jobName, isPR)) {
            def perfWorkingDirectory = "%WORKSPACE%\\artifacts\\${config}\\TestResults\\Performance"

                // Set the label.
                label('windows_server_2016_clr_perf')
                wrappers {
                    credentialsBinding {
                        string('BV_UPLOAD_SAS_TOKEN', 'CoreCLR Perf BenchView Sas')
                    }
                }

                if (isPR) {
                    parameters {
                        stringParam('BenchviewCommitName', '\${ghprbPullTitle}', 'The name that you will be used to build the full title of a run in Benchview.  The final name will be of the form SDK <private|rolling> BenchviewCommitName')
                    }
                }

                def runType = isPR ? 'private' : 'rolling'

                steps {
                   // Build solution and run the performance tests
                   batchFile("\"%WORKSPACE%\\build.cmd\" -configuration ${config} -sign -ci -perf /p:PerfIterations=10 /p:PerfOutputDirectory=\"${perfWorkingDirectory}\" /p:PerfCollectionType=stopwatch")

                   // Upload perf results to BenchView
                   batchFile("set perfWorkingDirectory=${perfWorkingDirectory}\n" +
                   "set configuration=${config}\n" +
                   "set architecture=${arch}\n" +
                   "set runType=${runType}\n" +
                   "\"%WORKSPACE%\\build\\uploadperftobenchview.cmd\"")
                }
            }

            def archiveSettings = new ArchivalSettings()
            archiveSettings.addFiles("artifacts/${config}/TestResults/Performance/**")
            archiveSettings.setAlwaysArchive()
            Utilities.addArchival(newJob, archiveSettings)
            Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")

            newJob.with {
                logRotator {
                    artifactDaysToKeep(30)
                    daysToKeep(30)
                    artifactNumToKeep(200)
                    numToKeep(200)
                }
                wrappers {
                    timeout {
                        absolute(240)
                    }
                }
            }

            if (isPR) {
                TriggerBuilder builder = TriggerBuilder.triggerOnPullRequest()
                builder.setGithubContext("${os} ${arch} SDK Perf Tests")

                builder.triggerOnlyOnComment()
                //Phrase is "test Windows_NT x64 SDK Perf Tests"
                builder.setCustomTriggerPhrase("(?i).*test\\W+${os}\\W+${arch}\\W+sdk\\W+perf\\W+tests.*")
                builder.triggerForBranch(branch)
                builder.emitTrigger(newJob)
            }
            else {
                TriggerBuilder builder = TriggerBuilder.triggerOnCommit()
                builder.emitTrigger(newJob)
            }
        }
      }
    }
}


Utilities.createHelperJob(this, project, branch,
    "Welcome to the ${project} Perf help",
    "Have a nice day!")
