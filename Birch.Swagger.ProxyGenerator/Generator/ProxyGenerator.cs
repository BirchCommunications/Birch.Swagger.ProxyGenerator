﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Birch.Swagger.ProxyGenerator.Swagger;

using Microsoft.CSharp;
using Microsoft.Owin.Testing;

using Newtonsoft.Json;

namespace Birch.Swagger.ProxyGenerator.Generator
{
    internal static class ProxyGenerator
    {
        private static StringBuilder FileText { get; set; }

        private static Dictionary<SwaggerApiProxySettingsEndPoint, string> SwaggerDocDictionaryList { get; set; }

        public static void Generate(string proxyOutputFile, SwaggerApiProxySettingsEndPoint[] endpoints, string baseUrl)
        {
            // init
            SwaggerDocDictionaryList = new Dictionary<SwaggerApiProxySettingsEndPoint, string>();
            FileText = new StringBuilder();

            Console.WriteLine();
            Console.WriteLine("Requesting Swagger documents..");
            List<Task> taskList = new List<Task>();
            foreach (var endPoint in endpoints)
            {
                var requestUri = endPoint.Url.StartsWith(baseUrl)
                                     ? endPoint.Url
                                     : string.Format("{0}{1}", baseUrl, endPoint.Url);
                Console.WriteLine("Requested: {0}", requestUri);
                taskList.Add(GetEndpointSwaggerDoc(requestUri, endPoint));
            }

            Console.WriteLine();
            Console.WriteLine("Waiting for Swagger documents to complete downloading...");
            Task.WaitAll(taskList.ToArray());

            ProcessSwaggerDocuments(proxyOutputFile);

        }

        public static void Generate(string proxyOutputFile, SwaggerApiProxySettingsEndPoint[] endpoints, TestServer testServer)
        {
            // init
            SwaggerDocDictionaryList = new Dictionary<SwaggerApiProxySettingsEndPoint, string>();
            FileText = new StringBuilder();

            Console.WriteLine();
            Console.WriteLine("Requesting Swagger documents..");
            List<Task> taskList = new List<Task>();
            foreach (var endPoint in endpoints)
            {
                Console.WriteLine("Requested: {0}", endPoint.Url);
                taskList.Add(GetEndpointSwaggerDoc(testServer, endPoint));
            }

            Console.WriteLine();
            Console.WriteLine("Waiting for Swagger documents to complete downloading...");
            Task.WaitAll(taskList.ToArray());

            ProcessSwaggerDocuments(proxyOutputFile);
        }

        private static void ProcessSwaggerDocuments(string proxyOutputFile)
        {
            Console.WriteLine();
            Console.WriteLine("Processing Swagger documents...");
            PrintHeaders();
            foreach (var swaggerDocDictionaryEntry in SwaggerDocDictionaryList)
            {
                var endPoint = swaggerDocDictionaryEntry.Key;
                Console.WriteLine("Processing {0}", endPoint.Url);
                WriteLine(string.Format("// {0} Proxy", endPoint.Url));

                string result;
                string methodNameAppend = string.Empty;
                if (endPoint.AppendAsyncToMethodName)
                {
                    methodNameAppend = "Async";
                }

                result = swaggerDocDictionaryEntry.Value;
                var parser = new SwaggerParser();
                var proxyDefinition = parser.ParseSwaggerDoc(result, endPoint.ParseOperationIdForProxyName);

                WriteLine(string.Format("namespace {0} {{", endPoint.Namespace));

                var proxies = proxyDefinition.Operations.Select(i => i.ProxyName).Distinct();
                foreach (var proxy in proxies)
                {
                    // start class defintion
                    WriteLine("/// <summary>");
                    WriteLine(string.Format("/// Web Proxy for {0}", proxy));
                    WriteLine("/// </summary>");
                    WriteLine(
                        string.Format(
                            "public class {0} : {1}",
                            SwaggerParser.FixTypeName(proxy) + "WebProxy",
                            endPoint.BaseProxyClass));
                    WriteLine("{");

                    WriteLine(
                        string.Format(
                            "public {0}{1}",
                            SwaggerParser.FixTypeName(proxy) + "WebProxy",
                            endPoint.ProxyConstructorSuffix));
                    WriteLine("{}");

                    WriteLine(@"// helper function for building uris.");
                    WriteLine(@"private string AppendQuery(string currentUrl, string paramName, string value)");
                    WriteLine(@"{");
                    WriteLine(@"if (currentUrl.Contains(""?""))");
                    WriteLine(@"currentUrl += string.Format(""&{0}={1}"", paramName, Uri.EscapeUriString(value));");
                    WriteLine(@"else");
                    WriteLine(@"currentUrl += string.Format(""?{0}={1}"", paramName, Uri.EscapeUriString(value));");
                    WriteLine(@"return currentUrl;");
                    WriteLine(@"}");
                    WriteLine();
                    List<Enum> proxyParamEnums = new List<Enum>();

                    // Async operations (web methods)
                    foreach (var operationDef in proxyDefinition.Operations.Where(i => i.ProxyName.Equals(proxy)))
                    {
                        string returnType = string.IsNullOrEmpty(operationDef.ReturnType)
                                                ? string.Empty
                                                : string.Format("<{0}>", operationDef.ReturnType);
                        var enums = operationDef.Parameters.Where(i => i.Type.EnumValues != null);
                        if (enums != null)
                        {
                            foreach (var enumParam in enums)
                            {
                                enumParam.Type.TypeName = operationDef.OperationId + enumParam.Type.Name;
                                proxyParamEnums.Add(
                                    new Enum() { Name = enumParam.Type.TypeName, Values = enumParam.Type.EnumValues });
                            }
                        }

                        string parameters = string.Join(
                            ", ",
                            operationDef.Parameters.OrderByDescending(i => i.IsRequired)
                                .Select(
                                    x =>
                                    (x.IsRequired == false)
                                        ? string.Format(
                                            "{0} {1} = {2}",
                                            GetDefaultType(x),
                                            x.Type.GetCleanTypeName(),
                                            GetDefaultValue(x))
                                        : string.Format("{0} {1}", x.Type.TypeName, x.Type.GetCleanTypeName())));

                        WriteLine("/// <summary>");
                        WriteLine(
                            string.Format(
                                "/// {0}",
                                (SecurityElement.Escape(operationDef.Description) ?? "").Replace("\n", "\n///")));
                        WriteLine("/// </summary>");
                        foreach (var parameter in operationDef.Parameters)
                        {
                            WriteLine(
                                string.Format(
                                    "/// <param name=\"{0}\">{1}</param>",
                                    parameter.Type.Name,
                                    (SecurityElement.Escape(parameter.Description) ?? "").Replace("\n", "\n///")));
                        }

                        WriteLine(
                            string.Format(
                                "public async Task{0} {1}{2} ({3})",
                                returnType,
                                SwaggerParser.FixTypeName(operationDef.OperationId),
                                methodNameAppend,
                                parameters));
                        WriteLine("{");

                        if (operationDef.Path.StartsWith("/"))
                        {
                            WriteLine(string.Format("var url = \"{0}\"", operationDef.Path.Substring(1)));
                        }
                        else
                        {
                            WriteLine(string.Format("var url = \"{0}\"", operationDef.Path));
                        }

                        foreach (var parameter in operationDef.Parameters.Where(i => i.ParameterIn == ParameterIn.Path))
                        {
                            WriteLine("");
                            WriteLine(
                                string.Format("\t.Replace(\"{{{0}}}\", {0}.ToString())", parameter.Type.GetCleanTypeName()));
                        }
                        WriteLine(";");

                        var queryParams = operationDef.Parameters.Where(i => i.ParameterIn == ParameterIn.Query).ToList();
                        if (queryParams != null && queryParams.Count > 0)
                        {
                            foreach (var parameter in queryParams)
                            {
                                if (parameter.IsRequired == false)
                                {
                                    WriteNullIfStatementOpening(parameter.Type.GetCleanTypeName(), parameter.Type.TypeName);
                                }

                                if (string.IsNullOrWhiteSpace(parameter.CollectionFormat) == false)
                                {
                                    // array
                                    if (parameter.CollectionFormat.Equals("csv", StringComparison.OrdinalIgnoreCase))
                                    {
                                        WriteLine(
                                            string.Format(
                                                "url = AppendQuery(url, \"{0}\", string.Join(\",\", {1}));",
                                                parameter.Type.Name,
                                                parameter.Type.GetCleanTypeName()));
                                    }
                                    else if (parameter.CollectionFormat.Equals("ssv", StringComparison.OrdinalIgnoreCase))
                                    {
                                        WriteLine(
                                            string.Format(
                                                "url = AppendQuery(url, \"{0}\", string.Join(\" \", {1}));",
                                                parameter.Type.Name,
                                                parameter.Type.GetCleanTypeName()));
                                    }
                                    else if (parameter.CollectionFormat.Equals("tsv", StringComparison.OrdinalIgnoreCase))
                                    {
                                        WriteLine(
                                            string.Format(
                                                "url = AppendQuery(url, \"{0}\", string.Join(\"\t\", {1}));",
                                                parameter.Type.Name,
                                                parameter.Type.GetCleanTypeName()));
                                    }
                                    else if (parameter.CollectionFormat.Equals("pipes", StringComparison.OrdinalIgnoreCase))
                                    {
                                        WriteLine(
                                            string.Format(
                                                "url = AppendQuery(url, \"{0}\", string.Join(\"\t\", {1}));",
                                                parameter.Type.Name,
                                                parameter.Type.GetCleanTypeName()));
                                    }
                                    else if (parameter.CollectionFormat.Equals("multi", StringComparison.OrdinalIgnoreCase))
                                    {
                                        WriteLine(string.Format("foreach(var item in {0})", parameter.Type.GetCleanTypeName()));
                                        WriteLine("{");
                                        WriteLine(
                                            string.Format(
                                                "url = AppendQuery(url, \"{0}\", item.ToString());",
                                                parameter.Type.Name));
                                        WriteLine("}");
                                    }
                                    else
                                    {
                                        //Warning("unknown collection format found");
                                        WriteLine(
                                            string.Format(
                                                "url = AppendQuery(url, \"{0}\", {1}.ToString());",
                                                parameter.Type.Name,
                                                parameter.Type.GetCleanTypeName()));
                                    }
                                }
                                else
                                {
                                    WriteLine(
                                        string.Format(
                                            "url = AppendQuery(url, \"{0}\", {1}.ToString());",
                                            parameter.Type.Name,
                                            parameter.Type.GetCleanTypeName()));
                                }

                                if (parameter.IsRequired == false)
                                {
                                    WriteLine("}");
                                }
                            }
                        }

                        WriteLine("");
                        WriteLine("using (var client = BuildHttpClient())");
                        WriteLine("{");
                        switch (operationDef.Method.ToUpperInvariant())
                        {
                            case "GET":
                                WriteLine("var response = await client.GetAsync(url).ConfigureAwait(false);");
                                break;

                            case "DELETE":
                                WriteLine("var response = await client.DeleteAsync(url).ConfigureAwait(false);");
                                break;

                            case "PUT":
                                var putBodyParam = operationDef.Parameters.FirstOrDefault(
                                    i => i.ParameterIn == ParameterIn.Body);
                                if (putBodyParam != null)
                                {
                                    WriteLine(
                                        string.Format(
                                            "var response = await client.PutAsJsonAsync(url, {0}).ConfigureAwait(false);",
                                            putBodyParam.Type.Name));
                                }
                                else if (operationDef.Parameters.Any(i => i.ParameterIn == ParameterIn.FormData))
                                {
                                    var formData =
                                        operationDef.Parameters.Where(i => i.ParameterIn == ParameterIn.FormData).ToList();
                                    WriteLine("var formKeyValuePairs = new List<KeyValuePair<string, string>>();");
                                    foreach (var formParam in formData.Where(x => x.Type.TypeName != "file"))
                                    {
                                        if (formParam.IsRequired == false)
                                        {
                                            WriteNullIfStatementOpening(formParam.Type.Name, formParam.Type.TypeName);
                                        }
                                        WriteLine(
                                            string.Format(
                                                "formKeyValuePairs.Add(new KeyValuePair<string, string>(\"{0}\", {0}));",
                                                formParam.Type.Name));
                                        if (formParam.IsRequired == false)
                                        {
                                            WriteLine("}");
                                        }
                                    }
                                    foreach (var formParam in formData.Where(x => x.Type.TypeName == "file"))
                                    {
                                        WriteLine(
                                            string.Format(
                                                "var fileContent = new ByteArrayContent({0}.Item1);",
                                                formParam.Type.Name));
                                        WriteLine(
                                            string.Format(
                                                "fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue(\"attachment\") { FileName = \"{0}.Item2\" };",
                                                formParam.Type.Name));
                                    }
                                    WriteLine("HttpContent content = new FormUrlEncodedContent(formKeyValuePairs);");
                                    WriteLine("var response = await client.PutAsync(url, content).ConfigureAwait(false);");
                                }
                                else
                                {
                                    WriteLine(
                                        "var response = await client.PutAsync(url, new StringContent(string.Empty)).ConfigureAwait(false);");
                                }

                                break;

                            case "POST":
                                var postBodyParam =
                                    operationDef.Parameters.FirstOrDefault(i => i.ParameterIn == ParameterIn.Body);
                                if (postBodyParam != null)
                                {
                                    WriteLine(
                                        string.Format(
                                            "var response = await client.PostAsJsonAsync(url, {0}).ConfigureAwait(false);",
                                            postBodyParam.Type.Name));
                                }
                                else if (operationDef.Parameters.Any(i => i.ParameterIn == ParameterIn.FormData))
                                {
                                    var formData =
                                        operationDef.Parameters.Where(i => i.ParameterIn == ParameterIn.FormData).ToList();
                                    WriteLine("var formKeyValuePairs = new List<KeyValuePair<string, string>>();");
                                    foreach (var formParam in formData.Where(x => x.Type.TypeName != "file"))
                                    {
                                        if (formParam.IsRequired == false)
                                        {
                                            WriteNullIfStatementOpening(formParam.Type.Name, formParam.Type.TypeName);
                                        }
                                        WriteLine(
                                            string.Format(
                                                "formKeyValuePairs.Add(new KeyValuePair<string, string>(\"{0}\", {0}));",
                                                formParam.Type.Name));
                                        if (formParam.IsRequired == false)
                                        {
                                            WriteLine("}");
                                        }
                                    }
                                    foreach (var formParam in formData.Where(x => x.Type.TypeName == "file"))
                                    {
                                        WriteLine(
                                            string.Format(
                                                "var {0}Content = new ByteArrayContent({0}.Item2);",
                                                formParam.Type.Name));
                                        WriteLine(
                                            string.Format(
                                                "{0}Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(\"attachment\") {{ FileName = {0}.Item1 }};",
                                                formParam.Type.Name));
                                    }
                                    WriteLine("HttpContent content = new FormUrlEncodedContent(formKeyValuePairs);");
                                    WriteLine("var response = await client.PostAsync(url, content).ConfigureAwait(false);");
                                }
                                else
                                {
                                    WriteLine(
                                        "var response = await client.PostAsync(url, new StringContent(string.Empty)).ConfigureAwait(false);");
                                }

                                break;

                            default:
                                break;
                        }
                        WriteLine("await EnsureSuccessStatusCodeAsync(response);");

                        if (string.IsNullOrWhiteSpace(operationDef.ReturnType) == false)
                        {
                            WriteLine(
                                string.Format(
                                    "return await response.Content.ReadAsAsync<{0}>().ConfigureAwait(false);",
                                    operationDef.ReturnType));
                        }

                        WriteLine("}"); // close up the using
                        WriteLine("}"); // close up the method
                    }

                    foreach (var proxyParamEnum in proxyParamEnums)
                    {
                        WriteLine(string.Format("public enum {0}", SwaggerParser.FixTypeName(proxyParamEnum.Name)));
                        WriteLine("{");
                        foreach (var value in proxyParamEnum.Values.Distinct())
                        {
                            WriteLine(string.Format("{0},", SwaggerParser.FixTypeName(value)));
                        }
                        WriteLine("}");
                    }

                    // close class def
                    WriteLine("}");
                }

                // Model Classes
                foreach (var classDef in proxyDefinition.ClassDefinitions)
                {
                    List<Enum> modelEnums = new List<Enum>();

                    WriteLine(
                        string.Format(
                            "public class {0} {1}",
                            classDef.Name,
                            string.IsNullOrEmpty(classDef.Inherits) ? string.Empty : string.Format(": {0}", classDef.Inherits)));
                    WriteLine("{");
                    ;
                    foreach (var prop in classDef.Properties)
                    {
                        WriteLine(string.Format("public {0} {1} {{ get; set; }}", prop.TypeName, prop.Name));
                        if (prop.EnumValues != null)
                        {
                            modelEnums.Add(new Enum() { Name = prop.TypeName, Values = prop.EnumValues });
                        }
                    }

                    foreach (var modelEnum in modelEnums)
                    {
                        var csharpCodeProvider = new CSharpCodeProvider();
                        WriteLine(string.Format("public enum {0}", csharpCodeProvider.CreateValidIdentifier(modelEnum.Name)));
                        WriteLine("{");
                        foreach (var value in modelEnum.Values.Distinct())
                        {
                            WriteLine(string.Format("{0},", SwaggerParser.FixTypeName(value)));
                        }
                        WriteLine("}");
                    }

                    WriteLine("}");
                }

                // close namespace def
                WriteLine("}");
                File.WriteAllText(proxyOutputFile, FileText.ToString());
            }
        }

        private static async Task GetEndpointSwaggerDoc(TestServer testServer, SwaggerApiProxySettingsEndPoint endPoint)
        {
            var swaggerString = await testServer.HttpClient.GetStringAsync(endPoint.Url);
            Console.WriteLine("Downloaded: {0}", endPoint.Url);
            SwaggerDocDictionaryList.Add(endPoint, swaggerString);
        }

        private static async Task GetEndpointSwaggerDoc(string requestUri, SwaggerApiProxySettingsEndPoint endPoint)
        {
            using (var httpClient = new HttpClient())
            {
                var swaggerString = await httpClient.GetStringAsync(requestUri);
                Console.WriteLine("Downloaded: {0}", requestUri);
                SwaggerDocDictionaryList.Add(endPoint, swaggerString);
            }
        }

        static string GetDefaultType(Parameter x)
        {
            var typeName = x.Type.TypeName;
            if (typeName == "file")
            {
                return "Tuple<string, byte[]>";
            }

            if (x.Type.IsNullableType)
                typeName += "?";

            var output = x.CollectionFormat == "multi" ? string.Format("List<{0}>", typeName) : typeName;
            return output;
        }

        static string GetDefaultValue(Parameter x)
        {
            if (!x.Type.IsNullableType && x.CollectionFormat != "multi" && x.Type.EnumValues != null && x.Type.EnumValues.Any())
            {
                return string.Format("{0}.{1}", x.Type.TypeName, x.Type.EnumValues.FirstOrDefault());
            }

            return "null";
        }

        static void PrintHeaders()
        {
            WriteLine("// This file was generated by Birch.Swagger.ProxyGenerator");
            WriteLine(string.Format("// Generate Date: {0}", DateTime.Now));
            WriteLine();
            WriteLine("using System;");
            WriteLine("using System.Collections.Generic;");
            WriteLine("using System.Threading.Tasks;");
            WriteLine("using System.Net.Http;");
            //WriteLine("using System.Net.Http.Headers;");
            WriteLine();
            WriteLine("// ReSharper disable All");
            WriteLine();
        }

        static void WriteNullIfStatementOpening(string parameterName, string typeName)
        {
            if (IsIntrinsicType(typeName))
            {
                WriteLine(string.Format("if ({0}.HasValue){{", parameterName));
            }
            else
            {
                WriteLine(string.Format("if ({0} != null){{", parameterName));
            }
        }

        static bool IsIntrinsicType(string typeName)
        {
            switch (typeName.ToLowerInvariant())
            {
                case "int":
                case "long":
                case "byte":
                case "DateTime":
                case "float":
                case "double":
                    return true;
                default:
                    return false;
            }
        }

        public static SwaggerApiProxySettings GetSettings(string path)
        {
            using (var settingStream = File.OpenRead(path))
            {
                StreamReader streamReader = new StreamReader(settingStream);
                var value = streamReader.ReadToEnd();
                return JsonConvert.DeserializeObject<SwaggerApiProxySettings>(value);
            }
        }

        private static void WriteLine()
        {
            FileText.AppendLine(string.Empty);
        }

        private static void WriteLine(string text)
        {
            FileText.AppendLine(text);
        }
    }
}