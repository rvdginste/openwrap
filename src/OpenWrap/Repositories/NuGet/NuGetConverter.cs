﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using OpenWrap.Dependencies;

namespace OpenWrap.Repositories.NuGet
{
    public static class NuGetConverter
    {
        public const string NuSpecSchema = "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd";

        static Dictionary<string, string> FrameworkProfiles = new Dictionary<string, string>
        {
                { "NET", "net" },
                { ".NET", "net" },
                { "NETFramework", "net" },
                { ".NETFramework", "net" },
                { "SL", "sl" },
                { "Silverlight", "sl" }
        };

        static Dictionary<string, string> FrameworkVersions = new Dictionary<string, string>
        {
                { "2.0", "20"},
                { "3.0", "30"},
                { "3.5", "35"},
                { "4.0", "40"},
                { "20", "20"},
                { "30", "30"},
                { "35", "35"},
                { "40", "40"},
                { "40ClientProfile", "40cp"},
                { "35ClientProfile", "35cp"}
        };
        public static void Convert(Stream nuGetPackage, Stream openWrapPackage)
        {
            if (nuGetPackage == null) throw new ArgumentNullException("nuGetPackage");
            if (openWrapPackage == null) throw new ArgumentNullException("openWrapPackage");
            
            

            PackageBuilder.NewFromFiles(openWrapPackage, Content(nuGetPackage));
        }
        public static IEnumerable<PackageContent> Content(Stream nuGetPackage)
        {
            PackageContent content = null;
            string temporaryFile = null;
            try
            {
                if (!nuGetPackage.CanSeek)
                {
                    temporaryFile = System.IO.Path.GetTempFileName();
                    using(var temporaryFileStream = File.OpenWrite(temporaryFile))
                        nuGetPackage.CopyTo(temporaryFileStream);

                    nuGetPackage = File.OpenRead(temporaryFile);
                }
                using (var inputZip = new ZipFile(nuGetPackage))
                {
                    foreach (var entry in inputZip.Cast<ZipEntry>().Where(x => x.IsFile))
                    {
                        var segments = entry.Name.Split('/');
                        if (segments.Length == 1 && System.IO.Path.GetExtension(entry.Name).EqualsNoCase(".nuspec"))
                            yield return ConvertSpecification(inputZip, entry);
                        else if (segments.Length >= 2 && segments[0].EqualsNoCase("lib"))
                            if ((content = ConvertAssembly(segments, inputZip, entry)) != null)
                                yield return content;
                    }
                }
            }
            finally
            {
                if (temporaryFile != null)
                {
                    nuGetPackage.Close();
                    File.Delete(temporaryFile);
                }
            }
        }


        static PackageContent ConvertAssembly(string[] segments, ZipFile inputZip, ZipEntry entry)
        {
            var destinationBinFolder = segments.Length == 2 ? "bin-net20" : ConvertAssemblyFolder(segments[1]);
            if (destinationBinFolder == null)
                return null;

            return new PackageContent
            {
                FileName = System.IO.Path.GetFileName(entry.Name),
                RelativePath = destinationBinFolder,
                Stream = () => inputZip.GetInputStream(entry),
                Size = entry.Size
            };
        }

        static string ConvertAssemblyFolder(string identifier)
        {
            
            var nuPackProfile = FrameworkProfiles.Keys.FirstOrDefault(x => identifier.StartsWith(x, StringComparison.OrdinalIgnoreCase));

            var versionString = nuPackProfile == null ? identifier : identifier.Substring(nuPackProfile.Length);
            var nuPackVersion = FrameworkVersions.Keys.FirstOrDefault(x => versionString.EqualsNoCase(x));

            if (nuPackProfile != null)
                nuPackProfile = FrameworkProfiles[nuPackProfile];
            if (nuPackVersion != null)
                nuPackVersion = FrameworkVersions[nuPackVersion];
            return "bin-" + (nuPackProfile == null && nuPackVersion == null ? identifier : ((nuPackProfile ?? "net") + (nuPackVersion ?? "20")));
        }

        static PackageContent ConvertSpecification(ZipFile file, ZipEntry entry)
        {
            var inputStream = file.GetInputStream(entry);

            var nuspec = new XmlDocument();

            nuspec.Load(inputStream);

            PackageDescriptor descriptor = NuSpecConverter.ConvertSpecificationToDescriptor(nuspec);
            var memoryStream = new MemoryStream();
            new PackageDescriptorReaderWriter().Write(descriptor, memoryStream);
            memoryStream.Position = 0;

            return new PackageContent
            {
                FileName = System.IO.Path.GetFileNameWithoutExtension(entry.Name) + ".wrapdesc",
                RelativePath = ".",
                Size = memoryStream.Length,
                Stream = () =>
                {
                    memoryStream.Position = 0;
                    return memoryStream;
                }
            };
        }
    }
    public class NuSpecConverter
    {
        static Version RemoveInsignificantVersionNumbers(string versionString)
        {
            if (versionString == null)
                return null;

            var ver = versionString.ToVersion();
            if (ver == null)
                return null;
            return new Version(ver.Major, ver.Minor);

        }

        static Version RemoveInsignificantVersionNumbers(StringBuilder currentIdentifier)
        {
            var versionString = currentIdentifier.ToString();
            return RemoveInsignificantVersionNumbers(versionString);
        }

        public static IEnumerable<VersionVertex> ConvertNuGetVersionRange(string value)
        {
            if (string.IsNullOrEmpty(value))
                yield break;
            var simpleVersion = value.ToVersion();
            if (simpleVersion != null)
            {
                // play around with version ranges to account for NuGet not making distinction between
                // revisions and implementation details of their tool.
                if (simpleVersion.Build == -1)
                {
                    yield return new ExactVersionVertex(simpleVersion);
                }
                else
                {
                    var lowerBound = RemoveInsignificantVersionNumbers(value);
                    var upperBound = new Version(lowerBound.Major, lowerBound.Minor + 1);
                    yield return new GreaterThanOrEqualVersionVertex(lowerBound);
                    yield return new LessThanVersionVertex(upperBound);
                }
                yield break;
            }
            Version beginVersion = null;
            Version endVersion = null;
            StringBuilder currentIdentifier = new StringBuilder();
            bool inclusiveBeginning = false;
            bool inclusiveEnding = false;
            bool endIncluded = false;
            Action clearVer = () => currentIdentifier = new StringBuilder();
            foreach(var ch in value)
            {
                if (ch == ' ') continue;
                if (ch == '[') { inclusiveBeginning = true; clearVer(); }
                else if (ch == '(') { inclusiveBeginning = false; clearVer(); }
                else if (ch == ']') { inclusiveEnding = true; }
                else if (ch == ')') { inclusiveEnding = false; }
                else if ((ch >= '0' && ch <= '9') || ch == '.') { currentIdentifier.Append(ch); }
                else if (ch == ',')
                {
                    if (currentIdentifier.Length > 0)
                    {
                        beginVersion = RemoveInsignificantVersionNumbers(currentIdentifier);
                        clearVer();
                    }
                    endIncluded = true;
                }
            }
            if (currentIdentifier.Length > 0 && endIncluded)
                endVersion = currentIdentifier.ToString().ToVersion();
            else if (currentIdentifier.Length > 0 && endIncluded == false && beginVersion == null)
                beginVersion = currentIdentifier.ToString().ToVersion();


            if (beginVersion != null && !endIncluded)
            {
                if (inclusiveBeginning && inclusiveEnding)
                    yield return new ExactVersionVertex(beginVersion);
                yield break;
            }
            if (beginVersion != null && inclusiveBeginning)
                yield return new GreaterThanOrEqualVersionVertex(beginVersion);
            else if (beginVersion != null && inclusiveBeginning == false)
                yield return new GreaterThanVersionVertex(beginVersion);

            if (endVersion != null && inclusiveEnding == false)
                yield return new LessThanVersionVertex(endVersion);
            else if (endVersion != null && inclusiveEnding == true)
                yield return new LessThanOrEqualVersionVertex(endVersion);


        }

        public static PackageDescriptor ConvertSpecificationToDescriptor(XmlDocument nuspec)
        {
            var ns = new XmlNamespaceManager(nuspec.NameTable);

            ns.AddNamespace("nuspec", NuGetConverter.NuSpecSchema);


            var descriptor = new PackageDescriptor
            {
                    Name = nuspec.Element(XPaths.PackageName, ns),
                    Version = nuspec.Element(XPaths.PackageVersion, ns).ToVersion(),
                    Description = StripLines(nuspec.Element(XPaths.PackageDescription, ns))
            };
            descriptor.Dependencies.AddRange(
                    nuspec.Elements(XPaths.PackageDependencies, ns).Select(CreateDependency)
                    );
            return descriptor;
        }

        static string StripLines(string element)
        {
            if (String.IsNullOrEmpty(element))
                return element;
            var noLineBreaks = element.Replace("\r\n", " ").Replace("\n", " ");
            while(noLineBreaks.Contains("  "))
                noLineBreaks = noLineBreaks.Replace("  ", " ");
            return noLineBreaks;
        }

        static PackageDependency CreateDependency(XmlNode xmlNode)
        {
            var dep = new PackageDependencyBuilder((xmlNode.Attributes["id"] ?? xmlNode.Attributes["id", NuGetConverter.NuSpecSchema]).Value);

            var version = xmlNode.Attributes["version"] ?? xmlNode.Attributes["version", NuGetConverter.NuSpecSchema];
            var minversion = xmlNode.Attributes["minversion"] ?? xmlNode.Attributes["minversion", NuGetConverter.NuSpecSchema];
            var maxversion = xmlNode.Attributes["maxversion"] ?? xmlNode.Attributes["maxversion", NuGetConverter.NuSpecSchema];
            if (minversion != null || maxversion != null)
            {
                if (minversion != null)
                    dep.VersionVertex(new GreaterThanOrEqualVersionVertex(minversion.Value.ToVersion()));
                if (maxversion != null)
                    dep.VersionVertex(new LessThanVersionVertex(maxversion.Value.ToVersion()));
            }
            else
            {
                dep.SetVersionVertices(ConvertNuGetVersionRange(version != null ? version.Value : null).DefaultIfEmpty(new AnyVersionVertex()));
            }
            return dep;
        }
    }
}
