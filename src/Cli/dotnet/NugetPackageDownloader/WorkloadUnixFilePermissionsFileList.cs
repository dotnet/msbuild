// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader.WorkloadUnixFilePermissions
{
    [Serializable]
    [DesignerCategory("code")]
    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "", IsNullable = false)]
    public class FileList
    {
        private FileListFile[] fileField;

        [XmlElement("File")]
        public FileListFile[] File
        {
            get => fileField;
            set => fileField = value;
        }

        public static FileList Deserialize(string pathToXml)
        {
            var serializer = new XmlSerializer(typeof(FileList));

            using var fs = new FileStream(pathToXml, FileMode.Open);
            var reader = XmlReader.Create(fs);
            FileList fileList = (FileList)serializer.Deserialize(reader);
            return fileList;
        }
    }

    [Serializable]
    [DesignerCategory("code")]
    [XmlType(AnonymousType = true)]
    public class FileListFile
    {
        private string pathField;

        private string permissionField;

        [XmlAttribute]
        public string Path
        {
            get => pathField;
            set => pathField = value;
        }

        [XmlAttribute]
        public string Permission
        {
            get => permissionField;
            set => permissionField = value;
        }
    }
}
