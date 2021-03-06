using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenWrap;
using OpenWrap.Build;
using OpenWrap.Build.PackageBuilders;
using OpenWrap.Commands.Wrap;
using OpenWrap.PackageModel;
using OpenWrap.Repositories;
using OpenWrap.Testing;
using Tests.Commands.build_wrap.inputs_to_providers;

namespace Tests.Commands.build_wrap
{
    public class failed_build : contexts.build_wrap
    {
        public failed_build()
        {

            given_current_directory_repository(new CurrentDirectoryRepository());
            given_descriptor(FileSystem.GetCurrentDirectory(),
                             new PackageDescriptor
                             {
                                 Name = "test",
                                 Version = "1.0.0.0".ToVersion(),
                                 Build = { "custom;typename=" + typeof(FailingBuild).AssemblyQualifiedName }
                             });
            when_executing_command();
        }

        [Test]
        public void package_not_built()
        {
            Environment.CurrentDirectoryRepository.PackagesByName.ShouldBeEmpty();
        }

        [Test]
        public void no_package_file_output()
        {
            Results.ShouldHaveError();
        }
    }

    public class FailingBuild : IPackageBuilder
    {
        public IEnumerable<BuildResult> Build()
        {
            yield return new ErrorBuildResult();
        }
    }
}