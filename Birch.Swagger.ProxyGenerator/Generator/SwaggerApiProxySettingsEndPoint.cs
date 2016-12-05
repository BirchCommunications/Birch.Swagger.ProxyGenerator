using System.Collections.Generic;

namespace Birch.Swagger.ProxyGenerator.Generator
{
    public class SwaggerApiProxySettingsEndPoint
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string Namespace { get; set; }
        public string Suffix { get; set; }
        public string BaseProxyClass { get; set; }
        public string ProxyConstructorSuffix { get; set; }
        public bool ParseOperationIdForProxyName { get; set; }
        public bool AppendAsyncToMethodName { get; set; }
        public List<string> ExcludedHeaderParameters { get; set; } = new List<string>();
        public List<string> ExcludedOperationIds { get; set; } = new List<string>();
    }
}