// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    /// <summary>
    /// This class contains a collection of ProductBuilder objects. Used for the BootstrapperBuilder's Build method.
    /// </summary>
    [ComVisible(true), Guid("D25C0741-99CA-49f7-9460-95E5F25EEF43"), ClassInterface(ClassInterfaceType.None)]
    public class ProductBuilderCollection : IProductBuilderCollection, IEnumerable
    {
        private readonly List<ProductBuilder> _list = new List<ProductBuilder>();

        internal ProductBuilderCollection()
        {
        }

        /// <summary>
        /// Adds a ProductBuilder to the ProductBuilderCollection
        /// </summary>
        /// <param name="builder">The ProductBuilder to add to this collection</param>
        public void Add(ProductBuilder builder)
        {
            _list.Add(builder);
        }

        /// <summary>
        /// Returns an enumerator that can iterate through the ProductBuilderCollection
        /// </summary>
        /// <returns>An enumerator that can iterate through the ProductBuilderCollection</returns>
        public IEnumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        internal int Count => _list.Count;

        internal ProductBuilder Item(int index)
        {
            return _list[index];
        }

        internal void Insert(int index, ProductBuilder builder)
        {
            _list.Insert(index, builder);
        }
    }
}
