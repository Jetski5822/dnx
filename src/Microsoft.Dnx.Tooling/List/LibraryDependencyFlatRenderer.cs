// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Dnx.Tooling.Algorithms;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling.List
{
    public class LibraryDependencyFlatRenderer
    {
        private readonly bool _showDetails;
        private readonly string _filterPattern;
        private readonly HashSet<string> _listedProjects;

        public LibraryDependencyFlatRenderer(bool showDetails, string filterPattern, IEnumerable<string> listedProjects)
        {
            _showDetails = showDetails;
            _filterPattern = filterPattern;
            _listedProjects = new HashSet<string>(listedProjects);
        }

        public IEnumerable<string> GetRenderContent(IGraphNode<LibraryResolution> root)
        {
            var dict = FindImmediateDependent(root);
            var libraries = dict.Keys.OrderBy(description => description.Identity.Name);
            var results = new List<string>();

            var gacOrFrameworkReferences = libraries.Where(library => library.Identity.IsGacOrFrameworkReference);

            if (gacOrFrameworkReferences.Any())
            {
                results.Add("Framework references:");
                RenderLibraries(gacOrFrameworkReferences, dict, results);
                results.Add(string.Empty);
            }

            var otherReferences = libraries.Where(library => !library.Identity.IsGacOrFrameworkReference);
            var referencesGroups = otherReferences.GroupBy(reference => reference.Type);
            foreach (var group in referencesGroups)
            {
                results.Add(string.Format("{0} references:", group.Key));
                RenderLibraries(group, dict, results);
                results.Add(string.Empty);
            }

            return results;
        }

        private IDictionary<LibraryResolution, ISet<LibraryResolution>> FindImmediateDependent(IGraphNode<LibraryResolution> root)
        {
            var result = new Dictionary<LibraryResolution, ISet<LibraryResolution>>();

            IGraphNodeExtensions.DepthFirstPreOrderWalk<Runtime.LibraryResolution>(
root,                visitNode: (Func<IGraphNode<Runtime.LibraryResolution>, IEnumerable<IGraphNode<Runtime.LibraryResolution>>, bool>)((IGraphNode<Runtime.LibraryResolution> node, IEnumerable<IGraphNode<Runtime.LibraryResolution>> ancestors) =>
                {
                    ISet<Runtime.LibraryResolution> slot;
                    if (!result.TryGetValue((LibraryResolution)node.Item, out slot))
                    {
                        slot = new HashSet<Runtime.LibraryResolution>();
                        result.Add((LibraryResolution)node.Item, (ISet<Runtime.LibraryResolution>)slot);
                    }

                    // first item in the path is the immediate parent
                    if (ancestors.Any())
                    {
                        slot.Add((Runtime.LibraryResolution)ancestors.First().Item);
                    }

                    return true;
                }));

            // removing the root package
            result.Remove(root.Item);

            return result;
        }

        private void RenderLibraries(IEnumerable<LibraryResolution> descriptions,
                                     IDictionary<LibraryResolution, ISet<LibraryResolution>> dependenciesMap,
                                     IList<string> results)
        {
            if (!string.IsNullOrEmpty(_filterPattern))
            {
                var regex = new Regex("^" + Regex.Escape(_filterPattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase);
                descriptions = descriptions.Where(library => regex.IsMatch(library.Identity.Name));
            }

            foreach (var description in descriptions)
            {
                var libDisplay = (_listedProjects.Contains(description.Identity.Name) ? "* " : "  ") + description.Identity.ToString();

                if (description.Resolved)
                {
                    results.Add(libDisplay);
                }
                else
                {
                    results.Add(string.Format("{0} - Unresolved", libDisplay).Red().Bold());
                }

                if (_showDetails)
                {
                    var dependenciesInGroup = dependenciesMap[description].GroupBy(dep => dep.Type);
                    foreach (var group in dependenciesInGroup)
                    {
                        results.Add(string.Format("    by {0}: {1}", group.Key, string.Join(", ", group.Select(desc => desc.Identity.ToString()).OrderBy(name => name))));
                    }
                    results.Add(string.Empty);
                }
            }
        }
    }
}