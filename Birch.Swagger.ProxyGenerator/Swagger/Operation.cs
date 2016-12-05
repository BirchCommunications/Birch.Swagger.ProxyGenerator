using System.Collections.Generic;
using System.Linq;

namespace Birch.Swagger.ProxyGenerator.Swagger
{
    public class Operation
    {
        public Operation(string returnType, string method, string path, List<Parameter> parameters, string operationId, string description, string proxyName, bool isExcluded)
        {
            Path = path;
            Method = method;
            Parameters = parameters;
            OperationId = operationId;
            Description = description;
            ReturnType = returnType;
            ProxyName = proxyName;

            // validate
            IsExcluded = isExcluded;
            GetValidationMessages();
            IsValid = !ValidationMessages.Any();

            if (isExcluded)
            {
                ValidationMessages.Add("Manually excluded via configuration");
            }
        }

        public string ProxyName { get; }
        public string Path { get; }
        public string Method { get; }
        public List<Parameter> Parameters { get; }
        public string OperationId { get; }
        public string Description { get; }
        public string ReturnType { get; }
        public bool IsExcluded { get; }
        public bool IsValid { get; }
        public List<string> ValidationMessages { get; } = new List<string>();

        private void GetValidationMessages()
        {
            var excludeMessage = IsExcluded
                ? string.Empty
                : $" please fix swagger document or you can exclude this method by adding \"{OperationId}\" to the ExcludedOperationId array in the Birch.Swagger.ProxyGenerator.config.json";

            // check for duplicate parameters
            var duplicateParameterNames = Parameters.GroupBy(x => x.Type.Name).Where(x => x.Count() > 1).ToList();
            if (duplicateParameterNames.Any())
            {
                ValidationMessages.AddRange(duplicateParameterNames.Select(x => $"Duplicate key \"{x.Key}\" detected{excludeMessage}"));
            }

            // check for GET methods with body
            if (Method.ToLower() == "get" && Parameters.Any(x => x.ParameterIn == ParameterIn.Body))
            {
                ValidationMessages.Add($"GET methods cannot contain a body parameter{excludeMessage}");
            }
        }
    }
}