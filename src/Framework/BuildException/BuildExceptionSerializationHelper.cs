// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd
{
    internal static class BuildExceptionSerializationHelper
    {
        private static Dictionary<string, Func<string, Exception?, BuildExceptionBase>>? s_exceptionFactories;

        private static readonly Func<string, Exception?, BuildExceptionBase> s_defaultFactory =
            (message, innerException) => new GenericBuildTransferredException(message, innerException);

        internal static bool IsSupportedExceptionType(Type type)
        {
            return type.IsClass &&
                   !type.IsAbstract &&
                   type.IsSubclassOf(typeof(Exception)) &&
                   type.IsSubclassOf(typeof(BuildExceptionBase));
        }

        internal static void InitializeSerializationContract(params Type[] exceptionTypesAllowlist)
        {
            InitializeSerializationContract((IEnumerable<Type>)exceptionTypesWhitelist);
        }

        internal static void InitializeSerializationContract(IEnumerable<Type> exceptionTypesWhitelist)
        {
            if (s_exceptionFactories != null)
            {
                return;
            }

            var exceptionFactories = new Dictionary<string, Func<string, Exception?, BuildExceptionBase>>();

            foreach (Type exceptionType in exceptionTypesWhitelist)
            {
                if (!IsSupportedExceptionType(exceptionType))
                {
                    EscapeHatches.ThrowInternalError($"Type {exceptionType.FullName} is not recognized as a build exception type.");
                }

                // First try to find a static method CreateFromRemote
                //   - to be used when exception has custom constructor logic (e.g. altering messages)
                MethodInfo? methodInfo = exceptionType.GetMethod(
                    "CreateFromRemote",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string), typeof(Exception) },
                    null);

                if (methodInfo != null)
                {
                    string key = GetExceptionSerializationKey(exceptionType);
                    var value = (Func<string, Exception?, BuildExceptionBase>)Delegate.CreateDelegate(typeof(Func<string, Exception?, BuildExceptionBase>), methodInfo);

                    exceptionFactories[key] = value;
                    continue;
                }

                // Otherwise use the constructor that accepts inner exception and a message
                ConstructorInfo? ctorInfo = exceptionType.GetConstructor(
                    BindingFlags.CreateInstance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { typeof(string), typeof(Exception) },
                    null);

                if (ctorInfo != null)
                {
                    string key = GetExceptionSerializationKey(exceptionType);
                    Func<string, Exception?, BuildExceptionBase> value = (message, innerException) =>
                        (BuildExceptionBase)ctorInfo.Invoke(new object?[] { message, innerException });

                    exceptionFactories[key] = value;
                }
                else
                {
                    Debug.Fail($"Unable to find a factory for exception type {exceptionType.FullName}");
                }
            }

            if (Interlocked.Exchange(ref s_exceptionFactories, exceptionFactories) != null)
            {
                EscapeHatches.ThrowInternalError("Serialization contract was already initialized.");
            }
        }

        internal static string GetExceptionSerializationKey(Type exceptionType)
        {
            return exceptionType.FullName ?? exceptionType.ToString();
        }

        internal static Func<string, Exception?, BuildExceptionBase> CreateExceptionFactory(string serializationType)
        {
            Func<string, Exception?, BuildExceptionBase>? factory = null;
            if (s_exceptionFactories == null)
            {
                EscapeHatches.ThrowInternalError("Serialization contract was not initialized.");
            }
            else
            {
                s_exceptionFactories.TryGetValue(serializationType, out factory);
            }

            return factory ?? s_defaultFactory;
        }
    }
}
