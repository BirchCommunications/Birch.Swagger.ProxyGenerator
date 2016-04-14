using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    [SuppressMessage("ReSharper", "UseStringInterpolation")]
    internal static class ProxyGenerator
    {
        private static StringBuilder FileText { get; set; }

        private static int TextPadding { get; set; }

        private static ConcurrentDictionary<SwaggerApiProxySettingsEndPoint, string> _swaggerDocDictionaryList = new ConcurrentDictionary<SwaggerApiProxySettingsEndPoint, string>();

        public static void Generate(string proxyOutputFile, SwaggerApiProxySettingsEndPoint[] endpoints, string baseUrl, string proxyGeneratorNamespace, string proxyGeneratorClassNamePrefix)
        {
            // init
            _swaggerDocDictionaryList = new ConcurrentDictionary<SwaggerApiProxySettingsEndPoint, string>();
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

            ProcessSwaggerDocuments(proxyOutputFile, proxyGeneratorNamespace, proxyGeneratorClassNamePrefix);
        }

        public static void Generate(string proxyOutputFile, SwaggerApiProxySettingsEndPoint[] endpoints, TestServer testServer, string proxyGeneratorNamespace, string baseUrl, string proxyGeneratorClassNamePrefix)
        {
            // init
            _swaggerDocDictionaryList = new ConcurrentDictionary<SwaggerApiProxySettingsEndPoint, string>();
            FileText = new StringBuilder();

            Console.WriteLine();
            Console.WriteLine("Requesting Swagger documents..");
            List<Task> taskList = new List<Task>();
            foreach (var endPoint in endpoints)
            {
                endPoint.Url = endPoint.Url.StartsWith(baseUrl)
                                     ? endPoint.Url
                                     : string.Format("{0}{1}", baseUrl, endPoint.Url);
                Console.WriteLine("Requested: {0}", endPoint.Url);
                taskList.Add(GetEndpointSwaggerDoc(testServer, endPoint, baseUrl));
            }

            Console.WriteLine();
            Console.WriteLine("Waiting for Swagger documents to complete downloading...");
            Task.WaitAll(taskList.ToArray());

            ProcessSwaggerDocuments(proxyOutputFile, proxyGeneratorNamespace, proxyGeneratorClassNamePrefix);
        }

        private static void ProcessSwaggerDocuments(string proxyOutputFile, string proxyGeneratorNamespace, string proxyGeneratorClassNamePrefix)
        {
            Console.WriteLine();
            Console.WriteLine("Processing Swagger documents...");

            PrintHeaders(proxyGeneratorNamespace);
            AddBaseProxyAndClasses(proxyGeneratorNamespace, proxyGeneratorClassNamePrefix);

            foreach (var swaggerDocDictionaryEntry in _swaggerDocDictionaryList.OrderBy(x => x.Key.Id))
            {
                var endPoint = swaggerDocDictionaryEntry.Key;
                Console.WriteLine("Processing {0}", endPoint.Url);
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

                WriteLine(string.Format("namespace {0} {{", endpointNamespace));

                // Interfaces
                var proxies = proxyDefinition.Operations.Select(i => i.ProxyName).Distinct().ToList();
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

                        var className = SwaggerParser.FixTypeName(proxy) + "WebProxy";
                        string parameters = string.Join(
                            ", ",
                            operationDef.Parameters.OrderByDescending(i => i.IsRequired)
                                .Select(
                                    x =>
                                    {
                                        // if parameter is enum include the namespace
                                        string parameter = x.Type.EnumValues != null ? string.Format("{0}.{1}.", endpointNamespace, className) : string.Empty;
                                        parameter += x.IsRequired == false
                                            ? string.Format(
                                                "{0} {1} = {3}{2}",
                                                GetDefaultType(x),
                                                x.Type.GetCleanTypeName(),
                                                GetDefaultValue(x),
                                                parameter)
                                            : string.Format("{0} {1}", GetDefaultType(x), x.Type.GetCleanTypeName());
                                        return parameter;
                                    }));
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
                foreach (var proxy in proxies)
                {
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
                        var enums = operationDef.Parameters.Where(i => i.Type.EnumValues != null);

                        foreach (var enumParam in enums)
                        {
                            enumParam.Type.TypeName = operationDef.OperationId + enumParam.Type.Name;
                            proxyParamEnums.Add(
                                new Enum { Name = enumParam.Type.TypeName, Values = enumParam.Type.EnumValues });
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
                                        : string.Format("{0} {1}", GetDefaultType(x), x.Type.GetCleanTypeName())));

                        WriteLine("/// <summary>");
                        var summary = (SecurityElement.Escape(operationDef.Description) ?? "").Replace("\n", "\n///");
                        WriteLine(string.IsNullOrWhiteSpace(summary) ? "///" : string.Format("/// {0}", summary));
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
                                "public async Task{0} {1}{2}({3})",
                                returnType,
                                SwaggerParser.FixTypeName(operationDef.OperationId),
                                methodNameAppend,
                                parameters));
                        WriteLine("{");

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
                                if (parameter.IsRequired == false && (parameter.Type.EnumValues == null || parameter.Type.EnumValues.Any() == false))
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

                                if (parameter.IsRequired == false && (parameter.Type.EnumValues == null || parameter.Type.EnumValues.Any() == false))
                                {
                                    WriteLine("}");
                                }
                            }
                        }

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

                        switch (operationDef.Method.ToUpperInvariant())
                        {
                            case "GET":
                                WriteLine("var response = await client.GetAsync(url).ConfigureAwait(false);");
                                break;

                            case "DELETE":
                                WriteLine("var response = await client.DeleteAsync(url).ConfigureAwait(false);");
                                break;

                            case "PUT":
                                Process(operationDef, "Put");
                                break;

                            case "POST":
                                Process(operationDef, "Post");

                                break;
                        }
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

                // Model Classes
                foreach (var classDef in proxyDefinition.ClassDefinitions)
                {
                    List<Enum> modelEnums = new List<Enum>();

                    WriteLine(
                        string.Format(
                            "public class {0}{1}",
                            classDef.Name,
                            string.IsNullOrEmpty(classDef.Inherits) ? string.Empty : string.Format(" : {0}", classDef.Inherits)));
                    WriteLine("{");
                    foreach (var prop in classDef.Properties)
                    {
                        var typeName = string.IsNullOrWhiteSpace(prop.TypeName) ? "object" : prop.TypeName;
                        WriteLine(string.Format("public {0} {1} {{ get; set; }}", typeName, prop.Name));
                        if (prop.EnumValues != null)
                        {
                            modelEnums.Add(new Enum() { Name = typeName, Values = prop.EnumValues });
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
                        WriteLine();
                    }

                    WriteLine("}");
                    WriteLine();
                }

                // close namespace def
                WriteLine("}");
            }

            File.WriteAllText(proxyOutputFile, FileText.ToString());
        }

        private static void Process(Operation operationDef, string httpMethod)
        {
            Func<Parameter, bool> isFilePredicate = x => x.Type.TypeName == "file";
            Func<Parameter, bool> notFilePredicate = x => x.Type.TypeName != "file";

            var putBodyParam = operationDef.Parameters
                .FirstOrDefault(i => i.ParameterIn == ParameterIn.Body);
            if (putBodyParam != null)
            {
                WriteLine(
                    string.Format(
                        "var response = await client.{1}AsJsonAsync(url, {0}).ConfigureAwait(false);",
                        putBodyParam.Type.Name, httpMethod));
            }
            else if (operationDef.Parameters.Any(i => i.ParameterIn == ParameterIn.FormData))
            {
                var formData = operationDef.Parameters
                    .Where(i => i.ParameterIn == ParameterIn.FormData)
                    .ToList();

                var hasFormContent = formData.Any(notFilePredicate);
                if (hasFormContent)
                {
                    WriteLine("var formKeyValuePairs = new List<KeyValuePair<string, string>>();");
                    foreach (var formParam in formData.Where(notFilePredicate))
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
            else
            {
                WriteLine(
                    string.Format("var response = await client.{0}Async(url, new StringContent(string.Empty)).ConfigureAwait(false);", httpMethod));
            }
        }

        private static void AddBaseProxyAndClasses(string proxyGeneratorNameSpace, string proxyGeneratorClassNamePrefix)
        {
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
            WriteLine("");
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
            // Web proxy response classes
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
            WriteLine("}");

            WriteLine("}");
        }

        private static async Task GetEndpointSwaggerDoc(TestServer testServer, SwaggerApiProxySettingsEndPoint endPoint, string baseUrl)
        {
            var swaggerString = await testServer.HttpClient.GetStringAsync(endPoint.Url.Replace(baseUrl, string.Empty));
            if (swaggerString == null)
            {
                throw new Exception(string.Format("Error downloading from: (TestServer){0}", endPoint.Url));
            }
            Console.WriteLine("Downloaded: {0}", endPoint.Url);
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
                Console.WriteLine("Downloaded: {0}", requestUri);
                _swaggerDocDictionaryList.GetOrAdd(endPoint, swaggerString);
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

        static void PrintHeaders(string proxyGeneratorNameSpace)
        {
            proxyGeneratorNameSpace = string.IsNullOrWhiteSpace(proxyGeneratorNameSpace)
                ? "Birch.Swagger.ProxyGenerator"
                : proxyGeneratorNameSpace;

            WriteLine("// This file was generated by Birch.Swagger.ProxyGenerator");
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

        static void WriteNullIfStatementOpening(string parameterName, string typeName)
        {
            WriteLine(IsIntrinsicType(typeName)
                ? string.Format("if ({0}.HasValue){{", parameterName)
                : string.Format("if ({0} != null){{", parameterName));
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
                var streamReader = new StreamReader(settingStream);
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
            if ((text == "}" || text == "};") && TextPadding != 0)
            {
                TextPadding--;
            }
            string textPadding = new string(' ', TextPadding * 4);
            FileText.AppendLine(string.Format("{1}{0}", text, textPadding));
            if (text.EndsWith("{"))
            {
                TextPadding++;
            }
        }
    }
}
