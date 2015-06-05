namespace Birch.Swagger.ProxyGenerator.Swagger
{
    public class Parameter
    {
        public TypeDefinition Type { get; set; }
        public ParameterIn ParameterIn { get; set; }
        public bool IsRequired { get; set; }
        public string Description { get; set; }
        public string CollectionFormat { get; set; }

        public Parameter(TypeDefinition type, ParameterIn parameterIn, bool isRequired, string description, string collectionFormat)
        {
            this.Type = type;
            this.ParameterIn = parameterIn;
            this.IsRequired = isRequired;
            this.Description = description;
            this.CollectionFormat = collectionFormat;
        }
    }
}