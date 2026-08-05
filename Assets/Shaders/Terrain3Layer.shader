Shader "Custom/URP_Terrain3Layer"
{
    Properties
    {
        [NoScaleOffset] _GroundAlbedo ("Ground Albedo", 2D) = "white" {}
        [NoScaleOffset] _GroundNormal ("Ground Normal", 2D) = "bump" {}
        _GroundTiling ("Ground Tiling", Float) = 1.0

        [NoScaleOffset] _LeavesAlbedo ("Leaves Albedo", 2D) = "white" {}
        [NoScaleOffset] _LeavesNormal ("Leaves Normal", 2D) = "bump" {}
        _LeavesTiling ("Leaves Tiling", Float) = 1.0

        [NoScaleOffset] _CliffAlbedo ("Cliff Albedo", 2D) = "white" {}
        [NoScaleOffset] _CliffNormal ("Cliff Normal", 2D) = "bump" {}
        _CliffTiling ("Cliff Tiling", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
}