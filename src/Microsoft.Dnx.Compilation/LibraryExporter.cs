// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using NuGet;

namespace Microsoft.Dnx.Compilation
{
    public class LibraryExporter
    {
        private readonly Dictionary<string, Func<RuntimeLibrary, CompilationTarget, LibraryExport>> _exporters;
        private readonly LibraryManager _manager;
        private readonly CompilationSession _compilationEngine;
        private readonly IProjectGraphProvider _projectGraphProvider;

        public LibraryExporter(LibraryManager manager, CompilationSession compilationEngine, IProjectGraphProvider projectGraphProvider)
        {
            _manager = manager;
            _compilationEngine = compilationEngine;
            _projectGraphProvider = projectGraphProvider;
            _exporters = new Dictionary<string, Func<RuntimeLibrary, CompilationTarget, LibraryExport>>()
            {
                { LibraryTypes.GlobalAssemblyCache, ExportAssemblyLibrary },
                { LibraryTypes.ReferenceAssembly, ExportAssemblyLibrary },
                { LibraryTypes.Project, ExportProject },
                { LibraryTypes.Package, ExportPackage }
            };
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
            RuntimeLibrary library,
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
            RuntimeLibrary projectLibrary,
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

        private LibraryExport ExportLibrary(RuntimeLibrary library, CompilationTarget target)
        {
            Func<RuntimeLibrary, CompilationTarget, LibraryExport> exporter;
            if (!_exporters.TryGetValue(library.Type, out exporter))
            {
                return null;
            }
            return exporter(library, target);
        }

        private LibraryExport ExportPackage(RuntimeLibrary library, CompilationTarget target)
        {
            // Assert in debug mode
            Debug.Assert(library.LockFileLibrary != null, "A package-typed library should have an associated Lock File entry!");
            Debug.Assert(library.Package != null, "A package-typed library should have an associated Package entry!");

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);

            if (!TryPopulateMetadataReferences(library, target.TargetFramework, references))
            {
                return null;
            }

            // REVIEW: This requires more design
            var sourceReferences = new List<ISourceReference>();

            foreach (var sharedSource in GetSharedSources(library, target.TargetFramework))
            {
                sourceReferences.Add(new SourceFileReference(sharedSource));
            }

            return new LibraryExport(references.Values.ToList(), sourceReferences);
        }

        private LibraryExport ExportProject(RuntimeLibrary library, CompilationTarget target)
        {
            // Assert in debug mode
            Debug.Assert(library.Project != null, "A project-typed library should have an associated Project!");

            // Throw in release mode
            if (library.Project == null)
            {
                throw new InvalidOperationException("Project-typed library does not have an associated Project!");
            }
            return ProjectExporter.ExportProject(
                library.Project,
                target,
                _compilationEngine,
                _projectGraphProvider);
        }

        private LibraryExport ExportAssemblyLibrary(RuntimeLibrary library, CompilationTarget target)
        {
            // We assume the path is to an assembly 
            Debug.Assert(library.Path != null, "Expected a GAC or Reference assembly to have a Path!");
            return new LibraryExport(new MetadataFileReference(library.Identity.Name, library.Path));
        }

        private IEnumerable<string> GetSharedSources(RuntimeLibrary library, FrameworkName targetFramework)
        {
            var directory = Path.Combine(library.Path, "shared");

            return library.Package.LockFileLibrary.Files.Where(path => path.StartsWith("shared" + Path.DirectorySeparatorChar))
                                                            .Select(path => Path.Combine(library.Path, path));
        }


        private bool TryPopulateMetadataReferences(RuntimeLibrary library, FrameworkName targetFramework, IDictionary<string, IMetadataReference> paths)
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
