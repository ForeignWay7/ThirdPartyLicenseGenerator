using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using ForeignWay.ThirdPartyLicenseGenerator.Helpers;
using ForeignWay.ThirdPartyLicenseGenerator.Types;

namespace ForeignWay.ThirdPartyLicenseGenerator
{
    public static class ThirdPartyLicenses
    {
        private const string NuGetUrl = "https://api.nuget.org/v3-flatcontainer/";
        private const string FallbackPackageUrl = "https://www.nuget.org/api/v2/package/{0}/{1}";
        
        private static readonly HttpClientHandler HttpClientHandler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            ServerCertificateCustomValidationCallback = IgnoreSslCertificateErrorCallback,
            UseCookies = false
        };

        private static readonly HttpClient HttpClient = new HttpClient(HttpClientHandler)
        {
            BaseAddress = new Uri(NuGetUrl),
            Timeout = TimeSpan.FromSeconds(1)
        };

        public static async Task<int> Generate(LicenseGeneratorParameters parameters, ICollection<ReferencedPackage>? additionalPackages = null)
        {
            var projects = await GetReferencedProjectsFromSolutionAsync(parameters.SolutionFile, parameters.Quiet);
            var filteredProjects = GetFilteredProjects(projects, parameters.ExcludedProjects, parameters.Quiet).ToList();

            var fullProjectsPath = GetFullProjectsPath(filteredProjects, parameters.SolutionFile);
            
            var referencedPackages = GetProjectsReferences(fullProjectsPath, parameters.Quiet);
            var filteredReferencedPackages = GetFilteredReferences(referencedPackages, parameters.ExcludedReferences, parameters.Quiet)
                .OrderBy(x => x.Name).ToList();
            
            await MapLicensesToProjectsReferencesAsync(filteredReferencedPackages, parameters.Quiet);

            if (additionalPackages?.Any() == true)
            {
                filteredReferencedPackages.AddRange(additionalPackages);
            }

            GenerateHtmlFromResult(filteredReferencedPackages, parameters.OutputDirectory, parameters.OutputFile);

            return 0;
        }

        private static async Task<IEnumerable<string>> GetReferencedProjectsFromSolutionAsync(string solutionFilePath, bool quiet)
        {
            var solutionFile = new FileInfo(solutionFilePath);
            if (solutionFile.Exists == false) throw new FileNotFoundException(solutionFilePath);

            var projectFiles = new List<string>();

            await using var fileStream = solutionFile.OpenRead();
            using var streamReader = new StreamReader(fileStream);
            while (await streamReader.ReadLineAsync() is { } line)
            {
                if (line.StartsWith("Project") == false) continue;

                var segments = line.Split(',');
                if (segments.Length < 2) continue;

                var supportedProjectExtensions = new[] { ".csproj", ".fsproj" };

                foreach (var segment in segments)
                {
                    var match = false;
                    foreach (var projectExtension in supportedProjectExtensions)
                    {
                        if (segment.Trim('"').EndsWith(projectExtension)) match = true;
                    }

                    if (match == false) continue;

                    projectFiles.Add(segment.CorrectPathCharacter().Trim('"'));
                }
            }

            CoreHelpers.WriteOutput("Found Project Files:", quiet);
            CoreHelpers.WriteOutput(string.Join(Environment.NewLine, projectFiles.ToArray()), quiet);

            return projectFiles;
        }

        private static IEnumerable<string> GetFilteredProjects(IEnumerable<string> projects, ICollection<string> projectsFilter, bool quiet)
        {
            if (projectsFilter.Any() == false) return projects;

            var filteredProjects = projects
                .Where(project => projectsFilter.Any(x => new FileInfo(project).Name.StartsWith(x, StringComparison.CurrentCultureIgnoreCase)) == false).ToList();

            CoreHelpers.WriteOutput("Filtered Project Files:", quiet);
            CoreHelpers.WriteOutput(string.Join(Environment.NewLine, filteredProjects.ToArray()), quiet);

            return filteredProjects;
        }

        private static IEnumerable<string> GetFullProjectsPath(IEnumerable<string> filteredProjects, string solutionFilePath)
        {
            var solutionDirectory = Path.GetDirectoryName(solutionFilePath);

            if (string.IsNullOrEmpty(solutionDirectory) || Directory.Exists(solutionDirectory) == false)
                throw new DirectoryNotFoundException($"The Directory {solutionDirectory} does not exist");

            return filteredProjects.Select(projectPath => Path.Combine(solutionDirectory, projectPath)).ToList();
        }

        private static IEnumerable<ReferencedPackage> GetProjectsReferences(IEnumerable<string> projects, bool quiet)
        {
            CoreHelpers.WriteOutput($"Starting {nameof(GetProjectsReferences)}...", quiet);

            var references = new List<ReferencedPackage>();

            foreach (var project in projects)
            {
                references.AddRange(GetProjectReferences(project));
            }

            var filteredReferences = references.Distinct();

            return filteredReferences;
        }

        private static IEnumerable<ReferencedPackage> GetProjectReferences(string projectPath)
        {
            if (File.Exists(projectPath) == false)
            {
                throw new FileNotFoundException(projectPath);
            }

            IEnumerable<ReferencedPackage> references = Array.Empty<ReferencedPackage>();

            // Then try to get references from new project file format
            if (references.Any() == false)
            {
                references = GetProjectReferencesFromNewProjectStyle(projectPath);
            }

            // Then if needed from old packages.config
            if (references.Any() == false)
            {
                references = GetProjectReferencesFromPackagesConfig(projectPath);
            }

            return references;
        }

        private static IEnumerable<ReferencedPackage> GetFilteredReferences(IEnumerable<ReferencedPackage> packages, ICollection<string> packagesFilter, bool quiet)
        {
            if (packagesFilter.Any() == false) return packages;

            CoreHelpers.WriteOutput($"Starting {nameof(GetFilteredReferences)}...", quiet);

            var filteredProjects = packages
                .Where(package => packagesFilter.Any(x => package.Name.StartsWith(x, StringComparison.CurrentCultureIgnoreCase)) == false).ToList();

            return filteredProjects;
        }

        private static IEnumerable<ReferencedPackage> GetProjectReferencesFromNewProjectStyle(string projectPath)
        {
            var projDefinition = XDocument.Load(projectPath);

            return projDefinition
                       .XPathSelectElements("/*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='PackageReference']")?
                       .Select(GetProjectReferenceFromElement) ??
                   Array.Empty<ReferencedPackage>();
        }

        private static ReferencedPackage GetProjectReferenceFromElement(XElement refElem)
        {
            string version, package = refElem.Attribute("Include")?.Value ?? string.Empty;

            var versionAttribute = refElem.Attribute("Version");

            if (versionAttribute != null)
                version = versionAttribute.Value;
            else // no version attribute, look for child element
                version = refElem
                    .Elements()
                    .FirstOrDefault(elem => elem.Name.LocalName == "Version")?.Value ?? string.Empty;

            return new ReferencedPackage(package, version);
        }

        private static IEnumerable<ReferencedPackage> GetProjectReferencesFromPackagesConfig(string projectPath)
        {
            var dir = Path.GetDirectoryName(projectPath);
            var packagesFile = Path.Join(dir, "packages.config");

            if (File.Exists(packagesFile) == false) return Array.Empty<ReferencedPackage>();

            var packagesConfig = XDocument.Load(packagesFile);

            return packagesConfig
                       .Element("packages")?
                       .Elements("package")
                       .Select(refElem =>
                           new ReferencedPackage(refElem.Attribute("id")?.Value ?? string.Empty, refElem.Attribute("version")?.Value ?? string.Empty))
                   ?? Array.Empty<ReferencedPackage>();
        }

        private static async Task MapLicensesToProjectsReferencesAsync(IEnumerable<ReferencedPackage> referencedPackages, bool quiet)
        {
            foreach (var package in referencedPackages)
            {
                var nuSpecPath = GetNuSpecPath(package);

                if (File.Exists(nuSpecPath))
                {
                    using var textReader = new StreamReader(nuSpecPath);
                    
                    await GetLicenseFromNuSpecFileAsync(package, textReader, quiet);
                }
                else
                {
                    await GetLicenseFromNuSpecFileAsync(package, null, quiet);
                    
                }
            }
        }

        private static async Task GetLicenseFromNuSpecFileAsync(ReferencedPackage package, TextReader? textReader, bool quiet)
        {
            if (textReader != null)
            {
                var serializer = new XmlSerializer(typeof(Package));

                if (serializer.Deserialize(new NamespaceIgnorantXmlTextReader(textReader)) is Package result)
                {
                    package.LicenseType = result.Metadata?.License?.Type; 
                    package.LicenseUrl = result.Metadata?.LicenseUrl;

                    return;
                }
            }

            await DownloadNuSpecAndGetLicenseAsync(package, quiet);
        }

        private static string GetNuSpecPath(ReferencedPackage package)
        {
            var userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
           
            return Path.Combine(userDir, ".nuget", "packages", package.Name, package.Version, $"{package.Name}.nuspec");
        }

        private static bool IgnoreSslCertificateErrorCallback(HttpRequestMessage message, X509Certificate2? cert,
            X509Chain? chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private static async Task DownloadNuSpecAndGetLicenseAsync(ReferencedPackage package, bool quiet)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{package.Name}/{package.Version}/{package.Name}.nuspec");
            using var response = await HttpClient.SendAsync(request);
            if (response.IsSuccessStatusCode == false)
            {
                CoreHelpers.WriteOutput($"{request.RequestUri} failed due to {response.StatusCode}!", quiet);
                
                var fallbackResult = await GetNuGetPackageFileResultAsync(package.Name, package.Version, $"{package.Name}.nuspec", quiet);
                
                if (fallbackResult == null) return;
                
                package.LicenseType = fallbackResult.Metadata?.License?.Type;
                package.LicenseUrl = fallbackResult.Metadata?.LicenseUrl;

                return;
            }

            CoreHelpers.WriteOutput($"Successfully received {request.RequestUri}", quiet);
            await using var responseText = await response.Content.ReadAsStreamAsync();
            using var textReader = new StreamReader(responseText);
            
            try
            {
                await GetLicenseFromNuSpecFileAsync(package, textReader, quiet);
            }
            catch (Exception e)
            {
                CoreHelpers.WriteOutput(e.ToString(), quiet);
                CoreHelpers.WriteOutput(e.Message, quiet);
            }
        }

        private static async Task<Package?> GetNuGetPackageFileResultAsync(string packageName, string versionNumber, string fileInPackage, bool quiet)
        {
            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(versionNumber)) return null;

            var fallbackEndpoint = new Uri(string.Format(FallbackPackageUrl, packageName, versionNumber));

            CoreHelpers.WriteOutput("Attempting to download: " + fallbackEndpoint, quiet);

            using var packageRequest = new HttpRequestMessage(HttpMethod.Get, fallbackEndpoint);
            using var packageResponse = await HttpClient.SendAsync(packageRequest, CancellationToken.None);

            if (packageResponse.IsSuccessStatusCode == false)
            {
                CoreHelpers.WriteOutput($"{packageRequest.RequestUri} failed due to {packageResponse.StatusCode}!", quiet);
                return null;
            }

            await using var fileStream = new MemoryStream();
            await packageResponse.Content.CopyToAsync(fileStream);

            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            var entry = archive.GetEntry(fileInPackage);
            if (entry is null)
            {
                CoreHelpers.WriteOutput($"{fileInPackage} was not found in NuGet Package: {packageName}", quiet);
                return null;
            }
            CoreHelpers.WriteOutput($"Attempting to read: {fileInPackage}", quiet);
            await using var entryStream = entry.Open();
            using var textReader = new StreamReader(entryStream);

            var serializer = new XmlSerializer(typeof(Package));
            if (serializer.Deserialize(new NamespaceIgnorantXmlTextReader(textReader)) is Package result)
            {
                return result;
            }

            return null;
        }

        private static void GenerateHtmlFromResult(IEnumerable<ReferencedPackage> packages, string outputDir, string outputFile)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append(@"<head>
                                    <link rel=""stylesheet"" href=""https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0/css/bootstrap.min.css"" integrity=""sha384-Gn5384xqQ1aoWXA+058RXPxPg6fy4IWvTNh0E263XmFcJlSAwiGgFAW/dAiS6JXm"" crossorigin=""anonymous"">
                                </head>");

            stringBuilder.Append(@"<body>
                                        <div class=""jumbotron"">
                                            <h1>Third party Licenses</h1>");
            stringBuilder.Append(
            @"<table class=""table table-striped"">
                            <thead class=""thead-dark"">
                                <tr>
                                    <th scope=""col""> Name </th>                 
                                    <th scope=""col""> Version </th>                  
                                    <th scope=""col""> License Type </th>
                                    <th scope=""col""> License Url </th>
                                </tr>
                            </thead>
                            <tbody>");

            foreach (var package in packages)
            {
                stringBuilder.Append(@$"<tr>                             
                                        <td> {package.Name} </td>                
                                        <td> {package.Version} </td>                
                                        <td> {package.LicenseType} </td>                
                                        <td> <a href=""{package.LicenseUrl}"">{package.LicenseUrl}</a> </td>              
                                    </tr>");
            }
            
            stringBuilder.Append(@"</tbody>
                                </table>
                                </div>
                                </body>");

            var path = Path.Combine(outputDir, outputFile);
            File.WriteAllText(path, stringBuilder.ToString());
        }
    }
}