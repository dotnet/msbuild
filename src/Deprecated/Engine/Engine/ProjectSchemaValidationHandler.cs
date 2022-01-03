// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Xml.Schema;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is used for validating projects against a designated schema.
    /// </summary>
    /// <owner>JomoF</owner>
    internal sealed class ProjectSchemaValidationHandler
    {
        // The parent Engine object for this project.
        private EngineLoggingServices engineLoggingServices ;

        // the location of the MSBuild binaries
        private string binPath;

        // Set to true if there was a syntax error in the project file.
        private bool syntaxError;

        // Event context information of where the event is raised
        private BuildEventContext buildEventContext;

        #region Constructors

        /// <summary>
        /// Private constructor because real constructor needs a file name.
        /// </summary>
        /// <owner>JomoF</owner>
        private ProjectSchemaValidationHandler()
        {
            // do nothing
        }

        /// <summary>
        /// This constructor initializes all required data.
        /// </summary>
        /// <owner>JomoF</owner>
        /// <param name="loggingServices"></param>
        /// <param name="binPath"></param>
        internal ProjectSchemaValidationHandler(BuildEventContext buildEventContext, EngineLoggingServices loggingServices, string binPath)
        {
            this.engineLoggingServices = loggingServices;
            this.binPath = binPath;
            this.buildEventContext = buildEventContext;
        }
        #endregion

        #region Methods

        /// <summary>
        /// Validates a project file against the given schema.
        /// </summary>
        /// <owner>JomoF</owner>
        /// <param name="projectFile"></param>
        /// <param name="schemaFile">Can be null.</param>
        internal void VerifyProjectFileSchema
        (
            string projectFile,
            string schemaFile
        )
        {
            using (StreamReader contentReader = new StreamReader(projectFile))
            {
                VerifyProjectSchema(contentReader, schemaFile, projectFile);
            }
        }

        /// <summary>
        /// Validates a project in an XML string against the given schema.
        /// </summary>
        /// <owner>JomoF</owner>
        /// <param name="projectXml"></param>
        /// <param name="schemaFile">Can be null.</param>
        internal void VerifyProjectSchema
        (
            string projectXml,
            string schemaFile
        )
        {
            using (StringReader contentReader = new StringReader(projectXml))
            {
                VerifyProjectSchema(contentReader, schemaFile, String.Empty /* no project file for in-memory XML */);
            }
        }

        /// <summary>
        /// Validates a project against the given schema -- if no schema is provided, uses the default schema.
        /// </summary>
        /// <owner>JomoF</owner>
        /// <param name="contentReader"></param>
        /// <param name="schemaFile">Can be null.</param>
        /// <param name="projectFile"></param>
        private void VerifyProjectSchema
        (
            TextReader contentReader,
            string schemaFile,
            string projectFile
        )
        {
            // Options for XmlReader object can be set only in constructor. After the object is created, they
            // become read-only. Because of that we need to create
            // XmlSettings structure, fill it in with correct parameters and pass into XmlReader constructor.

            XmlReaderSettings validatorSettings = new XmlReaderSettings();
            validatorSettings.ValidationType = ValidationType.Schema;
            validatorSettings.XmlResolver = null;
            validatorSettings.ValidationEventHandler += this.OnSchemaValidationError;
            
            if (string.IsNullOrEmpty(schemaFile))
            {
                schemaFile = Path.Combine(binPath, "Microsoft.Build.xsd");
            }

            // Log the schema file we're using, particularly since it can vary 
            // according to  the toolset being used
            engineLoggingServices.LogComment(buildEventContext, "SchemaFileLocation", schemaFile);

            XmlTextReader schemaReader = new XmlTextReader(schemaFile);
            schemaReader.DtdProcessing = DtdProcessing.Ignore;
            using (schemaReader)
            {
                try
                {
                    validatorSettings.Schemas.Add(XMakeAttributes.defaultXmlNamespace, schemaReader);

                    // We need full path to the project file to be able handle it as URI in ValidationEventHandler.
                    // Uri class cannot instantiate with relative paths.
                    if (projectFile.Length != 0)
                    {
                        projectFile = Path.GetFullPath(projectFile);
                    }

                    using (XmlReader validator = XmlReader.Create(contentReader, validatorSettings, projectFile)) // May also throw XmlSchemaException
                    {
                        this.syntaxError = false;
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

                        ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(!this.syntaxError, "SubCategoryForSchemaValidationErrors",
                            new BuildEventFileInfo(projectFile), "ProjectSchemaErrorHalt");
                    }
                }
                // handle errors in the schema itself
                catch (XmlException e)
                {
                    ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, "SubCategoryForSchemaValidationErrors", new BuildEventFileInfo(e),
                        "InvalidSchemaFile", schemaFile, e.Message);
                }
                // handle errors in the schema itself
                catch (XmlSchemaException e)
                {
                    ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, "SubCategoryForSchemaValidationErrors", new BuildEventFileInfo(e),
                        "InvalidSchemaFile", schemaFile, e.Message);
                }
            }
        }

        /// <summary>
        /// Receives any errors that occur while validating the project's schema.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnSchemaValidationError(object sender, ValidationEventArgs args)
        {
            this.syntaxError = true;

            // We should handle empty URI specially, because Uri class does not allow to instantiate with empty string.
            string filePath = String.Empty;

            if (args.Exception.SourceUri.Length != 0)
            {
                filePath = (new Uri(args.Exception.SourceUri)).LocalPath;
            }

            engineLoggingServices.LogError(buildEventContext, "SubCategoryForSchemaValidationErrors",
                new BuildEventFileInfo(filePath, args.Exception.LineNumber, args.Exception.LinePosition),
                "SchemaValidationError", args.Exception.Message);
        }

        #endregion
    }
}
