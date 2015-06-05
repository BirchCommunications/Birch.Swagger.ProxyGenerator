namespace Birch.Swagger.ProxyGenerator.Generator
{
    public class SwaggerApiProxySettings
    {
        public string BaseUrl { get; set; }
        public string ProxyOutputFile { get; set; }
        public string WebApiAssembly { get; set; }
        public string WebApiConfig { get; set; }
        public SwaggerApiProxySettingsEndPoint[] EndPoints { get; set; }
    }
}