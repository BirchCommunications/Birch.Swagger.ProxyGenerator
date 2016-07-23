using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace Birch.Swagger.ProxyGenerator.Startup
{
    public class Program
    {
        static int Main(string[] args)
        {
            Output.Write("Starting Birch.Swagger.ProxyGenerator...");
            Output.Write();
            try
            {
                // define variables for switches
                var settingsFile = string.Empty;
                var appConfigFile = string.Empty;
                var assemblyFile = string.Empty;
                var proxyOutputFile = string.Empty;
                var baseUrl = string.Empty;
                var isAutoRun = false;

                // base directory is exe directory unless switch override
                var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)
                    ?.Replace(@"file:\", string.Empty);

                // check for switch values
                if (args.Any())
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        var argument = args[i].ToLower();

                        if (argument == "-settingsfile")
                        {
                            settingsFile = args[i + 1];
                        }
                        else if (argument == "-webapiassembly")
                        {
                            assemblyFile = args[i + 1];
                        }
                        else if (argument == "-webapiconfig")
                        {
                            appConfigFile = args[i + 1];
                        }
                        else if (argument == "-proxyoutputfile")
                        {
                            proxyOutputFile = args[i + 1];
                        }
                        else if (argument == "-baseurl")
                        {
                            baseUrl = args[i + 1];
                        }
                        else if (argument == "-basedirectory")
                        {
                            baseDirectory = args[i + 1];
                        }
                        else if (argument == "-autorun")
                        {
                            isAutoRun = true;
                        }
                        else if (argument == "-verbose")
                        {
                            Output.Verbose = true;
                        }
                    }
                }

                // Load Proxy Generator Settings
                Output.Debug($"Loading settings... \n{settingsFile}");
                if (baseDirectory == null)
                {
                    Output.Write("Could not determine base directory,");
                    return ExitApplication(1);
                }
                Output.Debug($"Base Directory: {baseDirectory}");
                var combine = Path.Combine(baseDirectory, "Birch.Swagger.ProxyGenerator.config.json");
                if (string.IsNullOrWhiteSpace(settingsFile) && File.Exists(combine))
                {
                    settingsFile = combine;
                }

                if (string.IsNullOrWhiteSpace(settingsFile))
                {
                    Output.Write(
                        "Could not locate Birch.Swagger.ProxyGenerator.config.json in application directory"
                        + " and no path to the Swagger.WebApiProxy.Generator config file provided.");
                    Output.Write();
                    Output.Write("Exiting Proxy Generator.");
                    return ExitApplication(1);
                }

                var settings = GetSettings(settingsFile);

                // only pull value from config file if not set by switch
                // use default value if nothing provided.
                assemblyFile = string.IsNullOrWhiteSpace(assemblyFile) ? settings.WebApiAssembly : assemblyFile;

                appConfigFile = string.IsNullOrWhiteSpace(appConfigFile)
                    ? settings.WebApiConfig
                    : appConfigFile;
                appConfigFile = string.IsNullOrWhiteSpace(appConfigFile)
                    ? "web.config"
                    : appConfigFile;

                proxyOutputFile = string.IsNullOrWhiteSpace(proxyOutputFile)
                    ? settings.ProxyOutputFile
                    : proxyOutputFile;
                proxyOutputFile = string.IsNullOrWhiteSpace(proxyOutputFile)
                    ? "SwaggerProxy.cs"
                    : proxyOutputFile;

                var proxyGeneratorNamespace = string.IsNullOrWhiteSpace(settings.ProxyGeneratorNamespace)
                    ? "Birch.Swagger.ProxyGenerator"
                    : settings.ProxyGeneratorNamespace;

                var proxyGeneratorClassNamePrefix = string.IsNullOrWhiteSpace(settings.ProxyGeneratorClassNamePrefix)
                    ? "ProxyGenerator"
                    : settings.ProxyGeneratorClassNamePrefix;

                baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? settings.BaseUrl : baseUrl;
                baseUrl = string.IsNullOrWhiteSpace(baseUrl)
                    ? "http://mydomain.com/"
                    : baseUrl;

                // allow relative paths
                appConfigFile = Path.IsPathRooted(appConfigFile)
                    ? appConfigFile
                    : Path.GetFullPath(Path.Combine(baseDirectory, appConfigFile));
                if (!string.IsNullOrWhiteSpace(assemblyFile))
                {
                    assemblyFile = Path.IsPathRooted(assemblyFile)
                        ? assemblyFile
                        : Path.GetFullPath(Path.Combine(baseDirectory, assemblyFile));
                }
                proxyOutputFile = Path.IsPathRooted(proxyOutputFile)
                    ? proxyOutputFile
                    : Path.GetFullPath(Path.Combine(baseDirectory, proxyOutputFile));

                if (settings.AutoRunOnBuildDisabled && isAutoRun)
                {
                    Output.Write("AutoRunOnBuildDisabled has been set to true. Exiting proxy generator.");
                    return ExitApplication(0);
                }

                // nothing to process..
                if (string.IsNullOrWhiteSpace(assemblyFile) && string.IsNullOrWhiteSpace(baseUrl))
                {
                    Output.Write("No baseUrl or path to the WebApi assembly file provided, nothing to process.");
                    Output.Write();
                    Output.Write("Exiting Proxy Generator.");
                    return ExitApplication(1);
                }

                // print settings
                Output.Debug($"Mode: {(string.IsNullOrWhiteSpace(assemblyFile) ? "BaseUrl" : "In Memory")}");
                Output.Debug($"Output: {proxyOutputFile}");
                if (!string.IsNullOrWhiteSpace(assemblyFile))
                {
                    Output.Debug($"Assembly: {assemblyFile}");
                    Output.Debug($"App Config: {appConfigFile}");
                }

                Output.Debug();

                var argumentBuilder = new StringBuilder();
                argumentBuilder.Append($"-SettingsFile \"{settingsFile}\" ");
                argumentBuilder.Append($"-ProxyGeneratorNamespace \"{proxyGeneratorNamespace}\" ");
                argumentBuilder.Append($"-WebApiAssembly \"{assemblyFile}\" ");
                argumentBuilder.Append($"-ProxyGeneratorClassNamePrefix \"{proxyGeneratorClassNamePrefix}\" ");
                argumentBuilder.Append($"-ProxyOutputFile \"{proxyOutputFile}\" ");
                argumentBuilder.Append($"-BaseUrl \"{baseUrl}\" ");
                if (Output.Verbose) argumentBuilder.Append("-verbose ");
                var arguments = argumentBuilder.ToString();

                // copy config file
                var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)
                    ?.Replace(@"file:\", string.Empty) ?? string.Empty;
                var exeConfig = Path.Combine(exeDir, "Birch.Swagger.ProxyGenerator.exe.config");
                Output.Debug($"Copying \"{appConfigFile}\" to \"{exeConfig}\"");
                Output.Debug();
                File.Copy(appConfigFile, exeConfig, true);
                
                // starting process
                const string processName = "Birch.Swagger.ProxyGenerator.exe";
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

        public static SwaggerApiProxySettings GetSettings(string path)
        {
            using (var settingStream = File.OpenRead(path))
            {
                var streamReader = new StreamReader(settingStream);
                var value = streamReader.ReadToEnd();
                return JsonConvert.DeserializeObject<SwaggerApiProxySettings>(value);
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
