// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AttachmentProcessorDataCollector
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    internal class ExtensionInfo
    {
        public const string ExtensionType = "DataCollector";
        public const string ExtensionIdentifier = "my://sample/datacollector";
    }

    [DataCollectorFriendlyName("SampleDataCollector")]
    [DataCollectorTypeUri(ExtensionInfo.ExtensionIdentifier)]
    [DataCollectorAttachmentProcessor(typeof(SampleDataCollectorAttachmentProcessor))]
    public class SampleDataCollectorV2 : SampleDataCollectorV1 { }

    [DataCollectorFriendlyName("SampleDataCollector")]
    [DataCollectorTypeUri(ExtensionInfo.ExtensionIdentifier)]
    public class SampleDataCollectorV1 : DataCollector
    {
        private DataCollectionSink _dataCollectionSink;
        private DataCollectionEnvironmentContext _context;
        private readonly string _tempDirectoryPath = Path.GetTempPath();

        public override void Initialize(
            XmlElement configurationElement,
            DataCollectionEvents events,
            DataCollectionSink dataSink,
            DataCollectionLogger logger,
            DataCollectionEnvironmentContext environmentContext)
        {
            events.SessionEnd += SessionEnded_Handler;
            _dataCollectionSink = dataSink;
            _context = environmentContext;
        }

        private void SessionEnded_Handler(object sender, SessionEndEventArgs e)
        {
            string tmpAttachment = Path.Combine(_tempDirectoryPath, Guid.NewGuid().ToString("N"), "DataCollectorAttachmentProcessor_1.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(tmpAttachment));
            File.WriteAllText(tmpAttachment, $"SessionEnded_Handler_{Guid.NewGuid():N}");
            _dataCollectionSink.SendFileAsync(_context.SessionDataCollectionContext, tmpAttachment, true);
        }
    }

    public class SampleDataCollectorAttachmentProcessor : IDataCollectorAttachmentProcessor
    {
        public bool SupportsIncrementalProcessing => true;

        public IEnumerable<Uri> GetExtensionUris()
            => new List<Uri>() { new Uri(ExtensionInfo.ExtensionIdentifier) };

        public Task<ICollection<AttachmentSet>> ProcessAttachmentSetsAsync(XmlElement configurationElement, ICollection<AttachmentSet> attachments, IProgress<int> progressReporter, IMessageLogger logger, CancellationToken cancellationToken)
        {
            string finalFileName = configurationElement.FirstChild.InnerText;
            StringBuilder stringBuilder = new StringBuilder();
            string finalFolder = null;
            foreach (var attachmentSet in attachments)
            {
                foreach (var attachment in attachmentSet.Attachments.OrderBy(f => f.Uri.AbsolutePath))
                {
                    if (finalFolder is null)
                    {
                        finalFolder = Path.GetDirectoryName(attachment.Uri.AbsolutePath);
                    }

                    stringBuilder.AppendLine(File.ReadAllText(attachment.Uri.AbsolutePath).Trim());
                }
            }

            File.WriteAllText(Path.Combine(finalFolder, finalFileName), stringBuilder.ToString());

            List<AttachmentSet> mergedAttachment = new List<AttachmentSet>();
            var mergedAttachmentSet = new AttachmentSet(new Uri("my://sample/datacollector"), "SampleDataCollector");
            mergedAttachmentSet.Attachments.Add(UriDataAttachment.CreateFrom(Path.Combine(finalFolder, finalFileName), string.Empty));
            mergedAttachment.Add(mergedAttachmentSet);

            return Task.FromResult((ICollection<AttachmentSet>)new Collection<AttachmentSet>(mergedAttachment));
        }
    }
}
