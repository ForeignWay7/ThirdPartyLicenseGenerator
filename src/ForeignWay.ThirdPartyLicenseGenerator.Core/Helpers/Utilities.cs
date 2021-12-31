using System;

namespace ForeignWay.ThirdPartyLicenseGenerator.Helpers
{
    public static class Utilities
    {
        public static string CorrectPathCharacter(this string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            return path.Replace("\\", "/").Trim();
        }
    }
}