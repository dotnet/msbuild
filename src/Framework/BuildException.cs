// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    internal static class BuildExceptionSerializationHelper
    {
        private static readonly Dictionary<string, Func<string, Exception?, BuildExceptionBase>> s_exceptionFactories = FetchExceptionsConstructors();

        private static readonly Func<string, Exception?, BuildExceptionBase> s_defaultFactory =
            (message, innerException) => new GenericBuildTransferredException(message, innerException);

        private static Dictionary<string, Func<string, Exception?, BuildExceptionBase>> FetchExceptionsConstructors()
        {
            var exceptionFactories = new Dictionary<string, Func<string, Exception?, BuildExceptionBase>>();

            foreach (Type exceptionType in AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(BuildExceptionBase))))
            {
                MethodInfo? methodInfo = exceptionType.GetMethod(
                    "CreateFromRemote",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string), typeof(Exception) },
                    null);

                if (methodInfo != null)
                {
                    string key = GetExceptionSerializationKey(exceptionType);
                    var value = (Func<string, Exception?, BuildExceptionBase>) Delegate.CreateDelegate(typeof(Func<string, Exception?, BuildExceptionBase>), methodInfo);

                    exceptionFactories[key] = value;
                    continue;
                }

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
            }

            return exceptionFactories;
        }

        internal static string GetExceptionSerializationKey(Type exceptionType)
        {
            return exceptionType.FullName ?? exceptionType.ToString();
        }

        internal static Func<string, Exception?, BuildExceptionBase> CreateExceptionFactory(string serializationType)
        {
            Func<string, Exception?, BuildExceptionBase>? factory;
            if (!s_exceptionFactories.TryGetValue(serializationType, out factory))
            {
                factory = s_defaultFactory;
            }

            return factory;
        }
    }

    public abstract class BuildExceptionBase : Exception
    {
        private string? _remoteTypeName;
        private string? _remoteStackTrace;

        protected BuildExceptionBase()
            : base()
        { }

        protected BuildExceptionBase(string message)
            : base(message)
        { }

        protected BuildExceptionBase(
            string message,
            Exception? inner)
            : base(message, inner)
        { }

        // This is needed as soon as we allow opt out of the non-BinaryFormatter serialization
        protected BuildExceptionBase(SerializationInfo info, StreamingContext context)
            : base(info, context)
        { }

        public override string? StackTrace => string.IsNullOrEmpty(_remoteStackTrace) ? base.StackTrace : _remoteStackTrace;

        public override string ToString() => string.IsNullOrEmpty(_remoteTypeName) ? base.ToString() : $"{_remoteTypeName}->{base.ToString()}";

        protected virtual void InitializeCustomState(IDictionary<string, string?>? customKeyedSerializedData)
        { /* This is it. Override for exceptions with custom state */ }

        protected virtual IDictionary<string, string?>? FlushCustomState()
        {
            /* This is it. Override for exceptions with custom state */
            return null;
        }

        private void InitializeFromRemoteState(
            string remoteTypeName,
            string? remoteStackTrace,
            string? source,
            string? helpLink,
            int hresult,
            IDictionary<string, string?>? customKeyedSerializedData)
        {
            _remoteTypeName = remoteTypeName;
            _remoteStackTrace = remoteStackTrace;
            base.Source = source;
            base.HelpLink = helpLink;
            base.HResult = hresult;
            if (customKeyedSerializedData != null)
            {
                InitializeCustomState(customKeyedSerializedData);
            }
        }

        internal static void WriteExceptionToTranslator(ITranslator translator, Exception exception)
        {
            BinaryWriter writer = translator.Writer;
            writer.Write(exception.InnerException != null);
            if (exception.InnerException != null)
            {
                WriteExceptionToTranslator(translator, exception.InnerException);
            }

            writer.Write(BuildExceptionSerializationHelper.GetExceptionSerializationKey(exception.GetType()));
            writer.Write(exception.Message);
            writer.WriteOptionalString(exception.StackTrace);
            writer.WriteOptionalString(exception.Source);
            writer.WriteOptionalString(exception.HelpLink);
            // HResult is completely protected up till net4.5
#if NET || NET45_OR_GREATER
            writer.Write((byte)1);
            writer.Write(exception.HResult);
#else
            writer.Write((byte)0);
#endif

            IDictionary<string, string?>? customKeyedSerializedData = (exception as BuildExceptionBase)?.FlushCustomState();
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

            Exception exception = BuildExceptionSerializationHelper.CreateExceptionFactory(serializationType)(message, innerException);

            if (exception is BuildExceptionBase buildException)
            {
                buildException.InitializeFromRemoteState(
                    serializationType,
                    deserializedStackTrace,
                    source,
                    helpLink,
                    hResult,
                    customKeyedSerializedData);
            }

            return exception;
        }
    }

    /// <summary>
    /// A catch-all type for remote exceptions that we don't know how to deserialize.
    /// </summary>
    internal sealed class GenericBuildTransferredException : BuildExceptionBase
    {
        internal GenericBuildTransferredException(
            string message,
            Exception? inner)
            : base(message, inner)
        { }
    }
}
