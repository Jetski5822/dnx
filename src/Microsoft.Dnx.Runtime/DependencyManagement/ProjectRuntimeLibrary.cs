﻿using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    public class ProjectRuntimeLibrary : RuntimeLibrary
    {
        public ProjectRuntimeLibrary(
            LibraryRange libraryRange, 
            Project project, 
            IEnumerable<LibraryDependency> dependencies, 
            IEnumerable<string> assemblies, 
            FrameworkName framework, 
            bool resolved) :
                base(
                    libraryRange,
                    new LibraryIdentity(project.Name, project.Version, isGacOrFrameworkReference: false),
                    LibraryTypes.Project,
                    dependencies,
                    assemblies,
                    framework)
        {
            Project = project;
            Resolved = resolved;
            Compatible = resolved;
        }

        public Project Project { get; set; }
    }
}
