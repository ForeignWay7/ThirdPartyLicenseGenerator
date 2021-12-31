using System;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using ForeignWay.ThirdPartyLicenseGenerator.App.Helpers;
using ForeignWay.ThirdPartyLicenseGenerator.App.UserArguments;

namespace ForeignWay.ThirdPartyLicenseGenerator.App
{
    internal class Program
    {
       private static async Task<int> Main(string[] args)
       {
           var result = Parser.Default.ParseArguments<UserArgs>(args);
            
           return await result.MapResult(Execute, errors => Task.FromResult(1));
       }

        private static async Task<int> Execute(UserArgs args)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(args.SolutionFile?.Trim()))
                {
                    ShowMessage(-9);
                    return await Task.FromResult(-9);
                }
                if (args.SolutionFile?.Trim().EndsWith(".sln") == false)
                {
                    ShowMessage(-10);
                    return await Task.FromResult(-10);
                }
                if (File.Exists(args.SolutionFile?.Trim()) == false)
                {
                    ShowMessage(-10);
                    return await Task.FromResult(-10);
                }

                var parameters = ApplicationHelpers.MapUserArgsToLicenseGeneratorParameters(args);
                var result = await  ThirdPartyLicenses.Generate(parameters);

                ShowMessage(result);
                return await Task.FromResult(result);
            }
            catch
            {
                ShowMessage(-1);
                return await Task.FromResult(-1);
            }
        }
        
        private static void ShowMessage(int exitCode)
        {
            var resultMessage = exitCode switch
            {
                0 => "Res(0):\tGeneration successful..",
                -9 => "ERR(-9):\tA solution file path was not specified!",
                -10 => "ERR(-10):\tThe specified solution file is invalid!",
                _ => $"ERR({exitCode}):\tAn unknown error occurred.."
            };

            Console.WriteLine();
            Console.WriteLine(resultMessage);

            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
