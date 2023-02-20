// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Build.Logging.Ansi;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    internal class LiveLogger : ILogger
    {
        private readonly ConcurrentDictionary<int, ProjectNode> _projectsDictionary;

        private volatile bool _succeeded;

        private int _countStartedProjects = 0;

        private int _countFinishedProjects = 0;

        internal LiveLogger() => _projectsDictionary = new();

        #region ILogger
        public string Parameters { get; set; } = string.Empty;

        public LoggerVerbosity Verbosity { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            // Register for different events
            // Started
            eventSource.BuildStarted += OnBuildStarted;
            eventSource.ProjectStarted += OnProjectStarted;
            eventSource.TargetStarted += OnTargetStarted;
            eventSource.TaskStarted += OnTaskStarted;

            // Finished
            eventSource.BuildFinished += OnBuildFinished;
            eventSource.ProjectFinished += OnProjectFinished;
            eventSource.TargetFinished += OnTargetFinished;

            // Raised
            eventSource.MessageRaised += OnMessageRaised;
            eventSource.WarningRaised += OnWarningRaised;
            eventSource.ErrorRaised += OnErrorRaised;

            // Cancelled
            Console.CancelKeyPress += OnCancelKeyPressed;

            _ = Task.Run(Render);
        }

        public void Shutdown()
        {
            TerminalBuffer.Terminate();
            int errorCount = 0;
            int warningCount = 0;
            foreach (KeyValuePair<int, ProjectNode> kvp in _projectsDictionary)
            {
                int id = kvp.Key;
                ProjectNode projectNode = kvp.Value;
                if (projectNode.AdditionalDetailsCount > 0)
                {
                    Console.WriteLine(projectNode.ToAnsiString());
                    errorCount += projectNode.ErrorCount;
                    warningCount += projectNode.WarningCount;
                    foreach (MessageNode message in projectNode.GetAdditionalDetails())
                    {
                        Console.WriteLine($"    └── {message.ToAnsiString()}");
                    }

                    Console.WriteLine();
                }
            }

            Console.WriteLine();
            if (_succeeded)
            {
                Console.WriteLine(AnsiBuilder.Formatter.Color("Build succeeded.", ForegroundColor.Green));
            }
            else
            {
                Console.WriteLine(AnsiBuilder.Formatter.Color("Build failed.", ForegroundColor.Red));
            }

            Console.WriteLine($"\t{warningCount} Warnings(s)");
            Console.WriteLine($"\t{errorCount} Errors(s)");
        }
        #endregion

        private void Render()
        {
            // Initialize LiveLoggerBuffer
            TerminalBuffer.Initialize();

            // TODO: Fix. First line does not appear at top. Leaving empty line for now
            _ = TerminalBuffer.WriteNewLine(string.Empty);

            // First render
            TerminalBuffer.Render();
            int i = 0;

            // Rerender periodically
            while (!TerminalBuffer.IsTerminated)
            {
                // Delay by 1/60 seconds
                // Use task delay to avoid blocking the task, so that keyboard input is listened continously
                _ = Task.Delay(i++ / 60 * 1_000).ContinueWith(
                    t =>
                    {
                        // Rerender _projectsDictionary only when needed
                        foreach (KeyValuePair<int, ProjectNode> kvp in _projectsDictionary)
                        {
                            ProjectNode projectNode = kvp.Value;
                            projectNode.Log();
                        }

                        // Rerender buffer
                        TerminalBuffer.Render();
                    },
                    TaskScheduler.Default);

                if (Console.KeyAvailable)
                {
                    HandleKeyboardIput();
                }
            }
        }

        private void HandleKeyboardIput()
        {
            ConsoleKey key = Console.ReadKey().Key;
            if (key is ConsoleKey.UpArrow)
            {
                if (TerminalBuffer.TopLineIndex > 0)
                {
                    TerminalBuffer.DecrementTopLineIndex();
                }

                TerminalBuffer.ShouldRerender = true;
            }
            else if (key is ConsoleKey.DownArrow)
            {
                TerminalBuffer.IncrementTopLineIndex();
                TerminalBuffer.ShouldRerender = true;
            }
        }

        private void UpdateFooter()
        {
            float percentage = (float)_countFinishedProjects / _countStartedProjects;
            TerminalBuffer.FooterText = AnsiBuilder.Aligner.SpaceBetween(
                $"Build progress (approx.) [{AnsiBuilder.Graphics.ProgressBar(percentage)}]",
                AnsiBuilder.Formatter.Italic(AnsiBuilder.Formatter.Dim("[Up][Down] Scroll")),
                Console.BufferWidth);
        }

        private void UpdateNode<TEventArg>(BuildEventContext? context, TEventArg e, Action<TEventArg, ProjectNode> nodeUpdater)
        {
            if (context == null)
            {
                return;
            }

            int id = context!.ProjectInstanceId;
            if (_projectsDictionary.TryGetValue(id, out ProjectNode? projectNode) && projectNode != null)
            {
                nodeUpdater(e, projectNode);
                projectNode.ShouldRerender = true;
            }
        }

        #region handlers Build
        private void OnBuildStarted(object sender, BuildStartedEventArgs e)
        {
        }

        private void OnBuildFinished(object sender, BuildFinishedEventArgs e) => _succeeded = e.Succeeded;
        #endregion

        #region handlers Project
        private void OnProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            _countStartedProjects++;
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (_projectsDictionary.ContainsKey(id))
            {
                return;
            }

            ProjectNode projectNode = new(e);
            _ = _projectsDictionary.AddOrUpdate(id, projectNode, (x, y) => projectNode);
            UpdateFooter();
            projectNode.ShouldRerender = true;
        }

        private void OnProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            int id = e.BuildEventContext!.ProjectInstanceId;
            if (_projectsDictionary.TryGetValue(id, out ProjectNode? projectNode) && projectNode != null)
            {
                projectNode.Finished = true;
                _countFinishedProjects++;
                UpdateFooter();
                projectNode.ShouldRerender = true;
            }
        }
        #endregion

        #region handlers Target
        private void OnTargetStarted(object sender, TargetStartedEventArgs e) => UpdateNode(e.BuildEventContext, e, (x, y) => y.AddTarget(x));

        private void OnTargetFinished(object sender, TargetFinishedEventArgs e)
        {
            if (_projectsDictionary.TryGetValue(e.BuildEventContext!.ProjectInstanceId, out ProjectNode? projectNode) && projectNode != null)
            {
                projectNode.IncrementFinishedTargetsCount();
                projectNode.ShouldRerender = true;
            }
        }
        #endregion

        #region handlers Task
        private void OnTaskStarted(object sender, TaskStartedEventArgs e) => UpdateNode(e.BuildEventContext, e, (x, y) => y.AddTask(x));

        private void OnTaskFinished(object sender, TaskFinishedEventArgs e)
        {
        }
        #endregion

        #region handlers Raised messages, warnings and errors
        private void OnMessageRaised(object sender, BuildMessageEventArgs e)
        {
            if (e is TaskCommandLineEventArgs)
            {
                return;
            }

            UpdateNode(e.BuildEventContext, e, (x, y) => y.AddMessage(x));
        }

        private void OnWarningRaised(object sender, BuildWarningEventArgs e) => UpdateNode(e.BuildEventContext, e, (x, y) => y.AddWarning(x));

        private void OnErrorRaised(object sender, BuildErrorEventArgs e) => UpdateNode(e.BuildEventContext, e, (x, y) => y.AddError(x));

        private void OnCancelKeyPressed(object? sender, ConsoleCancelEventArgs eventArgs) => Shutdown();
        #endregion
    }
}
