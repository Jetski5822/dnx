// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Compilation
{
    public class LibraryExporter
    {
        private readonly LibraryManager _manager;
        private readonly CompilationSession _compilationEngine;
        private readonly IProjectGraphProvider _projectGraphProvider;

        public LibraryExporter(LibraryManager manager, CompilationSession compilationEngine, IProjectGraphProvider projectGraphProvider)
        {
            _manager = manager;
            _compilationEngine = compilationEngine;
            _projectGraphProvider = projectGraphProvider;
        }

        public LibraryExport ExportLibraryGraph(
            CompilationTarget target)
        {
            return ExportLibraryGraph(target, l => true);
        }

        public LibraryExport ExportLibraryGraph(
            CompilationTarget target,
            Func<Library, bool> include)
        {
            var library = _manager.GetRuntimeLibrary(target.Name);
            if (library == null)
            {
                return null;
            }
            return ExportLibraryGraph(library, target, include);
        }

        public LibraryExport ExportLibraryGraph(
            LibraryResolution library,
            CompilationTarget target,
            bool dependenciesOnly)
        {
            return ExportLibraryGraph(library, target, libraryInformation =>
            {
                if (dependenciesOnly)
                {
                    return !string.Equals(target.Name, libraryInformation.Name);
                }

                return true;
            });
        }

        public LibraryExport ExportLibraryGraph(
            LibraryResolution projectLibrary,
            CompilationTarget target,
            Func<Library, bool> include)
        {
            var dependencyStopWatch = Stopwatch.StartNew();
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: Resolving references for '{target.Name}' {target.Aspect}");

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);
            var sourceReferences = new Dictionary<string, ISourceReference>(StringComparer.OrdinalIgnoreCase);

            // Walk the dependency tree and resolve the library export for all references to this project
            var queue = new Queue<Node>();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rootNode = new Node
            {
                Library = _manager.GetLibrary(target.Name)
            };

            queue.Enqueue(rootNode);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();

                // Skip it if we've already seen it
                if (!processed.Add(node.Library.Name))
                {
                    continue;
                }

                if (include(node.Library))
                {
                    var libraryExport = ExportLibrary(target
                        .ChangeName(node.Library.Name)
                        .ChangeAspect(null));
                    if (libraryExport != null)
                    {
                        if (node.Parent == rootNode)
                        {
                            // Only export sources from first level dependencies
                            ProcessExport(libraryExport, references, sourceReferences);
                        }
                        else
                        {
                            // Skip source exports from anything else
                            ProcessExport(libraryExport, references, sourceReferences: null);
                        }
                    }
                }

                foreach (var dependency in node.Library.Dependencies)
                {
                    var childNode = new Node
                    {
                        Library = _manager.GetLibrary(dependency),
                        Parent = node
                    };

                    queue.Enqueue(childNode);
                }
            }

            dependencyStopWatch.Stop();
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: Resolved {references.Count} references for '{target.Name}' in {dependencyStopWatch.ElapsedMilliseconds}ms");

            return new LibraryExport(
                references.Values.ToList(),
                sourceReferences.Values.ToList());
        }

        private void ProcessExport(LibraryExport export,
                                          IDictionary<string, IMetadataReference> metadataReferences,
                                          IDictionary<string, ISourceReference> sourceReferences)
        {
            var references = new List<IMetadataReference>(export.MetadataReferences);

            foreach (var reference in references)
            {
                metadataReferences[reference.Name] = reference;
            }

            if (sourceReferences != null)
            {
                foreach (var sourceReference in export.SourceReferences)
                {
                    sourceReferences[sourceReference.Name] = sourceReference;
                }
            }
        }

        public LibraryExport ExportLibrary(CompilationTarget target)
        {
            var library = _manager.GetRuntimeLibrary(target.Name);
            if (library == null)
            {
                return null;
            }

            return ExportLibrary(library, target);
        }

        private LibraryExport ExportLibrary(LibraryResolution library, CompilationTarget target)
        {
            if (string.Equals(LibraryTypes.Package, library.Type, StringComparison.Ordinal))
            {
                return ExportPackage((PackageLibraryResolution)library, target);
            }
            else if (string.Equals(LibraryTypes.Project, library.Type, StringComparison.Ordinal))
            {
                return ExportProject((ProjectLibraryResolution)library, target);
            }
            else
            {
                return ExportAssemblyLibrary(library, target);
            }
        }

        private LibraryExport ExportPackage(LibraryResolution library, CompilationTarget target)
        {
            // Assert in debug mode
            var packageLibrary = (PackageLibraryResolution)library;

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);

            if (!TryPopulateMetadataReferences(packageLibrary, target.TargetFramework, references))
            {
                return null;
            }

            // REVIEW: This requires more design
            var sourceReferences = new List<ISourceReference>();

            foreach (var sharedSource in GetSharedSources(packageLibrary, target.TargetFramework))
            {
                sourceReferences.Add(new SourceFileReference(sharedSource));
            }

            return new LibraryExport(references.Values.ToList(), sourceReferences);
        }

        private LibraryExport ExportProject(LibraryResolution library, CompilationTarget target)
        {
            return ProjectExporter.ExportProject(
                ((ProjectLibraryResolution)library).Project,
                target,
                _compilationEngine,
                _projectGraphProvider);
        }

        private LibraryExport ExportAssemblyLibrary(LibraryResolution library, CompilationTarget target)
        {
            if(string.IsNullOrEmpty(library.Path))
            {
                Logger.TraceError($"[{nameof(LibraryExporter)}] Failed to export: {library.Identity.Name}");
                return null;
            }

            // We assume the path is to an assembly 
            return new LibraryExport(new MetadataFileReference(library.Identity.Name, library.Path));
        }

        private IEnumerable<string> GetSharedSources(PackageLibraryResolution library, FrameworkName targetFramework)
        {
            var directory = Path.Combine(library.Path, "shared");

            return library
                .Package
                .LockFileLibrary
                .Files
                .Where(path => path.StartsWith("shared" + Path.DirectorySeparatorChar))
                .Select(path => Path.Combine(library.Path, path));
        }


        private bool TryPopulateMetadataReferences(PackageLibraryResolution library, FrameworkName targetFramework, IDictionary<string, IMetadataReference> paths)
        {
            foreach (var assemblyPath in library.LockFileLibrary.CompileTimeAssemblies)
            {
                if (NuGetDependencyResolver.IsPlaceholderFile(assemblyPath))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(assemblyPath);
                var path = Path.Combine(library.Path, assemblyPath);
                paths[name] = new MetadataFileReference(name, path);
            }

            return true;
        }

        private class Node
        {
            public Library Library { get; set; }

            public Node Parent { get; set; }
        }
    }
}
