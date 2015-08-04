// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Compilation
{
    /// <summary>
    /// Provides access to the complete graph of dependencies for the application.
    /// </summary>
    public interface ILibraryExporter
    {
        LibraryExport ExportLibrary(string name);

        LibraryExport ExportLibrary(string name, string aspect);

        LibraryExport ExportLibraryGraph(string name);

        LibraryExport ExportLibraryGraph(string name, string aspect);

        // TODO(anurse): Clean this up before review!
        LibraryExport ExportLibraryGraph(string name, bool includeProjects);
    }
}
