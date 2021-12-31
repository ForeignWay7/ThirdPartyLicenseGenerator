using System;
using System.Collections.Generic;
using System.Linq;

namespace ForeignWay.ThirdPartyLicenseGenerator.Helpers
{
    public static class CoreHelpers
    {
        public static ICollection<string> GetCollectionFromStringArg(string? argument)
        {
            if (string.IsNullOrEmpty(argument)) return new List<string>();

            var argCollection = argument.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());

            return argCollection.ToList();
        }

        internal static void WriteOutput(string line, bool quiet)
        {
            if (quiet) return;

            Console.WriteLine();
            Console.WriteLine(line);
            Console.WriteLine();
        }
    }
}