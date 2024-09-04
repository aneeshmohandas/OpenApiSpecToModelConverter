# OpenApiSpecToModelConverter

`OpenApiSpecToModelConverter` is a .NET tool designed to convert OpenAPI specifications into strongly-typed type script models, making it easier to integrate APIs into your .NET or client side applications.

## Installation

To install the `OpenApiSpecToModelConverter` tool globally, use the following command:

```bash
dotnet tool install -g OpenApiSpecToModelConverter

## Usage

Once the tool is installed, you can use it to generate models from an OpenAPI specification file (YAML format).

### Basic Command

```bash
generate-model --InputFile <path-to-openapi-spec> --OutputFileName <output-filename>

### Parameters

- **`--InputFile` or `-i`**: Specifies the path to the OpenAPI specification file. This can be a local file path or a URL to the specification.
  
- **`--OutputFileName` or `-o`**: Specifies the output file name where the generated models will be saved.

### Example

Here’s an example command that converts an OpenAPI spec located at `./openapi.yaml` into Type script models in the `output` file:

```bash
generate-modell --InputFile ./openapui.yaml --OutputFileName output
