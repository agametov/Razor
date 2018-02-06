// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Tools
{
    internal class ExtensionAssemblyLoader
    {
        private readonly AssemblyLoadContext _loadContext;
        
        private readonly object _lock = new object();
        private readonly Dictionary<string, (Assembly assembly, AssemblyIdentity identity)> _loadedByPath;
        private readonly Dictionary<AssemblyIdentity, Assembly> _loadedByIdentity;
        private readonly Dictionary<string, AssemblyIdentity> _identityCache;
        private readonly Dictionary<string, List<string>> _wellKnownAssemblies;

        private ShadowCopyManager _shadowCopyManager;

        public ExtensionAssemblyLoader()
        {
            _loadedByPath = new Dictionary<string, (Assembly assembly, AssemblyIdentity identity)>(StringComparer.OrdinalIgnoreCase);
            _loadedByIdentity = new Dictionary<AssemblyIdentity, Assembly>();
            _identityCache = new Dictionary<string, AssemblyIdentity>(StringComparer.OrdinalIgnoreCase);
            _wellKnownAssemblies = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            _loadContext = AssemblyLoadContext.GetLoadContext(typeof(ExtensionAssemblyLoader).Assembly);
            _loadContext.Resolving += (context, name) =>
            {
                Debug.Assert(ReferenceEquals(context, _loadContext));
                return Load(name.FullName);
            };
        }

        public void AddAssemblyLocation(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (!Path.IsPathRooted(filePath))
            {
                throw new ArgumentException(nameof(filePath));
            }

            var assemblyName = Path.GetFileNameWithoutExtension(filePath);
            lock (_lock)
            {
                if (!_wellKnownAssemblies.TryGetValue(assemblyName, out var paths))
                {
                    paths = new List<string>();
                    _wellKnownAssemblies.Add(assemblyName, paths);
                }

                if (!paths.Contains(filePath))
                {
                    paths.Add(filePath);
                }
            }
        }

        public Assembly Load(string assemblyName)
        {
            if (!AssemblyIdentity.TryParseDisplayName(assemblyName, out var identity))
            {
                return null;
            }
            
            lock (_lock)
            {
                // First, check if this loader already loaded the requested assembly:
                if (_loadedByIdentity.TryGetValue(identity, out var assembly))
                {
                    return assembly;
                }

                // Second, check if an assembly file of the same simple name was registered with the loader:
                if (_wellKnownAssemblies.TryGetValue(identity.Name, out var paths))
                {
                    // Multiple assemblies of the same simple name but different identities might have been registered.
                    // Load the one that matches the requested identity (if any).
                    foreach (var path in paths)
                    {
                        var candidateIdentity = GetIdentity(path);

                        if (identity.Equals(candidateIdentity))
                        {
                            return LoadFromPathUnsafe(path, candidateIdentity);
                        }
                    }
                }

                // We only support loading by name from 'well-known' paths. If you need to load something by
                // name and you get here, then that means we don't know where to look.
                return null;
            }
        }

        public Assembly LoadFromPath(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (!Path.IsPathRooted(filePath))
            {
                throw new ArgumentException(nameof(filePath));
            }

            lock (_lock)
            {
                return LoadFromPathUnsafe(filePath, identity: null);
            }
        }

        private Assembly LoadFromPathUnsafe(string filePath, AssemblyIdentity identity)
        {
            // If we've already loaded the assembly by path there should be nothing else to do,
            // all of our data is up to date.
            if (_loadedByPath.TryGetValue(filePath, out var entry))
            {
                return entry.assembly;
            }

            // If we've already loaded the assembly by identity, then we might has some updating
            // to do.
            identity = identity ?? GetIdentity(filePath);
            if (identity != null && _loadedByIdentity.TryGetValue(identity, out var assembly))
            {
                // An assembly file might be replaced by another file with a different identity.
                // Last one wins.
                _loadedByPath[filePath] = (assembly, identity);
                return assembly;
            }

            // Ok we don't have this cached. Let's actually try to load the assembly.
            assembly = LoadFromPathUnsafeCore(filePath);
            identity = identity ?? AssemblyIdentity.FromAssemblyDefinition(assembly);

            // It's possible an assembly was loaded by two different paths. Just use the original then.
            if (_loadedByIdentity.TryGetValue(identity, out var duplicate))
            {
                assembly = duplicate;
            }
            else
            {
                _loadedByIdentity.Add(identity, assembly);
            }

            _loadedByPath[filePath] = (assembly, identity);
            return assembly;
        }

        private AssemblyIdentity GetIdentity(string filePath)
        {
            if (!_identityCache.TryGetValue(filePath, out var identity))
            {
                identity = ReadAssemblyIdentity(filePath);
                _identityCache.Add(filePath, identity);
            }

            return identity;
        }

        protected Assembly LoadFromPathUnsafeCore(string filePath)
        {
            if (_shadowCopyManager == null)
            {
                _shadowCopyManager = new ShadowCopyManager();
            }

            var copiedFilePath = _shadowCopyManager.AddAssembly(filePath);
            return _loadContext.LoadFromAssemblyPath(copiedFilePath);
        }

        private static AssemblyIdentity ReadAssemblyIdentity(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new PEReader(stream))
                {
                    var metadataReader = reader.GetMetadataReader();
                    var assemblyDefinition = metadataReader.GetAssemblyDefinition();

                    var name = metadataReader.GetString(assemblyDefinition.Name);
                    var version = assemblyDefinition.Version;
                    
                    var cultureName = assemblyDefinition.Culture.IsNil ? null : metadataReader.GetString(assemblyDefinition.Culture);
                    
                    var keyBytes = assemblyDefinition.PublicKey.IsNil ?
                        default :
                        metadataReader.GetBlobBytes(assemblyDefinition.PublicKey);
                    var key = keyBytes == null ? default : ImmutableArray.CreateRange(keyBytes);

                    var hasPublicKey = (assemblyDefinition.Flags & AssemblyFlags.PublicKey) != 0;

                    return new AssemblyIdentity(name, version, cultureName, key, hasPublicKey);
                }
            }
            catch
            {
            }

            return null;
        }
    }
}