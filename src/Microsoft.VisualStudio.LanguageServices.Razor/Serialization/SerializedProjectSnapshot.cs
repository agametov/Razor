// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Serialization
{
    internal class SerializedProjectSnapshot : ProjectSnapshot
    {
        private Project _workspaceProject;

        public SerializedProjectSnapshot(string filePath, RazorConfiguration configuration)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            FilePath = filePath;
            Configuration = configuration;
        }

        public override RazorConfiguration Configuration { get; }

        public override string FilePath { get; }

        public override bool IsInitialized => WorkspaceProject != null;

        public override bool IsUnloaded => false;

        public override VersionStamp Version => VersionStamp.Default;

        public override Project WorkspaceProject => _workspaceProject;

        public void InitializeWorkspaceProject(Project workspaceProject)
        {
            if (workspaceProject == null)
            {
                throw new ArgumentNullException(nameof(workspaceProject));
            }

            _workspaceProject = workspaceProject;
        }
    }
}
