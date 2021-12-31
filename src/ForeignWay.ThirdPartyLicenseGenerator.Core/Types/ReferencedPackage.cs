using System;

namespace ForeignWay.ThirdPartyLicenseGenerator.Types
{
    public class ReferencedPackage
    {
        public string Name { get; }
        public string Version { get; }
        public string? LicenseType { get; set; }
        public string? LicenseUrl { get; set; }


        public ReferencedPackage(string name, string version)
        {
            Name = name;
            Version = version;
        }

        public override string ToString()
        {
            return $"{Name}: {Version}";
        }

        public override bool Equals(object? obj)
        {
            if (obj is ReferencedPackage referencedPackage)
            {
                return Name.Equals(referencedPackage.Name, StringComparison.CurrentCultureIgnoreCase) && Version.Equals(Version);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name.GetHashCode(), Version.GetHashCode());
        }
    }
}