namespace Birch.Swagger.ProxyGenerator.Swagger
{
    public class TypeDefinition
    {
        public TypeDefinition(string typeName, string name, string[] enumValues = null, bool isNullableType = false)
        {
            this.TypeName = typeName;
            this.Name = name;
            this.EnumValues = enumValues;
            this.IsNullableType = isNullableType;
        }

        public string Name { get; set; }
        public string TypeName { get; set; }
        public string[] EnumValues { get; set; }
        public bool IsNullableType { get; set; }

        public string GetCleanTypeName()
        {
            return Name.Replace("$", "");
        }
    }
}