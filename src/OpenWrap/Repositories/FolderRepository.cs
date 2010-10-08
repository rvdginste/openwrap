using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenWrap.Dependencies;
using OpenFileSystem.IO;

namespace OpenWrap.Repositories
{
    /// <summary>
    /// Provides a repository that can read packages from a directory using the default structure.
    /// </summary>
    public class FolderRepository : IPackageRepository, ISupportCleaning, ISupportPublishing
    {
        readonly bool _anchorsEnabled;
        IDirectory _rootCacheDirectory;

        public FolderRepository(IDirectory packageBasePath, bool anchorsEnabled)
        {
            if (packageBasePath == null) throw new ArgumentNullException("packageBasePath");

            BasePath = packageBasePath;
            _anchorsEnabled = anchorsEnabled;

            _rootCacheDirectory = BasePath.GetOrCreateDirectory("_cache");
            Refresh();

        }

        public void Refresh()
        {
            Packages = (from wrapFile in BasePath.Files("*.wrap")
                        let packageFullName = wrapFile.NameWithoutExtension
                        let packageVersion = PackageNameUtility.GetVersion(packageFullName)
                        where packageVersion != null
                        let cacheDirectory = _rootCacheDirectory.GetDirectory(packageFullName)
                        select cacheDirectory.Exists
                                       ? (IPackageInfo)new UncompressedPackage(this, wrapFile, cacheDirectory, ExportBuilders.All, _anchorsEnabled)
                                       : (IPackageInfo)new CachedZipPackage(this, wrapFile, cacheDirectory, ExportBuilders.All, _anchorsEnabled)).ToList();
        }

        public IDirectory BasePath { get; set; }

        public ILookup<string, IPackageInfo> PackagesByName
        {
            get { return Packages.ToLookup(x => x.Name); }
        }

        protected List<IPackageInfo> Packages { get; set; }

        public IPackageInfo Find(PackageDependency dependency)
        {
            return PackagesByName.Find(dependency);
        }

        public IPackageInfo Publish(string packageFileName, Stream packageStream)
        {
            packageFileName = PackageNameUtility.NormalizeFileName(packageFileName);

            var wrapFile = BasePath.GetFile(packageFileName);
            if (wrapFile.Exists)
                return null;

            using (var file = wrapFile.OpenWrite())
                packageStream.CopyTo(file);

            var newPackage = new CachedZipPackage(this, wrapFile, _rootCacheDirectory.GetDirectory(wrapFile.NameWithoutExtension), ExportBuilders.All, _anchorsEnabled);
            Packages.Add(newPackage);
            return newPackage;
        }

        public bool CanPublish
        {
            get { return true; }
        }

        public string Name
        {
            get; set;
        }

        public IEnumerable<IPackageInfo> Clean(IEnumerable<IPackageInfo> packagesToKepp)
        {
            packagesToKepp = packagesToKepp.ToList();
            foreach(var packageInfo in Packages.Where(x=>!packagesToKepp.Contains(x)))
            {
                if (!Packages.Contains(packageInfo))
                    throw new ArgumentException("Supplied packageInfo must belong to the FolderRepository.", "packageInfo");

                Packages.Remove(packageInfo);

                _rootCacheDirectory.GetDirectory(packageInfo.FullName).Delete();
                BasePath.GetFile(packageInfo.FullName + ".wrap").Delete();

                yield return packageInfo;
            }
        }

    }
}