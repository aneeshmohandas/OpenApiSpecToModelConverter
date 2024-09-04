using System;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OpenApiSpecToModelCoverter
{
    class Program
    {
        private class Options
        {
            public Options(string inputFileLocation, string outputFileName)
            {
                OutputFileName = outputFileName;
                InputFileLocation = inputFileLocation;
            }

            [Option('o', "OutputFileName", Required = false, HelpText = "Output file name")]
            public string OutputFileName { get; }

            [Option('i', "InputFile", Required = false, HelpText = "Input file location.")]
            public string InputFileLocation { get; }
        }

        static void Main(string[] args)
        {
            var outputFileName = "";
            var inputFileLocation = "";
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    outputFileName = o.OutputFileName;
                    inputFileLocation = o.InputFileLocation;
                });
            if (string.IsNullOrEmpty(inputFileLocation))
            {
                Console.WriteLine("Enter input file location");
                inputFileLocation = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(outputFileName))
            {
                Console.WriteLine("Enter output file name");
                outputFileName = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(inputFileLocation) || string.IsNullOrEmpty(outputFileName))
            {
                Console.WriteLine("input file location and output file name  both are required");
                return;
            }
            // Read the OpenAPI YAML file
            var yamlContent = File.ReadAllText(inputFileLocation);

            // Deserialize YAML to C# object
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var openApiSpec = deserializer.Deserialize<dynamic>(yamlContent);

            // Generate TypeScript code
            var tsCode = GenerateTypeScript(openApiSpec);

            // Write TypeScript to file
            File.WriteAllText(outputFileName + ".ts", tsCode);

            Console.WriteLine($"TypeScript code generated and saved to {outputFileName}");
        }

        static string GenerateTypeScript(dynamic openApiSpec)
        {
            var tsBuilder = new StringBuilder();
            var generatedInterfaces = new HashSet<string>();
            var parentNames = new List<string>();

            if (openApiSpec.ContainsKey("components") && openApiSpec["components"].ContainsKey("schemas"))
            {
                var schemas = openApiSpec["components"]["schemas"];
                foreach (var schema in (IDictionary<object, object>)schemas)
                {
                    var schemaName = schema.Key.ToString();
                    var schemaDetails = schema.Value;
                    GenerateTypeScriptForSchema(schemaName ?? "", schemaDetails, tsBuilder, 1, generatedInterfaces, parentNames);
                    tsBuilder.AppendLine("}");
                    tsBuilder.AppendLine();
                }
            }

            return tsBuilder.ToString();
        }

        static string GenerateNestedInterfaceName(List<string> parentNames, string propertyName)
        {
            // Join parent names with the current property name using an underscore
            string joinedNames = string.Join("_", parentNames.Concat(new[] { propertyName }));

            // Capitalize the first letter of each segment to follow TypeScript naming conventions
            return string.Concat(joinedNames.Split('_').Select(part => char.ToUpper(part[0]) + part.Substring(1)));
        }

        static void GenerateTypeScriptForSchema(string parentName, dynamic schemaDetails, StringBuilder tsBuilder, int indentLevel, HashSet<string> generatedInterfaces, List<string> parentNames)
        {
            string indent = new string(' ', indentLevel * 4);

            if (schemaDetails.ContainsKey("properties"))
            {
                tsBuilder.AppendLine($"interface {parentName} {{");
                var properties = schemaDetails["properties"];
                foreach (var property in (IDictionary<object, object>)properties)
                {
                    var propertyName = property.Key.ToString();
                    var propertyDetails = property.Value;

                    // Determine TypeScript type based on OpenAPI property type
                    string tsType = DetermineTsTypeFromSchema(propertyName, propertyDetails, tsBuilder, indentLevel, generatedInterfaces, parentNames);

                    tsBuilder.AppendLine($"{indent}{propertyName}: {tsType};");
                }
            }

            if (schemaDetails.ContainsKey("allOf"))
            {
                foreach (var subSchema in schemaDetails["allOf"])
                {
                    if (subSchema.ContainsKey("$ref"))
                    {
                        // Handle references to other schemas
                        var refPath = subSchema["$ref"].ToString();
                        string[] refPathDetails = refPath.Split('/');
                        var refName = refPathDetails.Last(); // e.g., "#/components/schemas/Example" -> "Example"
                        tsBuilder.AppendLine($"interface {parentName} extends {refName}  {{");
                    }
                    if (subSchema.ContainsKey("properties"))
                    {
                        var properties = subSchema["properties"];
                        foreach (var property in (IDictionary<object, object>)properties)
                        {
                            var propertyName = property.Key.ToString();
                            var propertyDetails = property.Value;

                            // Determine TypeScript type based on OpenAPI property type
                            string tsType = DetermineTsTypeFromSchema(propertyName, propertyDetails, tsBuilder, indentLevel, generatedInterfaces, parentNames);

                            tsBuilder.AppendLine($"{indent}{propertyName}: {tsType};");
                        }
                    }
                }
            }
        }

        static string DetermineTsTypeFromSchema(string propertyName, dynamic propertyDetails, StringBuilder tsBuilder, int indentLevel, HashSet<string> generatedInterfaces, List<string> parentNames)
        {
            if (propertyDetails.ContainsKey("type"))
            {
                var type = propertyDetails["type"].ToString();
                switch (type)
                {
                    case "string":
                        return "string";
                    case "integer":
                        return "number";
                    case "number":
                        return "number";
                    case "boolean":
                        return "boolean";
                    case "array":
                        var itemsType = DetermineTsTypeFromSchema(propertyName, propertyDetails["items"], tsBuilder, indentLevel, generatedInterfaces, parentNames);
                        return $"{itemsType}[]";
                    case "object":
                        var newParentNames = new List<string>(parentNames) { propertyName };
                        var nestedInterfaceName = GenerateNestedInterfaceName(newParentNames, propertyName);
                        if (!generatedInterfaces.Contains(nestedInterfaceName))
                        {
                            generatedInterfaces.Add(nestedInterfaceName);
                            tsBuilder.AppendLine();
                            tsBuilder.AppendLine($"{new string(' ', indentLevel * 4)}interface {nestedInterfaceName} {{");
                            GenerateTypeScriptForSchema(nestedInterfaceName, propertyDetails, tsBuilder, indentLevel + 1, generatedInterfaces, newParentNames);
                            tsBuilder.AppendLine($"{new string(' ', indentLevel * 4)}}}");
                        }
                        return nestedInterfaceName;
                }
            }

            // Handling complex constructs like oneOf, allOf, anyOf
            if (propertyDetails.ContainsKey("oneOf"))
            {
                var types = new List<string>();
                foreach (var subSchema in propertyDetails["oneOf"])
                {
                    var tsType = DetermineTsTypeFromSchema(propertyName, subSchema, tsBuilder, indentLevel, generatedInterfaces, parentNames);
                    types.Add(tsType);
                }
                return string.Join(" | ", types); // Union type in TypeScript
            }

            if (propertyDetails.ContainsKey("allOf"))
            {
                var types = new List<string>();
                foreach (var subSchema in propertyDetails["allOf"])
                {
                    var tsType = DetermineTsTypeFromSchema(propertyName, subSchema, tsBuilder, indentLevel, generatedInterfaces, parentNames);
                    types.Add(tsType);
                }
                return string.Join(" & ", types); // Intersection type in TypeScript
            }

            if (propertyDetails.ContainsKey("anyOf"))
            {
                var types = new List<string>();
                foreach (var subSchema in propertyDetails["anyOf"])
                {
                    var tsType = DetermineTsTypeFromSchema(propertyName, subSchema, tsBuilder, indentLevel, generatedInterfaces, parentNames);
                    types.Add(tsType);
                }
                return string.Join(" | ", types); // Union type in TypeScript
            }

            if (propertyDetails.ContainsKey("$ref"))
            {
                // Handle references to other schemas
                var refPath = propertyDetails["$ref"].ToString();
                var refName = refPath.Split('/').Last(); // e.g., "#/components/schemas/Example" -> "Example"
                return refName;
            }

            return "any"; // fallback type
        }
        static void GenerateTypeScriptForSchema(dynamic schemaDetails, StringBuilder tsBuilder, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);

            if (schemaDetails.ContainsKey("properties"))
            {
                var properties = schemaDetails["properties"];
                foreach (var property in (IDictionary<object, object>)properties)
                {
                    var propertyName = property.Key.ToString();
                    var propertyDetails = property.Value;

                    // Determine TypeScript type based on OpenAPI property type
                    string tsType = DetermineTsTypeFromSchema(propertyDetails);

                    tsBuilder.AppendLine($"{indent}{propertyName}: {tsType};");
                }
            }
        }

        static string DetermineTsTypeFromSchema(dynamic propertyDetails)
        {
            if (propertyDetails.ContainsKey("type"))
            {
                var type = propertyDetails["type"].ToString();
                switch (type)
                {
                    case "string":
                        return "string";
                    case "integer":
                        return "number";
                    case "number":
                        return "number";
                    case "boolean":
                        return "boolean";
                    case "array":
                        var itemsType = DetermineTsTypeFromSchema(propertyDetails["items"]);
                        return $"{itemsType}[]";
                    case "object":
                        return "any"; // or you can recursively generate interfaces
                }
            }

            if (propertyDetails.ContainsKey("$ref"))
            {
                // Handle references to other schemas
                var refPath = propertyDetails["$ref"].ToString();
                var refName = refPath.Split('/').Last(); // e.g., "#/components/schemas/Example" -> "Example"
                return refName;
            }

            return "any"; // fallback type
        }
    }
}