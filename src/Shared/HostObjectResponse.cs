// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd
{
    internal class HostObjectResponse : INodePacket
    {
        public HostObjectResponse()
        {
            ExceptionMessage = string.Empty;
            ExceptionType = string.Empty;
            ExceptionStackTrace = string.Empty;
            _exceptionMessage = string.Empty;
            _exceptionType = string.Empty;
            _exceptionStackTrace = string.Empty;
        }

        public HostObjectResponse(int callId, object returnValue)
        {
            CallId = callId;
            ReturnValue = returnValue;
            ExceptionMessage = string.Empty;
            ExceptionType = string.Empty;
            ExceptionStackTrace = string.Empty;
            _exceptionMessage = string.Empty;
            _exceptionType = string.Empty;
            _exceptionStackTrace = string.Empty;
        }

        public HostObjectResponse(int callId, Exception exception)
        {
            CallId = callId;
            ExceptionMessage = exception?.Message ?? string.Empty;
            ExceptionType = exception?.GetType().FullName ?? string.Empty;
            ExceptionStackTrace = exception?.StackTrace ?? string.Empty;
            _exceptionMessage = ExceptionMessage;
            _exceptionType = ExceptionType;
            _exceptionStackTrace = ExceptionStackTrace;
        }

        public int CallId { get; set; }

        public object? ReturnValue { get; set; }

        public string ExceptionMessage { get; set; }

        public string ExceptionType { get; set; }

        public string ExceptionStackTrace { get; set; }

        public NodePacketType Type => NodePacketType.HostObjectResponse;

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _callId);
            translator.Translate(ref _exceptionMessage);
            translator.Translate(ref _exceptionType);
            translator.Translate(ref _exceptionStackTrace);

            TranslateReturnValue(translator);

            CallId = _callId;
            ExceptionMessage = _exceptionMessage;
            ExceptionType = _exceptionType;
            ExceptionStackTrace = _exceptionStackTrace;
        }

        private int _callId;
        private string _exceptionMessage;
        private string _exceptionType;
        private string _exceptionStackTrace;

        private void TranslateReturnValue(ITranslator translator)
        {
            ReturnValueType returnType = ReturnValueType.Null;

            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                if (ReturnValue == null)
                {
                    returnType = ReturnValueType.Null;
                }
                else if (ReturnValue is ITaskItem[])
                {
                    returnType = ReturnValueType.TaskItemArray;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unsupported return type: {ReturnValue.GetType().FullName}");
                }
            }

            translator.TranslateEnum(ref returnType, (int)returnType);

            switch (returnType)
            {
                case ReturnValueType.Null:
                    ReturnValue = null;
                    break;

                case ReturnValueType.TaskItemArray:
                    TranslateTaskItemArray(translator);
                    break;
            }
        }

        /// <summary>
        /// Translates an array of ITaskItem objects.
        /// We can't use TranslateArray directly because ITaskItem doesn't implement ITranslatable.
        /// Instead, we manually translate each item.
        /// </summary>
        private void TranslateTaskItemArray(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                // Writing: serialize the array
                ITaskItem[]? items = ReturnValue as ITaskItem[];
                int count = items?.Length ?? 0;
                translator.Translate(ref count);

                if (items != null)
                {
                    foreach (ITaskItem item in items)
                    {
                        // Translate each item's ItemSpec
                        string itemSpec = item.ItemSpec;
                        translator.Translate(ref itemSpec);

                        // Translate metadata count
                        int metadataCount = item.MetadataCount;
                        translator.Translate(ref metadataCount);

                        // Translate each metadata key-value pair
                        foreach (string metadataName in item.MetadataNames)
                        {
                            string name = metadataName;
                            string value = item.GetMetadata(metadataName);
                            translator.Translate(ref name);
                            translator.Translate(ref value);
                        }
                    }
                }
            }
            else
            {
                // Reading: deserialize the array
                int count = 0;
                translator.Translate(ref count);

                if (count > 0)
                {
                    ITaskItem[] items = new ITaskItem[count];

                    for (int i = 0; i < count; i++)
                    {
                        // Read ItemSpec
                        string? itemSpec = null;
                        translator.Translate(ref itemSpec);

                        // Read metadata count
                        int metadataCount = 0;
                        translator.Translate(ref metadataCount);

                        IDictionary<string, string> metadata = new Dictionary<string, string>();
                        // Read each metadata key-value pair
                        for (int j = 0; j < metadataCount; j++)
                        {
                            string? name = null;
                            string? value = null;
                            translator.Translate(ref name);
                            translator.Translate(ref value);
                            metadata.Add(name, value);
                        }

                        // Create TaskItem
                        TaskItemData item = new TaskItemData(itemSpec, metadata);

                        items[i] = item;
                    }

                    ReturnValue = items;
                }
                else
                {
                    ReturnValue = Array.Empty<ITaskItem>();
                }
            }
        }

        public static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            HostObjectResponse packet = new HostObjectResponse();
            packet.Translate(translator);
            return packet;
        }

        private enum ReturnValueType : byte
        {
            Null = 0,
            TaskItemArray = 1,
        }
    }
}
