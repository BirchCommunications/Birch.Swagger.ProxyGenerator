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
    public static class Output
    {
        public static bool Verbose { get; set; }
        public static void Write(string s = null) => Console.WriteLine(s);
        public static void Debug(string s = null)
        {
            if (Verbose) Write(s);
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class Program
    {
        static int Main(string[] args)
        {
            Output.Debug("Birch.Swagger.ProxyGenerator Started...");
            Output.Debug();
            
            // get settings
            var settings = Generator.ProxyGenerator.GetSettings(args);

            var appStopwatch = new Stopwatch();
            appStopwatch.Start();
            try
            {
                // Run generator against provided assmbly file or baseUrl
                if (!string.IsNullOrWhiteSpace(settings.WebApiAssembly))
                {
                    var processInMemoryStatus = ProcessInMemory(settings);
                    if (processInMemoryStatus != 0)
                    {
                        return ExitApplication(processInMemoryStatus);
                    }
                }
                else
                {
                    Generator.ProxyGenerator.Generate(settings);
                }

                // All done
                Output.Write();
                Output.Write("----------------------------------------------------");
                Output.Write("Proxy generation completed....");
                Output.Write($"Time Taken: {appStopwatch.Elapsed}");
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

        private static int ProcessInMemory(SwaggerApiProxySettings settings)
        {
            var directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            var exeBinDirectory = directoryName?.Replace(@"file:\", string.Empty) + @"\bin";

            Output.Debug($"Copying assembly xml comments to executable bin directory... \n{exeBinDirectory}");
            Output.Debug();
            var assemblyFile = settings.WebApiAssembly;
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
                Output.Write($"Could not copy assembly xml file. Exception: {ex.Message}");
                Output.Write();
            }

            Output.Debug($"Loading Owin Web API Assembly... \n{assemblyFile}");
            Output.Debug();
            AppDomain.CurrentDomain.AssemblyResolve +=
                (source, e) => CustomResolver(source, e, assemblyFile);
            var assembly = Assembly.LoadFrom(assemblyFile);

            var startupAttribute = assembly.CustomAttributes
                .FirstOrDefault(x => x.AttributeType == typeof(OwinStartupAttribute));
            if (startupAttribute == null)
            {
                Output.Write("Could not locate OWIN startup class.");
                return 1;
            }

            Output.Write("Starting in memory server...");
            
            var owinStartupClassType = (Type)startupAttribute.ConstructorArguments.First().Value;
            dynamic owinStartupClass = Activator.CreateInstance(owinStartupClassType);
            var testServer = TestServer.Create(builder => { owinStartupClass.Configuration(builder); });

            Output.Debug();
            Output.Debug("Generating Proxy...");
            Generator.ProxyGenerator.Generate(testServer, settings);
            return 0;
        }
        
        // ReSharper disable once UnusedParameter.Local
        static Assembly CustomResolver(object source, ResolveEventArgs e, string assemblyFile)
        {
            var name = $"{e.Name.Split(',')[0]}.dll";
            if (name.EndsWith(".XmlSerializers.dll"))
                return null;
            var searchPath = string.Format("{1}\\{0}", name, Path.GetDirectoryName(assemblyFile));
            Output.Debug($"Resolving {e.Name}");
            Assembly assembly;
            try
            {
                Output.Debug($"Trying: {searchPath}" );
                assembly = Assembly.LoadFrom(searchPath);
            }
            catch (Exception)
            {
                Output.Debug($"Returning null for assembly: {name}" );
                assembly = null;
            }

            return assembly;
        }
    }
}
