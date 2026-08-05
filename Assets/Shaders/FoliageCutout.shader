Shader "Custom/URP_FoliageCutout"
{
    Properties
    {
        _BaseMap ("Albedo Map (Alpha in A)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _SpecGlossMap ("Roughness Map", 2D) = "white" {}
        [NoScaleOffset] _MetallicGlossMap ("Metallic Map", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
}