// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.DataAnnotations;

class Program
{
    static void Main(string[] args)
    {
        var b = new VerifyCodeViewModel
        {
            Provider = "Required Test Provider"
        };
        Console.WriteLine(b.Provider);
    }

    public class VerifyCodeViewModel
    {
        [Required]
        public string Provider { get; set; }
    }
}
