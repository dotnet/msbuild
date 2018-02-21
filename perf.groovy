// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Import the utility functionality.

import jobs.generation.ArchivalSettings;
import jobs.generation.Utilities;

def project = GithubProject
def branch = GithubBranchName

def static getBuildJobName(def configuration, def os) {
    return configuration.toLowerCase() + '_' + os.toLowerCase()
}

// Setup SDK performance tests runs
[true, false].each { isPR ->
    ['Windows_NT'].each { os ->
        ['x64', 'x86'].each { arch ->
            def architecture = arch
            def jobName = "SDK_Perf_${os}_${arch}"
            def testEnv = ""
            def newJob = job(Utilities.getFullJobName(project, jobName, isPR)) {

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

                def configuration = 'Release'
                def runType = isPR ? 'private' : 'rolling'
                def benchViewName = isPR ? 'SDK private %BenchviewCommitName%' : 'SDK rolling %GIT_BRANCH_WITHOUT_ORIGIN% %GIT_COMMIT%'
                def uploadString = '-uploadToBenchview'
                def perfWorkingDirectory = '%WORKSPACE%\\artifacts\\${configuration}\\TestResults\\Performance'

                steps {
                    // Batch
                    batchFile("powershell -NoProfile wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile \"${perfWorkingDirectory}\\nuget.exe\"")
                    batchFile("if exist \"${perfWorkingDirectory}\\Microsoft.BenchView.JSONFormat\" rmdir /s /q \"${perfWorkingDirectory}\\Microsoft.BenchView.JSONFormat\"")
                    batchFile("\"${perfWorkingDirectory}\\nuget.exe\" install Microsoft.BenchView.JSONFormat -Source http://benchviewtestfeed.azurewebsites.net/nuget -OutputDirectory \"${perfWorkingDirectory}\" -Prerelease -ExcludeVersion")
                    // Do this here to remove the origin but at the front of the branch name as this is a problem for BenchView
                    //    we have to do it all as one statement because cmd is called each time and we lose the set environment variable
                    batchFile("if \"%GIT_BRANCH:~0,7%\" == \"origin/\" (set \"GIT_BRANCH_WITHOUT_ORIGIN=%GIT_BRANCH:origin/=%\") else (set \"GIT_BRANCH_WITHOUT_ORIGIN=%GIT_BRANCH%\")\n" +
                    "set \"BENCHVIEWNAME=${benchViewName}\"\n" +
                    "set \"BENCHVIEWNAME=%BENCHVIEWNAME:\"=\"\"%\"\n" +
                    "py \"${perfWorkingDirectory}\\Microsoft.BenchView.JSONFormat\\tools\\submission-metadata.py\" --name \"%BENCHVIEWNAME%\" --user-email \"dotnet-bot@microsoft.com\" --output \"${perfWorkingDirectory}\\submission-metadata.json\"\n" +
                    "py \"${perfWorkingDirectory}\\Microsoft.BenchView.JSONFormat\\tools\\build.py\" git --branch %GIT_BRANCH_WITHOUT_ORIGIN% --type ${runType} --output \"${perfWorkingDirectory}\\build.json\"")
                    batchFile("py \"%WORKSPACE%\\Microsoft.BenchView.JSONFormat\\tools\\machinedata.py\" --output \"${perfWorkingDirectory}\\machinedata.json\"")

                   // Build solution and run the performance tests
                   batchFile("\"%WORKSPACE%\\build.cmd\" -sign -ci -perf /p:PerfIterations=10 /p:PerfOutputDirectory=\"${perfWorkingDirectory}\" /p:PerfCollectionType=stopwatch")

                   //Create submission json and upload to Benchview
                   batchFile("for /f \"tokens=*\" %%a in ('dir /b/a-d ${perfWorkingDirectory}\\*.xml') do (py \"${perfWorkingDirectory}\\Microsoft.BenchView.JSONFormat\\tools\\measurement.py xunitscenario \"${perfWorkingDirectory}\\%%a\" --better desc --append --output \"${perfWorkingDirectory}\\measurement.json\")")
                   batchFile("py \"${perfWorkingDirectory}\\Microsoft.BenchView.JSONFormat\\tools\\submission.py\" \"${perfWorkingDirectory}\\measurement.json\"" +
                   "--build \"${perfWorkingDirectory}\\build.json\"" +
                   "--machine-data \"${perfWorkingDirectory}\\machinedata.json\"" +
                   "--metadata \"${perfWorkingDirectory}\\submission-metadata.json\"" +
                   "--group \"SDK Perf Tests\"" +
                   "--type \"${runType}\"" +
                   "--config-name \"${configuration}\"" +
                   "--config Configuration \"${configuration}\"" +
                   "--architecture \"${arch}\"" +
                   "--machinepool \"perfsnake\"" +
                   "--output \"${perfWorkingDirectory}\submission.json\"")
                   batchFile("py \"${perfWorkingDirectory}\\Microsoft.BenchView.JSONFormat\\tools\\upload.py \"${perfWorkingDirectory}\submission.json\" --container coreclr")
                }
            }

            def archiveSettings = new ArchivalSettings()
            archiveSettings.addFiles('${perfWorkingDirectory}/**')
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


Utilities.createHelperJob(this, project, branch,
    "Welcome to the ${project} Perf help",
    "Have a nice day!")
