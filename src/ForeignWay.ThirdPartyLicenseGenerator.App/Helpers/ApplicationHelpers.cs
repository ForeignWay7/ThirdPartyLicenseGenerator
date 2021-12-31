using System;
using ForeignWay.ThirdPartyLicenseGenerator.App.UserArguments;
using ForeignWay.ThirdPartyLicenseGenerator.Helpers;
using ForeignWay.ThirdPartyLicenseGenerator.Types;

namespace ForeignWay.ThirdPartyLicenseGenerator.App.Helpers
{
    internal static class ApplicationHelpers
    {
        public static LicenseGeneratorParameters MapUserArgsToLicenseGeneratorParameters(UserArgs userArgs)
        {
            if (string.IsNullOrEmpty(userArgs.SolutionFile)) throw new ArgumentNullException($"argument {userArgs.SolutionFile} was null..");

            if (userArgs.SolutionFile.EndsWith(".sln") == false)
                throw new ArgumentNullException($"argument {userArgs.SolutionFile} was not correct. This must be a solution or project file..");

            if (string.IsNullOrEmpty(userArgs.OutputDirectory)) throw new ArgumentNullException($"argument {userArgs.OutputDirectory} was null..");
            
            var excludedProjects = CoreHelpers.GetCollectionFromStringArg(userArgs.ProjectsFilterOption);
            var excludedReferences = CoreHelpers.GetCollectionFromStringArg(userArgs.ReferenceFilterOption);

            var quiet = userArgs.QuietOption?.Equals("q") == true;

            return new LicenseGeneratorParameters(userArgs.SolutionFile, excludedProjects, excludedReferences, userArgs.OutputDirectory,
                userArgs.OutputFileNameOption, quiet);
        }
    }
}