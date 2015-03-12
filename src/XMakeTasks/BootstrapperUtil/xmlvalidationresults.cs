// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Xml;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    /// <summary>
    /// Handles and stores XML validation events.
    /// </summary>
    internal class XmlValidationResults
    {
        private string _filePath;
        private ArrayList _validationErrors;
        private ArrayList _validationWarnings;

        /// <summary>
        /// Constructor which includes the path to the file being validated.
        /// </summary>
        /// <param name="filePath">The file which is being validated.</param>
        public XmlValidationResults(string filePath)
        {
            _filePath = filePath;
            _validationErrors = new ArrayList();
            _validationWarnings = new ArrayList();
        }

        /// <summary>
        /// Gets a string containing the name of the file being validated.
        /// </summary>
        /// <value>The name of the file being validated.</value>
        public string FilePath
        {
            get { return _filePath; }
        }

        /// <summary>
        /// The delegate which will handle validation events.
        /// </summary>
        public void SchemaValidationEventHandler(object sender, System.Xml.Schema.ValidationEventArgs e)
        {
            if (e.Severity == System.Xml.Schema.XmlSeverityType.Error)
            {
                _validationErrors.Add(e.Message);
            }
            else
            {
                _validationWarnings.Add(e.Message);
            }
        }

        /// <summary>
        /// Gets all of the validation errors of the file being validated.
        /// </summary>
        /// <value>An array of type string, containing all of the validation errors.</value>
        /// <remarks>This method uses ArrayList.Copy to copy the errors.</remarks>
        public string[] ValidationErrors
        {
            get
            {
                string[] a = new string[_validationErrors.Count];
                _validationErrors.CopyTo(a);
                return a;
            }
        }

        /// <summary>
        /// Gets a value indicating if there were no validation errors or warnings.
        /// </summary>
        /// <value>true if there were no validation errors or warnings; otherwise false.  The default value is false.</value>
        public bool ValidationPassed
        {
            get { return _validationErrors.Count == 0 && _validationWarnings.Count == 0; }
        }

        /// <summary>
        /// Gets all of the validation warnings of the file being validated.
        /// </summary>
        /// <value>An array of type string, containing all of the validation warnings.</value>
        /// <remarks>This method uses ArrayList.Copy to copy the warnings.</remarks>
        public string[] ValidationWarnings
        {
            get
            {
                string[] a = new string[_validationWarnings.Count];
                _validationWarnings.CopyTo(a);
                return a;
            }
        }
    }
}
