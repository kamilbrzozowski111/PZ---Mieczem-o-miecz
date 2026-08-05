using UnityEngine;
using UnityEditor;
using System.IO;

public class ShaderAutoGenerator : EditorWindow
{
    private const string SHADER_FOLDER = "Assets/Shaders";

    [MenuItem("Tools/Pipeline/1. Generate Custom URP Shaders")]
    public static void GenerateShaders()
    {
        if (!Directory.Exists(SHADER_FOLDER))
        {
            Directory.CreateDirectory(SHADER_FOLDER);
        }

        CreateTriplanarShader();
        CreateTerrainShader();
        CreateFoliageShader();

        AssetDatabase.Refresh();
        Debug.Log("=== SUKCES: Szadery URP otrzymały pasaż DepthOnly pod Mgłę Ekranową! ===");
    }

    private static void CreateTriplanarShader()
    {
        string path = Path.Combine(SHADER_FOLDER, "TriplanarPBR.shader");
        File.WriteAllText(path, GetTriplanarShaderCode());
    }

    private static void CreateTerrainShader()
    {
        string path = Path.Combine(SHADER_FOLDER, "Terrain3Layer.shader");
        File.WriteAllText(path, GetTerrainShaderCode());
    }

    private static void CreateFoliageShader()
    {
        string path = Path.Combine(SHADER_FOLDER, "FoliageCutout.shader");
        File.WriteAllText(path, GetFoliageShaderCode());
    }

    private static string GetTriplanarShaderCode()
    {
        return @"Shader ""Custom/URP_TriplanarPBR""
{
    Properties
    {
        _BaseMap (""Albedo Map"", 2D) = ""white"" {}
        [NoScaleOffset] _BumpMap (""Normal Map"", 2D) = ""bump"" {}
        [NoScaleOffset] _SpecGlossMap (""Roughness Map"", 2D) = ""white"" {}
        [NoScaleOffset] _MetallicGlossMap (""Metallic Map"", 2D) = ""black"" {}
        
        [Header(Emission)]
        [NoScaleOffset] _EmissionMap (""Emission Map"", 2D) = ""white"" {}
        [HDR] _EmissionColor (""Emission Color"", Color) = (0,0,0,1)

        _TileScale (""Tile Scale"", Float) = 0.5
        _BlendExponent (""Blend Exponent"", Range(1, 16)) = 4.0
    }

    SubShader
    {
        Tags { ""RenderType""=""Opaque"" ""RenderPipeline""=""UniversalPipeline"" }
        LOD 300

        Pass
        {
            Name ""ForwardLit""
            Tags { ""LightMode""=""UniversalForward"" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 worldPos     : TEXCOORD0;
                float3 worldNormal  : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
            TEXTURE2D(_SpecGlossMap);     SAMPLER(sampler_SpecGlossMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_EmissionMap);      SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _EmissionColor;
                float _TileScale;
                float _BlendExponent;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.worldPos = posInputs.positionWS;
                output.worldNormal = normInputs.normalWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.worldNormal);
                float3 blendWeights = pow(abs(N), _BlendExponent);
                blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z);

                float2 tiling = _BaseMap_ST.xy * _TileScale;
                float2 offset = _BaseMap_ST.zw;

                float2 uvX = (input.worldPos.zy + offset) * tiling;
                float2 uvY = (input.worldPos.xz + offset) * tiling;
                float2 uvZ = (input.worldPos.xy + offset) * tiling;

                half4 colX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX);
                half4 colY = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY);
                half4 colZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ);
                half4 albedo = colX * blendWeights.x + colY * blendWeights.y + colZ * blendWeights.z;

                half3 nX = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX));
                half3 nY = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY));
                half3 nZ = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ));

                float3 axisSign = sign(N);
                float3 worldNX = float3(nX.z * axisSign.x, nX.y, nX.x * axisSign.x);
                float3 worldNY = float3(nY.x, nY.z * axisSign.y, nY.y);
                float3 worldNZ = float3(nZ.x * axisSign.z, nZ.y, nZ.z * axisSign.z);

                half3 blendedNormal = normalize(
                    worldNX * blendWeights.x +
                    worldNY * blendWeights.y +
                    worldNZ * blendWeights.z
                );

                half rX = SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uvX).r;
                half rY = SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uvY).r;
                half rZ = SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uvZ).r;
                half roughness = saturate(rX * blendWeights.x + rY * blendWeights.y + rZ * blendWeights.z);

                half mX = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uvX).r;
                half mY = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uvY).r;
                half mZ = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uvZ).r;
                half metallic = mX * blendWeights.x + mY * blendWeights.y + mZ * blendWeights.z;

                half3 emX = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uvX).rgb;
                half3 emY = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uvY).rgb;
                half3 emZ = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uvZ).rgb;
                half3 blendedEmission = (emX * blendWeights.x + emY * blendWeights.y + emZ * blendWeights.z) * _EmissionColor.rgb;

                Light mainLight = GetMainLight();
                half3 viewDir = normalize(_WorldSpaceCameraPos - input.worldPos);
                half3 halfDir = normalize(mainLight.direction + viewDir);

                half NdotL = saturate(dot(blendedNormal, mainLight.direction));
                half NdotH = saturate(dot(blendedNormal, halfDir));

                half smoothness = 1.0 - roughness;
                half specPower = exp2(smoothness * 8.0 + 1.0);
                half specular = pow(NdotH, specPower) * metallic;

                half3 ambient = half3(0.15, 0.15, 0.15) * albedo.rgb;
                half3 diffuse = mainLight.color * NdotL * albedo.rgb;
                half3 specColor = mainLight.color * specular;

                half3 finalColor = diffuse + specColor + ambient + blendedEmission;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name ""DepthOnly""
            Tags { ""LightMode""=""DepthOnly"" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}";
    }

    private static string GetTerrainShaderCode()
    {
        return @"Shader ""Custom/URP_Terrain3Layer""
{
    Properties
    {
        [NoScaleOffset] _GroundAlbedo (""Ground Albedo"", 2D) = ""white"" {}
        [NoScaleOffset] _GroundNormal (""Ground Normal"", 2D) = ""bump"" {}
        _GroundTiling (""Ground Tiling"", Float) = 1.0

        [NoScaleOffset] _LeavesAlbedo (""Leaves Albedo"", 2D) = ""white"" {}
        [NoScaleOffset] _LeavesNormal (""Leaves Normal"", 2D) = ""bump"" {}
        _LeavesTiling (""Leaves Tiling"", Float) = 1.0

        [NoScaleOffset] _CliffAlbedo (""Cliff Albedo"", 2D) = ""white"" {}
        [NoScaleOffset] _CliffNormal (""Cliff Normal"", 2D) = ""bump"" {}
        _CliffTiling (""Cliff Tiling"", Float) = 1.0
    }

    SubShader
    {
        Tags { ""RenderType""=""Opaque"" ""RenderPipeline""=""UniversalPipeline"" }
        LOD 300

        Pass
        {
            Name ""ForwardLit""
            Tags { ""LightMode""=""UniversalForward"" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 worldNormal  : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float3 worldBitangent : TEXCOORD3;
                float4 color        : COLOR;
            };

            TEXTURE2D(_GroundAlbedo);   SAMPLER(sampler_GroundAlbedo);
            TEXTURE2D(_GroundNormal);   SAMPLER(sampler_GroundNormal);
            TEXTURE2D(_LeavesAlbedo);   SAMPLER(sampler_LeavesAlbedo);
            TEXTURE2D(_LeavesNormal);   SAMPLER(sampler_LeavesNormal);
            TEXTURE2D(_CliffAlbedo);    SAMPLER(sampler_CliffAlbedo);
            TEXTURE2D(_CliffNormal);    SAMPLER(sampler_CliffNormal);

            CBUFFER_START(UnityPerMaterial)
                float _GroundTiling;
                float _LeavesTiling;
                float _CliffTiling;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.uv = input.uv;
                output.worldNormal = normInputs.normalWS;
                output.worldTangent = normInputs.tangentWS;
                output.worldBitangent = normInputs.bitangentWS;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float mask = input.color.r;

                float s1 = smoothstep(0.0, 0.26, mask);
                float s2 = smoothstep(0.26, 0.65, mask);

                float wGround = 1.0 - s1;
                float wLeaves = s1 - s2;
                float wCliff  = s2;

                float2 uvGround = input.uv * _GroundTiling;
                float2 uvLeaves = input.uv * _LeavesTiling;
                float2 uvCliff  = input.uv * _CliffTiling;

                half4 colGround = SAMPLE_TEXTURE2D(_GroundAlbedo, sampler_GroundAlbedo, uvGround);
                half4 colLeaves = SAMPLE_TEXTURE2D(_LeavesAlbedo, sampler_LeavesAlbedo, uvLeaves);
                half4 colCliff  = SAMPLE_TEXTURE2D(_CliffAlbedo, sampler_CliffAlbedo, uvCliff);
                half3 finalAlbedo = colGround.rgb * wGround + colLeaves.rgb * wLeaves + colCliff.rgb * wCliff;

                half3 nGround = UnpackNormal(SAMPLE_TEXTURE2D(_GroundNormal, sampler_GroundNormal, uvGround));
                half3 nLeaves = UnpackNormal(SAMPLE_TEXTURE2D(_LeavesNormal, sampler_LeavesNormal, uvLeaves));
                half3 nCliff  = UnpackNormal(SAMPLE_TEXTURE2D(_CliffNormal, sampler_CliffNormal, uvCliff));
                half3 blendedNormalTS = normalize(nGround * wGround + nLeaves * wLeaves + nCliff * wCliff);

                float3x3 tbn = float3x3(normalize(input.worldTangent), normalize(input.worldBitangent), normalize(input.worldNormal));
                float3 finalNormalWS = normalize(mul(blendedNormalTS, tbn));

                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(finalNormalWS, mainLight.direction));
                half3 lighting = mainLight.color * NdotL + half3(0.15, 0.15, 0.15);

                half3 finalColor = finalAlbedo * lighting;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name ""DepthOnly""
            Tags { ""LightMode""=""DepthOnly"" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}";
    }

    private static string GetFoliageShaderCode()
    {
        return @"Shader ""Custom/URP_FoliageCutout""
{
    Properties
    {
        _BaseMap (""Albedo Map (Alpha in A)"", 2D) = ""white"" {}
        _Cutoff (""Alpha Cutoff"", Range(0.0, 1.0)) = 0.5
        [NoScaleOffset] _BumpMap (""Normal Map"", 2D) = ""bump"" {}
        [NoScaleOffset] _SpecGlossMap (""Roughness Map"", 2D) = ""white"" {}
        [NoScaleOffset] _MetallicGlossMap (""Metallic Map"", 2D) = ""black"" {}
    }

    SubShader
    {
        Tags { ""RenderType""=""TransparentCutout"" ""Queue""=""AlphaTest"" ""RenderPipeline""=""UniversalPipeline"" }
        LOD 300

        Pass
        {
            Name ""ForwardLit""
            Tags { ""LightMode""=""UniversalForward"" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 worldPos     : TEXCOORD1;
                float3 worldNormal  : TEXCOORD2;
                float3 worldTangent : TEXCOORD3;
                float3 worldBitangent : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_SpecGlossMap);   SAMPLER(sampler_SpecGlossMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.worldPos = posInputs.positionWS;
                output.worldNormal = normInputs.normalWS;
                output.worldTangent = normInputs.tangentWS;
                output.worldBitangent = normInputs.bitangentWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(albedo.a - _Cutoff);

                half3 nTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv));
                float3x3 tbn = float3x3(normalize(input.worldTangent), normalize(input.worldBitangent), normalize(input.worldNormal));
                float3 N = normalize(mul(nTS, tbn));

                half roughness = SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, input.uv).r;
                half metallic = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv).r;

                Light mainLight = GetMainLight();
                half3 viewDir = normalize(_WorldSpaceCameraPos - input.worldPos);
                half3 halfDir = normalize(mainLight.direction + viewDir);

                half NdotL = saturate(dot(N, mainLight.direction));
                half NdotH = saturate(dot(N, halfDir));

                half smoothness = 1.0 - roughness;
                half specPower = exp2(smoothness * 8.0 + 1.0);
                half specular = pow(NdotH, specPower) * metallic;

                half3 ambient = half3(0.2, 0.2, 0.2) * albedo.rgb;
                half3 diffuse = mainLight.color * NdotL * albedo.rgb;
                half3 specColor = mainLight.color * specular;

                half3 finalColor = diffuse + specColor + ambient;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name ""DepthOnly""
            Tags { ""LightMode""=""DepthOnly"" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Cutoff;
            CBUFFER_END

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}";
    }
}