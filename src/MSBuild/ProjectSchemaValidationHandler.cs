// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if FEATURE_XML_SCHEMA_VALIDATION
using System;
using System.IO;
using System.Xml;
using System.Xml.Schema;

using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This class is used for validating projects against a designated schema.
    /// </summary>
    internal sealed class ProjectSchemaValidationHandler
    {
        // Set to true if there was a syntax error in the project file.
        private bool _syntaxError;

        #region Methods

        /// <summary>
        /// Validates a project file against the given schema.  If no schema is given, validates 
        /// against the default schema
        /// </summary>
        /// <param name="projectFile">Path of the file to validate.</param>
        /// <param name="schemaFile">Can be null.</param>
        /// <param name="binPath">Path to the framework directory where the default schema for 
        /// this ToolsVersion can be found.</param>
        /// <returns>True if the project was successfully validated against the given schema, false otherwise</returns>
        internal static void VerifyProjectSchema
        (
            string projectFile,
            string schemaFile,
            string binPath
        )
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectFile, nameof(projectFile));
            ErrorUtilities.VerifyThrowArgumentNull(binPath, nameof(binPath));

            if (string.IsNullOrEmpty(schemaFile))
            {
                schemaFile = Path.Combine(binPath, "Microsoft.Build.xsd");
            }

            if (FileSystems.Default.FileExists(schemaFile))
            {
                // Print the schema file we're using, particularly since it can vary 
                // according to the toolset being used
                Console.WriteLine(AssemblyResources.GetString("SchemaFileLocation"), schemaFile);
            }
            else
            {
                // If we've gotten to this point, there is no schema to validate against -- just exit. 
                InitializationException.Throw
                    (
                    ResourceUtilities.FormatResourceStringStripCodeAndKeyword("SchemaNotFoundErrorWithFile", schemaFile),
                    null /* No associated command line switch */
                    );
            }

            ProjectSchemaValidationHandler validationHandler = new ProjectSchemaValidationHandler();

            validationHandler.VerifyProjectSchema(projectFile, schemaFile);
        }

        /// <summary>
        /// Validates a project against the given schema.  A schema file must be provided.
        /// </summary>
        private void VerifyProjectSchema
        (
            string projectFile,
            string schemaFile
        )
        {
            ErrorUtilities.VerifyThrowArgumentNull(schemaFile, nameof(schemaFile));
            ErrorUtilities.VerifyThrowArgumentNull(projectFile, nameof(projectFile));

            // Options for XmlReader object can be set only in constructor. After the object is created, they
            // become read-only. Because of that we need to create
            // XmlSettings structure, fill it in with correct parameters and pass into XmlReader constructor.

            XmlReaderSettings validatorSettings = new XmlReaderSettings();
            validatorSettings.ValidationType = ValidationType.Schema;
            validatorSettings.XmlResolver = null;
            validatorSettings.ValidationEventHandler += this.OnSchemaValidationError;

            XmlTextReader schemaReader = new XmlTextReader(schemaFile);
            schemaReader.DtdProcessing = DtdProcessing.Ignore;

            using (schemaReader)
            {
                try
                {
                    validatorSettings.Schemas.Add(XMakeAttributes.defaultXmlNamespace, schemaReader);

                    // We need full path to the project file to be able handle it as URI in ValidationEventHandler.
                    // Uri class cannot instantiate with relative paths.
                    projectFile = Path.GetFullPath(projectFile);

                    using (StreamReader contentReader = new StreamReader(projectFile))
                    {
                        using (XmlReader validator = XmlReader.Create(contentReader, validatorSettings, projectFile)) // May also throw XmlSchemaException
                        {
                            _syntaxError = false;
                            bool couldRead = true;

                            while (couldRead)
                            {
                                try
                                {
                                    couldRead = validator.Read();
                                }
                                catch (XmlException)
                                {
                                    // We swallow exception here because XmlValidator fires the validation event to report the error
                                    // And we handle the event. Also XmlValidator can continue parsing Xml text after throwing an exception.
                                    // Thus we don't need any special recover here.
                                }
                            }

                            VerifyThrowInitializationExceptionWithResource
                                    (
                                     !_syntaxError,
                                     projectFile,
                                     0 /* line */,
                                     0 /* end line */,
                                     0 /* column */,
                                     0 /* end column */,
                                     "ProjectSchemaErrorHalt"
                                    );
                        }
                    }
                }
                // handle errors in the schema itself
                catch (XmlException e)
                {
                    ThrowInitializationExceptionWithResource
                            (
                             (e.SourceUri.Length == 0) ? String.Empty : new Uri(e.SourceUri).LocalPath,
                             e.LineNumber,
                             0 /* end line */,
                             e.LinePosition,
                             0 /* end column */,
                             "InvalidSchemaFile",
                             schemaFile,
                             e.Message
                            );
                }
                // handle errors in the schema itself
                catch (XmlSchemaException e)
                {
                    ThrowInitializationExceptionWithResource
                            (
                             (e.SourceUri.Length == 0) ? String.Empty : new Uri(e.SourceUri).LocalPath,
                             e.LineNumber,
                             0 /* end line */,
                             e.LinePosition,
                             0 /* end column */,
                             "InvalidSchemaFile",
                             schemaFile,
                             e.Message
                            );
                }
            }
        }

        /// <summary>
        /// Given the parameters passed in, if the condition is false, builds an 
        /// error message and throws an InitializationException with that message. 
        /// </summary>
        private static void VerifyThrowInitializationExceptionWithResource
                (
                 bool condition,
                 string projectFile,
                 int fileLine,
                 int fileEndLine,
                 int fileColumn,
                 int fileEndColumn,
                 string resourceName,
                 params object[] args
                )
        {
            if (!condition)
            {
                ThrowInitializationExceptionWithResource
                        (
                         projectFile,
                         fileLine,
                         fileEndLine,
                         fileColumn,
                         fileEndColumn,
                         resourceName,
                         args
                        );
            }
        }

        /// <summary>
        /// Given the parameters passed in, builds an error message and throws an 
        /// InitializationException with that message. 
        /// </summary>
        private static void ThrowInitializationExceptionWithResource
                (
                 string projectFile,
                 int fileLine,
                 int fileEndLine,
                 int fileColumn,
                 int fileEndColumn,
                 string resourceName,
                 params object[] args
                )
        {
            InitializationException.Throw
                    (
                     BuildStringFromResource
                        (
                         projectFile,
                         fileLine,
                         fileEndLine,
                         fileColumn,
                         fileEndColumn,
                         resourceName,
                         args
                        ),
                     null /* No associated command line switch */
                    );
        }

        /// <summary>
        /// Given a resource string and information about a file, builds up a string
        /// containing the message.
        /// </summary>
        private static string BuildStringFromResource
                (
                 string projectFile,
                 int fileLine,
                 int fileEndLine,
                 int fileColumn,
                 int fileEndColumn,
                 string resourceName,
                 params object[] args
                )
        {
            string errorCode;
            string helpKeyword;
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out errorCode, out helpKeyword, resourceName, args);

            return EventArgsFormatting.FormatEventMessage
                (
                    "error",
                    AssemblyResources.GetString("SubCategoryForSchemaValidationErrors"),
                    message,
                    errorCode,
                    projectFile,
                    fileLine,
                    fileEndLine,
                    fileColumn,
                    fileEndColumn,
                    0 /* thread id */
                );
        }

        #endregion // Methods

        #region Event Handlers

        /// <summary>
        /// Receives any errors that occur while validating the project's schema.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnSchemaValidationError(object sender, ValidationEventArgs args)
        {
            _syntaxError = true;

            // We should handle empty URI specially, because Uri class does not allow to instantiate with empty string.
            string filePath = String.Empty;

            if (args.Exception.SourceUri.Length != 0)
            {
                filePath = (new Uri(args.Exception.SourceUri)).LocalPath;
            }

            Console.WriteLine
                    (
                     BuildStringFromResource
                        (
                         filePath,
                         args.Exception.LineNumber,
                         0 /* end line */,
                         args.Exception.LinePosition,
                         0 /* end column */,
                         "SchemaValidationError",
                         args.Exception.Message
                        )
                    );
        }

        #endregion // Event Handlers
    }
}
#endif