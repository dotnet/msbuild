// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Resources;
using Microsoft.Build.Framework;

#pragma warning disable MSBUILD001

namespace Microsoft.Build.Utilities;

/// <summary>
/// Runs cacheable tools and replays captured diagnostics through normal <see cref="ToolTask"/> logging.
/// </summary>
[Experimental("MSBUILD001")]
public abstract class CachedToolTask : ToolTask
{
    private OutputCapture? _outputCapture;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedToolTask"/> class.
    /// </summary>
    protected CachedToolTask()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedToolTask"/> class with the specified task resources.
    /// </summary>
    /// <param name="taskResources">The resource manager for localized task messages.</param>
    protected CachedToolTask(ResourceManager taskResources)
        : base(taskResources)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedToolTask"/> class with the specified task resources and help keyword prefix.
    /// </summary>
    /// <param name="taskResources">The resource manager for localized task messages.</param>
    /// <param name="helpKeywordPrefix">The help keyword prefix for task messages.</param>
    protected CachedToolTask(ResourceManager taskResources, string helpKeywordPrefix)
        : base(taskResources, helpKeywordPrefix)
    {
    }

    /// <summary>
    /// Gets the tool name and cache environment variable names.
    /// </summary>
    protected abstract InvocationCacheConfiguration CacheConfiguration { get; }

    /// <summary>
    /// Describes all relevant inputs, execution context, and owned outputs.
    /// </summary>
    /// <param name="pathToTool">The path to the tool executable.</param>
    /// <returns>The description, or null to bypass caching. Called only when caching is enabled.</returns>
    protected abstract InvocationDescription? DescribeInvocation(string pathToTool);

    /// <inheritdoc />
    protected sealed override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
    {
        if (!TryExecuteCached(pathToTool, responseFileCommands, commandLineCommands, out int exitCode))
        {
            exitCode = ExecuteToolCore(pathToTool, responseFileCommands, commandLineCommands);
        }

        return NormalizeExitCodeForLoggedErrors(exitCode);
    }

    private bool TryExecuteCached(string pathToTool, string responseFileCommands, string commandLineCommands,
        out int exitCode)
    {
        exitCode = -1;
        InvocationCache? cache = InvocationCache.TryCreateFromEnvironment(Log, CacheConfiguration, TaskEnvironment);
        if (cache is null)
        {
            return false;
        }

        InvocationDescription? invocation = DescribeInvocation(pathToTool);
        if (invocation is null)
        {
            return false;
        }

        string workingDirectory = GetWorkingDirectory();
        AbsolutePath resolvedWorkingDirectory;
        InvocationDescription resolvedInvocation;
        try
        {
            resolvedWorkingDirectory = cache.ResolveWorkingDirectory(workingDirectory ?? string.Empty);
            resolvedInvocation = invocation.ResolvePaths(resolvedWorkingDirectory);
            cache.ValidateOutputs(resolvedInvocation.Outputs, resolvedInvocation.Inputs, resolvedWorkingDirectory.Value);
        }
        catch (ArgumentException ex)
        {
            Log.LogMessage(MessageImportance.Low, $"{CacheConfiguration.ToolName} cache bypassed: {ex.Message}");
            return false;
        }

        var result = new InvocationResult(-1);
        OutputCapture? capture = null;
        int? actualToolExitCode = null;
        try
        {
            if (!cache.TryExecuteCachedCore(resolvedInvocation, ExecuteAndCapture, resolvedWorkingDirectory.Value, validateOutputs: false, out result))
            {
                return false;
            }

            exitCode = actualToolExitCode ?? result.ExitCode;
            return true;
        }
        finally
        {
            // Close capture before replay so late callbacks cannot mutate the lists,
            // including when ExecuteToolCore throws before returning.
            _outputCapture = null;
            IReadOnlyList<string> standardOutput;
            IReadOnlyList<string> standardError;
            if (capture is not null)
            {
                capture.Complete(out standardOutput, out standardError);
            }
            else
            {
                standardOutput = result.StandardOutput;
                standardError = result.StandardError;
            }

            ReplayOutput(standardOutput, standardError);
        }

        InvocationResult ExecuteAndCapture()
        {
            capture = new OutputCapture();
            _outputCapture = capture;
            int toolExitCode = ExecuteToolCore(pathToTool, responseFileCommands, commandLineCommands);
            actualToolExitCode = toolExitCode;
            capture.Complete(out IReadOnlyList<string> standardOutput, out IReadOnlyList<string> standardError);
            bool uncacheable = TerminatedTool || EofWaitTimedOut || ToolCanceled.WaitOne(0);
            if (EofWaitTimedOut && toolExitCode == 0)
            {
                Log.LogMessage(MessageImportance.Low, $"{CacheConfiguration.ToolName} cache store skipped: output capture did not reach EOF.");
            }

            int cacheExitCode = uncacheable && toolExitCode == 0
                ? -1
                : toolExitCode;
            return new InvocationResult(cacheExitCode, standardOutput, standardError);
        }
    }

    /// <summary>
    /// Executes the tool on cache misses and bypasses, including any tool-specific output preparation.
    /// </summary>
    /// <param name="pathToTool">The path to the tool executable.</param>
    /// <param name="responseFileCommands">The commands to write to the response file.</param>
    /// <param name="commandLineCommands">The commands to pass on the command line.</param>
    /// <returns>The tool execution exit code.</returns>
    protected virtual int ExecuteToolCore(string pathToTool, string responseFileCommands, string commandLineCommands)
        => base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);

    internal override bool TryCaptureStandardOutputData(string data)
        => _outputCapture is { } capture && capture.TryAddStandardOutput(data);

    internal override bool TryCaptureStandardErrorData(string data)
        => _outputCapture is { } capture && capture.TryAddStandardError(data);

    private void ReplayOutput(IReadOnlyList<string> standardOutput, IReadOnlyList<string> standardError)
    {
        foreach (string line in standardOutput)
        {
            LogEventsFromTextOutput(line, StandardOutputImportanceToUse);
        }

        foreach (string line in standardError)
        {
            if (LogStandardErrorAsError)
            {
                Log.LogError(line);
            }
            else
            {
                LogEventsFromTextOutput(line, StandardErrorImportanceToUse);
            }
        }
    }

    internal sealed class OutputCapture
    {
        private readonly object _lock = new();
        private readonly List<string> _standardOutput = [];
        private readonly List<string> _standardError = [];
        private bool _closed;

        public bool TryAddStandardOutput(string line)
        {
            lock (_lock)
            {
                if (_closed)
                {
                    return false;
                }

                _standardOutput.Add(line);
                return true;
            }
        }

        public bool TryAddStandardError(string line)
        {
            lock (_lock)
            {
                if (_closed)
                {
                    return false;
                }

                _standardError.Add(line);
                return true;
            }
        }

        public void Complete(out IReadOnlyList<string> standardOutput, out IReadOnlyList<string> standardError)
        {
            lock (_lock)
            {
                _closed = true;
                standardOutput = _standardOutput;
                standardError = _standardError;
            }
        }
    }
}
