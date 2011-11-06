using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenFileSystem.IO;

namespace OpenWrap.Build.Tasks
{
    public class MSBuildInstructionEmitter
    {
        readonly IFileSystem _fileSystem;
        public bool IncludePdbFiles { get; set; }
        public bool IncludeCodeDocFiles { get; set; }
        public string BasePath { get; set; }
        public string ExportName { get; set; }

        public MSBuildInstructionEmitter(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
            ContentFiles = new List<string>();
            AllAssemblyReferenceFiles = new List<string>();
            OpenWrapReferenceFiles = new List<string>();
            PdbFiles = new List<string>();
            CodeDocFiles = new List<string>();
            SatelliteAssemblies = new List<string>();
            SerializationAssemblies = new List<string>();
            ContractReferenceAssemblies = new List<string>();
            OutputAssemblyFiles = new List<string>();
            IncludePdbFiles = true;
            IncludeCodeDocFiles = true;
            IncludeSourceFiles = false;
            SourceFiles = new List<string>();
        }

        public bool IncludeSourceFiles { get; set; }

        public ICollection<string> ContentFiles { get; set; }
        public ICollection<string> AllAssemblyReferenceFiles { get; set; }
        public ICollection<string> OpenWrapReferenceFiles { get; set; }
        public ICollection<string> PdbFiles { get; set; }
        public ICollection<string> CodeDocFiles { get; set; }
        public ICollection<string> SatelliteAssemblies { get; set; }
        public ICollection<string> SerializationAssemblies { get; set; }
        public ICollection<string> ContractReferenceAssemblies { get; set; }

        public ICollection<string> OutputAssemblyFiles { get; set; }

        public ICollection<string> SourceFiles { get; set; }

        public IEnumerable<KeyValuePair<string, string>> GenerateInstructions()
        {
            if (string.IsNullOrEmpty(BasePath)) throw new InvalidOperationException("BasePath is not defined.");
            if (string.IsNullOrEmpty(ExportName)) throw new InvalidOperationException("ExportName is not defined.");

            return GenerateInstructionsCore();
        }

        IEnumerable<KeyValuePair<string, string>> GenerateInstructionsCore()
        {
            var baseDir = _fileSystem.GetDirectory(BasePath);
            var includedAssemblies = AllAssemblyReferenceFiles
                .Where(x => !IsOpenWrapReferenceOrRelatedFile(x))
                .Select(baseDir.GetFile)
                .Concat(OutputAssemblyFiles.Select(baseDir.GetFile))
                .Where(IsNetAssembly)
                .ToList();

            foreach (var file in includedAssemblies)
            {
                yield return Key(ExportName, file.Path.FullPath);
            }

            foreach (var content in ContentFiles)
            {
                var relativePath = new OpenFileSystem.IO.Path(ExportName).Combine(new OpenFileSystem.IO.Path(content).MakeRelative(baseDir.Path).FullPath).DirectoryName;
                yield return Key(relativePath, baseDir.GetFile(content).Path.FullPath);
            }
            var associatedFiles = Enumerable.Empty<string>();
            if (IncludeCodeDocFiles) associatedFiles = associatedFiles.Concat(CodeDocFiles);
            if (IncludePdbFiles) associatedFiles = associatedFiles.Concat(PdbFiles);

            foreach (var associated in associatedFiles)
            {
                var associatedFile = baseDir.GetFile(associated);
                if (ShouldIncludeRelatedFiles(includedAssemblies, associatedFile, _ => _))
                    yield return Key(ExportName, associatedFile.Path.FullPath);
            }
            if (IncludeSourceFiles)
            {
                foreach(var source in SourceFiles)
                {
                    var relativePath = new OpenFileSystem.IO.Path("source").Combine(new OpenFileSystem.IO.Path(source).MakeRelative(baseDir.Path).FullPath).DirectoryName;
                    var file = baseDir.GetFile(source);
                    yield return Key(relativePath, file.Path.FullPath);
                }
            }
            foreach (var satellite in SatelliteAssemblies)
            {
                var associatedFile = baseDir.GetFile(satellite);
                IFile relatedFile = GetRelatedFile(includedAssemblies, associatedFile, x => RemoveSuffix(x, ".resources"));

                if (relatedFile != null)
                {
                    var relativePath = new OpenFileSystem.IO.Path(ExportName).Combine(new OpenFileSystem.IO.Path(satellite).MakeRelative(relatedFile.Parent.Path).FullPath).DirectoryName;
                    yield return Key(relativePath, associatedFile.Path.FullPath);
                }
            }
            foreach (var serializationAssemblyPath in SerializationAssemblies)
            {
                var associatedFile = baseDir.GetFile(serializationAssemblyPath);
                if (ShouldIncludeRelatedFiles(includedAssemblies, associatedFile, x => RemoveSuffix(x, ".XmlSerializers")))
                    yield return Key(ExportName, associatedFile.Path.FullPath);
            }
            foreach (var contractReferenceAssembly in ContractReferenceAssemblies)
            {
                var associatedFile = baseDir.GetFile(contractReferenceAssembly);
                IFile relatedFile = GetRelatedFile(includedAssemblies, associatedFile, x => RemoveSuffix(x, ".Contracts"));

                if (relatedFile != null)
                {
                    var relativePath = new OpenFileSystem.IO.Path(ExportName).Combine(new OpenFileSystem.IO.Path(contractReferenceAssembly).MakeRelative(relatedFile.Parent.Path).FullPath).DirectoryName;
                    yield return Key(relativePath, associatedFile.Path.FullPath);
                }
            }
        }

        bool IsNetAssembly(IFile file)
        {
            return file.Extension.EqualsNoCase(".dll")
                   || file.Extension.EqualsNoCase(".exe");
        }

        static string RemoveSuffix(string arg, string suffix)
        {
            return arg.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                           ? arg.Substring(0, arg.Length - suffix.Length)
                           : null;
        }

        bool IsOpenWrapReferenceOrRelatedFile(string file)
        {
            if (OpenWrapReferenceFiles.Contains(file))
            {
                return true;
            }

            string baseFileName = System.IO.Path.GetFileNameWithoutExtension(file);
            var suffices = new[] { ".Contracts", ".XmlSerializers", ".resources" };
            baseFileName = suffices.Aggregate((string)null, (aggr, e) => aggr ?? RemoveSuffix(baseFileName, e)) ?? baseFileName;

            return OpenWrapReferenceFiles
                .Select(System.IO.Path.GetFileNameWithoutExtension)
                .ContainsNoCase(baseFileName);
        }

        static IFile GetRelatedFile(IEnumerable<IFile> includedAssemblies, IFile file, Func<string, string> converter)
        {
            var converted = converter(file.NameWithoutExtension);

            return converted == null
                ? null
                : includedAssemblies.SingleOrDefault(x => x.NameWithoutExtension.EqualsNoCase(converted));
        }

        static bool ShouldIncludeRelatedFiles(IEnumerable<IFile> includedAssemblies, IFile file, Func<string, string> converter)
        {
            var converted = converter(file.NameWithoutExtension);

            return converted == null
                ? false
                : includedAssemblies.Any(x => x.NameWithoutExtension.EqualsNoCase(converted));
        }

        static KeyValuePair<string, string> Key(string name, string value)
        {
            return new KeyValuePair<string, string>(name, value);
        }
    }
}