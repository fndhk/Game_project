Shader "Custom/CharacterScanDots"
{
    Properties
    {
        _DotColor ("Dot Color", Color) = (1, 1, 1, 1)
        _DotSpacing ("Dot Spacing", Float) = 9
        _DotRadius ("Dot Radius", Range(0.02, 0.48)) = 0.16
        _DotSoftness ("Dot Softness", Range(0.001, 0.25)) = 0.06
        _DotJitter ("Dot Jitter", Range(0, 0.45)) = 0.12
        _Brightness ("Brightness", Range(0.1, 5.0)) = 1.5
        _FresnelPower ("Fresnel Power", Range(0.5, 8.0)) = 2.2
        _FresnelStrength ("Fresnel Strength", Range(0, 1.0)) = 0.18
        _WaveFeather ("Wave Feather", Range(0.001, 2.0)) = 0.12
        _BaseAlpha ("Base Alpha", Range(0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalRenderPipeline"
        }

        Pass
        {
            Name "CharacterScanDots"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DotColor;
                float _DotSpacing;
                float _DotRadius;
                float _DotSoftness;
                float _DotJitter;
                float _Brightness;
                float _FresnelPower;
                float _FresnelStrength;
                float _WaveFeather;
                float _BaseAlpha;
            CBUFFER_END

            float4 _ScanPulseOriginWS;
            float _ScanPulseRadius;
            float _ScanPulseThickness;
            float _ScanPulseActive;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 파동이 없을 때는 캐릭터가 보이지 않게 한다.
                clip(_ScanPulseActive - 0.001);

                // 현재 픽셀이 파동 띠 안에 있는지 계산한다.
                float worldDistance = distance(input.positionWS, _ScanPulseOriginWS.xyz);
                float bandDistance = abs(worldDistance - _ScanPulseRadius);
                float waveMask = 1.0 - smoothstep(_ScanPulseThickness, _ScanPulseThickness + _WaveFeather, bandDistance);

                // 화면 기준 점 패턴을 만든다.
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float2 pixelCoord = screenUV * _ScaledScreenParams.xy;
                float safeSpacing = max(_DotSpacing, 1.0);
                float2 gridCoord = pixelCoord / safeSpacing;
                float2 cellIndex = floor(gridCoord);
                float2 localCoord = frac(gridCoord) - 0.5;

                float jitterX = (Hash21(cellIndex + 1.37) - 0.5) * _DotJitter;
                float jitterY = (Hash21(cellIndex + 8.91) - 0.5) * _DotJitter;
                float2 jitteredLocal = localCoord + float2(jitterX, jitterY);

                float circleDistance = length(jitteredLocal);
                float dotMask = 1.0 - smoothstep(_DotRadius, _DotRadius + _DotSoftness, circleDistance);

                // 실루엣 가장자리가 조금 더 잘 읽히도록 프레넬을 약하게 더한다.
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = pow(saturate(1.0 - abs(dot(normalWS, viewDirWS))), _FresnelPower);
                float silhouetteBoost = lerp(1.0, saturate(1.0 + fresnel), _FresnelStrength);

                float alpha = dotMask * waveMask * _BaseAlpha;
                alpha *= silhouetteBoost;

                clip(alpha - 0.01);

                float3 finalColor = _DotColor.rgb * _Brightness;
                return half4(finalColor, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
