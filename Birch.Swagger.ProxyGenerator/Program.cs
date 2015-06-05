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
                        }
                    }
                }

                try
                {
                    string exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace(@"file:\", string.Empty);
                    var combine = Path.Combine(exeDirectory, "Birch.Swagger.ProxyGenerator.config.json");
                    if (string.IsNullOrWhiteSpace(settingsFile) && File.Exists(combine))
                    {
                        settingsFile = combine;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Could not locate Birch.Swagger.ProxyGenerator.config.json in application directory.");
                }

                if (string.IsNullOrWhiteSpace(settingsFile))
                {
                    Console.WriteLine("Could not locate Birch.Swagger.ProxyGenerator.config.json in application directory.");
                    Console.WriteLine("No path to the Swagger.WebApiProxy.Generator config file provided. Exiting.");
                    return 1;
                }

                Console.WriteLine("Loading settings... \n{0}", settingsFile);
                Console.WriteLine();
                var settings = Generator.ProxyGenerator.GetSettings(settingsFile);
                var endpoints = settings.EndPoints;

                appConfigFile = string.IsNullOrWhiteSpace(appConfigFile) ? settings.WebApiConfig : appConfigFile;
                assemblyFile = string.IsNullOrWhiteSpace(assemblyFile) ? settings.WebApiAssembly : assemblyFile;
                proxyOutputFile = string.IsNullOrWhiteSpace(proxyOutputFile) ? settings.ProxyOutputFile : proxyOutputFile;
                baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? settings.BaseUrl : baseUrl;

                if (string.IsNullOrWhiteSpace(assemblyFile) && string.IsNullOrWhiteSpace(baseUrl))
                {
                    Console.WriteLine("No baseUrl or path to the WebApi assembly file provided. Exiting.");
                    return 1;
                }

                if (!string.IsNullOrWhiteSpace(assemblyFile))
                {
                    var processInMemoryStatus = ProcessInMemory(assemblyFile, appConfigFile, proxyOutputFile, endpoints);
                    if (processInMemoryStatus != 0)
                    {
                        return processInMemoryStatus;
                    }
                }
                else
                {
                    Generator.ProxyGenerator.Generate(proxyOutputFile, endpoints, baseUrl);
                }

                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine("Proxy generation completed....");
                Console.WriteLine("Time Taken: {0}", appStopwatch.Elapsed.ToString());
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception has occured: {0}", ex.Message);
                Console.WriteLine("StackTrace: {0}", ex.StackTrace);
                return 1;
            }
        }

        private static int ProcessInMemory(string assemblyFile, string appConfigFile, string proxyOutputFile, SwaggerApiProxySettingsEndPoint[] endpoints)
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
            Generator.ProxyGenerator.Generate(proxyOutputFile, endpoints, testServer);
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
