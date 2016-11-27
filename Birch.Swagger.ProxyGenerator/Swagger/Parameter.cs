namespace Birch.Swagger.ProxyGenerator.Swagger
{
    public class Parameter
    {
        public TypeDefinition Type { get; set; }
        public ParameterIn ParameterIn { get; set; }
        public bool IsRequired { get; set; }
        public string Description { get; set; }
        public string CollectionFormat { get; set; }
        public string DefaultValue { get; set; }

        public Parameter(TypeDefinition type, ParameterIn parameterIn, bool isRequired, string description, string collectionFormat, string propDefaultValue)
        {
            Type = type;
            ParameterIn = parameterIn;
            IsRequired = isRequired;
            Description = description;
            CollectionFormat = collectionFormat;
            DefaultValue = propDefaultValue;
        }
    }
}