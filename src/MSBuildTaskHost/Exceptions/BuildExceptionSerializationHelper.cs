// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.TaskHost.Exceptions;

internal static class BuildExceptionSerializationHelper
{
    private static readonly Dictionary<string, Func<string, Exception?, BuildExceptionBase>> s_exceptionFactories = new()
    {
        { GetSerializationKey<InternalErrorException>(), InternalErrorException.CreateFromRemote }
    };

    private static readonly Func<string, Exception?, BuildExceptionBase> s_defaultFactory =
        (message, innerException) => new GeneralBuildTransferredException(message, innerException);

    public static string GetSerializationKey<T>()
        where T : BuildExceptionBase
        => GetSerializationKey(typeof(T));

    public static string GetSerializationKey(Type exceptionType)
        => exceptionType.FullName ?? exceptionType.ToString();

    public static BuildExceptionBase DeserializeException(string serializationType, string message, Exception? innerException)
    {
        if (!s_exceptionFactories.TryGetValue(serializationType, out var factory))
        {
            factory = s_defaultFactory;
        }

        return factory(message, innerException);
    }
}
