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

namespace Microsoft.DotNet.ApiCompatibility.Logging
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
        private readonly HashSet<string> _noWarn;

        /// <summary>
        /// Creates a new instance of <see cref="SuppressionEngine"/>.
        /// </summary>
        /// <param name="suppressionsFile">The path to the suppressions file to be used for initialization.</param>
        /// <param name="noWarn">Suppression ids to suppress specific errors. Multiple suppressions are separated by a ';' character.</param>
        public SuppressionEngine(string? suppressionsFile = null, string ? noWarn = null)
        {
            _validationSuppressions = ParseSuppressionFile(suppressionsFile);
            _noWarn = string.IsNullOrEmpty(noWarn) ? new HashSet<string>() : new HashSet<string>(noWarn?.Split(';'));
        }

        /// <summary>
        /// Checks if the passed in error is suppressed or not.
        /// </summary>
        /// <param name="diagnosticId">The diagnostic ID of the error to check.</param>
        /// <param name="target">The target of where the <paramref name="diagnosticId"/> should be applied.</param>
        /// <param name="left">Optional. The left operand in a APICompat error.</param>
        /// <param name="right">Optional. The right operand in a APICompat error.</param>
        /// <returns><see langword="true"/> if the error is already suppressed. <see langword="false"/> otherwise.</returns>
        public bool IsErrorSuppressed(string diagnosticId, string? target, string? left = null, string? right = null, bool isBaselineSuppression = false)
        {
            return IsErrorSuppressed(new Suppression(diagnosticId)
            {
                Target = target,
                Left = left,
                Right = right,
                IsBaselineSuppression = isBaselineSuppression
            });
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
                if (_noWarn.Contains(error.DiagnosticId) || _validationSuppressions.Contains(error))
                {
                    return true;
                }
                else if (error.DiagnosticId.StartsWith("cp", StringComparison.InvariantCultureIgnoreCase))
                {
                    // See if the error is globally suppressed by checking if the same diagnosticid and target or with the same left and right
                    return _validationSuppressions.Contains(new Suppression(error.DiagnosticId) { Target = error.Target, IsBaselineSuppression = error.IsBaselineSuppression }) ||
                           _validationSuppressions.Contains(new Suppression(error.DiagnosticId) { Left = error.Left, Right = error.Right, IsBaselineSuppression = error.IsBaselineSuppression });
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
        public void AddSuppression(string diagnosticId, string? target, string? left = null, string? right = null, bool isBaselineSuppression = false)
        {
            AddSuppression(new Suppression(diagnosticId)
            {
                Target = target,
                Left = left,
                Right = right,
                IsBaselineSuppression = isBaselineSuppression
            });
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
        /// Writes all suppressions in collection down to a file, if empty it doesn't write anything.
        /// </summary>
        /// <param name="supressionFile">The path to the file to be written.</param>
        /// <returns>Whether it wrote the file.</returns>
        public bool WriteSuppressionsToFile(string supressionFile)
        {
            if (_validationSuppressions.Count <= 0)
                return false;

            using (Stream writer = GetWritableStream(supressionFile))
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    XmlTextWriter xmlWriter = new(writer, Encoding.UTF8)
                    {
                        Formatting = Formatting.Indented,
                        Indentation = 2
                    };
                    _serializer.Serialize(xmlWriter, _validationSuppressions.ToArray());
                    AfterWrittingSuppressionsCallback(writer);
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            }

            return true;
        }

        protected virtual void AfterWrittingSuppressionsCallback(Stream stream)
        {
            // Do nothing. Used for tests.
        }

        private HashSet<Suppression> ParseSuppressionFile(string? file)
        {
            if (string.IsNullOrEmpty(file?.Trim()))
            {
                return new HashSet<Suppression>();
            }

            using (Stream reader = GetReadableStream(file!))
            {
                if (_serializer.Deserialize(reader) is Suppression[] deserializedSuppressions)
                {
                    return new HashSet<Suppression>(deserializedSuppressions);
                }

                return new HashSet<Suppression>();
            }
        }

        protected virtual Stream GetReadableStream(string supressionFile) => new FileStream(supressionFile, FileMode.Open);

        protected virtual Stream GetWritableStream(string suppressionFile) => new FileStream(suppressionFile, FileMode.Create);
    }
}
