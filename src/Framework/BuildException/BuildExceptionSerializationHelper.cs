// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.Build.Framework.BuildException
{
    internal static class BuildExceptionSerializationHelper
    {
        public class TypeConstructionTuple
        {
            public TypeConstructionTuple(Type type, Func<string, Exception?, BuildExceptionBase> factory)
            {
                Type = type;
                Factory = factory;
            }

            public Type Type { get; }
            public Func<string, Exception?, BuildExceptionBase> Factory { get; }
        }

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

        internal static void InitializeSerializationContract(params TypeConstructionTuple[] exceptionsAllowlist)
        {
            InitializeSerializationContract((IEnumerable<TypeConstructionTuple>)exceptionsAllowlist);
        }

        internal static void InitializeSerializationContract(IEnumerable<TypeConstructionTuple> exceptionsAllowlist)
        {
            if (s_exceptionFactories != null)
            {
                return;
            }

            var exceptionFactories = new Dictionary<string, Func<string, Exception?, BuildExceptionBase>>();

            foreach (TypeConstructionTuple typeConstructionTuple in exceptionsAllowlist)
            {
                Type exceptionType = typeConstructionTuple.Type;
                Func<string, Exception?, BuildExceptionBase> exceptionFactory = typeConstructionTuple.Factory;

                Assumed.True(IsSupportedExceptionType(exceptionType), $"Type {exceptionType.FullName} is not recognized as a build exception type.");

                string key = GetExceptionSerializationKey(exceptionType);
                exceptionFactories[key] = exceptionFactory;
            }

            if (Interlocked.Exchange(ref s_exceptionFactories, exceptionFactories) != null)
            {
                InternalError.Throw("Serialization contract was already initialized.");
            }
        }

        internal static string GetExceptionSerializationKey(Type exceptionType)
        {
            return exceptionType.FullName ?? exceptionType.ToString();
        }

        internal static Func<string, Exception?, BuildExceptionBase> CreateExceptionFactory(string serializationType)
        {
            Assumed.NotNull(s_exceptionFactories, "Serialization contract was not initialized.");

            return s_exceptionFactories.TryGetValue(serializationType, out Func<string, Exception?, BuildExceptionBase>? factory)
                ? factory ?? s_defaultFactory
                : s_defaultFactory;
        }
    }
}
