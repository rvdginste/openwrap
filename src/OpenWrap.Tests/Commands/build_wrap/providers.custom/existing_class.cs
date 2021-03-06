using NUnit.Framework;
using OpenWrap;
using OpenWrap.PackageModel;
using OpenWrap.Repositories;
using OpenWrap.Testing;

namespace Tests.Commands.build_wrap.providers.custom
{
    public class existing_class : contexts.build_wrap
    {
        public existing_class()
        {
            given_current_directory_repository(new CurrentDirectoryRepository());
            given_descriptor(FileSystem.GetCurrentDirectory(),
                             new PackageDescriptor
                             {
                                 Name = "test",
                                 Version = "1.0.0.0".ToVersion(),
                                 Build = { "custom;typename=" + typeof(NullPackageBuilder).AssemblyQualifiedName }
                             });
            when_executing_command();
        }

        [Test]
        public void build_method_is_called()
        {
            NullPackageBuilder.Called.ShouldBeTrue();
        }
    }
}