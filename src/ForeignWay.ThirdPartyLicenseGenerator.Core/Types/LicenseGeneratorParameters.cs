using System.Collections.Generic;

namespace ForeignWay.ThirdPartyLicenseGenerator.Types
{
    public class LicenseGeneratorParameters
    {
        public string SolutionFile { get; }
        public ICollection<string> ExcludedProjects { get; }
        public ICollection<string> ExcludedReferences { get; }
        public string OutputDirectory { get; }
        public string OutputFile { get; }
        public bool Quiet { get; }


        public LicenseGeneratorParameters(string solutionFile, ICollection<string>? excludedProjects, ICollection<string>? excludedReferences,
            string outputDirectory, string? outputFile, bool quiet)
        {
            SolutionFile = solutionFile.Trim();
            ExcludedProjects = excludedProjects ?? new List<string>();
            ExcludedReferences = excludedReferences ?? new List<string>();
            OutputDirectory = outputDirectory.Trim();
            OutputFile = FormatOutputFile(outputFile);
            Quiet = quiet;
        }

        private static string FormatOutputFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "licenses.html";

            if (fileName.EndsWith(".html") == false) return fileName.Trim() + ".html";

            return fileName;
        }
    }
}