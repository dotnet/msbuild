// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    internal static class BuildExceptionSerializationHelper
    {
        private class BuildExceptionConstructionCallbacks
        {
            public BuildExceptionConstructionCallbacks(
                Func<string, Exception?, Exception> factory,
                Action<Exception, BuildExceptionRemoteState> instnaceInitializer,
                Func<Exception, IDictionary<string, string?>?> remoteStateExtractor)
            {
                Factory = factory;
                InstnaceInitializer = instnaceInitializer;
                RemoteStateExtractor = remoteStateExtractor;
            }

            internal Func<string, Exception?, Exception> Factory { get; }
            internal Action<Exception, BuildExceptionRemoteState> InstnaceInitializer { get; }
            internal Func<Exception, IDictionary<string, string?>?> RemoteStateExtractor { get; }
        }

        private static Dictionary<string, BuildExceptionConstructionCallbacks>? s_exceptionFactories;

        private static readonly BuildExceptionConstructionCallbacks s_defaultFactory =
            new BuildExceptionConstructionCallbacks(
                (message, innerException) => new GenericBuildTransferredException(message, innerException),
                GetInstanceInitializer(typeof(GenericBuildTransferredException))!,
                _ => null);

        private static Action<Exception, BuildExceptionRemoteState>? GetInstanceInitializer(Type exceptionType)
        {
            while (!exceptionType.Name.Equals(nameof(BuildExceptionBase)) && exceptionType.BaseType != null)
            {
                exceptionType = exceptionType.BaseType!;
            }

            MethodInfo? methodInfo = exceptionType.GetMethod(
                "InitializeFromRemoteState",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(BuildExceptionRemoteState) },
                null);

            if (methodInfo != null)
            {
                // Not possible - contravariance not supported. We'd need to use Expression trees and compile them.
                // return
                //    (Action<Exception, BuildExceptionRemoteState>)
                //    Delegate.CreateDelegate(typeof(Action<Exception, BuildExceptionRemoteState>), null, methodInfo);

                return (exception, remoteState) => methodInfo.Invoke(exception, new object[] { remoteState });
            }

            return null;
        }

        private static Func<Exception, IDictionary<string, string?>?>? GetRemoteStateExtractor(Type exceptionType)
        {
            MethodInfo? methodInfo = exceptionType.GetMethod(
                "FlushCustomState",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (methodInfo != null)
            {
                // Not possible - contravariance not supported. We'd need to use Expression trees and compile them.
                // return
                //    (Func<Exception, IDictionary<string, string?>?>)
                //    Delegate.CreateDelegate(typeof(Func<BuildExceptionBase, IDictionary<string, string?>?>), null,
                //        methodInfo);

                return (exception) => (IDictionary<string, string?>?)methodInfo.Invoke(exception, null);
            }

            return null;
        }

        internal static bool IsSupportedExceptionType(Type type)
        {
            return type.IsClass &&
                   !type.IsAbstract &&
                   type.IsSubclassOf(typeof(Exception)) &&
                   (type.IsSubclassOf(typeof(BuildExceptionBase)) ||
                    // This is to support InvalidProjectFileException which is cannot be a subclass of BuildExceptionBase from Microsoft.Build.Framework
                    type.BaseType!.Name.Equals(nameof(BuildExceptionBase)));
        }

        internal static void InitializeSerializationContract(params Type[] exceptionTypesWhitelist)
        {
            InitializeSerializationContract((IEnumerable<Type>)exceptionTypesWhitelist);
        }

        internal static void InitializeSerializationContract(IEnumerable<Type> exceptionTypesWhitelist)
        {
            var exceptionFactories = new Dictionary<string, BuildExceptionConstructionCallbacks>();

            foreach (Type exceptionType in exceptionTypesWhitelist)
            {
                if (!IsSupportedExceptionType(exceptionType))
                {
                    EscapeHatches.ThrowInternalError($"Type {exceptionType.FullName} is not recognized as a build exception type.");
                }

                Func<Exception, IDictionary<string, string?>?>? remoteStateExtractor =
                    GetRemoteStateExtractor(exceptionType);

                // First try to find a static method CreateFromRemote
                //   - to be used when exception has custom constructor logic (e.g. altering messages)
                Func<string, Exception?, Exception>? factory = null;
                bool hasFactory = false;
                MethodInfo? methodInfo = exceptionType.GetMethod(
                    "CreateFromRemote",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string), typeof(Exception) },
                    null);

                if (methodInfo != null)
                {
                    factory = (Func<string, Exception?, Exception>)Delegate.CreateDelegate(
                        typeof(Func<string, Exception?, Exception>), methodInfo);
                }
                else
                {
                    // Then fallback to a constructor with (string, Exception) signature
                    ConstructorInfo? ctorInfo = exceptionType.GetConstructor(
                        BindingFlags.CreateInstance | BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance,
                        null,
                        new[] { typeof(string), typeof(Exception) },
                        null);

                    if (ctorInfo != null)
                    {
                        factory = (message, innerException) =>
                            (Exception)ctorInfo.Invoke(new object?[] { message, innerException });
                    }
                }

                // Lastly we need to have 'InitializeFromRemoteState' method
                if (factory != null)
                {
                    Action<Exception, BuildExceptionRemoteState>? instanceInitializer =
                        GetInstanceInitializer(exceptionType);
                    if (instanceInitializer != null)
                    {
                        exceptionFactories.Add(GetExceptionSerializationKey(exceptionType),
                            new BuildExceptionConstructionCallbacks(factory, instanceInitializer, remoteStateExtractor!));
                        hasFactory = true;
                    }
                }

                if (!hasFactory)
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

        private static BuildExceptionConstructionCallbacks CreateExceptionFactory(string serializationType)
        {
            BuildExceptionConstructionCallbacks? factory = null;
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

        internal static void WriteExceptionToTranslator(ITranslator translator, Exception exception)
        {
            BinaryWriter writer = translator.Writer;
            writer.Write(exception.InnerException != null);
            if (exception.InnerException != null)
            {
                WriteExceptionToTranslator(translator, exception.InnerException);
            }

            string serializationType = GetExceptionSerializationKey(exception.GetType());
            writer.Write(serializationType);
            writer.Write(exception.Message);
            writer.WriteOptionalString(exception.StackTrace);
            writer.WriteOptionalString(exception.Source);
            writer.WriteOptionalString(exception.HelpLink);
            // HResult is completely protected up till net4.5
#if NET || NET45_OR_GREATER
            int? hresult = exception.HResult;
#else
            int? hresult = null;
#endif
            writer.WriteOptionalInt32(hresult);

            IDictionary<string, string?>? customKeyedSerializedData = CreateExceptionFactory(serializationType).RemoteStateExtractor(exception);
            if (customKeyedSerializedData == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(customKeyedSerializedData.Count);
                foreach (var pair in customKeyedSerializedData)
                {
                    writer.Write(pair.Key);
                    writer.WriteOptionalString(pair.Value);
                }
            }

            Debug.Assert((exception.Data?.Count ?? 0) == 0,
                "Exception Data is not supported in BuildTransferredException");
        }

        internal static Exception ReadExceptionFromTranslator(ITranslator translator)
        {
            BinaryReader reader = translator.Reader;
            Exception? innerException = null;
            if (reader.ReadBoolean())
            {
                innerException = ReadExceptionFromTranslator(translator);
            }

            string serializationType = reader.ReadString();
            string message = reader.ReadString();
            string? deserializedStackTrace = reader.ReadOptionalString();
            string? source = reader.ReadOptionalString();
            string? helpLink = reader.ReadOptionalString();
            int hResult = reader.ReadOptionalInt32();

            IDictionary<string, string?>? customKeyedSerializedData = null;
            if (reader.ReadByte() == 1)
            {
                int count = reader.ReadInt32();
                customKeyedSerializedData = new Dictionary<string, string?>(count, StringComparer.CurrentCulture);

                for (int i = 0; i < count; i++)
                {
                    customKeyedSerializedData[reader.ReadString()] = reader.ReadOptionalString();
                }
            }

            BuildExceptionConstructionCallbacks constructionCallbacks = CreateExceptionFactory(serializationType);

            Exception exception = constructionCallbacks.Factory(message, innerException);
            constructionCallbacks.InstnaceInitializer(exception, new BuildExceptionRemoteState(
                serializationType,
                deserializedStackTrace,
                source,
                helpLink,
                hResult,
                customKeyedSerializedData));

            return exception;
        }
    }
}
