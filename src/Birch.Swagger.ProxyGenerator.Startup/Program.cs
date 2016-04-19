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
            Console.WriteLine("Starting Birch.Swagger.ProxyGenerator...");
            Console.WriteLine();
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
                    }
                }

                // Load Proxy Generator Settings
                Console.WriteLine("Loading settings... \n{0}", settingsFile);
                if (baseDirectory == null)
                {
                    Console.WriteLine("Could not determine base directory,");
                    return ExitApplication(1);
                }
                Console.WriteLine("Base Directory: {0}", baseDirectory);
                var combine = Path.Combine(baseDirectory, "Birch.Swagger.ProxyGenerator.config.json");
                if (string.IsNullOrWhiteSpace(settingsFile) && File.Exists(combine))
                {
                    settingsFile = combine;
                }

                if (string.IsNullOrWhiteSpace(settingsFile))
                {
                    Console.WriteLine(
                        "Could not locate Birch.Swagger.ProxyGenerator.config.json in application directory"
                        + " and no path to the Swagger.WebApiProxy.Generator config file provided.");
                    Console.WriteLine();
                    Console.WriteLine("Exiting Proxy Generator.");
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
                    Console.WriteLine("AutoRunOnBuildDisabled has been set to true. Exiting proxy generator.");
                    return ExitApplication(0);
                }

                // nothing to process..
                if (string.IsNullOrWhiteSpace(assemblyFile) && string.IsNullOrWhiteSpace(baseUrl))
                {
                    Console.WriteLine("No baseUrl or path to the WebApi assembly file provided, nothing to process.");
                    Console.WriteLine();
                    Console.WriteLine("Exiting Proxy Generator.");
                    return ExitApplication(1);
                }

                // print settings
                Console.WriteLine("Mode: {0}", string.IsNullOrWhiteSpace(assemblyFile) ? "BaseUrl" : "In Memory");
                Console.WriteLine("Output: {0}", proxyOutputFile);
                if (!string.IsNullOrWhiteSpace(assemblyFile))
                {
                    Console.WriteLine("Assembly: {0}", assemblyFile);
                    Console.WriteLine("App Config: {0}", appConfigFile);
                }

                Console.WriteLine();

                var argumentBuilder = new StringBuilder();
                argumentBuilder.Append($"-SettingsFile \"{settingsFile}\" ");
                argumentBuilder.Append($"-ProxyGeneratorNamespace \"{proxyGeneratorNamespace}\" ");
                argumentBuilder.Append($"-WebApiAssembly \"{assemblyFile}\" ");
                argumentBuilder.Append($"-ProxyGeneratorClassNamePrefix \"{proxyGeneratorClassNamePrefix}\" ");
                argumentBuilder.Append($"-ProxyOutputFile \"{proxyOutputFile}\" ");
                argumentBuilder.Append($"-BaseUrl \"{baseUrl}\" ");
                var arguments = argumentBuilder.ToString();

                // copy config file
                var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)
                    ?.Replace(@"file:\", string.Empty) ?? string.Empty;
                var exeConfig = Path.Combine(exeDir, "Birch.Swagger.ProxyGenerator.exe.config");
                Console.WriteLine("Copying \"{0}\" to \"{1}\"", appConfigFile, exeConfig);
                Console.WriteLine();
                File.Copy(appConfigFile, exeConfig, true);

                var process = new Process
                {
                    StartInfo =
                        {
                            FileName = "Birch.Swagger.ProxyGenerator.exe",
                            Arguments = arguments,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                };
                process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
                process.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                Console.WriteLine("Removing \"{0}\"", exeConfig);

                File.Delete("Birch.Swagger.ProxyGenerator.exe.config");

                return ExitApplication(0);
            }
            catch (AggregateException aex)
            {
                foreach (Exception ex in aex.InnerExceptions)
                {
                    Console.WriteLine("An exception has occured: {1} - {0}", ex.Message, ex.GetType());
                    Console.WriteLine("StackTrace: {0}", ex.StackTrace);
                    Console.WriteLine();
                }

                Console.WriteLine("Exiting Proxy Generator.");
                return ExitApplication(1);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception has occured: {1} - {0}", ex.Message, ex.GetType());
                Console.WriteLine("StackTrace: {0}", ex.StackTrace);
                Console.WriteLine();
                Console.WriteLine("Exiting Proxy Generator.");
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

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            return exitCode;
        }
    }
}
