﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Birch.Swagger.ProxyGenerator.Swagger;

using Microsoft.CSharp;
using Microsoft.Owin.Testing;

namespace Birch.Swagger.ProxyGenerator.Generator
{
    [SuppressMessage("ReSharper", "UseStringInterpolation")]
    internal static class ProxyGenerator
    {
        private static StringBuilder FileText { get; set; }
        private static int TextPadding { get; set; }
        private static int ActionCount { get; set; }
        private static int ClassCount { get; set; }
        private static int ModelCount { get; set; }

        private static ConcurrentDictionary<SwaggerApiProxySettingsEndPoint, string> _swaggerDocDictionaryList = new ConcurrentDictionary<SwaggerApiProxySettingsEndPoint, string>();

        public static void Generate(string proxyOutputFile, SwaggerApiProxySettingsEndPoint[] endpoints, string baseUrl, string proxyGeneratorNamespace, string proxyGeneratorClassNamePrefix)
        {
            // init
            _swaggerDocDictionaryList = new ConcurrentDictionary<SwaggerApiProxySettingsEndPoint, string>();
            FileText = new StringBuilder();

            Output.Write("Requesting Swagger documents..");
            List<Task> taskList = new List<Task>();
            foreach (var endPoint in endpoints)
            {
                var requestUri = endPoint.Url.StartsWith(baseUrl)
                                     ? endPoint.Url
                                     : string.Format("{0}{1}", baseUrl, endPoint.Url);
                Output.Debug($"Requested: {requestUri}");
                taskList.Add(GetEndpointSwaggerDoc(requestUri, endPoint));
            }

            Output.Debug();
            Output.Debug("Waiting for Swagger documents to complete downloading...");
            Task.WaitAll(taskList.ToArray());

            ProcessSwaggerDocuments(proxyOutputFile, proxyGeneratorNamespace, proxyGeneratorClassNamePrefix);
        }

        public static void Generate(string proxyOutputFile, SwaggerApiProxySettingsEndPoint[] endpoints, TestServer testServer, string proxyGeneratorNamespace, string baseUrl, string proxyGeneratorClassNamePrefix)
        {
            // init
            _swaggerDocDictionaryList = new ConcurrentDictionary<SwaggerApiProxySettingsEndPoint, string>();
            FileText = new StringBuilder();

            Output.Debug();
            Output.Write("Requesting Swagger documents..");
            List<Task> taskList = new List<Task>();
            foreach (var endPoint in endpoints)
            {
                endPoint.Url = endPoint.Url.StartsWith(baseUrl)
                                     ? endPoint.Url
                                     : string.Format("{0}{1}", baseUrl, endPoint.Url);
                Output.Debug($"Requested: {endPoint.Url}");
                taskList.Add(GetEndpointSwaggerDoc(testServer, endPoint, baseUrl));
            }

            Output.Debug();
            Output.Debug("Waiting for Swagger documents to complete downloading...");
            Task.WaitAll(taskList.ToArray());

            ProcessSwaggerDocuments(proxyOutputFile, proxyGeneratorNamespace, proxyGeneratorClassNamePrefix);
        }

        private static void ProcessSwaggerDocuments(string proxyOutputFile, string proxyGeneratorNamespace, string proxyGeneratorClassNamePrefix)
        {
            Output.Write();
            Output.Write("Processing Swagger documents...");

            WriteUsingStatements(proxyGeneratorNamespace);
            WriteBaseProxyAndClasses(proxyGeneratorNamespace, proxyGeneratorClassNamePrefix);

            foreach (var swaggerDocDictionaryEntry in _swaggerDocDictionaryList.OrderBy(x => x.Key.Id))
            {
                var endPoint = swaggerDocDictionaryEntry.Key;
                Output.Write($"Processing {endPoint.Url}");
                WriteLine();
                WriteLine(string.Format("// {0} Proxy", endPoint.Url));

                string methodNameAppend = string.Empty;
                if (endPoint.AppendAsyncToMethodName)
                {
                    methodNameAppend = "Async";
                }

                var result = swaggerDocDictionaryEntry.Value;
                var parser = new SwaggerParser();
                var proxyDefinition = parser.ParseSwaggerDoc(result, endPoint.ParseOperationIdForProxyName);
                var namespaceSuffix = string.IsNullOrWhiteSpace(endPoint.Namespace)
                    ? endPoint.Id
                    : endPoint.Namespace;
                var endpointNamespace = string.Format("{0}.{1}", proxyGeneratorNamespace, namespaceSuffix);
                var proxies = proxyDefinition.Operations.Select(i => i.ProxyName).Distinct().ToList();

                // start namespace def
                WriteLine(string.Format("namespace {0}", endpointNamespace));
                WriteLine("{");

                // Interfaces
                WriteIntefaces(proxyDefinition, methodNameAppend, proxies);

                // Classes
                WriteClasses(proxyGeneratorClassNamePrefix, proxies, endPoint, proxyDefinition, methodNameAppend);

                // Model Classes
                WriteModels(proxyDefinition);

                // close namespace def
                WriteLine("}");
            }
            var condense = ConfigurationManager.AppSettings["proxy-generator:condense"] == null
                || ConfigurationManager.AppSettings["proxy-generator:condense"].ToLower() == "true";
            WriteFile(proxyOutputFile, condense);
        }

        private static void WriteFile(string proxyOutputFile, bool condense)
        {
            var infoHeader = new[]
            {
                "// This file was generated by Birch.Swagger.ProxyGenerator",
                "// Proxy Class Count: {{ProxyGenerator.ClassCount}}",
                "// Proxy Action Count: {{ProxyGenerator.ActionCount}}",
                "// Proxy Model Count: {{ProxyGenerator.ModelCount}}",
                "// Total Line Count: {{ProxyGenerator.TotalLineCount}}",
                "// Comment Line Count: {{ProxyGenerator.CommentLineCount}}",
                "// Summary Line Count: {{ProxyGenerator.SummaryLineCount}}",
                "// Empty Line Count: {{ProxyGenerator.EmptyLineCount}}",
                "// Bracket Only Line Count: {{ProxyGenerator.BracketLineCount}}",
                "// ; Only Line Count: {{ProxyGenerator.SeicolonLineCount}}",
            };
            var fileText = $"{string.Join(Environment.NewLine, infoHeader)}{Environment.NewLine}{FileText.ToString().Trim()}";

            if (condense)
            {
                fileText = Regex.Replace(fileText, @"^\s*$[\r\n]*", "", RegexOptions.Multiline);
                fileText = Regex.Replace(fileText, @"[\r\n]+\s*{", " {", RegexOptions.Multiline);
                fileText = Regex.Replace(fileText, @"[\r\n]+\s*}", "}", RegexOptions.Multiline);
                fileText = Regex.Replace(fileText, @"[\r\n]+\s*;", ";", RegexOptions.Multiline);
                fileText = Regex.Replace(fileText, @"[\r\n]+\s*\.", ".", RegexOptions.Multiline);
            }

            var lines = fileText.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
            var totalLines = lines.Length;
            var commentCount = lines.Count(x => x.Trim().StartsWith("// "));
            var summaryCount = lines.Count(x => x.Trim().StartsWith("/// "));
            var bracketCount = lines.Count(x => Regex.IsMatch(x.Trim(), "^[{}]$"));
            var semicolonCount = lines.Count(x => Regex.IsMatch(x.Trim(), "^;$"));
            var modelCount = ModelCount;
            var classCount = ClassCount;
            var actionCount = ActionCount;
            var emptyCount = lines.Count(string.IsNullOrWhiteSpace);
            fileText = fileText
                .Replace("{{ProxyGenerator.TotalLineCount}}", $"{totalLines}")
                .Replace("{{ProxyGenerator.ClassCount}}", $"{classCount}")
                .Replace("{{ProxyGenerator.ActionCount}}", $"{actionCount}")
                .Replace("{{ProxyGenerator.ModelCount}}", $"{modelCount}")
                .Replace("{{ProxyGenerator.BracketLineCount}}",
                    $"{bracketCount} ({((double) bracketCount/totalLines).ToString("0.00%")} of total)")
                .Replace("{{ProxyGenerator.CommentLineCount}}",
                    $"{commentCount} ({((double) commentCount/totalLines).ToString("0.00%")} of total)")
                .Replace("{{ProxyGenerator.EmptyLineCount}}",
                    $"{emptyCount} ({((double) emptyCount/totalLines).ToString("0.00%")} of total)")
                .Replace("{{ProxyGenerator.SummaryLineCount}}",
                    $"{summaryCount} ({((double)summaryCount / totalLines).ToString("0.00%")} of total)")
                .Replace("{{ProxyGenerator.SeicolonLineCount}}",
                    $"{semicolonCount} ({((double)semicolonCount / totalLines).ToString("0.00%")} of total)");

            File.WriteAllText(proxyOutputFile, fileText);
        }

        private static void WriteModels(ProxyDefinition proxyDefinition)
        {
            foreach (var classDef in proxyDefinition.ClassDefinitions)
            {
                List<Enum> modelEnums = new List<Enum>();
                ModelCount++;
                WriteLine(
                    string.Format(
                        "public class {0}{1}",
                        classDef.Name,
                        string.IsNullOrEmpty(classDef.Inherits) ? string.Empty : string.Format(" : {0}", classDef.Inherits)));
                WriteLine("{");
                foreach (var prop in classDef.Properties)
                {
                    var typeName = string.IsNullOrWhiteSpace(prop.TypeName) ? "object" : prop.TypeName;
                    if (prop.EnumValues != null)
                    {
                        modelEnums.Add(new Enum { Name = typeName.Replace("?", string.Empty), Values = prop.EnumValues });
                    }

                    WriteLine(string.Format("public {0} {1} {{ get; set; }}", typeName, prop.Name));
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
                    WriteLine();
                }

                WriteLine("}");
                WriteLine();
            }

        }

        private static void WriteClasses(string proxyGeneratorClassNamePrefix, List<string> proxies,
            SwaggerApiProxySettingsEndPoint endPoint, ProxyDefinition proxyDefinition, string methodNameAppend)
        {
            foreach (var proxy in proxies)
            {
                ClassCount++;
                var baseProxyName = proxyGeneratorClassNamePrefix + "BaseProxy";

                // start class defintion
                WriteLine("/// <summary>");
                WriteLine(string.Format("/// Web Proxy for {0}", proxy));
                WriteLine("/// </summary>");
                var className = SwaggerParser.FixTypeName(proxy) + "WebProxy";
                WriteLine(
                    string.Format(
                        "public class {0} : {1}, I{0}",
                        className,
                        string.IsNullOrWhiteSpace(endPoint.BaseProxyClass) ? baseProxyName : endPoint.BaseProxyClass));
                WriteLine("{");

                WriteLine(
                    string.Format(
                        "public {0}{1}",
                        className,
                        endPoint.ProxyConstructorSuffix));
                WriteLine("{}");
                WriteLine();
                List<Enum> proxyParamEnums = new List<Enum>();

                // Async operations (web methods)
                var proxy1 = proxy;
                foreach (var operationDef in proxyDefinition.Operations.Where(i => i.ProxyName.Equals(proxy1)))
                {
                    string returnType = string.IsNullOrEmpty(operationDef.ReturnType)
                        ? string.Empty
                        : string.Format("<{0}>", operationDef.ReturnType);

                    // enums
                    var enums = operationDef.Parameters.Where(i => i.Type.EnumValues != null);
                    foreach (var enumParam in enums)
                    {
                        enumParam.Type.TypeName = operationDef.OperationId + enumParam.Type.Name;
                        proxyParamEnums.Add(
                            new Enum { Name = enumParam.Type.TypeName, Values = enumParam.Type.EnumValues });
                        if (enumParam.DefaultValue != "null")
                        {
                            enumParam.DefaultValue = enumParam.DefaultValue;
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
                                            x.DefaultValue)
                                        : string.Format("{0} {1}", GetDefaultType(x), x.Type.GetCleanTypeName())));

                    WriteLine("/// <summary>");
                    var summary = (SecurityElement.Escape(operationDef.Description) ?? "").Replace(Environment.NewLine, $"{Environment.NewLine}///");
                    WriteLine(string.IsNullOrWhiteSpace(summary) ? "///" : string.Format("/// {0}", summary));
                    WriteLine("/// </summary>");
                    foreach (var parameter in operationDef.Parameters)
                    {
                        WriteLine(
                            string.Format(
                                "/// <param name=\"{0}\">{1}</param>",
                                parameter.Type.Name,
                                (SecurityElement.Escape(parameter.Description) ?? "").Replace(Environment.NewLine, $"{Environment.NewLine}///")));
                    }
                    var actionName = SwaggerParser.FixTypeName(operationDef.OperationId);
                    WriteLine(
                        string.Format(
                            "public async Task{0} {1}{2}({3})",
                            returnType,
                            actionName,
                            methodNameAppend,
                            parameters));
                    WriteLine("{");
                    WriteUrl(operationDef);
                    WriteActionMethodInvoke(operationDef, returnType, actionName);
                    WriteLine("}"); // close up the method
                    WriteLine();
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
                    WriteLine();
                }

                // close class def
                WriteLine("}");
                WriteLine();
            }
        }

        private static void WriteUrl(Operation operationDef)
        {
            WriteLine(operationDef.Path.StartsWith("/")
                ? string.Format("var url = \"{0}\"", operationDef.Path.Substring(1))
                : string.Format("var url = \"{0}\"", operationDef.Path));

            foreach (var parameter in operationDef.Parameters.Where(i => i.ParameterIn == ParameterIn.Path))
            {
                WriteLine(string.Format("\t.Replace(\"{{{0}}}\", {0}.ToString())", parameter.Type.GetCleanTypeName()));
            }
            WriteLine(";");

            var queryParams = operationDef.Parameters.Where(i => i.ParameterIn == ParameterIn.Query).ToList();
            if (queryParams.Count > 0)
            {
                foreach (var parameter in queryParams)
                {
                    if (parameter.IsRequired == false && parameter.DefaultValue == "null" &&
                        (parameter.Type.EnumValues == null || parameter.Type.EnumValues.Any() == false))
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

                    if (parameter.IsRequired == false && parameter.DefaultValue == "null" &&
                        (parameter.Type.EnumValues == null || parameter.Type.EnumValues.Any() == false))
                    {
                        WriteLine("}");
                    }
                }
            }
        }

        private static void WriteActionMethodInvoke(Operation operationDef, string returnType, string actionName)
        {
            var hasFormData = operationDef.Parameters.Any(i => i.ParameterIn == ParameterIn.FormData);
            var bodyParam = operationDef.Parameters.FirstOrDefault(i => i.ParameterIn == ParameterIn.Body);
            var bodyParamText = bodyParam != null ? $", {bodyParam.Type.Name}" : string.Empty;
            var returnText = string.IsNullOrWhiteSpace(returnType) ? string.Empty : "return ";
            switch (operationDef.Method.ToUpperInvariant())
            {
                case "GET":
                    WriteLine(GetActionString("Get", returnType, actionName, returnText, bodyParamText));
                    break;

                case "DELETE":
                    WriteLine(GetActionString("Delete", returnType, actionName, returnText, bodyParamText));
                    break;

                case "PUT":
                    if (hasFormData)
                    {
                        WriteStartRequest(operationDef);
                        ProcessFormData(operationDef, "Put");
                        WriteEndRequest(returnType, operationDef);
                    }
                    else
                    {
                        WriteLine(GetActionString("Put", returnType, actionName, returnText, bodyParamText));
                    }
                    break;

                case "POST":
                    if (hasFormData)
                    {
                        WriteStartRequest(operationDef);
                        ProcessFormData(operationDef, "Post");
                        WriteEndRequest(returnType, operationDef);
                    }
                    else
                    {
                        WriteLine(GetActionString("Post", returnType, actionName, returnText, bodyParamText));
                    }
                    break;
            }
        }

        private static string GetActionString(string method, string returnType, string actionName, string returnText, string bodyParamText)
        {
            return $"{returnText}await {method}{returnType}(url, \"{actionName}\"{bodyParamText}).ConfigureAwait(false);";
        }

        private static void WriteEndRequest(string returnType, Operation operationDef)
        {
            WriteLine("stopwatch.Stop();");
            WriteLine(string.Format("var output = new WebProxyResponse{0}", returnType));
            WriteLine("{");
            WriteLine("Response = response,");
            WriteLine("RequestDuration = stopwatch.Elapsed,");
            if (!string.IsNullOrWhiteSpace(returnType))
            {
                WriteLine(string.Format("ExpectedResponseType = typeof({0})",
                    returnType.Substring(1, returnType.Length - 2)));
            }
            WriteLine("};");
            WriteLine("await AfterRequestAsync(output);");

            if (string.IsNullOrWhiteSpace(operationDef.ReturnType) == false)
            {
                WriteLine("if (output.Exception == null)");
                WriteLine("{");
                WriteLine(
                    string.Format(
                        "output.Body = await response.Content.ReadAsAsync<{0}>().ConfigureAwait(false);",
                        operationDef.ReturnType));
                WriteLine("}");
            }
            WriteLine("if (output.Exception != null)");
            WriteLine("{");
            WriteLine("throw output.Exception;");
            WriteLine("}");
            if (string.IsNullOrWhiteSpace(operationDef.ReturnType) == false)
            {
                WriteLine("return output.Body;");
            }
            WriteLine();
            WriteLine("}"); // close up the using
        }

        private static void WriteStartRequest(Operation operationDef)
        {
            WriteLine();
            WriteLine("using (var client = BuildHttpClient())");
            WriteLine("{");
            WriteLine("var beforeRequestActionArgs = new BeforeRequestActionArgs");
            WriteLine("{");
            WriteLine("Uri = url,");
            WriteLine(string.Format("ActionName = \"{0}\",", SwaggerParser.FixTypeName(operationDef.OperationId)));
            WriteLine(string.Format("Method = \"{0}\",", operationDef.Method.ToUpperInvariant()));
            WriteLine("};");
            WriteLine("await BeforeRequestAsync(beforeRequestActionArgs);");
            WriteLine("var stopwatch = new Stopwatch();");
            WriteLine("stopwatch.Start();");
        }

        private static void WriteIntefaces(ProxyDefinition proxyDefinition, string methodNameAppend, IEnumerable<string> proxies)
        {
            foreach (var proxy in proxies)
            {
                WriteLine(
                    string.Format(
                        "public interface {0}",
                        string.Format("I{0}WebProxy", SwaggerParser.FixTypeName(proxy))));
                WriteLine("{");
                var proxy1 = proxy;

                foreach (var operationDef in proxyDefinition.Operations.Where(i => i.ProxyName.Equals(proxy1)))
                {
                    string returnType = string.IsNullOrEmpty(operationDef.ReturnType)
                        ? string.Empty
                        : string.Format("<{0}>", operationDef.ReturnType);
                    var enums = operationDef.Parameters.Where(i => i.Type.EnumValues != null);
                    foreach (var enumParam in enums)
                    {
                        enumParam.Type.TypeName = operationDef.OperationId + enumParam.Type.Name;
                    }
                    string parameters = string.Join(
                        ", ",
                        operationDef.Parameters.OrderByDescending(i => i.IsRequired)
                            .Select(
                                x =>
                                {
                                    var defaultType = GetDefaultType(x);

                                    if (x.Type.EnumValues != null)
                                    {
                                        var className = SwaggerParser.FixTypeName(proxy) + "WebProxy";
                                        defaultType = className + "." + defaultType;
                                        x.DefaultValue = defaultType + "." + x.DefaultValue;
                                    }
                                    return (x.IsRequired == false)
                                        ? string.Format(
                                            "{0} {1} = {2}",
                                            defaultType,
                                            x.Type.GetCleanTypeName(),
                                            x.DefaultValue)
                                        : string.Format("{0} {1}", defaultType, x.Type.GetCleanTypeName());
                                }));

                    ActionCount++;
                    WriteLine(
                        string.Format(
                            "Task{0} {1}{2}({3});",
                            returnType,
                            SwaggerParser.FixTypeName(operationDef.OperationId),
                            methodNameAppend,
                            parameters));
                }
                WriteLine("}");
            }
            WriteLine();
        }

        private static void ProcessFormData(Operation operationDef, string httpMethod)
        {
            Func<Parameter, bool> isFilePredicate = x => x.Type.TypeName == "file";
            Func<Parameter, bool> notFilePredicate = x => x.Type.TypeName != "file";
            var formData = operationDef.Parameters
                    .Where(i => i.ParameterIn == ParameterIn.FormData)
                    .ToList();

            var hasFormContent = formData.Any(notFilePredicate);
            if (hasFormContent)
            {
                WriteLine("var formKeyValuePairs = new List<KeyValuePair<string, string>>();");
                foreach (var formParam in formData.Where(notFilePredicate))
                {
                    if (formParam.IsRequired == false && formParam.DefaultValue == "null")
                    {
                        WriteNullIfStatementOpening(formParam.Type.Name, formParam.Type.TypeName);
                    }
                    WriteLine(
                        string.Format(
                            "formKeyValuePairs.Add(new KeyValuePair<string, string>(\"{0}\", {0}));",
                            formParam.Type.Name));
                    if (formParam.IsRequired == false && formParam.DefaultValue == null)
                    {
                        WriteLine("}");
                    }
                }
            }

            var hasFile = formData.Any(isFilePredicate);
            if (hasFile)
            {
                // TODO: support multiple input files properly
                foreach (var formParam in formData.Where(isFilePredicate))
                {
                    WriteLine(
                        string.Format(
                            "var fileContent = new ByteArrayContent({0}.Item2);",
                            formParam.Type.Name));
                    WriteLine(
                        string.Format(
                            "fileContent.Headers.ContentDisposition.FileName = {0}.Item1;",
                            formParam.Type.Name));
                }
            }
            WriteLine("HttpResponseMessage response;");
            if (hasFile)
            {
                WriteLine(
                    "using (var content = new MultipartFormDataContent(\"---------------------------\" + DateTime.Now.ToString()))");
                WriteLine("{");
                WriteLine("content.Add(fileContent, \"file\");");
                if (hasFormContent)
                {
                    WriteLine("using (var formUrlEncodedContent = new FormUrlEncodedContent(formKeyValuePairs))");
                    WriteLine("{");
                    WriteLine("content.Add(formUrlEncodedContent);");
                    WriteLine("}");
                }
            }
            else
            {
                WriteLine("var content = new FormUrlEncodedContent(formKeyValuePairs);");
            }

            WriteLine(string.Format("response = await client.{0}Async(url, content).ConfigureAwait(false);", httpMethod));
            if (hasFile)
            {
                WriteLine("}");
            }
        }

        private static void WriteBaseProxyAndClasses(string proxyGeneratorNameSpace, string proxyGeneratorClassNamePrefix)
        {
            // TODO: move to template of some sort
            WriteLine(string.Format("namespace {0}", proxyGeneratorNameSpace));
            WriteLine("{");

            // Base Proxy
            var baseProxyName = proxyGeneratorClassNamePrefix + "BaseProxy";
            WriteLine(string.Format("public abstract class {0}", baseProxyName));
            WriteLine("{");
            WriteLine("protected readonly Uri BaseUrl;");
            WriteLine("public readonly List<Action<BeforeRequestActionArgs>> GlobalBeforeRequestActions;");
            WriteLine("public readonly List<Action<IWebProxyResponse>> GlobalAfterRequestActions;");
            WriteLine("public readonly List<Action<BeforeRequestActionArgs>> BeforeRequestActions;");
            WriteLine("public readonly List<Action<IWebProxyResponse>> AfterRequestActions;");
            WriteLine();

            WriteLine(string.Format("protected {0}(Uri baseUrl)", baseProxyName));
            WriteLine("{");
            WriteLine("BaseUrl = baseUrl;");
            WriteLine("GlobalBeforeRequestActions = new List<Action<BeforeRequestActionArgs>>();");
            WriteLine("GlobalAfterRequestActions = new List<Action<IWebProxyResponse>>();");
            WriteLine("BeforeRequestActions = new List<Action<BeforeRequestActionArgs>>();");
            WriteLine("AfterRequestActions = new List<Action<IWebProxyResponse>>();");
            WriteLine("}");
            WriteLine();

            WriteLine("/// <summary>");
            WriteLine("/// Builds the HTTP client.");
            WriteLine("/// </summary>");
            WriteLine("/// <returns></returns>");
            WriteLine("protected virtual HttpClient BuildHttpClient()");
            WriteLine("{");
            WriteLine("var httpClient = new HttpClient");
            WriteLine("{");
            WriteLine("BaseAddress = BaseUrl");
            WriteLine("};");
            WriteLine("return httpClient;");
            WriteLine("}");
            WriteLine();

            WriteLine("/// <summary>");
            WriteLine("/// Runs before the request asynchronous.");
            WriteLine("/// </summary>");
            WriteLine("/// <param name=\"requestUri\">The request URI.</param>");
            WriteLine("/// <param name=\"requestMethod\">The request method.</param>");
            WriteLine("/// <returns></returns>");
            WriteLine("public virtual Task BeforeRequestAsync(BeforeRequestActionArgs actionArgs)");
            WriteLine("{");
            WriteLine("foreach (var globalBeforeRequestAction in GlobalBeforeRequestActions)");
            WriteLine("{");
            WriteLine("globalBeforeRequestAction.Invoke(actionArgs);");
            WriteLine("}");
            WriteLine();
            WriteLine("foreach (var beforeRequestAction in BeforeRequestActions)");
            WriteLine("{");
            WriteLine("beforeRequestAction.Invoke(actionArgs);");
            WriteLine("}");
            WriteLine("BeforeRequestActions.Clear();");
            WriteLine("return Task.FromResult(0);");
            WriteLine("}");
            WriteLine();

            WriteLine("/// <summary>");
            WriteLine("/// Runs After the request asynchronous.");
            WriteLine("/// </summary>");
            WriteLine("/// <param name=\"response\">The response.</param>");
            WriteLine("/// <param name=\"webProxyResponse\">The web proxy response.</param>");
            WriteLine("/// <returns></returns>");
            WriteLine("public virtual async Task AfterRequestAsync(IWebProxyResponse webProxyResponse)");
            WriteLine("{");
            WriteLine("foreach (var globalAfterRequestAction in GlobalAfterRequestActions)");
            WriteLine("{");
            WriteLine("globalAfterRequestAction.Invoke(webProxyResponse);");
            WriteLine("}");
            WriteLine();
            WriteLine("foreach (var afterRequestAction in AfterRequestActions)");
            WriteLine("{");
            WriteLine("afterRequestAction.Invoke(webProxyResponse);");
            WriteLine("}");
            WriteLine("AfterRequestActions.Clear();");
            WriteLine();
            WriteLine("if (webProxyResponse.Response.IsSuccessStatusCode)");
            WriteLine("{");
            WriteLine("return;");
            WriteLine("}");
            WriteLine();
            WriteLine("try");
            WriteLine("{");
            WriteLine("var content = await webProxyResponse.Response.Content.ReadAsStringAsync().ConfigureAwait(false);");
            WriteLine("webProxyResponse.Exception = new SimpleHttpResponseException(webProxyResponse.Response.StatusCode, content);");
            WriteLine("}");
            WriteLine("finally");
            WriteLine("{");
            WriteLine("webProxyResponse.Response.Content?.Dispose();");
            WriteLine("}");
            WriteLine("}");
            WriteLine();

            WriteAppendQueryHelper();

            // Web proxy response classes
            WriteWebProxyResponseClasses();

            // request helpers
            WriteSimpleActionHelper("Get");
            WriteSimpleActionHelper("Delete");
            WriteCommandActionHelper("Put");
            WriteCommandActionHelper("Post");

            WriteLine("}");
            WriteLine("}");
        }

        private static void WriteAppendQueryHelper()
        {
            WriteLine("/// <summary>");
            WriteLine("/// Appends the query.");
            WriteLine("/// </summary>");
            WriteLine("/// <param name=\"currentUrl\">The current URL.</param>");
            WriteLine("/// <param name=\"paramName\">Name of the parameter.</param>");
            WriteLine("/// <param name=\"value\">The value.</param>");
            WriteLine("/// <returns></returns>");
            WriteLine("protected string AppendQuery(string currentUrl, string paramName, string value)");
            WriteLine("{");
            WriteLine("if (currentUrl.Contains(\"?\"))");
            WriteLine("{");
            WriteLine("currentUrl += string.Format(\"&{0}={1}\", paramName, Uri.EscapeUriString(value));");
            WriteLine("}");
            WriteLine("else");
            WriteLine("{");
            WriteLine("currentUrl += string.Format(\"?{0}={1}\", paramName, Uri.EscapeUriString(value));");
            WriteLine("}");
            WriteLine("return currentUrl;");
            WriteLine("}");
        }

        private static void WriteWebProxyResponseClasses()
        {
            WriteLine("public class WebProxyResponse<T> : IWebProxyResponse");
            WriteLine("{");
            WriteLine("public HttpResponseMessage Response { get; set; }");
            WriteLine("public TimeSpan RequestDuration { get; set; }");
            WriteLine("public Type ExpectedResponseType { get; set; }");
            WriteLine("public T Body { get; set; }");
            WriteLine("public Exception Exception { get; set; }");
            WriteLine("}");
            WriteLine("public class WebProxyResponse : IWebProxyResponse");
            WriteLine("{");
            WriteLine("public HttpResponseMessage Response { get; set; }");
            WriteLine("public TimeSpan RequestDuration { get; set; }");
            WriteLine("public Type ExpectedResponseType { get; set; }");
            WriteLine("public Exception Exception { get; set; }");
            WriteLine("}");
            WriteLine("public interface IWebProxyResponse");
            WriteLine("{");
            WriteLine("HttpResponseMessage Response { get; set; }");
            WriteLine("TimeSpan RequestDuration { get; set; }");
            WriteLine("Type ExpectedResponseType { get; set; }");
            WriteLine("Exception Exception { get; set; }");
            WriteLine("}");
            WriteLine("public class BeforeRequestActionArgs");
            WriteLine("{");
            WriteLine("public string Uri { get; set; }");
            WriteLine("public string ActionName { get; set; }");
            WriteLine("public string Method { get; set; }");
            WriteLine("}");
            WriteLine();
            WriteLine("public class SimpleHttpResponseException : Exception");
            WriteLine("{");
            WriteLine("public HttpStatusCode StatusCode { get; private set; }");
            WriteLine();
            WriteLine("public SimpleHttpResponseException(HttpStatusCode statusCode, string content)");
            WriteLine(": base(content)");
            WriteLine("{");
            WriteLine("StatusCode = statusCode;");
            WriteLine("}");
            WriteLine("}");
            WriteLine();
        }

        private static void WriteSimpleActionHelper(string action)
        {
            WriteNoResponseHelper(action);
            WriteResponseHelper(action);
        }

        private static void WriteCommandActionHelper(string action)
        {
            WriteNoResponseHelper(action, true);
            WriteResponseHelper(action, true);
        }

        private static void WriteResponseHelper(string action, bool hasBody = false)
        {
            var bodyText = hasBody ? ", object body = null" : string.Empty;
            var defaultBody = action == "Put" || action == "Post"
                ? ", new StringContent(string.Empty)"
                : string.Empty;
            WriteLine($"public async Task<T> {action}<T>(string url, string actionName{bodyText})");
            WriteLine("{");
            WriteLine("using (var client = BuildHttpClient())");
            WriteLine("{");
            WriteLine("var beforeRequestActionArgs = new BeforeRequestActionArgs");
            WriteLine("{");
            WriteLine("Uri = url,");
            WriteLine("ActionName = actionName,");
            WriteLine($"Method = \"{action.ToUpper()}\"");
            WriteLine("};");
            WriteLine("await BeforeRequestAsync(beforeRequestActionArgs);");
            WriteLine("var stopwatch = new Stopwatch();");
            WriteLine("stopwatch.Start();");
            if (hasBody)
            {
                WriteLine("HttpResponseMessage response;");
                WriteLine("if (body == null)");
                WriteLine("{");
                WriteLine($"response = await client.{action}Async(url{defaultBody}).ConfigureAwait(false);");
                WriteLine("}");
                WriteLine("else");
                WriteLine("{");
                WriteLine($"response = await client.{action}AsJsonAsync(url, body).ConfigureAwait(false);");
                WriteLine("}");
            }
            else
            {
                WriteLine($"var response = await client.{action}Async(url{defaultBody}).ConfigureAwait(false);");
            }
            WriteLine("stopwatch.Stop();");
            WriteLine("var output = new WebProxyResponse<T>");
            WriteLine("{");
            WriteLine("Response = response,");
            WriteLine("RequestDuration = stopwatch.Elapsed,");
            WriteLine("ExpectedResponseType = typeof(T)");
            WriteLine("};");
            WriteLine("await AfterRequestAsync(output);");
            WriteLine("if (output.Exception == null)");
            WriteLine("{");
            WriteLine("output.Body =");
            WriteLine("await response.Content.ReadAsAsync<T>().ConfigureAwait(false);");
            WriteLine("}");
            WriteLine("if (output.Exception != null)");
            WriteLine("{");
            WriteLine("throw output.Exception;");
            WriteLine("}");
            WriteLine("return output.Body;");
            WriteLine("}");
            WriteLine("}");
            WriteLine();
        }

        private static void WriteNoResponseHelper(string action, bool hasBody = false)
        {
            var bodyText = hasBody ? ", object body = null" : string.Empty;
            var defaultBody = action == "Put" || action == "Post"
                ? ", new StringContent(string.Empty)"
                : string.Empty;

            WriteLine($"public async Task {action}(string url, string actionName{bodyText})");
            WriteLine("{");
            WriteLine("using (var client = BuildHttpClient())");
            WriteLine("{");
            WriteLine("var beforeRequestActionArgs = new BeforeRequestActionArgs");
            WriteLine("{");
            WriteLine("Uri = url,");
            WriteLine("ActionName = actionName,");
            WriteLine($"Method = \"{action.ToUpper()}\",");
            WriteLine("};");
            WriteLine("await BeforeRequestAsync(beforeRequestActionArgs);");
            WriteLine("var stopwatch = new Stopwatch();");
            WriteLine("stopwatch.Start();");
            if (hasBody)
            {
                WriteLine("HttpResponseMessage response;");
                WriteLine("if (body == null)");
                WriteLine("{");
                WriteLine($"response = await client.{action}Async(url{defaultBody}).ConfigureAwait(false);");
                WriteLine("}");
                WriteLine("else");
                WriteLine("{");
                WriteLine($"response = await client.{action}AsJsonAsync(url, body).ConfigureAwait(false);");
                WriteLine("}");
            }
            else
            {
                WriteLine($"var response = await client.{action}Async(url{defaultBody}).ConfigureAwait(false);");
            }
            WriteLine("stopwatch.Stop();");
            WriteLine("var output = new WebProxyResponse");
            WriteLine("{");
            WriteLine("Response = response,");
            WriteLine("RequestDuration = stopwatch.Elapsed");
            WriteLine("};");
            WriteLine("await AfterRequestAsync(output);");
            WriteLine("");
            WriteLine("if (output.Exception != null)");
            WriteLine("{");
            WriteLine("throw output.Exception;");
            WriteLine("}");
            WriteLine("}");
            WriteLine("}");
            WriteLine();
        }

        private static async Task GetEndpointSwaggerDoc(TestServer testServer, SwaggerApiProxySettingsEndPoint endPoint, string baseUrl)
        {
            var swaggerString = await testServer.HttpClient.GetStringAsync(endPoint.Url.Replace(baseUrl, string.Empty));
            if (swaggerString == null)
            {
                throw new Exception(string.Format("Error downloading from: (TestServer){0}", endPoint.Url));
            }
            Output.Write($"Downloaded: {endPoint.Url}");
            _swaggerDocDictionaryList.GetOrAdd(endPoint, swaggerString);
        }

        private static async Task GetEndpointSwaggerDoc(string requestUri, SwaggerApiProxySettingsEndPoint endPoint)
        {
            using (var client = new HttpClient())
            {
                var swaggerString = await client.GetStringAsync(requestUri);
                if (swaggerString == null)
                {
                    throw new Exception(string.Format("Error downloading from: (TestServer){0}", endPoint.Url));
                }
                Output.Debug($"Downloaded: {requestUri}");
                _swaggerDocDictionaryList.GetOrAdd(endPoint, swaggerString);
            }
        }

        private static string GetDefaultType(Parameter x)
        {
            var typeName = x.Type.TypeName;
            if (typeName == "file")
            {
                return "Tuple<string, byte[]>";
            }

            var output = x.CollectionFormat == "multi" ? string.Format("List<{0}>", typeName) : typeName;
            return output;
        }

        private static void WriteUsingStatements(string proxyGeneratorNameSpace)
        {
            proxyGeneratorNameSpace = string.IsNullOrWhiteSpace(proxyGeneratorNameSpace)
                ? "Birch.Swagger.ProxyGenerator"
                : proxyGeneratorNameSpace;

            WriteLine("using System;");
            WriteLine("using System.Collections.Generic;");
            WriteLine("using System.Diagnostics;");
            WriteLine("using System.Net;");
            WriteLine("using System.Threading.Tasks;");
            WriteLine("using System.Net.Http;");
            WriteLine("using System.Net.Http.Headers;");
            WriteLine(string.Format("using {0};", proxyGeneratorNameSpace));
            WriteLine();
        }

        private static void WriteNullIfStatementOpening(string parameterName, string typeName)
        {
            WriteLine(IsIntrinsicType(typeName)
                ? string.Format("if ({0}.HasValue){{", parameterName)
                : string.Format("if ({0} != null){{", parameterName));
        }

        private static bool IsIntrinsicType(string typeName)
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

        private static readonly ConcurrentDictionary<int, string> PaddingCache = new ConcurrentDictionary<int, string>();

        private static void WriteLine()
        {
            FileText.AppendLine(string.Empty);
        }

        private static void WriteLine(string text)
        {
            if ((text == "}" || text == "};") && TextPadding != 0)
            {
                TextPadding--;
            }

            var textPadding = PaddingCache.GetOrAdd(TextPadding, i => new string(' ', i * 4));
            FileText.AppendLine(string.Format("{1}{0}", text, textPadding));
            if (text.EndsWith("{"))
            {
                TextPadding++;
            }
        }
    }
}
