using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace NugetUtility
{
    public class Methods
    {
        public Methods()
        {
        }

        private string nugetUrl = "https://api.nuget.org/v3-flatcontainer/";

        /// <summary>
        /// Retreive the project references from csproj file
        /// </summary>
        /// <param name="projectPath">The Project Path</param>
        /// <returns></returns>
        public IEnumerable<string> GetProjectReferences(string projectPath)
        {
            IEnumerable<string> references = new List<string>();
            XDocument projDefinition = XDocument.Load(projectPath);
            try
            {
                references = projDefinition
                             .Element("Project")
                             .Elements("ItemGroup")
                             .Elements("PackageReference")
                             .Select(refElem => (refElem.Attribute("Include") == null ? "" : refElem.Attribute("Include").Value) + "," +
                                                (refElem.Attribute("Version") == null ? "" : refElem.Attribute("Version").Value));
            }
            catch (System.Exception ex)
            {
                throw ex;
            }

            return references;
        }


        /// <summary>
        /// Get Nuget References per project
        /// </summary>
        /// <param name="project">project name</param>
        /// <param name="references">List of projects</param>
        /// <returns></returns>
        public async Task<Dictionary<string, Package>> GetNugetInformationAsync(string project, IEnumerable<string> references)
        {
            System.Console.WriteLine(Environment.NewLine + "project:" + project + Environment.NewLine);
            Dictionary<string, Package> licenses = new Dictionary<string, Package>();
            foreach (var reference in references)
            {
                string referenceName = reference.Split(',')[0];
                string versionNumber = reference.Split(',')[1];
                using (var httpClient = new HttpClient {Timeout = TimeSpan.FromSeconds(10)})
                {
                    string requestUrl = nugetUrl + referenceName + "/" + versionNumber + "/" + referenceName + ".nuspec";
                    Console.WriteLine(requestUrl);
                    try
                    {
                        HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                        HttpResponseMessage response = null;
                        response = await httpClient.SendAsync(req);
                        string responseText = await response.Content.ReadAsStringAsync();
                        XmlSerializer serializer = new XmlSerializer(typeof(Package));
                        using (TextReader writer = new StringReader(responseText))
                        {
                            try
                            {
                                Package result = (Package) serializer.Deserialize(new NamespaceIgnorantXmlTextReader(writer));
                                licenses.Add(reference, result);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                throw;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            return licenses;
        }

        public string GetProjectExtension()
        {
            return ".csproj";
        }

        public async Task<bool> PrintReferencesAsync(string projectPath, bool uniqueList, bool jsonOutput, bool output)
        {
            bool result = false;
            var licenses = new List<Dictionary<string, Package>>();
            var projects = Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories).Where(i => i.EndsWith(GetProjectExtension()));
            foreach (var project in projects)
            {
                var references = this.GetProjectReferences(project);
                if (uniqueList)
                {
                    licenses.Add(await this.GetNugetInformationAsync(project, references));
                }
                else
                {
                    licenses.Add(await this.GetNugetInformationAsync(project, references));
                    PrintLicenses(licenses, project);
                }

                result = true;
            }

            if (jsonOutput)
                PrintInJson(licenses);
            else if (uniqueList)
                PrintUniqueLicenses(licenses, output);

            return result;
        }

        public void PrintLicenses(List<Dictionary<string, Package>> licenses, string project)
        {
            if (licenses.Any())
            {
                Console.WriteLine(Environment.NewLine + "References:");
                foreach (var license in licenses)
                {
                    var result = license.ToStringTable(new[] { "#", "Package", "Version", "License" },
                                                            a => "#",
                                                            a => $"[{a.Value.Metadata.Id ?? "---"}]({a.Value.Metadata.ProjectUrl ?? string.Empty})",
                                                            a => a.Value.Metadata.Version ?? "---",
                                                            a => $"[{a.Value.Metadata.License?.Text ?? "---"}]({a.Value.Metadata.LicenseUrl ?? string.Empty})");
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(project), Path.GetFileNameWithoutExtension(project) + "_Notices.md"), result);
                    Console.WriteLine(result);

                }
            }
        }

        public void PrintUniqueLicenses(List<Dictionary<string, Package>> licenses, bool output)
        {
            if (licenses.Any())
            {
                Console.WriteLine(Environment.NewLine + "References:");
                foreach (var license in licenses)
                {
                    var result = license.ToStringTable(new[] { "#", "Package", "Version", "License" },
                                                            a => "#",
                                                            a => $"[{a.Value.Metadata.Id ?? "---"}]({a.Value.Metadata.ProjectUrl ?? string.Empty})",
                                                            a => a.Value.Metadata.Version ?? "---",
                                                            a => $"[{a.Value.Metadata.License?.Text ?? "---"}]({a.Value.Metadata.LicenseUrl ?? string.Empty})");
                    Console.WriteLine(result);
                }

                if (output)
                {
                    var sb = new StringBuilder();
                    foreach (var license in licenses)
                    {
                        foreach (var lic in license)
                        {
                            var packageData = lic.Value;
                            if (packageData != null)
                            {
                                sb.Append(new string('#', 100));
                                sb.AppendLine();
                                sb.Append("Package-ID:");
                                sb.Append(packageData.Metadata.Id);
                                sb.AppendLine();
                                sb.Append("Package-Version:");
                                sb.Append(packageData.Metadata.Version);
                                sb.AppendLine();
                                sb.Append("Project URL:");
                                sb.Append(packageData.Metadata.ProjectUrl ?? string.Empty);
                                sb.AppendLine();
                                sb.Append("Description:");
                                sb.Append(packageData.Metadata.Description ?? string.Empty);
                                sb.AppendLine();
                                sb.Append("License Type:");
                                sb.Append(packageData.Metadata.License != null ? packageData.Metadata.License.Text : string.Empty);
                                sb.AppendLine();
                                sb.Append("License Url:");
                                sb.Append(packageData.Metadata.LicenseUrl ?? string.Empty);
                                sb.AppendLine();
                                sb.AppendLine();
                            }
                        }
                    }

                    File.WriteAllText("licenses.txt", sb.ToString());
                }
            }
        }

        public void PrintInJson(List<Dictionary<string, Package>> licenses)
        {
            IList<LibraryInfo> libraryInfos = new List<LibraryInfo>();

            foreach (Dictionary<string,Package>  packageLicense in licenses)
            {
                foreach (KeyValuePair<string, Package> license in packageLicense)
                {
                    libraryInfos.Add(
                        new LibraryInfo
                        {
                            PackageName = license.Value.Metadata.Id ?? string.Empty,
                            PackageVersion = license.Value.Metadata.Version ?? string.Empty,
                            PackageUrl = license.Value.Metadata.ProjectUrl ?? string.Empty,
                            Description = license.Value.Metadata.Description ?? string.Empty,
                            LicenseType = license.Value.Metadata.License != null ? license.Value.Metadata.License.Text : string.Empty,
                            LicenseUrl = license.Value.Metadata.LicenseUrl ?? string.Empty
                        });
                }
            }

            var fileStream = new FileStream("licenses.json", FileMode.Create);
            using (var streamWriter = new StreamWriter(fileStream))
            {
                streamWriter.Write(JsonConvert.SerializeObject(libraryInfos));
                streamWriter.Flush();
            }

            fileStream.Close();
        }
    }
}