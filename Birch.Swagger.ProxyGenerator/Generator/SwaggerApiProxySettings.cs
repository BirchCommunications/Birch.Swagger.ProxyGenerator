namespace Birch.Swagger.ProxyGenerator.Generator
{
    public class SwaggerApiProxySettings
    {
        public bool AutoRunOnBuildDisabled { get; set; }
        public string BaseUrl { get; set; }
        public string ProxyOutputFile { get; set; }
        public string WebApiAssembly { get; set; }
        public string WebApiConfig { get; set; }
        public string ProxyGeneratorClassNamePrefix { get; set; }
        public string ProxyGeneratorNamespace { get; set; }
        public SwaggerApiProxySettingsEndPoint[] EndPoints { get; set; }
    }
}