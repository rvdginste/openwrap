﻿using System;
using System.Linq;
using NUnit.Framework;
using OpenWrap.Commands.Wrap;
using OpenWrap.Repositories;
using OpenWrap.Testing;
using Tests.Commands.contexts;

namespace Tests.Commands.build_wrap
{
    public class building_a_meta_package : command<BuildWrapCommand>
    {
        static Version version = new Version("1.2.3.4");
        const string packageName = "mypackage";
        public building_a_meta_package()
        {
            given_current_directory_repository(new CurrentDirectoryRepository());
            Environment.Descriptor.Name = packageName;
            Environment.Descriptor.Version = version;
            Environment.Descriptor.Build.Add("none");
            Environment.Descriptor.Version = version;
            
            when_executing_command();
        }


        [Test]
        public void is_packaged()
        {
            Environment.CurrentDirectoryRepository
                    .PackagesByName[packageName]
                    .ShouldHaveCountOf(1);
        }

        [Test]
        public void has_correct_version_number()
        {
            Environment.CurrentDirectoryRepository
                    .PackagesByName[packageName]
                    .ShouldHaveCountOf(1)
                    .First().Version.ShouldBe(version);
        }

        [Test]
        public void package_information_is_output()
        {
            Results.OfType<PackageBuilt>().Single().ShouldNotBeNull().PackageFile.Exists.ShouldBeTrue();
        }
    }
}
