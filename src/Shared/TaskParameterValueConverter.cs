// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
#if NET
using System.Runtime.CompilerServices;
#endif
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd;

internal static class TaskParameterValueConverter
{
    private static class TypedItemFactories
    {
        internal static readonly ConcurrentDictionary<Type, Func<ITaskItem, ITaskItem>> Cache = new();
    }

    internal static object? ConvertToParameterType(object? value, Type parameterType)
    {
        if (value is null || parameterType.IsInstanceOfType(value))
        {
            return value;
        }

        if (value is Array array && parameterType.IsArray)
        {
            Type elementType = parameterType.GetElementType()!;
#if NET
            Array converted = Array.CreateInstanceFromArrayType(parameterType, array.Length);
#else
            Array converted = Array.CreateInstance(elementType, array.Length);
#endif
            for (int i = 0; i < array.Length; i++)
            {
                converted.SetValue(ConvertToParameterType(array.GetValue(i), elementType), i);
            }

            return converted;
        }

        if (value is ITaskItem item
            && TaskParameterTypeVerifier.TryGetSupportedTaskItemValueType(parameterType, out Type? valueType))
        {
            return CreateTaskItemOfT(valueType, item);
        }

        // Path values have already been rooted by the sending engine. Outputs retain their
        // canonical string representation; only task inputs need their declared type restored.
        return value is string text
            ? ValueTypeParser.Parse(text, parameterType)
            : Convert.ChangeType(value, parameterType, CultureInfo.InvariantCulture);
    }

    internal static ITaskItem CreateTaskItemOfT(Type genericArgument, ITaskItem item)
    {
        Func<ITaskItem, ITaskItem> factory = TypedItemFactories.Cache.GetOrAdd(genericArgument, static t =>
        {
#if NET
            if (!RuntimeFeature.IsDynamicCodeSupported)
            {
                throw new NotSupportedException(
                    "Task parameters typed as TaskItem<T> or ITaskItem<T> require runtime code generation " +
                    "(Type.MakeGenericType and expression compilation) and are not supported when MSBuild " +
                    "runs trimmed or with Native AOT.");
            }
#endif
            ConstructorInfo constructor = typeof(TaskItem<>).MakeGenericType(t).GetConstructor([typeof(ITaskItem)])!;
            ParameterExpression itemParameter = Expression.Parameter(typeof(ITaskItem), "item");
            return Expression.Lambda<Func<ITaskItem, ITaskItem>>(
                Expression.Convert(Expression.New(constructor, itemParameter), typeof(ITaskItem)),
                itemParameter).Compile();
        });

        return factory(item);
    }
}
