// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Engine.UnitTests;

/// <summary>
/// Task that throws exception based on input parameters.
/// </summary>
public sealed class TaskThatThrows : Utilities.Task
{
    public string? ExceptionType { get; set; }

    public string? ExceptionMessage { get; set; }

    public override bool Execute()
    {
        string exceptionMessage = string.IsNullOrWhiteSpace(ExceptionMessage) ? "Default exception message" : ExceptionMessage!;

        Type exceptionType = string.IsNullOrWhiteSpace(ExceptionType) ? typeof(Exception) : Type.GetType(ExceptionType) ?? typeof(Exception);

        ConstructorInfo? ctor = exceptionType.GetConstructor([typeof(string)]);
        Exception exceptionInstance = (Exception)(ctor?.Invoke([exceptionMessage]) ?? new Exception(exceptionMessage));

        throw exceptionInstance;
    }
}
