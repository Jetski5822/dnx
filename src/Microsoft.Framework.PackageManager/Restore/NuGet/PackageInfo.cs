// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet;

namespace NuGet
{
    public class PackageInfo
    {
        public string Id { get; set; }
        public SemanticVersion Version { get; set; }
        public string ContentUri { get; set; }
    }
}