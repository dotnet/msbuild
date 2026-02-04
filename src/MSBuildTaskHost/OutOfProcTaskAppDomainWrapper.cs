// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.TaskHost.BackEnd;
using Microsoft.Build.TaskHost.Resources;
using Microsoft.Build.TaskHost.Utilities;

namespace Microsoft.Build.TaskHost;

/// <summary>
/// Class for executing a task in an AppDomain.
/// </summary>
internal sealed class OutOfProcTaskAppDomainWrapper : IDisposable
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>
    /// This is an appDomain instance if any is created for running this task.
    /// </summary>
    private AppDomain? _taskAppDomain;

    /// <summary>
    /// This is responsible for invoking Execute on the Task
    /// Any method calling <see cref="ExecuteTask"/> must remember to call <see cref="Dispose"/>.
    /// </summary>
    /// <remarks>
    /// We also allow the Task to have a reference to the BuildEngine by design
    /// at ITask.BuildEngine.
    /// </remarks>
    /// <param name="buildEngine">The <see cref="IBuildEngine"/> to use.</param>
    /// <param name="taskName">The name of the task to be executed.</param>
    /// <param name="taskLocation">The path of the task binary.</param>
    /// <param name="taskFile">The path to the project file in which the task invocation is located.</param>
    /// <param name="taskLine">The line in the project file where the task invocation is located.</param>
    /// <param name="taskColumn">The column in the project file where the task invocation is located.</param>
    /// <param name="appDomainSetup">The <see cref="AppDomainSetup"/> that we want to use to launch AppDomain-isolated tasks.</param>
    /// <param name="taskParameters">Parameters that will be passed to the task when created.</param>
    /// <returns>Task completion result showing success, failure or if there was a crash.</returns>
    public OutOfProcTaskHostTaskResult ExecuteTask(
        IBuildEngine buildEngine,
        string taskName,
        string taskLocation,
        string taskFile,
        int taskLine,
        int taskColumn,
        AppDomainSetup appDomainSetup,
        Dictionary<string, TaskParameter> taskParameters)
    {
        _taskAppDomain = null;

        LoadedType taskType;
        try
        {
            TypeLoader typeLoader = new(TaskLoader.IsTaskClass);
            taskType = typeLoader.Load(
                taskName,
                taskLocation,
                logWarning: (format, args) => { },
                useTaskHost: false,
                taskHostParamsMatchCurrentProc: true);
        }
        catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
        {
            return OutOfProcTaskHostTaskResult.CrashedDuringInitialization(
                GetRelevantException(e),
                "TaskInstantiationFailureError",
                [taskName, taskLocation, string.Empty]);
        }

        return InstantiateAndExecuteTask(
            buildEngine,
            taskType,
            taskName,
            taskLocation,
            taskFile,
            taskLine,
            taskColumn,
            appDomainSetup,
            taskParameters);
    }

    /// <summary>
    /// This is responsible for cleaning up the task after the OutOfProcTaskHostNode has gathered everything it needs from this execution
    /// For example: We will need to hold on new AppDomains created until we finish getting all outputs from the task
    /// Add any other cleanup tasks here. Any method calling ExecuteTask must remember to call CleanupTask.
    /// </summary>
    public void Dispose()
    {
        if (_taskAppDomain != null)
        {
            AppDomain.Unload(_taskAppDomain);
        }

        TaskLoader.RemoveAssemblyResolver();
    }

    /// <summary>
    /// Do the work of actually instantiating and running the task.
    /// </summary>
    private OutOfProcTaskHostTaskResult InstantiateAndExecuteTask(
        IBuildEngine buildEngine,
        LoadedType taskType,
        string taskName,
        string taskLocation,
        string taskFile,
        int taskLine,
        int taskColumn,
        AppDomainSetup appDomainSetup,
        Dictionary<string, TaskParameter> taskParameters)
    {
        _taskAppDomain = null;
        ITask wrappedTask;

        try
        {
            wrappedTask = TaskLoader.CreateTask(
                taskType,
                taskName,
                taskFile,
                taskLine,
                taskColumn,
                LogErrorDelegate,
                appDomainSetup,
                appDomainCreated: null, // custom app domain assembly loading won't be available for task host
                isOutOfProc: true,
                out _taskAppDomain)!;

            wrappedTask.BuildEngine = buildEngine;
        }
        catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
        {
            return OutOfProcTaskHostTaskResult.CrashedDuringInitialization(
                GetRelevantException(e),
                resourceId: "TaskInstantiationFailureError",
                resourceArgs: [taskName, taskLocation, string.Empty]);
        }

        if (TryAssignInputs(wrappedTask, taskName, taskParameters) is { } result)
        {
            return result;
        }

        bool success = false;
        try
        {
            // If it didn't crash and return before now, we're clear to go ahead and execute here.
            success = wrappedTask.Execute();
        }
        catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
        {
            return OutOfProcTaskHostTaskResult.CrashedDuringExecution(e);
        }

        Dictionary<string, object?>? finalParameterValues = CollectOutputs(wrappedTask);

        return success
            ? OutOfProcTaskHostTaskResult.Success(finalParameterValues)
            : OutOfProcTaskHostTaskResult.Failure(finalParameterValues);

        void LogErrorDelegate(string taskLocation, int taskLine, int taskColumn, string message, params object[] messageArgs)
        {
            BuildErrorEventArgs error = new(
                subcategory: null,
                code: null,
                file: taskLocation,
                lineNumber: taskLine,
                columnNumber: taskColumn,
                endLineNumber: 0,
                endColumnNumber: 0,
                message: ResourceUtilities.FormatString(AssemblyResources.GetString(message), messageArgs),
                helpKeyword: null,
                senderName: taskName);

            buildEngine.LogErrorEvent(error);
        }
    }

    private static OutOfProcTaskHostTaskResult? TryAssignInputs(
        ITask wrappedTask, string taskName, Dictionary<string, TaskParameter> taskParameters)
    {
        Type wrappedTaskType = wrappedTask.GetType();

        foreach (KeyValuePair<string, TaskParameter> kvp in taskParameters)
        {
            string name = kvp.Key;
            TaskParameter parameter = kvp.Value;

            try
            {
                PropertyInfo property = wrappedTaskType.GetProperty(name, PublicInstance);
                property.SetValue(wrappedTask, parameter?.WrappedParameter, index: null);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                return OutOfProcTaskHostTaskResult.CrashedDuringInitialization(
                    GetRelevantException(e),
                    resourceId: "InvalidTaskAttributeError",
                    resourceArgs: [name, parameter?.ToString() ?? string.Empty, taskName]);
            }
        }

        return null;
    }

    private static Dictionary<string, object?>? CollectOutputs(ITask wrappedTask)
    {
        Type wrappedTaskType = wrappedTask.GetType();

        Dictionary<string, object?>? outputs = null;

        foreach (PropertyInfo property in wrappedTaskType.GetProperties(PublicInstance))
        {
            // only record outputs
            if (property.GetCustomAttributes(typeof(OutputAttribute), inherit: true).Length > 0)
            {
                outputs ??= new(StringComparer.OrdinalIgnoreCase);

                try
                {
                    outputs[property.Name] = property.GetValue(wrappedTask, index: null);
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    // If it's not a critical exception, we assume there's some sort of problem in the property getter.
                    // So, save the exception and we'll re-throw once we're back on the main node side of the
                    // communications pipe.
                    outputs[property.Name] = e;
                }
            }
        }

        return outputs;
    }

    private static Exception GetRelevantException(Exception e)
        => e is TargetInvocationException ? e.InnerException : e;
}
