using System.Collections.Generic;

namespace Birch.Swagger.ProxyGenerator.Swagger
{
    public class ProxyDefinition
    {
        public ProxyDefinition()
        {
            this.ClassDefinitions = new List<ClassDefinition>();
            this.Operations = new List<Operation>();
        }

        public string Title { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }

        public List<ClassDefinition> ClassDefinitions { get; set; }
        public List<Operation> Operations { get; set; }
    }
}