Shader "Hidden/Spear/SpriteBlurKernel"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        // Pass 0: horizontal convolution. Input is straight alpha, output is premultiplied.
        Pass
        {
            Name "Horizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #define BLUR_HORIZONTAL 1
            #define BLUR_PREMULTIPLY_INPUT 1
            #define BLUR_UNPREMULTIPLY_OUTPUT 0
            #define BLUR_DIRECTION float2(1.0, 0.0)

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_LinearClamp);

            float4 _SourceRect;     // xy = min UV, zw = size UV of the sprite inside the source texture
            float2 _TexelSize;      // 1 / render target size
            float2 _SourceTexelSize; // 1 / source texture size
            float _Sigma;           // gaussian sigma in source texels
            float _TapStep;         // texels between two taps
            int _TapCount;          // taps on each side of the center

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                o.uv = uv;
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float sigma = max(_Sigma, 1e-4);
                float invTwoSigmaSq = 1.0 / (2.0 * sigma * sigma);

                float4 sum = 0.0;
                float weightSum = 0.0;

                [loop]
                for (int i = -_TapCount; i <= _TapCount; i++)
                {
                    float distanceTexels = i * _TapStep;
                    float weight = exp(-distanceTexels * distanceTexels * invTwoSigmaSq);

                    // Out of bounds taps are transparent, but they still count in the
                    // normalization: this is a zero padded convolution, not edge clamping.
                    weightSum += weight;

                    float2 tapUV = uv + BLUR_DIRECTION * (distanceTexels * _TexelSize);

                    if (any(tapUV < 0.0) || any(tapUV > 1.0))
                        continue;

                    float2 sourceUV = _SourceRect.xy + tapUV * _SourceRect.zw;
                    float2 inset = 0.5 * abs(_SourceTexelSize);
                    sourceUV = clamp(sourceUV, _SourceRect.xy + inset, _SourceRect.xy + _SourceRect.zw - inset);

                    float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, sourceUV);
                    c.rgb *= c.a;

                    sum += c * weight;
                }

                sum /= max(weightSum, 1e-6);

            #if BLUR_UNPREMULTIPLY_OUTPUT
                half alpha = sum.a;
                half3 rgb = alpha > 1e-5 ? sum.rgb / alpha : half3(0.0, 0.0, 0.0);
                return half4(rgb, alpha);
            #else
                return sum;
            #endif
            }
            ENDHLSL
        }

        // Pass 1: vertical convolution. Input is premultiplied, output is straight alpha.
        Pass
        {
            Name "Vertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #define BLUR_HORIZONTAL 0
            #define BLUR_PREMULTIPLY_INPUT 0
            #define BLUR_UNPREMULTIPLY_OUTPUT 1
            #define BLUR_DIRECTION float2(0.0, 1.0)

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_LinearClamp);

            float4 _SourceRect;
            float2 _TexelSize;
            float2 _SourceTexelSize;
            float _Sigma;
            float _TapStep;
            int _TapCount;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                o.uv = uv;
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float sigma = max(_Sigma, 1e-4);
                float invTwoSigmaSq = 1.0 / (2.0 * sigma * sigma);

                float4 sum = 0.0;
                float weightSum = 0.0;

                [loop]
                for (int i = -_TapCount; i <= _TapCount; i++)
                {
                    float distanceTexels = i * _TapStep;
                    float weight = exp(-distanceTexels * distanceTexels * invTwoSigmaSq);

                    weightSum += weight;

                    float2 tapUV = uv + BLUR_DIRECTION * (distanceTexels * _TexelSize);

                    if (any(tapUV < 0.0) || any(tapUV > 1.0))
                        continue;

                    float2 sourceUV = _SourceRect.xy + tapUV * _SourceRect.zw;
                    float2 inset = 0.5 * abs(_SourceTexelSize);
                    sourceUV = clamp(sourceUV, _SourceRect.xy + inset, _SourceRect.xy + _SourceRect.zw - inset);

                    float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, sourceUV);

                    sum += c * weight;
                }

                sum /= max(weightSum, 1e-6);

                half alpha = sum.a;
                half3 rgb = alpha > 1e-5 ? sum.rgb / alpha : half3(0.0, 0.0, 0.0);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
