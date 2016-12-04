using System.Collections.Generic;

namespace Birch.Swagger.ProxyGenerator.Generator
{
    public class SwaggerApiProxySettings
    {
        public bool AutoRunOnBuildDisabled { get; set; }
        public string BaseUrl { get; set; } = "http://mydomain.com/";
        public string BaseDirectory { get; set; }
        public string ProxyOutputFile { get; set; } = "SwaggerProxy.cs";
        public string WebApiAssembly { get; set; }
        public string WebApiConfig { get; set; } = "web.config";
        public string ProxyGeneratorClassNamePrefix { get; set; } = "ProxyGenerator";
        public string ProxyGeneratorNamespace { get; set; } = "Birch.Swagger.ProxyGenerator";
        public string ProxyConstructorSuffix { get; set; } = "(Uri baseUrl) : base(baseUrl)";
        public SwaggerApiProxySettingsEndPoint[] EndPoints { get; set; }
        public List<string> ExcludedHeaderParameters { get; set; } = new List<string>();
    }
}