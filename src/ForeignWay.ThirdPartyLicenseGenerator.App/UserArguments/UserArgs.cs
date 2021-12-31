using CommandLine;

namespace ForeignWay.ThirdPartyLicenseGenerator.App.UserArguments
{
    internal class UserArgs
    {
        [Option('f', "solution-file", HelpText = "The solution file to generate the nuget licenses from. This must be a .sln file.")]
        public string? SolutionFile { get; set; }
        

        [Option('t', "exclude-projects-filter", Default = null, HelpText = "Comma separated values text of projects to exclude. Supports Starts with matching such as 'UnwantedProj*'")]
        public string? ProjectsFilterOption { get; set; }


        [Option('r', "exclude-reference-filter", Default = null, HelpText = "Comma separated values text of references to exclude. Supports Starts with matching such as 'UnwantedReference*'")]
        public string? ReferenceFilterOption { get; set; }


        [Option('d', "output-directory", Default = null, HelpText = "Output Directory")]
        public string? OutputDirectory { get; set; }


        [Option('s', "output-file", Default = null, HelpText = "Output filename")]
        public string? OutputFileNameOption { get; set; }


        [Option('q', "quiet", Default = null, HelpText = "indicates whether a log should be displayed.")]
        public string? QuietOption { get; set; }

    }
}