// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;
using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;
using LegacyThreadingData = Microsoft.Build.Execution.LegacyThreadingData;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for the TaskBuilder component
    /// </summary>
    public class TaskBuilder_Tests : ITargetBuilderCallback
    {
        private readonly ITestOutputHelper _testOutput;

        /// <summary>
        /// Prepares the environment for the test.
        /// </summary>
        public TaskBuilder_Tests(ITestOutputHelper output)
        {
            _testOutput = output;
        }

        /*********************************************************************************
         *
         *                                  OUTPUT PARAMS
         *
         *********************************************************************************/

        /// <summary>
        /// Verifies that we do look up the task during execute when the condition is true.
        /// </summary>
        [Fact]
        public void TasksAreDiscoveredWhenTaskConditionTrue()
        {
            MockLogger logger = new MockLogger();
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                      <Target Name='t'>
                         <NonExistentTask Condition=""'1'=='1'""/>
                         <Message Text='Made it'/>
                      </Target>
                      </Project>");

            using ProjectFromString projectFromString = new(projectFileContents);
            Project project = projectFromString.Project;
            List<ILogger> loggers = new List<ILogger>();
            loggers.Add(logger);
            project.Build("t", loggers);

            logger.AssertLogContains("MSB4036");
            logger.AssertLogDoesntContain("Made it");
        }

        [Fact]
        public void TasksOnlyLogStartedEventOnceEach()
        {
            using TestEnvironment env = TestEnvironment.Create();
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(
            @"<Project>
              <Target Name='t'>
                  <Message Text='Made it'/>
              </Target>
            </Project>");

            TransientTestFile projectFile = env.CreateFile("myProj.proj", projectFileContents);
            env.SetEnvironmentVariable("DOTNET_PERFLOG_DIR", @"C:\Users\namytelk\Desktop");

            string results = RunnerUtilities.ExecMSBuild(projectFile.Path + " /v:diag", out bool success);

            int count = 0;
            for (int index = results.IndexOf("Task \"Message\""); index >= 0; index = results.IndexOf("Task \"Message\"", index))
            {
                count++;
                index += 14; // Skip to the end of this string
            }

            count.ShouldBe(1);
        }

        /// <summary>
        /// Tests that when the task condition is false, Execute still returns true even though we never loaded
        /// the task.  We verify that we never loaded the task because if we did try, the task load itself would
        /// have failed, resulting in an error.
        /// </summary>
        [Fact]
        public void TasksNotDiscoveredWhenTaskConditionFalse()
        {
            MockLogger logger = new MockLogger();
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                      <Target Name='t'>
                         <NonExistentTask Condition=""'1'=='2'""/>
                         <Message Text='Made it'/>
                      </Target>
                      </Project>");

            using ProjectFromString projectFromString = new(projectFileContents);
            Project project = projectFromString.Project;
            List<ILogger> loggers = new List<ILogger>();
            loggers.Add(logger);
            project.Build("t", loggers);

            logger.AssertLogContains("Made it");
        }

        /// <summary>
        /// Data structure to hold process information for hang detection
        /// </summary>
        private sealed class MSBuildProcessInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public DateTime StartTime { get; set; }
            public long WorkingSetMB { get; set; }
            public int ThreadCount { get; set; }
            public TimeSpan CpuTime { get; set; }
            public bool IsResponding { get; set; }
            public string CommandLine { get; set; }
        }

        /// <summary>
        /// Enhanced wait method with MSBuild hang detection and comprehensive diagnostics
        /// </summary>
        /// <param name="buildSubmission">The build submission to wait for</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="eventLog">Event log for timeline tracking</param>
        /// <returns>True if completed within timeout, false otherwise</returns>
        private bool WaitWithMSBuildHangDetection(
            BuildSubmission buildSubmission, 
            string operationName,
            List<(DateTime Time, string Event, string Details)> eventLog)
        {
            var startTime = DateTime.UtcNow;
            eventLog.Add((startTime, "WaitStart", $"Beginning {operationName} wait"));

            // Phase 1: Normal timeout (2-3 seconds)
            const int normalTimeoutMs = 2000;
            const int extendedTimeoutMs = 15000;
            const int monitoringIntervalMs = 1000;

            _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Starting {operationName} with normal timeout {normalTimeoutMs}ms");

            bool completed = buildSubmission.WaitHandle.WaitOne(normalTimeoutMs);
            if (completed)
            {
                var elapsed = DateTime.UtcNow - startTime;
                eventLog.Add((DateTime.UtcNow, "CompletedNormal", $"Completed in {elapsed.TotalMilliseconds:F0}ms"));
                _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {operationName} completed normally in {elapsed.TotalMilliseconds:F0}ms");
                return true;
            }

            // Phase 2: Extended monitoring with hang detection
            eventLog.Add((DateTime.UtcNow, "ExtendedMonitoringStart", "Normal timeout expired, starting extended monitoring"));
            _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {operationName} normal timeout expired, starting extended monitoring");

            var monitoringStart = DateTime.UtcNow;
            var processSnapshots = new List<List<MSBuildProcessInfo>>();
            var hangPatterns = new List<string>();

            // Collect system information
            var systemInfo = GetSystemInfo();
            _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] System Info: {systemInfo}");
            eventLog.Add((DateTime.UtcNow, "SystemInfo", systemInfo));

            // Extended monitoring loop
            while (DateTime.UtcNow - monitoringStart < TimeSpan.FromMilliseconds(extendedTimeoutMs - normalTimeoutMs))
            {
                // Check if completed
                completed = buildSubmission.WaitHandle.WaitOne(100);
                if (completed)
                {
                    var totalElapsed = DateTime.UtcNow - startTime;
                    eventLog.Add((DateTime.UtcNow, "CompletedExtended", $"Completed during extended monitoring in {totalElapsed.TotalMilliseconds:F0}ms total"));
                    _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {operationName} completed during extended monitoring in {totalElapsed.TotalMilliseconds:F0}ms total");
                    return true;
                }

                // Collect process information
                var currentProcesses = CollectMSBuildProcessInfo();
                processSnapshots.Add(currentProcesses);

                // Log current state
                var elapsed = DateTime.UtcNow - startTime;
                _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {operationName} still waiting, elapsed: {elapsed.TotalMilliseconds:F0}ms, MSBuild processes: {currentProcesses.Count}");
                eventLog.Add((DateTime.UtcNow, "MonitoringCheck", $"Elapsed: {elapsed.TotalMilliseconds:F0}ms, Processes: {currentProcesses.Count}"));

                // Detect hang patterns every few seconds
                if (processSnapshots.Count >= 3)
                {
                    var patterns = DetectHangPatterns(processSnapshots, buildSubmission);
                    hangPatterns.AddRange(patterns);
                    
                    if (patterns.Any())
                    {
                        _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Hang patterns detected: {string.Join(", ", patterns)}");
                        eventLog.Add((DateTime.UtcNow, "HangPatterns", string.Join(", ", patterns)));
                    }
                }

                // Create process dumps at specific intervals
                var monitoringElapsed = DateTime.UtcNow - monitoringStart;
                if (monitoringElapsed.TotalSeconds >= 6 && monitoringElapsed.TotalSeconds < 7)
                {
                    CreateProcessDumps(currentProcesses, "6sec");
                }
                else if (monitoringElapsed.TotalSeconds >= 10 && monitoringElapsed.TotalSeconds < 11)
                {
                    CreateProcessDumps(currentProcesses, "10sec");
                }

                Thread.Sleep(monitoringIntervalMs);
            }

            // Final timeout - comprehensive failure analysis
            var finalElapsed = DateTime.UtcNow - startTime;
            eventLog.Add((DateTime.UtcNow, "FinalTimeout", $"Final timeout after {finalElapsed.TotalMilliseconds:F0}ms"));
            
            // Final process dump
            var finalProcesses = CollectMSBuildProcessInfo();
            CreateProcessDumps(finalProcesses, "final");

            // Generate comprehensive failure report
            GenerateFailureReport(operationName, eventLog, processSnapshots, hangPatterns, finalElapsed);

            return false;
        }

        /// <summary>
        /// Collect information about all MSBuild-related processes
        /// </summary>
        private List<MSBuildProcessInfo> CollectMSBuildProcessInfo()
        {
            var processes = new List<MSBuildProcessInfo>();
            var targetProcessNames = new[] { "msbuild", "dotnet", "MSBuild", "VBCSCompiler", "csc", "cmd", "powershell", "sh", "bash" };

            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        if (targetProcessNames.Any(name => process.ProcessName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            var processInfo = new MSBuildProcessInfo
                            {
                                Id = process.Id,
                                Name = process.ProcessName,
                                StartTime = process.StartTime,
                                WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                                ThreadCount = process.Threads.Count,
                                CpuTime = process.TotalProcessorTime,
                                IsResponding = process.Responding,
                                CommandLine = GetProcessCommandLine(process)
                            };
                            processes.Add(processInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Some processes may not be accessible, skip them
                        _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Warning: Could not access process {process.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Error collecting process information: {ex.Message}");
            }

            return processes;
        }

        /// <summary>
        /// Get command line for a process (best effort)
        /// </summary>
        private string GetProcessCommandLine(Process process)
        {
            try
            {
                // This is a simplified approach - in real scenarios, you might use WMI or other methods
                return process.MainModule?.FileName ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Detect hang patterns in process snapshots
        /// </summary>
        private List<string> DetectHangPatterns(List<List<MSBuildProcessInfo>> snapshots, BuildSubmission buildSubmission)
        {
            var patterns = new List<string>();

            if (snapshots.Count < 2) 
            {
                return patterns;
            }

            var latest = snapshots.Last();
            var previous = snapshots[snapshots.Count - 2];

            // Pattern 1: Process count explosion
            if (latest.Count > previous.Count + 5)
            {
                patterns.Add($"ProcessExplosion({latest.Count - previous.Count} new processes)");
            }

            // Pattern 2: Unresponsive processes
            var unresponsiveCount = latest.Count(p => !p.IsResponding);
            if (unresponsiveCount > 0)
            {
                patterns.Add($"UnresponsiveProcesses({unresponsiveCount})");
            }

            // Pattern 3: Memory spikes
            var highMemoryProcesses = latest.Where(p => p.WorkingSetMB > 500).ToList();
            if (highMemoryProcesses.Any())
            {
                patterns.Add($"HighMemoryUsage({highMemoryProcesses.Count} processes > 500MB)");
            }

            // Pattern 4: Thread explosion
            var highThreadProcesses = latest.Where(p => p.ThreadCount > 50).ToList();
            if (highThreadProcesses.Any())
            {
                patterns.Add($"HighThreadCount({highThreadProcesses.Count} processes > 50 threads)");
            }

            // Pattern 5: BuildResult state unchanged
            if (buildSubmission.BuildResult == null)
            {
                patterns.Add("BuildResultNull");
            }

            return patterns;
        }

        /// <summary>
        /// Create process dumps for diagnostics
        /// </summary>
        private void CreateProcessDumps(List<MSBuildProcessInfo> processes, string suffix)
        {
            try
            {
                // Create a temp directory for dumps
                var dumpDir = Path.Combine(Path.GetTempPath(), "MSBuildHangDumps", $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{suffix}");
                Directory.CreateDirectory(dumpDir);

                foreach (var processInfo in processes.Where(p => p.Name.Contains("dotnet") || p.Name.Contains("MSBuild")))
                {
                    try
                    {
                        // Try to use dotnet-dump if available
                        var dumpFile = Path.Combine(dumpDir, $"{processInfo.Name}_{processInfo.Id}.dmp");
                        
                        using (var dumpProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "dotnet",
                                Arguments = $"dump collect -p {processInfo.Id} -o \"{dumpFile}\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        })
                        {
                            dumpProcess.Start();
                            if (dumpProcess.WaitForExit(5000)) // 5 second timeout
                            {
                                _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Created dump: {dumpFile}");
                            }
                            else
                            {
                                dumpProcess.Kill();
                                _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Dump creation timed out for process {processInfo.Id}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Failed to create dump for process {processInfo.Id}: {ex.Message}");
                    }
                }

                _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Dump directory: {dumpDir}");
            }
            catch (Exception ex)
            {
                _testOutput.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Error creating dumps: {ex.Message}");
            }
        }

        /// <summary>
        /// Get system information for context
        /// </summary>
        private string GetSystemInfo()
        {
            try
            {
                var cpuCount = Environment.ProcessorCount;
                var totalMemory = GC.GetTotalMemory(false) / (1024 * 1024);
                var osVersion = Environment.OSVersion.ToString();
                var isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) || 
                          !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
                          !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_PIPELINES"));

                return $"CPU: {cpuCount} cores, Memory: {totalMemory}MB, OS: {osVersion}, CI: {isCI}";
            }
            catch (Exception ex)
            {
                return $"Error getting system info: {ex.Message}";
            }
        }

        /// <summary>
        /// Generate comprehensive failure report
        /// </summary>
        private void GenerateFailureReport(string operationName, List<(DateTime Time, string Event, string Details)> eventLog, 
            List<List<MSBuildProcessInfo>> processSnapshots, List<string> hangPatterns, TimeSpan totalElapsed)
        {
            _testOutput.WriteLine($"\n====== MSBuild Hang Detection Report ======");
            _testOutput.WriteLine($"Operation: {operationName}");
            _testOutput.WriteLine($"Total Elapsed: {totalElapsed.TotalMilliseconds:F0}ms");
            _testOutput.WriteLine($"Hang Patterns Detected: {hangPatterns.Count}");
            
            if (hangPatterns.Any())
            {
                _testOutput.WriteLine($"Patterns: {string.Join(", ", hangPatterns.Distinct())}");
            }

            _testOutput.WriteLine($"\n--- Event Timeline ---");
            foreach (var (Time, Event, Details) in eventLog)
            {
                var elapsed = Time - eventLog.First().Time;
                _testOutput.WriteLine($"[+{elapsed.TotalMilliseconds:F0}ms] {Event}: {Details}");
            }

            if (processSnapshots.Any())
            {
                _testOutput.WriteLine($"\n--- Process Summary ---");
                var finalSnapshot = processSnapshots.Last();
                foreach (var process in finalSnapshot.OrderByDescending(p => p.WorkingSetMB))
                {
                    _testOutput.WriteLine($"PID {process.Id}: {process.Name}, {process.WorkingSetMB}MB, {process.ThreadCount} threads, Responding: {process.IsResponding}");
                }
            }

            // Root cause analysis
            _testOutput.WriteLine($"\n--- Root Cause Analysis ---");
            if (hangPatterns.Any(p => p.Contains("ProcessExplosion") || p.Contains("HighMemoryUsage") || p.Contains("HighThreadCount")))
            {
                _testOutput.WriteLine("VERDICT: Likely genuine MSBuild hang detected");
                _testOutput.WriteLine("RECOMMENDATION: File MSBuild bug report with diagnostic data");
            }
            else if (totalElapsed.TotalSeconds < 10)
            {
                _testOutput.WriteLine("VERDICT: Likely timing issue on slow environment");
                _testOutput.WriteLine("RECOMMENDATION: Consider increasing timeout for this environment");
            }
            else
            {
                _testOutput.WriteLine("VERDICT: Inconclusive - may be genuine hang or very slow environment");
                _testOutput.WriteLine("RECOMMENDATION: Review process dumps and retry with longer timeout");
            }

            _testOutput.WriteLine($"============================================\n");
        }

        [Fact]
        public void CanceledTasksDoNotLogMSB4181()
        {
            using (TestEnvironment env = TestEnvironment.Create(_testOutput))
            {
                BuildManager manager = new BuildManager();
                ProjectCollection collection = new ProjectCollection();

                string sleepCommand = Helpers.GetSleepCommand(TimeSpan.FromSeconds(10));
                string contents = @"
                    <Project ToolsVersion ='Current'>
                     <Target Name='test'>
                        <Exec Command='" + sleepCommand + @"'/>
                     </Target>
                    </Project>";

                MockLogger logger = new MockLogger(_testOutput);
                using ManualResetEvent waitCommandExecuted = new ManualResetEvent(false);
                string unescapedSleepCommand = sleepCommand.Replace("&quot;", "\"").Replace("&gt;", ">");
                logger.AdditionalHandlers.Add((sender, args) =>
                {
                    if (unescapedSleepCommand.Equals(args.Message))
                    {
                        waitCommandExecuted.Set();
                    }
                });

                using ProjectFromString projectFromString = new(contents, null, MSBuildConstants.CurrentToolsVersion, collection);
                Project project = projectFromString.Project;
                project.FullPath = env.CreateFile().Path;

                var _parameters = new BuildParameters
                {
                    ShutdownInProcNodeOnBuildFinish = true,
                    Loggers = new ILogger[] { logger },
                    EnableNodeReuse = false
                };

                BuildRequestData data = new BuildRequestData(project.CreateProjectInstance(), new string[] { "test" }, collection.HostServices);
                manager.BeginBuild(_parameters);
                BuildSubmission asyncResult = manager.PendBuildRequest(data);
                asyncResult.ExecuteAsync(null, null);
                
                // Enhanced diagnostic timeline tracking
                var eventLog = new List<(DateTime Time, string Event, string Details)>();
                eventLog.Add((DateTime.UtcNow, "TestStart", "CanceledTasksDoNotLogMSB4181 test started"));

                int timeoutMilliseconds = 2000;
                bool isCommandExecuted = waitCommandExecuted.WaitOne(timeoutMilliseconds);
                eventLog.Add((DateTime.UtcNow, "CommandWaitComplete", $"Command executed: {isCommandExecuted}"));

                manager.CancelAllSubmissions();
                eventLog.Add((DateTime.UtcNow, "CancelRequested", "CancelAllSubmissions called"));

                // Use enhanced wait method with hang detection
                bool isSubmissionComplated = WaitWithMSBuildHangDetection(asyncResult, "BuildSubmissionCompletion", eventLog);
                
                BuildResult result = asyncResult.BuildResult;
                manager.EndBuild();
                eventLog.Add((DateTime.UtcNow, "TestEnd", "Test cleanup completed"));

                // Original test assertions with enhanced error messages
                isCommandExecuted.ShouldBeTrue($"Waiting for that the sleep command is executed failed in the timeout period {timeoutMilliseconds} ms.");
                
                if (!isSubmissionComplated)
                {
                    // If we get here, the enhanced diagnostics have already been logged
                    _testOutput.WriteLine("Enhanced diagnostics completed. Check the hang detection report above for details.");
                }
                
                isSubmissionComplated.ShouldBeTrue($"Waiting for that the build submission is completed failed in the timeout period {timeoutMilliseconds} ms. See hang detection report above for detailed analysis.");

                // No errors from cancelling a build.
                logger.ErrorCount.ShouldBe(0);
                // Warn because the task is being cancelled.
                // NOTE: This assertion will fail when debugging into it because "waiting on exec to cancel" warning will be logged.
                logger.WarningCount.ShouldBe(1);
                // Build failed because it was cancelled.
                result.OverallResult.ShouldBe(BuildResultCode.Failure);
                // Should log "Cmd being cancelled because build was cancelled" warning
                logger.AssertLogContains("MSB5021");
                // Should NOT log "exec failed without logging error"
                logger.AssertLogDoesntContain("MSB4181");

                collection.Dispose();
                manager.Dispose();
            }
        }

        /// <summary>
        /// Verify when task outputs are overridden the override messages are correctly displayed
        /// </summary>
        [Fact]
        public void OverridePropertiesInCreateProperty()
        {
            MockLogger logger = new MockLogger();
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                      <ItemGroup>
                         <EmbeddedResource Include='a.resx'>
                            <LogicalName>foo</LogicalName>
                         </EmbeddedResource>
                         <EmbeddedResource Include='b.resx'>
                            <LogicalName>bar</LogicalName>
                         </EmbeddedResource>
                         <EmbeddedResource Include='c.resx'>
                            <LogicalName>barz</LogicalName>
                         </EmbeddedResource>
                      </ItemGroup>
                      <Target Name='t'>
                         <CreateProperty Value=""@(EmbeddedResource->'/assemblyresource:%(Identity),%(LogicalName)', ' ')""
                                         Condition=""'%(LogicalName)' != '' "">
                             <Output TaskParameter=""Value"" PropertyName=""LinkSwitches""/>
                         </CreateProperty>
                         <Message Text='final:[$(LinkSwitches)]'/>
                      </Target>
                      </Project>");

            using ProjectFromString projectFromString = new(projectFileContents);
            Project project = projectFromString.Project;
            List<ILogger> loggers = new List<ILogger>();
            loggers.Add(logger);
            project.Build("t", loggers);

            logger.AssertLogContains(new string[] { "final:[/assemblyresource:c.resx,barz]" });
            logger.AssertLogContains(new string[] { ResourceUtilities.FormatResourceStringStripCodeAndKeyword("TaskStarted", "CreateProperty") });
            logger.AssertLogContains(new string[] { ResourceUtilities.FormatResourceStringStripCodeAndKeyword("PropertyOutputOverridden", "LinkSwitches", "/assemblyresource:a.resx,foo", "/assemblyresource:b.resx,bar") });
            logger.AssertLogContains(new string[] { ResourceUtilities.FormatResourceStringStripCodeAndKeyword("PropertyOutputOverridden", "LinkSwitches", "/assemblyresource:b.resx,bar", "/assemblyresource:c.resx,barz") });
        }

        /// <summary>
        /// Verify that when a task outputs are inferred the override messages are displayed
        /// </summary>
        [Fact]
        public void OverridePropertiesInInferredCreateProperty()
        {
            string[] files = null;
            try
            {
                files = ObjectModelHelpers.GetTempFiles(2, new DateTime(2005, 1, 1));

                MockLogger logger = new MockLogger();
                string projectFileContents = ObjectModelHelpers.CleanupFileContents(
                    @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                      <ItemGroup>
                        <i Include='" + files[0] + "'><output>" + files[1] + @"</output></i>
                      </ItemGroup>
                      <ItemGroup>
                         <EmbeddedResource Include='a.resx'>
                        <LogicalName>foo</LogicalName>
                          </EmbeddedResource>
                            <EmbeddedResource Include='b.resx'>
                            <LogicalName>bar</LogicalName>
                        </EmbeddedResource>
                            <EmbeddedResource Include='c.resx'>
                            <LogicalName>barz</LogicalName>
                        </EmbeddedResource>
                        </ItemGroup>
                      <Target Name='t2' DependsOnTargets='t'>
                        <Message Text='final:[$(LinkSwitches)]'/>
                      </Target>
                      <Target Name='t' Inputs='%(i.Identity)' Outputs='%(i.Output)'>
                        <Message Text='start:[Hello]'/>
                        <CreateProperty Value=""@(EmbeddedResource->'/assemblyresource:%(Identity),%(LogicalName)', ' ')""
                                         Condition=""'%(LogicalName)' != '' "">
                             <Output TaskParameter=""Value"" PropertyName=""LinkSwitches""/>
                        </CreateProperty>
                        <Message Text='end:[hello]'/>
                    </Target>
                    </Project>");

                using ProjectFromString projectFromString = new(projectFileContents);
                Project project = projectFromString.Project;
                List<ILogger> loggers = new List<ILogger>();
                loggers.Add(logger);
                project.Build("t2", loggers);

                // We should only see messages from the second target, as the first is only inferred
                logger.AssertLogDoesntContain("start:");
                logger.AssertLogDoesntContain("end:");

                logger.AssertLogContains(new string[] { "final:[/assemblyresource:c.resx,barz]" });
                logger.AssertLogDoesntContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("TaskStarted", "CreateProperty"));
                logger.AssertLogContains(new string[] { ResourceUtilities.FormatResourceStringStripCodeAndKeyword("PropertyOutputOverridden", "LinkSwitches", "/assemblyresource:a.resx,foo", "/assemblyresource:b.resx,bar") });
                logger.AssertLogContains(new string[] { ResourceUtilities.FormatResourceStringStripCodeAndKeyword("PropertyOutputOverridden", "LinkSwitches", "/assemblyresource:b.resx,bar", "/assemblyresource:c.resx,barz") });
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(files);
            }
        }

        /// <summary>
        /// Tests that tasks batch on outputs correctly.
        /// </summary>
        [Fact]
        public void TaskOutputBatching()
        {
            MockLogger logger = new MockLogger();
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <ItemGroup>
                        <TaskParameterItem Include=""foo"">
                            <ParameterName>Value</ParameterName>
                            <ParameterName2>Include</ParameterName2>
                            <PropertyName>MetadataProperty</PropertyName>
                            <ItemType>MetadataItem</ItemType>
                        </TaskParameterItem>
                    </ItemGroup>
                    <Target Name='Build'>
                        <CreateProperty Value=""@(TaskParameterItem)"">
                            <Output TaskParameter=""Value"" PropertyName=""Property1""/>
                        </CreateProperty>
                        <Message Text='Property1=[$(Property1)]' />

                        <CreateProperty Value=""@(TaskParameterItem)"">
                            <Output TaskParameter=""%(TaskParameterItem.ParameterName)"" PropertyName=""Property2""/>
                        </CreateProperty>
                        <Message Text='Property2=[$(Property2)]' />

                        <CreateProperty Value=""@(TaskParameterItem)"">
                            <Output TaskParameter=""Value"" PropertyName=""%(TaskParameterItem.PropertyName)""/>
                        </CreateProperty>
                        <Message Text='MetadataProperty=[$(MetadataProperty)]' />

                        <CreateItem Include=""@(TaskParameterItem)"">
                            <Output TaskParameter=""Include"" ItemName=""TestItem1""/>
                        </CreateItem>
                        <Message Text='TestItem1=[@(TestItem1)]' />

                        <CreateItem Include=""@(TaskParameterItem)"">
                            <Output TaskParameter=""%(TaskParameterItem.ParameterName2)"" ItemName=""TestItem2""/>
                        </CreateItem>
                        <Message Text='TestItem2=[@(TestItem2)]' />

                        <CreateItem Include=""@(TaskParameterItem)"">
                            <Output TaskParameter=""Include"" ItemName=""%(TaskParameterItem.ItemType)""/>
                        </CreateItem>
                        <Message Text='MetadataItem=[@(MetadataItem)]' />
                    </Target>
                </Project>");

            using ProjectFromString projectFromString = new(projectFileContents);
            Project project = projectFromString.Project;
            List<ILogger> loggers = new List<ILogger>();
            loggers.Add(logger);
            project.Build(loggers);

            logger.AssertLogContains("Property1=[foo]");
            logger.AssertLogContains("Property2=[foo]");
            logger.AssertLogContains("MetadataProperty=[foo]");
            logger.AssertLogContains("TestItem1=[foo]");
            logger.AssertLogContains("TestItem2=[foo]");
            logger.AssertLogContains("MetadataItem=[foo]");
        }

        /// <summary>
        /// MSbuildLastTaskResult property contains true or false indicating
        /// the success or failure of the last task.
        /// </summary>
        [Fact]
        public void MSBuildLastTaskResult()
        {
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets='t2' ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <Target Name='t'>
        <Message Text='[start:$(MSBuildLastTaskResult)]'/> <!-- Should be blank -->
        <Warning Text='warning'/>
        <Message Text='[0:$(MSBuildLastTaskResult)]'/> <!-- Should be true, only a warning-->
        <!-- task's Execute returns false -->
        <Copy SourceFiles='|' DestinationFolder='c:\' ContinueOnError='true' />
        <PropertyGroup>
           <p>$(MSBuildLastTaskResult)</p>
        </PropertyGroup>
        <Message Text='[1:$(MSBuildLastTaskResult)]'/> <!-- Should be false: propertygroup did not reset it -->
        <Message Text='[p:$(p)]'/> <!-- Should be false as stored earlier -->
        <Message Text='[2:$(MSBuildLastTaskResult)]'/> <!-- Message succeeded, should now be true -->
    </Target>
    <Target Name='t2' DependsOnTargets='t'>
        <Message Text='[3:$(MSBuildLastTaskResult)]'/> <!-- Should still have true -->
        <!-- check Error task as well -->
        <Error Text='error' ContinueOnError='true' />
        <Message Text='[4:$(MSBuildLastTaskResult)]'/> <!-- Should be false -->
        <!-- trigger OnError target, ContinueOnError is false -->
        <Error Text='error2'/>
        <OnError ExecuteTargets='t3'/>
    </Target>
    <Target Name='t3' >
        <Message Text='[5:$(MSBuildLastTaskResult)]'/> <!-- Should be false -->
    </Target>
</Project>");

            using ProjectFromString projectFromString = new(projectFileContents);
            Project project = projectFromString.Project;
            List<ILogger> loggers = new List<ILogger>();
            MockLogger logger = new MockLogger();
            loggers.Add(logger);
            project.Build("t2", loggers);

            logger.AssertLogContains("[start:]");
            logger.AssertLogContains("[0:true]");
            logger.AssertLogContains("[1:false]");
            logger.AssertLogContains("[p:false]");
            logger.AssertLogContains("[2:true]");
            logger.AssertLogContains("[3:true]");
            logger.AssertLogContains("[4:false]");
            logger.AssertLogContains("[4:false]");
        }

        /// <summary>
        /// Verifies that we can add "recursivedir" built-in metadata as target outputs.
        /// This is to support wildcards in CreateItem. Allowing anything
        /// else could let the item get corrupt (inconsistent values for Filename and FullPath, for example)
        /// </summary>
        [Fact]
        public void TasksCanAddRecursiveDirBuiltInMetadata()
        {
            MockLogger logger = new MockLogger(this._testOutput);

            string projectFileContents = ObjectModelHelpers.CleanupFileContents($@"
<Project>
<Target Name='t'>
 <CreateItem Include='{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\**\*.dll'>
   <Output TaskParameter='Include' ItemName='x' />
 </CreateItem>
<Message Text='@(x)'/>
 <Message Text='[%(x.RecursiveDir)]'/>
</Target>
</Project>");

            using ProjectFromString projectFromString = new(projectFileContents);
            Project project = projectFromString.Project;
            project.Build("t", new[] { logger }).ShouldBeTrue();

            // Assuming the current directory of the test .dll has at least one subfolder
            // such as Roslyn, the log will contain [Roslyn\] (or [Roslyn/] on Unix)
            string slashAndBracket = Path.DirectorySeparatorChar.ToString() + "]";
            logger.AssertLogContains(slashAndBracket);
            logger.AssertLogDoesntContain("MSB4118");
            logger.AssertLogDoesntContain("MSB3031");
        }

        /// <summary>
        /// Verify CreateItem prevents adding any built-in metadata explicitly, even recursivedir.
        /// </summary>
        [Fact]
        public void OtherBuiltInMetadataErrors()
        {
            MockLogger logger = new MockLogger();
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
<Target Name='t'>
 <CreateItem Include='Foo' AdditionalMetadata='RecursiveDir=1'>
   <Output TaskParameter='Include' ItemName='x' />
 </CreateItem>
</Target>
</Project>");

            using ProjectFromString projectFromString = new(projectFileContents);
            Project project = projectFromString.Project;
            List<ILogger> loggers = new List<ILogger>();
            loggers.Add(logger);
            bool result = project.Build("t", loggers);

            Assert.False(result);
            logger.AssertLogContains("MSB3031");
        }

        /// <summary>
        /// Verify CreateItem prevents adding any built-in metadata explicitly, even recursivedir.
        /// </summary>
        [Fact]
        public void OtherBuiltInMetadataErrors2()
        {
            MockLogger logger = new MockLogger();
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
<Target Name='t'>
 <CreateItem Include='Foo' AdditionalMetadata='Extension=1'/>
</Target>
</Project>");

            using ProjectFromString projectFromString = new(projectFileContents);
            Project project = projectFromString.Project;
            List<ILogger> loggers = new List<ILogger>();
            loggers.Add(logger);
            bool result = project.Build("t", loggers);

            Assert.False(result);
            logger.AssertLogContains("MSB3031");
        }

        /// <summary>
        /// Verify that properties can be passed in to a task and out as items, despite the
        /// built-in metadata restrictions.
        /// </summary>
        [Fact]
        public void PropertiesInItemsOutOfTask()
        {
            MockLogger logger = new MockLogger();
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
<Target Name='t'>
 <PropertyGroup>
   <p>c:\a.ext</p>
 </PropertyGroup>
 <CreateItem Include='$(p)'>
   <Output TaskParameter='Include' ItemName='x' />
 </CreateItem>
 <Message Text='[%(x.Extension)]'/>
</Target>
</Project>");

            using ProjectFromString projectFromString = new(projectFileContents);
            Project project = projectFromString.Project;
            List<ILogger> loggers = new List<ILogger>();
            loggers.Add(logger);
            bool result = project.Build("t", loggers);

            Assert.True(result);
            logger.AssertLogContains("[.ext]");
        }

        /// <summary>
        /// Verify that properties can be passed in to a task and out as items, despite
        /// having illegal characters for a file name
        /// </summary>
        [Fact]
        public void IllegalFileCharsInItemsOutOfTask()
        {
            MockLogger logger = new MockLogger();
            string projectFileContents = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
<Target Name='t'>
 <PropertyGroup>
   <p>||illegal||</p>
 </PropertyGroup>
 <CreateItem Include='$(p)'>
   <Output TaskParameter='Include' ItemName='x' />
 </CreateItem>
 <Message Text='[@(x)]'/>
</Target>
</Project>");

            using ProjectFromString projectFromString = new(projectFileContents);
            Project project = projectFromString.Project;
            List<ILogger> loggers = new List<ILogger>();
            loggers.Add(logger);
            bool result = project.Build("t", loggers);

            Assert.True(result);
            logger.AssertLogContains("[||illegal||]");
        }

        /// <summary>
        /// If an item being output from a task has null metadata, we shouldn't crash.
        /// </summary>
        [Fact]
        public void NullMetadataOnOutputItems()
        {
            string customTaskPath = Assembly.GetExecutingAssembly().Location;

            string projectContents = @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <UsingTask TaskName=`NullMetadataTask` AssemblyFile=`" + customTaskPath + @"` />

  <Target Name=`Build`>
    <NullMetadataTask>
      <Output TaskParameter=`OutputItems` ItemName=`Outputs`/>
    </NullMetadataTask>

    <Message Text=`[%(Outputs.Identity): %(Outputs.a)]` Importance=`High` />
  </Target>
</Project>";

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(projectContents, _testOutput, LoggerVerbosity.Diagnostic);
            logger.AssertLogContains("[foo: ]");
        }

        /// <summary>
        /// If an item being output from a task has null metadata, we shouldn't crash.
        /// </summary>
        [Fact]
        public void NullMetadataOnLegacyOutputItems()
        {
            string customTaskPath = Assembly.GetExecutingAssembly().Location;

            string projectContents = @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <UsingTask TaskName=`NullMetadataTask` AssemblyFile=`" + customTaskPath + @"` />

  <Target Name=`Build`>
    <NullMetadataTask>
      <Output TaskParameter=`OutputItems` ItemName=`Outputs`/>
    </NullMetadataTask>

    <Message Text=`[%(Outputs.Identity): %(Outputs.a)]` Importance=`High` />
  </Target>
</Project>";

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(projectContents, _testOutput, LoggerVerbosity.Diagnostic);
            logger.AssertLogContains("[foo: ]");
        }

        /// <summary>
        /// If an item returned from a task has bare-minimum metadata implementation, we shouldn't crash.
        /// </summary>
        [Fact]
        public void MinimalLegacyOutputItems()
        {
            string customTaskPath = Assembly.GetExecutingAssembly().Location;

            string projectContents = $"""
                                     <Project>
                                       <UsingTask TaskName="TaskThatReturnsMinimalItem" AssemblyFile="{customTaskPath}" />

                                       <Target Name="Build">
                                         <TaskThatReturnsMinimalItem>
                                           <Output TaskParameter="MinimalTaskItemOutput" ItemName="Outputs"/>
                                         </TaskThatReturnsMinimalItem>

                                         <Message Text="[%(Outputs.Identity): %(Outputs.a)]" Importance="High" />
                                       </Target>
                                     </Project>
                                     """;

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(projectContents, _testOutput, LoggerVerbosity.Diagnostic);
        }

        /// <summary>
        /// Regression test for https://github.com/dotnet/msbuild/issues/5080
        /// </summary>
        [Fact]
        public void SameAssemblyFromDifferentRelativePathsSharesAssemblyLoadContext()
        {
            string realTaskPath = Assembly.GetExecutingAssembly().Location;

            string fileName = Path.GetFileName(realTaskPath);
            string directoryName = Path.GetDirectoryName(realTaskPath);

            using var env = TestEnvironment.Create();

            string customTaskFolder = Path.Combine(directoryName, "buildCrossTargeting");
            env.CreateFolder(customTaskFolder);

            string projectContents = @"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <UsingTask TaskName=`RegisterObject` AssemblyFile=`" + Path.Combine(customTaskFolder, "..", fileName) + @"` />
  <UsingTask TaskName=`RetrieveObject` AssemblyFile=`" + realTaskPath + @"` />

  <Target Name=`Build`>
    <RegisterObject />
    <RetrieveObject />
  </Target>
</Project>";

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(projectContents, _testOutput);

            logger.AssertLogDoesntContain("MSB4018");
        }


#if FEATURE_CODETASKFACTORY
        /// <summary>
        /// If an item being output from a task has null metadata, we shouldn't crash.
        /// </summary>
        [Fact]
        public void NullMetadataOnOutputItems_InlineTask()
        {
            string projectContents = @"
                    <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`NullMetadataTask_v12` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll`>
                            <ParameterGroup>
                               <OutputItems ParameterType=`Microsoft.Build.Framework.ITaskItem[]` Output=`true` />
                            </ParameterGroup>
                            <Task>
                                <Code>
                                <![CDATA[
                                    OutputItems = new ITaskItem[1];

                                    IDictionary<string, string> metadata = new Dictionary<string, string>();
                                    metadata.Add(`a`, null);

                                    OutputItems[0] = new TaskItem(`foo`, (IDictionary)metadata);

                                    return true;
                                ]]>
                                </Code>
                            </Task>
                        </UsingTask>
                      <Target Name=`Build`>
                        <NullMetadataTask_v12>
                          <Output TaskParameter=`OutputItems` ItemName=`Outputs` />
                        </NullMetadataTask_v12>

                        <Message Text=`[%(Outputs.Identity): %(Outputs.a)]` Importance=`High` />
                      </Target>
                    </Project>";

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(projectContents, _testOutput, LoggerVerbosity.Diagnostic);
            logger.AssertLogContains("[foo: ]");
        }

        /// <summary>
        /// If an item being output from a task has null metadata, we shouldn't crash.
        /// </summary>
        [Fact(Skip = "This test fails when diagnostic logging is available, as deprecated EscapingUtilities.UnescapeAll method cannot handle null value. This is not relevant to non-deprecated version of this method.")]
        public void NullMetadataOnLegacyOutputItems_InlineTask()
        {
            string projectContents = @"
                    <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`NullMetadataTask_v4` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildFrameworkToolsPath)\Microsoft.Build.Tasks.v4.0.dll`>
                            <ParameterGroup>
                               <OutputItems ParameterType=`Microsoft.Build.Framework.ITaskItem[]` Output=`true` />
                            </ParameterGroup>
                            <Task>
                                <Code>
                                <![CDATA[
                                    OutputItems = new ITaskItem[1];

                                    IDictionary<string, string> metadata = new Dictionary<string, string>();
                                    metadata.Add(`a`, null);

                                    OutputItems[0] = new TaskItem(`foo`, (IDictionary)metadata);

                                    return true;
                                ]]>
                                </Code>
                            </Task>
                        </UsingTask>
                      <Target Name=`Build`>
                        <NullMetadataTask_v4>
                          <Output TaskParameter=`OutputItems` ItemName=`Outputs` />
                        </NullMetadataTask_v4>

                        <Message Text=`[%(Outputs.Identity): %(Outputs.a)]` Importance=`High` />
                      </Target>
                    </Project>";

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(projectContents, _testOutput);
            logger.AssertLogContains("[foo: ]");
        }

        /// <summary>
        /// If an item being output from a task has null metadata, we shouldn't crash.
        /// </summary>
        [Fact(Skip = "This test fails when diagnostic logging is available, as deprecated EscapingUtilities.UnescapeAll method cannot handle null value. This is not relevant to non-deprecated version of this method.")]
        [Trait("Category", "non-mono-tests")]
        public void NullMetadataOnLegacyOutputItems_InlineTask_Diagnostic()
        {
            string projectContents = @"
                    <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`NullMetadataTask_v4` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildFrameworkToolsPath)\Microsoft.Build.Tasks.v4.0.dll`>
                            <ParameterGroup>
                               <OutputItems ParameterType=`Microsoft.Build.Framework.ITaskItem[]` Output=`true` />
                            </ParameterGroup>
                            <Task>
                                <Code>
                                <![CDATA[
                                    OutputItems = new ITaskItem[1];

                                    IDictionary<string, string> metadata = new Dictionary<string, string>();
                                    metadata.Add(`a`, null);

                                    OutputItems[0] = new TaskItem(`foo`, (IDictionary)metadata);

                                    return true;
                                ]]>
                                </Code>
                            </Task>
                        </UsingTask>
                      <Target Name=`Build`>
                        <NullMetadataTask_v4>
                          <Output TaskParameter=`OutputItems` ItemName=`Outputs` />
                        </NullMetadataTask_v4>

                        <Message Text=`[%(Outputs.Identity): %(Outputs.a)]` Importance=`High` />
                      </Target>
                    </Project>";

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(projectContents, _testOutput, loggerVerbosity: LoggerVerbosity.Diagnostic);
            logger.AssertLogContains("[foo: ]");
        }
#endif

        /// <summary>
        /// Validates that the defining project metadata is set (or not set) as expected in
        /// various task output-related operations, using a task built against the current
        /// version of MSBuild.
        /// </summary>
        [Fact]
        public void ValidateDefiningProjectMetadataOnTaskOutputs()
        {
            string customTaskPath = Assembly.GetExecutingAssembly().Location;
            ValidateDefiningProjectMetadataOnTaskOutputsHelper(customTaskPath);
        }

        /// <summary>
        /// Validates that the defining project metadata is set (or not set) as expected in
        /// various task output-related operations, using a task built against V4 MSBuild,
        /// which didn't support the defining project metadata.
        /// </summary>
        [Fact]
        public void ValidateDefiningProjectMetadataOnTaskOutputs_LegacyItems()
        {
            string customTaskPath = Assembly.GetExecutingAssembly().Location;
            ValidateDefiningProjectMetadataOnTaskOutputsHelper(customTaskPath);
        }

#if FEATURE_APARTMENT_STATE
        /// <summary>
        /// Tests that putting the RunInSTA attribute on a task causes it to run in the STA thread.
        /// </summary>
        [Fact]
        public void TestSTAThreadRequired()
        {
            TestSTATask(true, false, false);
        }

        /// <summary>
        /// Tests an STA task with an exception
        /// </summary>
        [Fact]
        public void TestSTAThreadRequiredWithException()
        {
            TestSTATask(true, false, true);
        }

        /// <summary>
        /// Tests an STA task with failure.
        /// </summary>
        [Fact]
        public void TestSTAThreadRequiredWithFailure()
        {
            TestSTATask(true, true, false);
        }

        /// <summary>
        /// Tests an MTA task.
        /// </summary>
        [Fact]
        public void TestSTAThreadNotRequired()
        {
            TestSTATask(false, false, false);
        }

        /// <summary>
        /// Tests an MTA task with an exception.
        /// </summary>
        [Fact]
        public void TestSTAThreadNotRequiredWithException()
        {
            TestSTATask(false, false, true);
        }

        /// <summary>
        /// Tests an MTA task with failure.
        /// </summary>
        [Fact]
        public void TestSTAThreadNotRequiredWithFailure()
        {
            TestSTATask(false, true, false);
        }
#endif

        #region ITargetBuilderCallback Members

        /// <summary>
        /// Empty impl
        /// </summary>
        Task<ITargetResult[]> ITargetBuilderCallback.LegacyCallTarget(string[] targets, bool continueOnError, ElementLocation referenceLocation)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Empty impl
        /// </summary>
        void IRequestBuilderCallback.Yield()
        {
        }

        /// <summary>
        /// Empty impl
        /// </summary>
        void IRequestBuilderCallback.Reacquire()
        {
        }

        /// <summary>
        /// Empty impl
        /// </summary>
        void IRequestBuilderCallback.EnterMSBuildCallbackState()
        {
        }

        /// <summary>
        /// Empty impl
        /// </summary>
        void IRequestBuilderCallback.ExitMSBuildCallbackState()
        {
        }

        /// <summary>
        /// Empty impl
        /// </summary>
        int IRequestBuilderCallback.RequestCores(object monitorLockObject, int requestedCores, bool waitForCores)
        {
            return 0;
        }

        /// <summary>
        /// Empty impl
        /// </summary>
        void IRequestBuilderCallback.ReleaseCores(int coresToRelease)
        {
        }

        #endregion

        #region IRequestBuilderCallback Members

        /// <summary>
        /// Empty impl
        /// </summary>
        Task<BuildResult[]> IRequestBuilderCallback.BuildProjects(string[] projectFiles, PropertyDictionary<ProjectPropertyInstance>[] properties, string[] toolsVersions, string[] targets, bool waitForResults, bool skipNonexistentTargets)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not implemented.
        /// </summary>
        Task IRequestBuilderCallback.BlockOnTargetInProgress(int blockingRequestId, string blockingTarget, BuildResult partialBuildResult)
        {
            throw new NotImplementedException();
        }

        #endregion

        /*********************************************************************************
         *
         *                                     Helpers
         *
         *********************************************************************************/

        /// <summary>
        /// Helper method for validating the setting of defining project metadata on items
        /// coming from task outputs
        /// </summary>
        private void ValidateDefiningProjectMetadataOnTaskOutputsHelper(string customTaskPath)
        {
            string projectAPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "a.proj");
            string projectBPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "b.proj");

            string projectAContents = @"
                <Project xmlns=`msbuildnamespace` ToolsVersion=`msbuilddefaulttoolsversion`>
                    <UsingTask TaskName=`ItemCreationTask` AssemblyFile=`" + customTaskPath + @"` />
                    <Import Project=`b.proj` />

                    <Target Name=`Run`>
                      <ItemCreationTask
                        InputItemsToPassThrough=`@(PassThrough)`
                        InputItemsToCopy=`@(Copy)`>
                          <Output TaskParameter=`OutputString` ItemName=`A` />
                          <Output TaskParameter=`PassedThroughOutputItems` ItemName=`B` />
                          <Output TaskParameter=`CreatedOutputItems` ItemName=`C` />
                          <Output TaskParameter=`CopiedOutputItems` ItemName=`D` />
                      </ItemCreationTask>

                      <Warning Text=`A is wrong: EXPECTED: [a] ACTUAL: [%(A.DefiningProjectName)]` Condition=`'%(A.DefiningProjectName)' != 'a'` />
                      <Warning Text=`B is wrong: EXPECTED: [a] ACTUAL: [%(B.DefiningProjectName)]` Condition=`'%(B.DefiningProjectName)' != 'a'` />
                      <Warning Text=`C is wrong: EXPECTED: [a] ACTUAL: [%(C.DefiningProjectName)]` Condition=`'%(C.DefiningProjectName)' != 'a'` />
                      <Warning Text=`D is wrong: EXPECTED: [a] ACTUAL: [%(D.DefiningProjectName)]` Condition=`'%(D.DefiningProjectName)' != 'a'` />
                    </Target>
                </Project>
";

            string projectBContents = @"
                <Project xmlns=`msbuildnamespace` ToolsVersion=`msbuilddefaulttoolsversion`>

                    <ItemGroup>
                        <PassThrough Include=`aaa.cs` />
                        <Copy Include=`bbb.cs` />
                    </ItemGroup>
                </Project>
";

            try
            {
                File.WriteAllText(projectAPath, ObjectModelHelpers.CleanupFileContents(projectAContents));
                File.WriteAllText(projectBPath, ObjectModelHelpers.CleanupFileContents(projectBContents));

                MockLogger logger = new MockLogger(_testOutput);
                ObjectModelHelpers.BuildTempProjectFileExpectSuccess("a.proj", logger);
                logger.AssertNoWarnings();
            }
            finally
            {
                if (File.Exists(projectAPath))
                {
                    File.Delete(projectAPath);
                }

                if (File.Exists(projectBPath))
                {
                    File.Delete(projectBPath);
                }
            }
        }

#if FEATURE_APARTMENT_STATE
        /// <summary>
        /// Executes an STA task test.
        /// </summary>
        private void TestSTATask(bool requireSTA, bool failTask, bool throwException)
        {
            MockLogger logger = new MockLogger();
            logger.AllowTaskCrashes = throwException;

            string taskAssemblyName;
            Project project = CreateSTATestProject(requireSTA, failTask, throwException, out taskAssemblyName);

            List<ILogger> loggers = new List<ILogger>();
            loggers.Add(logger);

            BuildParameters parameters = new BuildParameters();
            parameters.Loggers = new ILogger[] { logger };
            BuildResult result = BuildManager.DefaultBuildManager.Build(parameters, new BuildRequestData(project.CreateProjectInstance(), new string[] { "Foo" }));
            if (requireSTA)
            {
                logger.AssertLogContains("STA");
            }
            else
            {
                logger.AssertLogContains("MTA");
            }

            if (throwException)
            {
                logger.AssertLogContains("EXCEPTION");
                Assert.Equal(BuildResultCode.Failure, result.OverallResult);
                return;
            }
            else
            {
                logger.AssertLogDoesntContain("EXCEPTION");
            }

            if (failTask)
            {
                logger.AssertLogContains("FAIL");
                Assert.Equal(BuildResultCode.Failure, result.OverallResult);
            }
            else
            {
                logger.AssertLogDoesntContain("FAIL");
            }

            if (!throwException && !failTask)
            {
                Assert.Equal(BuildResultCode.Success, result.OverallResult);
            }
        }

        /// <summary>
        /// Helper to create a project which invokes the STA helper task.
        /// </summary>
        private Project CreateSTATestProject(bool requireSTA, bool failTask, bool throwException, out string assemblyToDelete)
        {
            assemblyToDelete = GenerateSTATask(requireSTA);

            string projectFileContents = ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
<UsingTask TaskName='ThreadTask' AssemblyFile='" + assemblyToDelete + @"'/>
	<Target Name='Foo'>
		<ThreadTask Fail='" + failTask + @"' ThrowException='" + throwException + @"'/>
	</Target>
</Project>");

            using ProjectFromString projectFromString = new(projectFileContents);
            Project project = projectFromString.Project;

            return project;
        }

        /// <summary>
        /// Helper to create the STA test task.
        /// </summary>
        private string GenerateSTATask(bool requireSTA)
        {
            string taskContents =
                @"
using System;
using Microsoft.Build.Framework;

namespace ClassLibrary2
{" + (requireSTA ? "[RunInSTA]" : String.Empty) + @"
    public class ThreadTask : ITask
    {
#region ITask Members

        public IBuildEngine BuildEngine
        {
            get;
            set;
        }

        public bool ThrowException
        {
            get;
            set;
        }

        public bool Fail
        {
            get;
            set;
        }

        public bool Execute()
        {
            string message;
            if (System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA)
            {
                message = ""STA"";
            }
            else
            {
                message = ""MTA"";
            }

            BuildEngine.LogMessageEvent(new BuildMessageEventArgs(message, """", ""ThreadTask"", MessageImportance.High));

            if (ThrowException)
            {
                throw new InvalidOperationException(""EXCEPTION"");
            }

            if (Fail)
            {
                BuildEngine.LogMessageEvent(new BuildMessageEventArgs(""FAIL"", """", ""ThreadTask"", MessageImportance.High));
            }

            return !Fail;
        }

        public ITaskHost HostObject
        {
            get;
            set;
        }

#endregion
    }
}";
            return CustomTaskHelper.GetAssemblyForTask(taskContents);
        }
#endif

        /// <summary>
        /// The mock component host object.
        /// </summary>
        private sealed class MockHost : MockLoggingService, IBuildComponentHost, IBuildComponent
        {
            #region IBuildComponentHost Members

            /// <summary>
            /// The config cache
            /// </summary>
            private IConfigCache _configCache;

            /// <summary>
            /// The logging service
            /// </summary>
            private ILoggingService _loggingService;

            /// <summary>
            /// The results cache
            /// </summary>
            private IResultsCache _resultsCache;

            /// <summary>
            /// The request builder
            /// </summary>
            private IRequestBuilder _requestBuilder;

            /// <summary>
            /// The target builder
            /// </summary>
            private ITargetBuilder _targetBuilder;

            /// <summary>
            /// The build parameters.
            /// </summary>
            private BuildParameters _buildParameters;

            /// <summary>
            /// Retrieves the LegacyThreadingData associated with a particular component host
            /// </summary>
            private LegacyThreadingData _legacyThreadingData;

            private ISdkResolverService _sdkResolverService;

            /// <summary>
            /// Constructor
            ///
            /// UNDONE: Refactor this, and the other MockHosts, to use a common base implementation.  The duplication of the
            /// logging implementation alone is unfortunate.
            /// </summary>
            public MockHost()
            {
                _buildParameters = new BuildParameters();
                _legacyThreadingData = new LegacyThreadingData();

                _configCache = new ConfigCache();
                ((IBuildComponent)_configCache).InitializeComponent(this);

                _loggingService = this;

                _resultsCache = new ResultsCache();
                ((IBuildComponent)_resultsCache).InitializeComponent(this);

                _requestBuilder = new RequestBuilder();
                ((IBuildComponent)_requestBuilder).InitializeComponent(this);

                _targetBuilder = new TargetBuilder();
                ((IBuildComponent)_targetBuilder).InitializeComponent(this);

                _sdkResolverService = new MockSdkResolverService();
                ((IBuildComponent)_sdkResolverService).InitializeComponent(this);
            }

            /// <summary>
            /// Returns the node logging service.  We don't distinguish here.
            /// </summary>
            public ILoggingService LoggingService
            {
                get
                {
                    return _loggingService;
                }
            }

            /// <summary>
            /// Retrieves the name of the host.
            /// </summary>
            public string Name
            {
                get
                {
                    return "TaskBuilder_Tests.MockHost";
                }
            }

            /// <summary>
            /// Returns the build parameters.
            /// </summary>
            public BuildParameters BuildParameters
            {
                get
                {
                    return _buildParameters;
                }
            }

            /// <summary>
            /// Retrieves the LegacyThreadingData associated with a particular component host
            /// </summary>
            LegacyThreadingData IBuildComponentHost.LegacyThreadingData
            {
                get
                {
                    return _legacyThreadingData;
                }
            }

            /// <summary>
            /// Constructs and returns a component of the specified type.
            /// </summary>
            /// <param name="type">The type of component to return</param>
            /// <returns>The component</returns>
            public IBuildComponent GetComponent(BuildComponentType type)
            {
                return type switch
                {
                    BuildComponentType.ConfigCache => (IBuildComponent)_configCache,
                    BuildComponentType.LoggingService => (IBuildComponent)_loggingService,
                    BuildComponentType.ResultsCache => (IBuildComponent)_resultsCache,
                    BuildComponentType.RequestBuilder => (IBuildComponent)_requestBuilder,
                    BuildComponentType.TargetBuilder => (IBuildComponent)_targetBuilder,
                    BuildComponentType.SdkResolverService => (IBuildComponent)_sdkResolverService,
                    _ => throw new ArgumentException("Unexpected type " + type),
                };
            }

            public TComponent GetComponent<TComponent>(BuildComponentType type) where TComponent : IBuildComponent
                => (TComponent)GetComponent(type);

            /// <summary>
            /// Register a component factory.
            /// </summary>
            public void RegisterFactory(BuildComponentType type, BuildComponentFactoryDelegate factory)
            {
            }

            #endregion
        }
    }
}
