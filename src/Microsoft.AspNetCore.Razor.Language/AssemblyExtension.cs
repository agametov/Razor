// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.Language
{
    internal class AssemblyExtension : RazorExtension
    {
        public AssemblyExtension(string extensionName, string assemblyName, string assemblyFilePath)
        {
            if (extensionName == null)
            {
                throw new ArgumentNullException(nameof(extensionName));
            }

            if (assemblyName == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            if (assemblyFilePath == null)
            {
                throw new ArgumentNullException(nameof(assemblyFilePath));
            }

            ExtensionName = extensionName;
            AssemblyName = assemblyName;
            AssemblyFilePath = assemblyFilePath;
        }

        public override string ExtensionName { get; }

        public string AssemblyName { get; }

        public string AssemblyFilePath { get; }
    }
}
