using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.Runtime.DependencyManagement;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class PackageRuntimeLibrary : RuntimeLibrary
    {
        public PackageRuntimeLibrary(
            LibraryRange requestedRange, 
            PackageInfo package, 
            LockFileTargetLibrary lockFileLibrary, 
            IEnumerable<LibraryDependency> dependencies, 
            bool resolved, 
            bool compatible)
            : base(
                  requestedRange,
                  new LibraryIdentity(package.Id, package.Version, isGacOrFrameworkReference: false),
                  LibraryTypes.Package,
                  dependencies,
                  assemblies: Enumerable.Empty<string>(),
                  framework: null)
        {
            Package = package;
            LockFileLibrary = lockFileLibrary;
            Resolved = resolved;
            Compatible = compatible;
        }

        public LockFileTargetLibrary LockFileLibrary { get; set; }
        public PackageInfo Package { get; set; }
    }
}
