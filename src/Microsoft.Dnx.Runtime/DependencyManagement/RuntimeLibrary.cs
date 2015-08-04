// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    public class RuntimeLibrary
    {
        public RuntimeLibrary(LibraryRange requestedRange, LibraryIdentity identity, string type)
            : this(requestedRange, identity, type, Enumerable.Empty<LibraryDependency>(), assemblies: Enumerable.Empty<string>(), framework: null)
        {
        }

        public RuntimeLibrary(LibraryRange requestedRange, LibraryIdentity identity, string type, IEnumerable<string> assemblies)
            : this(requestedRange, identity, type, Enumerable.Empty<LibraryDependency>(), assemblies, framework: null)
        {
        }

        public RuntimeLibrary(LibraryRange requestedRange, LibraryIdentity identity, string type, IEnumerable<LibraryDependency> dependencies)
            : this(requestedRange, identity, type, dependencies, assemblies: Enumerable.Empty<string>(), framework: null)
        {
        }

        public RuntimeLibrary(
            LibraryRange requestedRange,
            LibraryIdentity identity,
            Project project,
            IEnumerable<LibraryDependency> dependencies,
            IEnumerable<string> assemblies,
            FrameworkName framework)
            : this(requestedRange, identity, LibraryTypes.Project, dependencies, assemblies, framework)
        {
            Path = project.ProjectFilePath;
        }

        public RuntimeLibrary(
            LibraryRange requestedRange,
            LibraryIdentity identity,
            string type, 
            IEnumerable<LibraryDependency> dependencies, 
            IEnumerable<string> assemblies,
            FrameworkName framework)
        {
            System.Diagnostics.Debug.Assert(type != LibraryTypes.Project, "Don't use this constructor to create project-typed libraries!");

            RequestedRange = requestedRange;
            Identity = identity;
            Type = type;
            Dependencies = dependencies ?? Enumerable.Empty<LibraryDependency>();
            Assemblies = assemblies ?? Enumerable.Empty<string>();
            Framework = framework;
        }

        public LibraryRange RequestedRange { get; }
        public LibraryIdentity Identity { get; }

        public string Type { get; }
        public FrameworkName Framework { get; }

        public string Path { get; set; }
        public bool Resolved { get; set; } = true;
        public bool Compatible { get; set; } = true;
        public IEnumerable<string> Assemblies { get; set; }
        public IEnumerable<LibraryDependency> Dependencies { get; set; }
        public Project Project { get; set; }

        internal Library ToLibrary()
        {
            return new Library(
                Identity.Name,
                Identity.Version?.GetNormalizedVersionString(),
                Path,
                Type,
                Dependencies.Select(d => d.Name),
                Assemblies.Select(a => new AssemblyName(a)));
        }
    }
}
