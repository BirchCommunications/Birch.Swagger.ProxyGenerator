using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using Birch.Swagger.ProxyGenerator.Generator;

using Microsoft.Owin;
using Microsoft.Owin.Testing;

namespace Birch.Swagger.ProxyGenerator
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Birch.Swagger.ProxyGenerator Started...");
            Console.WriteLine();
            Stopwatch appStopwatch = new Stopwatch();
            appStopwatch.Start();
            try
            {
                // define variables for switches
                var settingsFile = string.Empty;
                var appConfigFile = string.Empty;
                var assemblyFile = string.Empty;
                var proxyOutputFile = string.Empty;
                var baseUrl = string.Empty;
                var angularProxy = false;
                // base directory is exe directory unless switch override
                string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)
                    .Replace(@"file:\", string.Empty);

                // check for switch values
                if (args.Any())
                {
                    for (int i = 0; i < args.Count(); i++)
                    {
                        switch (args[i].ToLower())
                        {
                            case "-settingsfile":
                                settingsFile = args[i + 1];
                                break;
                            case "-webapiassembly":
                                assemblyFile = args[i + 1];
                                break;
                            case "-webapiconfig":
                                appConfigFile = args[i + 1];
                                break;
                            case "-proxyoutputfile":
                                proxyOutputFile = args[i + 1];
                                break;
                            case "-baseurl":
                                baseUrl = args[i + 1];
                                break;
                            case "-basedirectory":
                                baseDirectory = args[i + 1];
                                break;
                            case "-angularproxy":
                                angularProxy = args[i + 1].ToLower() == "true";
                                break;
                        }
                    }
                }

                // Load Proxy Generator Settings
                Console.WriteLine("Loading settings... \n{0}", settingsFile);
                Console.WriteLine("Base Path: {0}", baseDirectory);

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
                    return 1;
                }

                var settings = Generator.ProxyGenerator.GetSettings(settingsFile);
                var endpoints = settings.EndPoints;

                // only pull value from config file if not set by switch
                appConfigFile = string.IsNullOrWhiteSpace(appConfigFile) ? settings.WebApiConfig : appConfigFile;
                assemblyFile = string.IsNullOrWhiteSpace(assemblyFile) ? settings.WebApiAssembly : assemblyFile;
                proxyOutputFile = string.IsNullOrWhiteSpace(proxyOutputFile)
                                      ? settings.ProxyOutputFile
                                      : proxyOutputFile;
                baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? settings.BaseUrl : baseUrl;

                // allow relative paths
                appConfigFile = Path.IsPathRooted(appConfigFile)
                                    ? appConfigFile
                                    : Path.GetFullPath(Path.Combine(baseDirectory, appConfigFile));
                assemblyFile = Path.IsPathRooted(assemblyFile)
                                   ? assemblyFile
                                   : Path.GetFullPath(Path.Combine(baseDirectory, assemblyFile));
                proxyOutputFile = Path.IsPathRooted(proxyOutputFile)
                                      ? proxyOutputFile
                                      : Path.GetFullPath(Path.Combine(baseDirectory, proxyOutputFile));

                // nothing to process..
                if (string.IsNullOrWhiteSpace(assemblyFile) && string.IsNullOrWhiteSpace(baseUrl))
                {
                    Console.WriteLine("No baseUrl or path to the WebApi assembly file provided, nothing to process.");
                    Console.WriteLine();
                    Console.WriteLine("Exiting Proxy Generator.");
                    return 1;
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

                // Run generator against provided assmbly file or baseUrl
                if (!string.IsNullOrWhiteSpace(assemblyFile))
                {
                    var processInMemoryStatus = ProcessInMemory(assemblyFile, appConfigFile, proxyOutputFile, endpoints, angularProxy);
                    if (processInMemoryStatus != 0)
                    {
                        return processInMemoryStatus;
                    }
                }
                else
                {
                    Generator.ProxyGenerator.Generate(proxyOutputFile, endpoints, baseUrl, angularProxy);
                }

                // All done
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine("Proxy generation completed....");
                Console.WriteLine("Time Taken: {0}", appStopwatch.Elapsed.ToString());
                Console.WriteLine();
                return 0;
            }
            catch (AggregateException aex)
            {
                foreach (Exception ex in aex.InnerExceptions)
                {
                    Console.WriteLine("An exception has occured: {1} - {0}", ex.Message, ex.GetType().ToString());
                    Console.WriteLine("StackTrace: {0}", ex.StackTrace);
                    Console.WriteLine();
                }

                Console.WriteLine("Exiting Proxy Generator.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception has occured: {1} - {0}", ex.Message, ex.GetType().ToString());
                Console.WriteLine("StackTrace: {0}", ex.StackTrace);
                Console.WriteLine();
                Console.WriteLine("Exiting Proxy Generator.");
                return 1;
            }
        }

        private static int ProcessInMemory(string assemblyFile, string appConfigFile, string proxyOutputFile, SwaggerApiProxySettingsEndPoint[] endpoints, bool angularProxy)
        {
            string exeBinDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)
                                         .Replace(@"file:\", string.Empty) + @"\bin";

            Console.WriteLine("Copying assembly xml comments to executable bin directory... \n{0}", exeBinDirectory);
            Console.WriteLine();
            try
            {
                var sourcePath = assemblyFile.Replace(".dll", ".xml");
                var destFileName = Path.Combine(exeBinDirectory, Path.GetFileName(sourcePath));
                if (!Directory.Exists(exeBinDirectory))
                {
                    Directory.CreateDirectory(exeBinDirectory);
                }
                File.Copy(sourcePath, destFileName, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not copy assembly xml file. Exception: {0}", ex.Message);
                Console.WriteLine();
            }

            Console.WriteLine("Loading app.config... \n{0}", appConfigFile);
            Console.WriteLine();
            AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", appConfigFile);
            ResetConfigMechanism();

            Console.WriteLine("Loading Owin Web API Assembly... \n{0}", assemblyFile);
            Console.WriteLine();
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler((source, e) => CustomResolver(source, e, assemblyFile));
            var assembly = Assembly.LoadFrom(assemblyFile);

            Type owinStartupClassType = null;
            var startupAttribute = assembly.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(OwinStartupAttribute));
            Console.WriteLine("Locating Startup Class... \n{0}", startupAttribute.ConstructorArguments.First().Value.ToString());
            Console.WriteLine();

            if (startupAttribute == null)
            {
                Console.WriteLine("Could not located OwinStartupAttribute.");
                return 1;
            }

            Console.WriteLine("Starting in memory server...");
            Console.WriteLine();
            owinStartupClassType = (Type)startupAttribute.ConstructorArguments.First().Value;
            dynamic owinStartupClass = Activator.CreateInstance(owinStartupClassType);
            TestServer testServer = TestServer.Create(builder => { owinStartupClass.Configuration(builder); });

            Console.WriteLine("Generating Proxy...");
            Generator.ProxyGenerator.Generate(proxyOutputFile, endpoints, testServer, angularProxy);
            return 0;
        }

        // ReSharper disable once UnusedParameter.Local
        static Assembly CustomResolver(object source, ResolveEventArgs e, string assemblyFile)
        {
            var name = string.Format("{0}.dll", e.Name.Split(',')[0]);
            var searchPath = string.Format("{1}\\{0}", name, Path.GetDirectoryName(assemblyFile));
            Console.WriteLine("Resolving {0}", e.Name);
            Assembly assembly = Assembly.LoadFrom(searchPath);
            return assembly;
        }

        private static void ResetConfigMechanism()
        {
            typeof(ConfigurationManager).GetField("s_initState", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, 0);

            typeof(ConfigurationManager).GetField("s_configSystem", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, null);

            typeof(ConfigurationManager).Assembly.GetTypes()
                .First(x => x.FullName == "System.Configuration.ClientConfigPaths")
                .GetField("s_current", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, null);
        }
    }
}
