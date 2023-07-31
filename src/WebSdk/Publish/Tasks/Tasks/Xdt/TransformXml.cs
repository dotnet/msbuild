// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.Publish.Tasks.MsDeploy;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;
using Microsoft.Web.XmlTransform;

namespace Microsoft.NET.Sdk.Publish.Tasks.Xdt
{
    public class TransformXml : Task
    {
        private string _sourceFile = null;
        private string _transformFile = null;
        private string _destinationFile = null;
        private string _sourceRootPath = string.Empty;
        private string _transformRootPath = string.Empty;
        private bool _ignoreError = false;
        private bool stackTrace = false;

        [Required]
        public String Source
        {
            get
            {
                return Utility.GetFilePathResolution(_sourceFile, SourceRootPath);
            }
            set { _sourceFile = value; }
        }

        public string SourceRootPath
        {
            get { return _sourceRootPath; }
            set { _sourceRootPath = value; }
        }

        public bool IgnoreError
        {
            get { return _ignoreError; }
            set { _ignoreError = value; }
        }


        [Required]
        public String Transform
        {
            get
            {
                return Utility.GetFilePathResolution(_transformFile, TransformRootPath);
            }
            set
            {
                _transformFile = value;
            }
        }

        public string TransformRootPath
        {
            get
            {
                if (string.IsNullOrEmpty(_transformRootPath))
                {
                    return this.SourceRootPath;
                }
                else
                {
                    return _transformRootPath;
                }
            }
            set { _transformRootPath = value; }
        }


        [Required]
        public String Destination
        {
            get
            {
                return _destinationFile;
            }
            set
            {
                _destinationFile = value;
            }
        }

        public bool StackTrace
        {
            get
            {
                return stackTrace;
            }
            set
            {
                stackTrace = value;
            }
        }

        public override bool Execute()
        {
            return RunXmlTransform();
        }

        public bool RunXmlTransform(bool isLoggingEnabled = true)
        {
            bool succeeded = true;
            IXmlTransformationLogger logger = null;
            if (isLoggingEnabled && !IgnoreError)
            {
                logger = new TaskTransformationLogger(Log, StackTrace);
            }

            XmlTransformation transformation = null;
            XmlTransformableDocument document = null;

            try
            {
                logger?.StartSection(string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.BUILDTASK_TransformXml_TransformationStart, Source));
                document = OpenSourceFile(Source);

                logger?.LogMessage(string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.BUILDTASK_TransformXml_TransformationApply, Transform));
                transformation = OpenTransformFile(Transform, logger);

                succeeded = transformation.Apply(document);

                if (succeeded)
                {
                    logger?.LogMessage(string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.BUILDTASK_TransformXml_TransformOutput, Destination));

                    SaveTransformedFile(document, Destination);
                }

                if (IgnoreError)
                {
                    return true;
                }
            }
            catch (System.Xml.XmlException ex)
            {
                if (IgnoreError)
                {
                    return true;
                }

                string localPath = Source;
                if (!string.IsNullOrEmpty(ex.SourceUri))
                {
                    Uri sourceUri = new Uri(ex.SourceUri);
                    localPath = sourceUri.LocalPath;
                }

                logger?.LogError(localPath, ex.LineNumber, ex.LinePosition, ex.Message);
                succeeded = false;
            }
            catch (Exception ex)
            {
                if (IgnoreError)
                {
                    return true;
                }

                logger?.LogErrorFromException(ex);
                succeeded = false;
            }
            finally
            {
                logger?.EndSection(string.Format(System.Globalization.CultureInfo.CurrentCulture, succeeded ?
                    Resources.BUILDTASK_TransformXml_TransformationSucceeded :
                    Resources.BUILDTASK_TransformXml_TransformationFailed));
                if (transformation != null)
                {
                    transformation.Dispose();
                }
                if (document != null)
                {
                    document.Dispose();
                }
            }

            return succeeded;
        }

        private void SaveTransformedFile(XmlTransformableDocument document, string destinationFile)
        {
            try
            {
                document.Save(destinationFile);
            }
            catch (System.Xml.XmlException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception(
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.BUILDTASK_TransformXml_DestinationWriteFailed, ex.Message),
                    ex);
            }
        }

        private XmlTransformableDocument OpenSourceFile(string sourceFile)
        {
            try
            {
                XmlTransformableDocument document = new XmlTransformableDocument();

                document.PreserveWhitespace = true;
                document.Load(sourceFile);

                return document;
            }
            catch (System.Xml.XmlException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception(
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.BUILDTASK_TransformXml_SourceLoadFailed, ex.Message),
                    ex);
            }
        }

        private XmlTransformation OpenTransformFile(string transformFile, IXmlTransformationLogger logger)
        {
            try
            {
                return new XmlTransformation(transformFile, logger);
            }
            catch (System.Xml.XmlException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception(
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.BUILDTASK_TransformXml_TransformLoadFailed, ex.Message),
                    ex);
            }
        }
    }
}
