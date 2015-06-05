using System.Collections.Generic;

namespace Birch.Swagger.ProxyGenerator.Swagger
{
    public class Operation
    {
        public Operation(string returnType, string method, string path, List<Parameter> parameters, string operationId, string description, string proxyName)
        {
            this.Path = path;
            this.Method = method;
            this.Parameters = parameters;
            this.OperationId = operationId;
            this.Description = description;
            this.ReturnType = returnType;
            this.ProxyName = proxyName;
        }

        public string ProxyName { get; set; }
        public string Path { get; set; }
        public string Method { get; set; }
        public List<Parameter> Parameters { get; set; }
        public string OperationId { get; set; }
        public string Description { get; set; }
        public string ReturnType { get; set; }
    }
}