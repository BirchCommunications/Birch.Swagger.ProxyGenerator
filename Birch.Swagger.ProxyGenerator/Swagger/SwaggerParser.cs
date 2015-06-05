using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json.Linq;

namespace Birch.Swagger.ProxyGenerator.Swagger
{
    public class SwaggerParser
    {
        public ProxyDefinition ParseSwaggerDoc(string document, bool parseOperationIdForProxyName)
        {
            var jObject = JObject.Parse(document);

            var proxyDefinition = new ProxyDefinition();

            var infoToken = jObject["info"];
            proxyDefinition.Title = infoToken["title"].ToString();
            var descriptionToken = infoToken["description"];
            proxyDefinition.Description = descriptionToken != null ? descriptionToken.ToString() : null;

            this.ParsePaths(jObject, proxyDefinition, parseOperationIdForProxyName);
            this.ParseDefinitions(jObject, proxyDefinition);

            return proxyDefinition;
        }

        private void ParsePaths(JObject jObject, ProxyDefinition proxyDefinition, bool parseOperationIdForProxyName)
        {
            foreach (var pathToken in jObject["paths"].Cast<JProperty>())
            {
                var path = pathToken.Name;
                foreach (var operationToken in pathToken.First.Cast<JProperty>())
                {
                    var proxyName = string.Empty;
                    var method = operationToken.Name;
                    var operationId = operationToken.First["operationId"].ToString();

                    if (parseOperationIdForProxyName)
                    {
                        if (operationId.Contains("_"))
                        {
                            var underscoreLocation = operationId.IndexOf("_", StringComparison.OrdinalIgnoreCase);
                            proxyName = operationId.Substring(0, underscoreLocation);
                            operationId = operationId.Substring(underscoreLocation + 1);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(proxyName))
                    {
                        // didn't get the proxy name from the operation id, let's try the tags
                        var tagToken = operationToken.First["tags"];
                        if (tagToken != null)
                        {
                            var tags = tagToken.ToObject<List<string>>();
                            proxyName = tags.First();
                        }
                    }

                    var descriptionToken = operationToken.First["description"];
                    string description = null;
                    if (descriptionToken != null)
                    {
                        description = descriptionToken.ToString();
                    }

                    string returnType;
                    var schema = operationToken.First["responses"]["200"];
                    if (schema != null)
                    {
                        bool dummyNullable;
                        returnType = this.GetTypeName(schema, out dummyNullable);
                        if (returnType != null && returnType.Equals("Void"))
                            returnType = null;
                    }
                    else
                    {
                        returnType = null;
                    }

                    var parameters = new List<Parameter>();
                    var paramTokens = operationToken.First["parameters"];
                    if (paramTokens != null)
                    {
                        foreach (var prop in paramTokens)
                        {
                            var type = ParseType(prop);

                            var isRequired = prop["required"].ToObject<bool>();

                            ParameterIn parameterIn;
                            if (prop["in"].ToString().Equals("path"))
                            {
                                parameterIn = ParameterIn.Path;
                            }
                            else if (prop["in"].ToString().Equals("query"))
                            {
                                parameterIn = ParameterIn.Query;
                            }
                            else if (prop["in"].ToString().Equals("formData"))
                            {
                                parameterIn = ParameterIn.FormData;
                            }
                            else
                            {
                                parameterIn = ParameterIn.Body;
                            }


                            var propDescriptionToken = prop["description"];
                            string propDescription = string.Empty;
                            if (propDescriptionToken != null)
                            {
                                propDescription = propDescriptionToken.ToString();
                            }

                            string collectionFormat = string.Empty;
                            var collectionFormatToken = prop["collectionFormat"];
                            if (collectionFormatToken != null)
                            {
                                collectionFormat = collectionFormatToken.ToString();
                            }

                            parameters.Add(new Parameter(type, parameterIn, isRequired, propDescription, collectionFormat));
                        }
                    }

                    proxyDefinition.Operations.Add(new Operation(returnType, method, path, parameters, operationId, description, proxyName));
                }
            }
        }

        private void ParseDefinitions(JObject jObject, ProxyDefinition proxyDefinition)
        {
            foreach (var definitionToken in jObject["definitions"].Where(i => i.Type == JTokenType.Property).Cast<JProperty>())
            {
                bool addIt = true;
                var classDefinition = new ClassDefinition(definitionToken.Name);
                var allOf = definitionToken.First["allOf"];
                if (allOf != null)
                {
                    foreach (var itemToken in allOf)
                    {
                        var refType = itemToken["$ref"] as JValue;
                        if (refType != null)
                        {
                            classDefinition.Inherits = refType.Value.ToString();
                        }

                        var properties = itemToken["properties"];
                        if (properties != null)
                        {
                            foreach (var prop in properties)
                            {
                                var type = ParseType(prop);
                                classDefinition.Properties.Add(type);
                            }
                        }
                    }
                }
                else
                {
                    var properties = definitionToken.Value["properties"];
                    if (properties != null)
                    {
                        foreach (var prop in properties)
                        {
                            var type = ParseType(prop);
                            classDefinition.Properties.Add(type);
                        }
                    }
                    else
                    {
                        addIt = false;
                    }
                }


                classDefinition.Name = FixGenericName(classDefinition.Name);
                if (classDefinition.Name.Equals("Void", StringComparison.InvariantCulture))
                {
                    addIt = false;
                }

                if (addIt)
                {
                    proxyDefinition.ClassDefinitions.Add(classDefinition);
                }
            }
        }

        private TypeDefinition ParseType(JToken token)
        {
            bool isNullable;
            JToken workingToken;
            string name;
            if (token.First is JProperty)
            {
                workingToken = token;
                name = workingToken["name"].ToString();
            }
            else
            {
                workingToken = token.First;
                name = ((JProperty)token).Name;
            }

            var typeName = GetTypeName(workingToken, out isNullable);
            var enumToken = workingToken["enum"];
            string[] enumValues = null;
            if (enumToken != null)
            {
                List<string> enumList = new List<string>();
                bool anyRawNumbers = false;

                foreach (var enumValueToken in enumToken)
                {
                    var enumValue = enumValueToken.ToString();
                    decimal value;
                    if (Decimal.TryParse(enumValue, out value))
                    {
                        anyRawNumbers = true;
                    }
                    enumList.Add(enumValue);
                }
                if (anyRawNumbers == false)
                {
                    enumValues = enumList.ToArray();
                    typeName = FixTypeName(name + "Values");
                }
            }

            typeName = FixGenericName(typeName);
            TypeDefinition type = new TypeDefinition(typeName, name, enumValues, isNullable);
            return type;
        }

        private string ParseRef(string input)
        {
            return input.StartsWith("#/definitions/") ? input.Substring("#/definitions/".Length) : input;
        }

        private static string FixGenericName(string input)
        {
            if (input.Contains("[") == false || input.Contains("]") == false)
            {
                return input;
            }

            if (input.StartsWith("Dictionary[") || input.StartsWith("IDictionary["))
            {
                return input.Replace("[", "<").Replace("]", ">");
            }

            var firstBracket = input.IndexOf("[", StringComparison.InvariantCulture) + 1;
            var secondBracket = input.IndexOf("]", StringComparison.InvariantCulture);
            string typeName = input.Substring(firstBracket, secondBracket - firstBracket);
            string genericName = input.Substring(0, firstBracket - 1);

            return typeName + genericName;
        }

        private string GetTypeName(JToken token, out bool isNullable)
        {

            var refType = token["$ref"] as JValue;
            bool hasNullFlag = false;
            if (refType != null)
            {
                isNullable = false;
                return FixTypeName(this.ParseRef(refType.Value.ToString()));
            }

            var schema = token["schema"];
            if (schema != null)
            {
                return FixTypeName(this.GetTypeName(schema, out isNullable));
            }

            var type = token["type"] as JValue;
            if (type == null)
            {
                isNullable = false;
                return null;
            }

            var nullableToken = token["x-nullable"] as JValue;
            if (nullableToken != null)
            {
                hasNullFlag = true;
            }

            if (type.Value.Equals("array"))
            {
                isNullable = false;
                var jToken = token["items"];
                bool throwawayNullable; // we don't care what the underlying
                return string.Format("List<{0}>", this.GetTypeName(jToken, out throwawayNullable));
            }
            if (type.Value.Equals("boolean"))
            {
                isNullable = true;
                return (hasNullFlag) ? "bool?" : "bool";
            }

            if (type.Value.Equals("file"))
            {
                isNullable = true;
                return "file";
            }
            if (type.Value.Equals("string"))
            {
                var format = token["format"] as JValue;
                if (format == null)
                {
                    isNullable = false;
                    return "string";
                }


                if (format.Value.Equals("date") || format.Value.Equals("date-time"))
                {
                    isNullable = true;
                    return (hasNullFlag) ? "DateTime?" : "DateTime";
                }

                if (format.Value.Equals("byte"))
                {
                    isNullable = true;
                    return (hasNullFlag) ? "byte?" : "byte";
                }

                isNullable = false;
                return "string";
            }

            if (type.Value.Equals("integer"))
            {
                isNullable = true;
                var format = token["format"] as JValue;
                if (format != null)
                {
                    if (format.Value.Equals("int32"))
                        return (hasNullFlag) ? "int?" : "int";


                    if (format.Value.Equals("int64"))
                        return (hasNullFlag) ? "long?" : "long";
                }

                return "int";
            }

            if (type.Value.Equals("number"))
            {
                isNullable = true;
                var format = token["format"] as JValue;
                if (format != null)
                {
                    if (format.Value.Equals("float"))
                        return (hasNullFlag) ? "float?" : "float";

                    if (format.Value.Equals("double"))
                        return (hasNullFlag) ? "double?" : "double";

                    if (format.Value.Equals("decimal"))
                        return (hasNullFlag) ? "decimal?" : "decimal";
                }
            }



            isNullable = false;
            return "";
        }

        public static string FixTypeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            //var csharpCodeProvider = new CSharpCodeProvider();
            var output = input.Replace(" ", "");
            output = SwaggerParser.FixGenericName(output);

            if (char.IsLetter(output[0]) == false)
                output = "_" + output;

            return output;
        }

    }
}