// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AspNetCore.Razor.Tools
{
    internal static class LoaderTestResources
    {
        static LoaderTestResources()
        {
            Gamma = CreateAssemblyBlob("Gamma", Array.Empty<AssemblyBlob>(), @"
using System.Text;
using Delta;

namespace Gamma
{
    public class G
    {
        public void Write(StringBuilder sb, string s)
        {
            D d = new D();

            d.Write(sb, ""Gamma: "" + s);
        }
    }
}
");

            Alpha = CreateAssemblyBlob("Alpha", new[] { Gamma }, @"
using System.Text;
using Gamma;

namespace Alpha
{
    public class A
    {
        public void Write(StringBuilder sb, string s)
        {
            G g = new G();

            g.Write(sb, ""Alpha: "" + s);
        }
    }
}
");

            Beta = CreateAssemblyBlob("Beta", new[] { Gamma }, @"
using System.Text;
using Gamma;

namespace Beta
{
    public class B
    {
        public void Write(StringBuilder sb, string s)
        {
            G g = new G();

            g.Write(sb, ""Beta: "" + s);
        }
    }
}
");

            Delta = CreateAssemblyBlob("Delta", Array.Empty<AssemblyBlob>(), @"
using System.Text;

namespace Delta
{
    public class D
    {
        public void Write(StringBuilder sb, string s)
        {
            sb.AppendLine(""Delta: "" + s);
        }
    }
}
");
        }

        public static AssemblyBlob Alpha { get; }

        public static AssemblyBlob Beta { get; }

        public static AssemblyBlob Delta { get; }

        public static AssemblyBlob Gamma { get; }

        private static AssemblyBlob CreateAssemblyBlob(string assemblyName, AssemblyBlob[] references, string text)
        {
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { CSharpSyntaxTree.ParseText(text) },
                references.Select(r => r.ToMetadataReference()));

            using (var assemblyStream = new MemoryStream())
            using (var symbolStream = new MemoryStream())
            {
                compilation.Emit(assemblyStream, symbolStream);
                return new AssemblyBlob(assemblyName, assemblyStream.GetBuffer(), symbolStream.GetBuffer());
            }
        }

        public class AssemblyBlob
        {
            public AssemblyBlob(string assemblyName, byte[] assemblyBytes, byte[] symbolBytes)
            {
                AssemblyName = assemblyName;
                AssemblyBytes = assemblyBytes;
                SymbolBytes = symbolBytes;
            }

            public string AssemblyName { get; }

            public byte[] AssemblyBytes { get; }

            public byte[] SymbolBytes { get; }

            public MetadataReference ToMetadataReference()
            {
                return MetadataReference.CreateFromImage(AssemblyBytes);
            }
        }
    }
}
