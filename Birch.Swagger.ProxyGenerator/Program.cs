using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using Birch.Swagger.ProxyGenerator.Generator;

using Microsoft.Owin;
using Microsoft.Owin.Testing;
using Newtonsoft.Json;

namespace Birch.Swagger.ProxyGenerator
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Birch.Swagger.ProxyGenerator Started...");
            Console.WriteLine();
            // check for switch values
            var assemblyFile = string.Empty;
            var proxyOutputFile = string.Empty;
            var baseUrl = string.Empty;
            var proxyGeneratorNamespace = string.Empty;
            var proxyGeneratorClassNamePrefix = string.Empty;
            var settingsFile = string.Empty;
            if (args.Any())
            {
                for (var i = 0; i < args.Length; i++)
                {
                    var argument = args[i].ToLower();
                    if (argument == "-settingsfile")
                    {
                        settingsFile = args[i + 1];
                    }
                    else if(argument == "-proxygeneratornamespace")
                    {
                        proxyGeneratorNamespace = args[i + 1];
                    }
                    else if (argument == "-webapiassembly")
                    {
                        assemblyFile = args[i + 1];
                    }
                    else if (argument == "-proxygeneratorclassnameprefix")
                    {
                        proxyGeneratorClassNamePrefix = args[i + 1];
                    }
                    else if (argument == "-proxyoutputfile")
                    {
                        proxyOutputFile = args[i + 1];
                    }
                    else if (argument == "-baseurl")
                    {
                        baseUrl = args[i + 1];
                    }
                }
            }

            var settings = GetSettings(settingsFile);
            var endpoints = settings.EndPoints;

            Stopwatch appStopwatch = new Stopwatch();
            appStopwatch.Start();
            try
            {
                // Run generator against provided assmbly file or baseUrl
                if (!string.IsNullOrWhiteSpace(assemblyFile))
                {
                    var processInMemoryStatus = ProcessInMemory(assemblyFile, proxyOutputFile, endpoints, proxyGeneratorNamespace, baseUrl, proxyGeneratorClassNamePrefix);
                    if (processInMemoryStatus != 0)
                    {
                        return ExitApplication(processInMemoryStatus);
                    }
                }
                else
                {
                    Generator.ProxyGenerator.Generate(proxyOutputFile, endpoints, baseUrl, proxyGeneratorNamespace, proxyGeneratorClassNamePrefix);
                }

                // All done
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine("Proxy generation completed....");
                Console.WriteLine("Time Taken: {0}", appStopwatch.Elapsed);
                Console.WriteLine();
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

        private static int ProcessInMemory(string assemblyFile, string proxyOutputFile, SwaggerApiProxySettingsEndPoint[] endpoints, string proxyGeneratorNamespace, string baseUrl, string proxyGeneratorClassNamePrefix)
        {
            var directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            var exeBinDirectory = directoryName?.Replace(@"file:\", string.Empty) + @"\bin";

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

            Console.WriteLine("Loading Owin Web API Assembly... \n{0}", assemblyFile);
            Console.WriteLine();
            AppDomain.CurrentDomain.AssemblyResolve +=
                (source, e) => CustomResolver(source, e, assemblyFile);
            var assembly = Assembly.LoadFrom(assemblyFile);

            var startupAttribute = assembly.CustomAttributes
                .FirstOrDefault(x => x.AttributeType == typeof(OwinStartupAttribute));
            if (startupAttribute == null)
            {
                Console.WriteLine("Could not locate OWIN startup class.");
                return 1;
            }

            Console.WriteLine("Starting in memory server...");
            Console.WriteLine();
            var owinStartupClassType = (Type)startupAttribute.ConstructorArguments.First().Value;
            dynamic owinStartupClass = Activator.CreateInstance(owinStartupClassType);
            var testServer = TestServer.Create(builder => { owinStartupClass.Configuration(builder); });

            Console.WriteLine("Generating Proxy...");
            Generator.ProxyGenerator.Generate(proxyOutputFile, endpoints, testServer, proxyGeneratorNamespace, baseUrl, proxyGeneratorClassNamePrefix);
            return 0;
        }

        public static SwaggerApiProxySettings GetSettings(string path)
        {
            Console.WriteLine("Getting settings from: {0}", path);
            using (var settingStream = File.OpenRead(path))
            {
                var streamReader = new StreamReader(settingStream);
                var value = streamReader.ReadToEnd();
                return JsonConvert.DeserializeObject<SwaggerApiProxySettings>(value);
            }
        }

        // ReSharper disable once UnusedParameter.Local
        static Assembly CustomResolver(object source, ResolveEventArgs e, string assemblyFile)
        {
            var name = $"{e.Name.Split(',')[0]}.dll";
            if (name.EndsWith(".XmlSerializers.dll"))
                return null;
            var searchPath = string.Format("{1}\\{0}", name, Path.GetDirectoryName(assemblyFile));
            Console.WriteLine("Resolving {0}", e.Name);
            Assembly assembly;
            try
            {
                Console.WriteLine("Trying: {0}", searchPath);
                assembly = Assembly.LoadFrom(searchPath);
            }
            catch (Exception)
            {
                Console.WriteLine("Returning null for assembly: {0}", name);
                assembly = null;
            }

            return assembly;
        }
    }
}
