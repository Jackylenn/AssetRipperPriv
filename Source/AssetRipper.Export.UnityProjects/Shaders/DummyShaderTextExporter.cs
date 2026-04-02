using AssetRipper.Assets;
using AssetRipper.Assets.Generics;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Extensions.Enums.Shader.SerializedShader;
using AssetRipper.SourceGenerated.Subclasses.SerializedProperties;
using AssetRipper.SourceGenerated.Subclasses.SerializedProperty;

namespace AssetRipper.Export.UnityProjects.Shaders;

public sealed class DummyShaderTextExporter : ShaderExporterBase
{
	public override bool Export(IExportContainer container, IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		return ExportShader((IShader)asset, path, fileSystem);
	}

	public static bool ExportShader(IShader shader, string path, FileSystem fileSystem)
	{
		using Stream fileStream = fileSystem.File.Create(path);
		using InvariantStreamWriter writer = new(fileStream);
		return ExportShader(shader, writer);
	}

	public static bool ExportShader(IShader shader, TextWriter writer)
	{
		if (shader.Has_ParsedForm())
		{
			writer.Write($"Shader \"{shader.ParsedForm.Name}\" {{\n");
			Export(shader.ParsedForm.PropInfo, writer);
			writer.Write("\t//DummyShaderTextExporter\n");
			WriteGeneratedShaderBody(shader, writer);
			writer.Write('\n');

			if (shader.ParsedForm.FallbackName != string.Empty)
			{
				writer.WriteIndent(1);
				writer.Write($"Fallback \"{shader.ParsedForm.FallbackName}\"\n");
			}
			if (shader.ParsedForm.CustomEditorName != string.Empty)
			{
				writer.WriteIndent(1);
				writer.Write($"//CustomEditor \"{shader.ParsedForm.CustomEditorName}\"\n");
			}
			writer.Write('}');
		}
		else
		{
			string header = shader.Script.String;
			int subshaderIndex = header.IndexOf("SubShader");
			if (subshaderIndex < 0)
			{
				return false;
			}
			writer.WriteString(header, 0, subshaderIndex);

			writer.Write("\t//DummyShaderTextExporter\n");
			writer.Write(GenericFallbackShader);

			writer.Write('}');
		}
		return true;
	}

	private static void WriteGeneratedShaderBody(IShader shader, TextWriter writer)
	{
		(string? texturePropertyName, string? colorPropertyName) = GetPrimaryProperties(shader);
		bool isTransparent = IsTransparentShader(shader);

		if (texturePropertyName is null && colorPropertyName is null)
		{
			writer.Write(GenericFallbackShader);
			return;
		}

		writer.Write("\tSubShader{\n");
		if (isTransparent)
		{
			writer.Write("\t\tTags { \"RenderType\"=\"Transparent\" \"Queue\"=\"Transparent\" }\n");
			writer.Write("\t\tLOD 200\n");
			writer.Write("\t\tBlend SrcAlpha OneMinusSrcAlpha\n");
			writer.Write("\t\tZWrite Off\n\n");
		}
		else
		{
			writer.Write("\t\tTags { \"RenderType\"=\"Opaque\" }\n");
			writer.Write("\t\tLOD 200\n\n");
		}

		writer.Write("\t\tPass\n");
		writer.Write("\t\t{\n");
		writer.Write("\t\t\tHLSLPROGRAM\n");
		writer.Write("\t\t\t#pragma vertex vert\n");
		writer.Write("\t\t\t#pragma fragment frag\n\n");
		writer.Write("\t\t\tfloat4x4 unity_ObjectToWorld;\n");
		writer.Write("\t\t\tfloat4x4 unity_MatrixVP;\n");
		if (texturePropertyName is not null)
		{
			writer.Write($"\t\t\tfloat4 {texturePropertyName}_ST;\n");
		}
		if (colorPropertyName is not null)
		{
			writer.Write($"\t\t\tfloat4 {colorPropertyName};\n");
		}
		writer.Write('\n');

		writer.Write("\t\t\tstruct Vertex_Stage_Input\n");
		writer.Write("\t\t\t{\n");
		writer.Write("\t\t\t\tfloat4 pos : POSITION;\n");
		if (texturePropertyName is not null)
		{
			writer.Write("\t\t\t\tfloat2 uv : TEXCOORD0;\n");
		}
		writer.Write("\t\t\t};\n\n");

		writer.Write("\t\t\tstruct Vertex_Stage_Output\n");
		writer.Write("\t\t\t{\n");
		if (texturePropertyName is not null)
		{
			writer.Write("\t\t\t\tfloat2 uv : TEXCOORD0;\n");
		}
		writer.Write("\t\t\t\tfloat4 pos : SV_POSITION;\n");
		writer.Write("\t\t\t};\n\n");

		writer.Write("\t\t\tVertex_Stage_Output vert(Vertex_Stage_Input input)\n");
		writer.Write("\t\t\t{\n");
		writer.Write("\t\t\t\tVertex_Stage_Output output;\n");
		if (texturePropertyName is not null)
		{
			writer.Write($"\t\t\t\toutput.uv = (input.uv.xy * {texturePropertyName}_ST.xy) + {texturePropertyName}_ST.zw;\n");
		}
		writer.Write("\t\t\t\toutput.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));\n");
		writer.Write("\t\t\t\treturn output;\n");
		writer.Write("\t\t\t}\n\n");

		if (texturePropertyName is not null)
		{
			writer.Write($"\t\t\tTexture2D<float4> {texturePropertyName};\n");
			writer.Write($"\t\t\tSamplerState sampler{texturePropertyName};\n\n");
		}

		writer.Write("\t\t\tfloat4 frag(Vertex_Stage_Output input) : SV_TARGET\n");
		writer.Write("\t\t\t{\n");
		string colorExpression = GetColorExpression(texturePropertyName, colorPropertyName);
		writer.Write($"\t\t\t\treturn {colorExpression};\n");
		writer.Write("\t\t\t}\n\n");

		writer.Write("\t\t\tENDHLSL\n");
		writer.Write("\t\t}\n");
		writer.Write("\t}\n");
	}

	private static string GetColorExpression(string? texturePropertyName, string? colorPropertyName)
	{
		string textureExpression = texturePropertyName is null
			? "float4(1.0, 1.0, 1.0, 1.0)"
			: $"{texturePropertyName}.Sample(sampler{texturePropertyName}, input.uv.xy)";
		return colorPropertyName is null
			? textureExpression
			: $"{textureExpression} * {colorPropertyName}";
	}

	private static (string? TexturePropertyName, string? ColorPropertyName) GetPrimaryProperties(IShader shader)
	{
		AccessListBase<ISerializedProperty>? properties = shader.ParsedForm?.PropInfo.Props;
		if (properties is null || properties.Count == 0)
		{
			return default;
		}

		string? textureProperty = GetPreferredPropertyName(
			properties,
			SerializedPropertyType.Texture,
			new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_BaseColorTexture", "_Albedo", "_Diffuse", "_DiffuseMap", "_MainTexture" });
		string? colorProperty = GetPreferredPropertyName(
			properties,
			SerializedPropertyType.Color,
			new[] { "_Color", "_BaseColor", "_BaseColour", "_TintColor", "_MainColor" });
		return (textureProperty, colorProperty);
	}

	private static string? GetPreferredPropertyName(AccessListBase<ISerializedProperty> properties, SerializedPropertyType propertyType, IReadOnlyList<string> preferredNames)
	{
		foreach (string preferredName in preferredNames)
		{
			ISerializedProperty? match = properties.FirstOrDefault(property => property.GetType_() == propertyType && property.Name == preferredName);
			if (match is not null)
			{
				return match.Name;
			}
		}

		return properties.FirstOrDefault(property => property.GetType_() == propertyType)?.Name;
	}

	private static bool IsTransparentShader(IShader shader)
	{
		string shaderName = shader.ParsedForm?.Name ?? string.Empty;
		return shaderName.Contains("transparent", StringComparison.OrdinalIgnoreCase)
			|| shaderName.Contains("fade", StringComparison.OrdinalIgnoreCase)
			|| shaderName.Contains("alpha", StringComparison.OrdinalIgnoreCase);
	}

	private static void Export(ISerializedProperties _this, TextWriter writer)
	{
		writer.WriteIndent(1);
		writer.Write("Properties {\n");
		foreach (ISerializedProperty prop in _this.Props)
		{
			Export(prop, writer);
		}
		writer.WriteIndent(1);
		writer.Write("}\n");
	}

	private static void Export(ISerializedProperty _this, TextWriter writer)
	{
		writer.WriteIndent(2);
		foreach (Utf8String attribute in _this.Attributes)
		{
			writer.Write($"[{attribute}] ");
		}
		SerializedPropertyFlag flags = (SerializedPropertyFlag)_this.Flags;
		if (flags.IsHideInInspector())
		{
			writer.Write("[HideInInspector] ");
		}
		if (flags.IsPerRendererData())
		{
			writer.Write("[PerRendererData] ");
		}
		if (flags.IsNoScaleOffset())
		{
			writer.Write("[NoScaleOffset] ");
		}
		if (flags.IsNormal())
		{
			writer.Write("[Normal] ");
		}
		if (flags.IsHDR())
		{
			writer.Write("[HDR] ");
		}
		if (flags.IsGamma())
		{
			writer.Write("[Gamma] ");
		}

		writer.Write($"{_this.Name} (\"{_this.Description}\", ");

		switch (_this.GetType_())
		{
			case SerializedPropertyType.Color:
			case SerializedPropertyType.Vector:
				writer.Write("Vector");
				break;

			case SerializedPropertyType.Float:
				writer.Write("Float");
				break;

			case SerializedPropertyType.Range:
				writer.Write($"Range({_this.DefValue_1_.ToStringInvariant()}, {_this.DefValue_2_.ToStringInvariant()})");
				break;

			case SerializedPropertyType.Texture:
				switch (_this.DefTexture.TexDim)
				{
					case 1:
						writer.Write("any");
						break;
					case 2:
						writer.Write("2D");
						break;
					case 3:
						writer.Write("3D");
						break;
					case 4:
						writer.Write("Cube");
						break;
					case 5:
						writer.Write("2DArray");
						break;
					case 6:
						writer.Write("CubeArray");
						break;
					default:
						throw new NotSupportedException("Texture dimension isn't supported");

				}
				break;

			case SerializedPropertyType.Int:
				writer.Write("Int");
				break;

			default:
				throw new NotSupportedException($"Serialized property type {_this.Type} isn't supported");
		}
		writer.Write(") = ");

		switch (_this.GetType_())
		{
			case SerializedPropertyType.Color:
			case SerializedPropertyType.Vector:
				writer.Write($"({_this.DefValue_0_.ToStringInvariant()},{_this.DefValue_1_.ToStringInvariant()},{_this.DefValue_2_.ToStringInvariant()},{_this.DefValue_3_.ToStringInvariant()})");
				break;

			case SerializedPropertyType.Float:
			case SerializedPropertyType.Range:
			case SerializedPropertyType.Int:
				writer.Write(_this.DefValue_0_.ToStringInvariant());
				break;

			case SerializedPropertyType.Texture:
				writer.Write($"\"{_this.DefTexture.DefaultName}\" {{}}");
				break;

			default:
				throw new NotSupportedException($"Serialized property type {_this.Type} isn't supported");
		}
		writer.Write('\n');
	}

	private const string GenericFallbackShader = """
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0);
			}

			ENDHLSL
		}
	}
	""".Replace("\r", "");
}
