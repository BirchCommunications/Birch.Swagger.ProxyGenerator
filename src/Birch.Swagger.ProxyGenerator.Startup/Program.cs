using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Birch.Swagger.ProxyGenerator.Startup
{
    /// <summary>
    /// Birch.Swagger.ProxyGenerator bootstrapper
    /// </summary>
    public class Program
    {
        static int Main(string[] args)
        {
            Output.Write("Starting Birch.Swagger.ProxyGenerator...");
            Output.Write();
            try
            {
                // check for switch values
                if (args.Any())
                {
                    for (var i = 0; i < args.Length; i++)
                    {
                        var argument = args[i].ToLower();
                        if (argument == "-verbose")
                        {
                            Output.Verbose = true;
                        }
                    }
                }

                // get settings
                var swaggerApiProxySettings = Generator.ProxyGenerator.GetSettings(args);

                // check if auto run has been disabled
                if (swaggerApiProxySettings.AutoRunOnBuildDisabled)
                {
                    Output.Write("AutoRunOnBuildDisabled has been set to true. Exiting proxy generator.");
                    return ExitApplication(0);
                }
                
                // Validate settings
                if (string.IsNullOrWhiteSpace(swaggerApiProxySettings.WebApiAssembly) && string.IsNullOrWhiteSpace(swaggerApiProxySettings.BaseUrl))
                {
                    Output.Write("No baseUrl or path to the WebApi assembly file provided, nothing to process.");
                    Output.Write();
                    Output.Write("Exiting Proxy Generator.");
                    return ExitApplication(1);
                }

                // print settings
                Output.Debug($"Mode: {(string.IsNullOrWhiteSpace(swaggerApiProxySettings.WebApiAssembly) ? "BaseUrl" : "In Memory")}");
                Output.Debug($"Output: {swaggerApiProxySettings.ProxyOutputFile}");
                if (!string.IsNullOrWhiteSpace(swaggerApiProxySettings.WebApiAssembly))
                {
                    Output.Debug($"Assembly: {swaggerApiProxySettings.WebApiAssembly}");
                    Output.Debug($"App Config: {swaggerApiProxySettings.WebApiConfig}");
                }

                Output.Debug();
                
                // copy config file
                var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)
                    ?.Replace(@"file:\", string.Empty) ?? string.Empty;
                var exeConfig = Path.Combine(exeDir, "Birch.Swagger.ProxyGenerator.exe.config");
                Output.Debug($"Copying \"{swaggerApiProxySettings.WebApiConfig}\" to \"{exeConfig}\"");
                Output.Debug();
                File.Copy(swaggerApiProxySettings.WebApiConfig, exeConfig, true);
                
                // start process
                const string processName = "Birch.Swagger.ProxyGenerator.exe";
                var arguments = string.Join(" ", args.Select(x => $"\"{x.Replace("\\", "\\\\")}\""));
                Output.Debug($"Starting process \"{processName}\" with arguments \"{arguments}\".");
                var process = new Process
                {
                    StartInfo =
                        {
                            FileName = processName,
                            Arguments = arguments,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                };
                process.OutputDataReceived += (s, e) => Output.Write(e.Data);
                process.ErrorDataReceived += (s, e) => Output.Write(e.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                Output.Debug($"Removing \"{exeConfig}\"");

                File.Delete("Birch.Swagger.ProxyGenerator.exe.config");

                return ExitApplication(0);
            }
            catch (AggregateException aex)
            {
                foreach (Exception ex in aex.InnerExceptions)
                {
                    Output.Write($"An exception has occured: {ex.GetType()} - {ex.Message}");
                    Output.Write($"StackTrace: {ex.StackTrace}");
                    Output.Write();
                }

                Output.Write("Exiting Proxy Generator.");
                return ExitApplication(1);
            }
            catch (Exception ex)
            {
                Output.Write($"An exception has occured: {ex.GetType()} - {ex.Message}");
                Output.Write($"StackTrace: {ex.StackTrace}");
                Output.Write();
                Output.Write("Exiting Proxy Generator.");
                return ExitApplication(1);
            }
        }

        private static int ExitApplication(int exitCode)
        {
            if (!Debugger.IsAttached)
            {
                return exitCode;
            }

            Output.Write("Press any key to continue...");
            Console.ReadKey();
            return exitCode;
        }
    }
}
