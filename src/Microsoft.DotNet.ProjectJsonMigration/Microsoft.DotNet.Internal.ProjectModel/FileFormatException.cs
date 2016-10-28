// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Internal.ProjectModel
{
    public sealed class FileFormatException : Exception
    {
        private FileFormatException(string message) :
            base(message)
        {
        }

        private FileFormatException(string message, Exception innerException) :
            base(message, innerException)
        {
        }

        public string Path { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        public override string ToString()
        {
            return $"{Path}({Line},{Column}): Error: {base.ToString()}";
        }

        internal static FileFormatException Create(Exception exception, string filePath)
        {
            return new FileFormatException(exception.Message, exception)
                .WithFilePath(filePath)
                .WithLineInfo(exception);
        }

        internal static FileFormatException Create(Exception exception, JToken jsonValue, string filePath)
        {
            var result = Create(exception, jsonValue)
                .WithFilePath(filePath);

            return result;
        }

        internal static FileFormatException Create(Exception exception, JToken jsonValue)
        {
            var result = new FileFormatException(exception.Message, exception)
                .WithLineInfo(jsonValue);

            return result;
        }

        internal static FileFormatException Create(string message, JToken jsonValue, string filePath)
        {
            var result = Create(message, jsonValue)
                .WithFilePath(filePath);

            return result;
        }

        internal static FileFormatException Create(string message, string filePath)
        {
            var result = new FileFormatException(message)
                .WithFilePath(filePath);

            return result;
        }

        internal static FileFormatException Create(string message, JToken jsonValue)
        {
            var result = new FileFormatException(message)
                .WithLineInfo(jsonValue);

            return result;
        }

        internal FileFormatException WithFilePath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            Path = path;

            return this;
        }

        private FileFormatException WithLineInfo(Exception exception)
        {
            if (exception is JsonReaderException)
            {
                WithLineInfo((JsonReaderException) exception);
            }

            return this;
        }

        private FileFormatException WithLineInfo(JsonReaderException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            Line = exception.LineNumber;
            Column = exception.LinePosition;

            return this;
        }

        private FileFormatException WithLineInfo(JToken value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var lineInfo = (IJsonLineInfo)value;
            Line = lineInfo.LineNumber;
            Column = lineInfo.LinePosition;

            return this;
        }
    }
}
