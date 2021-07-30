// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.DotNet.Compatibility.ErrorSuppression
{
    /// <summary>
    /// Collection of Suppressions which is able to add suppressions, check if a specific error is suppressed, and write all suppressions
    /// down to a file. The engine is thread-safe.
    /// </summary>
    public class SuppressionEngine
    {
        protected HashSet<Suppression> _validationSuppressions;
        private readonly ReaderWriterLockSlim _readerWriterLock = new();
        private readonly XmlSerializer _serializer = new(typeof(Suppression[]), new XmlRootAttribute("Suppressions"));

        protected SuppressionEngine(string suppressionFile)
        {
            _validationSuppressions = ParseSuppressionFile(suppressionFile);
        }

        protected SuppressionEngine()
        {
            _validationSuppressions = new HashSet<Suppression>();
        }

        /// <summary>
        /// Checks if the passed in error is suppressed or not.
        /// </summary>
        /// <param name="diagnosticId">The diagnostic ID of the error to check.</param>
        /// <param name="target">The target of where the <paramref name="diagnosticId"/> should be applied.</param>
        /// <param name="left">Optional. The left operand in a APICompat error.</param>
        /// <param name="right">Optional. The right operand in a APICompat error.</param>
        /// <returns><see langword="true"/> if the error is already suppressed. <see langword="false"/> otherwise.</returns>
        public bool IsErrorSuppressed(string? diagnosticId, string? target, string? left = null, string? right = null, bool isBaselineSuppression = false)
        {
            var suppressionToCheck = new Suppression()
            {
                DiagnosticId = diagnosticId,
                Target = target,
                Left = left,
                Right = right,
                IsBaselineSuppression = isBaselineSuppression
            };
            return IsErrorSuppressed(suppressionToCheck);
        }

        /// <summary>
        /// Checks if the passed in error is suppressed or not.
        /// </summary>
        /// <param name="error">The <see cref="Suppression"/> error to check.</param>
        /// <returns><see langword="true"/> if the error is already suppressed. <see langword="false"/> otherwise.</returns>
        public bool IsErrorSuppressed(Suppression error)
        {
            _readerWriterLock.EnterReadLock();
            try
            {
                if (_validationSuppressions.Contains(error))
                {
                    return true;
                }
                else if (error.DiagnosticId == null || error.DiagnosticId.StartsWith("cp", StringComparison.InvariantCultureIgnoreCase))
                {
                    // See if the error is globally suppressed by checking if the same diagnosticid and target or with the same left and right
                    return _validationSuppressions.Contains(new Suppression { DiagnosticId = error.DiagnosticId, Target = error.Target, IsBaselineSuppression = error.IsBaselineSuppression}) ||
                           _validationSuppressions.Contains(new Suppression { DiagnosticId = error.DiagnosticId, Left = error.Left, Right = error.Right, IsBaselineSuppression = error.IsBaselineSuppression });
                }

                return false;
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Adds a suppression to the collection.
        /// </summary>
        /// <param name="diagnosticId">The diagnostic ID of the error to add.</param>
        /// <param name="target">The target of where the <paramref name="diagnosticId"/> should be applied.</param>
        /// <param name="left">Optional. The left operand in a APICompat error.</param>
        /// <param name="right">Optional. The right operand in a APICompat error.</param>
        public void AddSuppression(string? diagnosticId, string? target, string? left = null, string? right = null, bool isBaselineSuppression = false)
        {
            var suppressionToAdd = new Suppression()
            {
                DiagnosticId = diagnosticId,
                Target = target,
                Left = left,
                Right = right,
                IsBaselineSuppression = isBaselineSuppression
            };
            AddSuppression(suppressionToAdd);
        }

        /// <summary>
        /// Adds a suppression to the collection.
        /// </summary>
        /// <param name="suppression">The <see cref="Suppression"/> to be added.</param>
        public void AddSuppression(Suppression suppression)
        {
            _readerWriterLock.EnterUpgradeableReadLock();
            try
            {
                if (!_validationSuppressions.Contains(suppression))
                {
                    _readerWriterLock.EnterWriteLock();
                    try
                    {
                        _validationSuppressions.Add(suppression);
                    }
                    finally
                    {
                        _readerWriterLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _readerWriterLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Writes all suppressions in collection down to a file.
        /// </summary>
        /// <param name="supressionFile">The path to the file to be written.</param>
        public void WriteSuppressionsToFile(string supressionFile)
        {
            using (Stream writer = GetWritableStream(supressionFile))
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    XmlTextWriter xmlWriter = new XmlTextWriter(writer, Encoding.UTF8);
                    xmlWriter.Formatting = Formatting.Indented;
                    xmlWriter.Indentation = 2;
                    _serializer.Serialize(xmlWriter, _validationSuppressions.ToArray());
                    AfterWrittingSuppressionsCallback(writer);
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            }
        }

        protected virtual void AfterWrittingSuppressionsCallback(Stream stream)
        {
            // Do nothing. Used for tests.
        }

        /// <summary>
        /// Creates a new instance of <see cref="SuppressionEngine"/> based on the contents of a given suppression file.
        /// </summary>
        /// <param name="suppressionFile">The path to the suppressions file to be used for initialization.</param>
        /// <returns>An instance of <see cref="SuppressionEngine"/>.</returns>
        public static SuppressionEngine CreateFromFile(string suppressionFile)
            => new SuppressionEngine(suppressionFile);

        /// <summary>
        /// Creates a new instance of <see cref="SuppressionEngine"/> which is empty.
        /// </summary>
        /// <returns>An instance of <see cref="SuppressionEngine"/>.</returns>
        public static SuppressionEngine Create()
            => new SuppressionEngine();

        private HashSet<Suppression> ParseSuppressionFile(string? file)
        {
            if (string.IsNullOrEmpty(file?.Trim()))
            {
                return new HashSet<Suppression>();
            }

            HashSet<Suppression> result;

            using (Stream reader = GetReadableStream(file!))
            {
                Suppression[]? deserializedSuppressions = _serializer.Deserialize(reader) as Suppression[];
                if (deserializedSuppressions == null)
                {
                    result = new HashSet<Suppression>();
                }
                else
                {
                    result = new HashSet<Suppression>(deserializedSuppressions);
                }
            }
            return result;
        }

        protected virtual Stream GetReadableStream(string supressionFile) => new FileStream(supressionFile, FileMode.Open);

        protected virtual Stream GetWritableStream(string suppressionFile) => new FileStream(suppressionFile, FileMode.OpenOrCreate);
    }
}
