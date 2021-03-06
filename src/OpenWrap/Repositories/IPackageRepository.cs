using System.Collections.Generic;
using System.Linq;
using OpenWrap.PackageModel;

namespace OpenWrap.Repositories
{
    public interface IPackageRepository
    {
        ILookup<string, IPackageInfo> PackagesByName { get; }

        IEnumerable<IPackageInfo> FindAll(PackageDependency dependency);

        void RefreshPackages();
        string Name { get; }
        string Token { get; }
        string Type { get; }

        TFeature Feature<TFeature>() where TFeature : class, IRepositoryFeature;
    }
}