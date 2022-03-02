// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    public sealed class BuildUri : TaskExtension
    {
        public ITaskItem[] InputUri { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// Gets or sets the scheme name of the URI.
        /// </summary>
        public string UriScheme { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user name associated with the user that accesses the URI.
        /// </summary>
        public string UriUserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password associated with the user that accesses the URI.
        /// </summary>
        public string UriPassword { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Domain Name System (DNS) host name or IP address of a server.
        /// </summary>
        public string UriHost { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the port number of the URI.
        /// </summary>
        public int UriPort { get; set; } = UseDefaultPortForScheme;

        /// <summary>
        /// Gets or sets the path to the resource referenced by the URI.
        /// </summary>
        public string UriPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets any query information included in the URI.
        /// </summary>
        public string UriQuery { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the fragment portion of the URI.
        /// </summary>
        public string UriFragment { get; set; } = string.Empty;

        [Output]
        public ITaskItem[] OutputUri { get; private set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            if (InputUri.Length == 0)
            {
                // For an empty set of input uris, create a single item from the provided parameters.
                OutputUri = new ITaskItem[] { CreateUriTaskItem(new TaskItem()) };
            }
            else
            {
                OutputUri = new ITaskItem[InputUri.Length];
                for (int idx = 0; idx < InputUri.Length; ++idx)
                {
                    OutputUri[idx] = CreateUriTaskItem(InputUri[idx]);
                }
            }
            return true;
        }

        private ITaskItem CreateUriTaskItem(ITaskItem item)
        {
            // Create a UriBuilder.
            // UriBuilder ctor can throw ArgumentNullException and UriFormatException.
            var builder = string.IsNullOrEmpty(item.ItemSpec) ? new UriBuilder() : new UriBuilder(item.ItemSpec);
            // Scheme
            if (!string.IsNullOrEmpty(UriScheme))
            {
                // The Scheme property setter throws an ArgumentException for an invalid scheme.
                builder.Scheme = UriScheme;
                // If a scheme has been provided and a port has not, use the default port for the scheme.
                if (UriPort == UseDefaultPortForScheme)
                {
                    builder.Port = UseDefaultPortForScheme;
                }
            }
            // UserName
            if (!string.IsNullOrEmpty(UriUserName))
            {
                builder.UserName = UriUserName;
            }
            // Password
            if (!string.IsNullOrEmpty(UriPassword))
            {
                builder.Password = UriPassword;
            }
            // Host
            if (!string.IsNullOrEmpty(UriHost))
            {
                builder.Host = UriHost;
            }
            // Port
            // If a scheme was provided and a port was not, then UriPort and builder.Port will both be -1.
            if (UriPort != builder.Port)
            {
                // The Port property setter throws an ArgumentOutOfRangeException for a port number less than -1 or greater than 65,535.
                builder.Port = UriPort;
            }
            // Path
            if (!string.IsNullOrEmpty(UriPath))
            {
                builder.Path = UriPath;
            }
            // Query
            if (!string.IsNullOrEmpty(UriQuery))
            {
                builder.Query = UriQuery;
            }
            // Fragment
            if (!string.IsNullOrEmpty(UriFragment))
            {
                builder.Fragment = UriFragment;
            }

            // Create a TaskItem from the UriBuilder and set custom metadata.
            var uri = new TaskItem(item) { ItemSpec = builder.Uri.AbsoluteUri };
            uri.SetMetadata("UriScheme", builder.Scheme);
            uri.SetMetadata("UriUserName", builder.UserName);
            uri.SetMetadata("UriPassword", builder.Password);
            uri.SetMetadata("UriHost", builder.Host);
            uri.SetMetadata("UriHostNameType", Uri.CheckHostName(builder.Host).ToString());
            uri.SetMetadata("UriPort", builder.Port.ToString());
            uri.SetMetadata("UriPath", builder.Path);
            uri.SetMetadata("UriQuery", builder.Query);
            uri.SetMetadata("UriFragment", builder.Fragment);

            return uri;
        }

        private const int UseDefaultPortForScheme = -1;
    }
}