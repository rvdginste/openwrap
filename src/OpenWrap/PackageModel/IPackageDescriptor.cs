﻿using System;
using System.Collections.Generic;

namespace OpenWrap.PackageModel
{
    public interface IPackageDescriptor : IEnumerable<IPackageDescriptorEntry>
    {
        bool Anchored { get; set; }
        DateTimeOffset Created { get; }
        ICollection<string> Build { get; }
        ICollection<PackageDependency> Dependencies { get; }
        string Title { get; set; }
        string Namespace { get; }
        string Description { get; }
        string FullName { get; }
        PackageIdentifier Identifier { get; }
        string Name { get; set; }
        ICollection<PackageNameOverride> Overrides { get; }
        bool UseProjectRepository { get; }
        bool UseSymLinks { get; }
        Version Version { get; set; }
        string ReferencedAssemblies { get; }
        ICollection<string> DirectoryStructure { get; }
        bool StorePackages { get; }
        IPackageDescriptor CreateScoped(IEnumerable<IPackageDescriptorEntry> read);
        IEnumerable<IPackageDescriptorEntry> GetPersistableEntries();
    }
}