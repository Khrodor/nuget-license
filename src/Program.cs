using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CommandLine;

namespace NugetUtility
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var licenses = new List<Tuple<string, string, string>>();
            var methods = new Methods();
            var result = CommandLine.Parser.Default.ParseArguments<PackageOptions>(args);

            await result.MapResult(
                (PackageOptions options) =>
                {
                    if (string.IsNullOrEmpty(options.ProjectDirectory))
                    {
                        Console.WriteLine("ERROR(S):");
                        Console.WriteLine("-i\tInput the Directory Path (csproj file)");
                    }
                    else
                    {
                        System.Console.WriteLine("Project Reference(s) Analysis...");
                        bool licensesHasRetrieved = methods.PrintReferencesAsync(options.ProjectDirectory, options.UniqueOutput, options.JsonOutput, options.Output).Result;
                    }
                    return Task.FromResult(0);
                },
                errors => Task.FromResult(1));

            Console.ReadLine();
        }
    }
}
