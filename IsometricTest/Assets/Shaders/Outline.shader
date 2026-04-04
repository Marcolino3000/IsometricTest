Shader "Custom/URP/SpriteOutlinePixel"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness ("Outline Thickness (pixels)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalRenderPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_TexelSize; // x = 1/width, y = 1/height
            float4 _Color;

            float4 _OutlineColor;
            float _OutlineThickness;

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy * _OutlineThickness;

                float alpha = SampleAlpha(i.uv);

                // Sample neighbors
                float a1 = SampleAlpha(i.uv + float2(texel.x, 0));
                float a2 = SampleAlpha(i.uv + float2(-texel.x, 0));
                float a3 = SampleAlpha(i.uv + float2(0, texel.y));
                float a4 = SampleAlpha(i.uv + float2(0, -texel.y));

                float a5 = SampleAlpha(i.uv + texel);
                float a6 = SampleAlpha(i.uv - texel);
                float a7 = SampleAlpha(i.uv + float2(texel.x, -texel.y));
                float a8 = SampleAlpha(i.uv + float2(-texel.x, texel.y));

                float neighborAlpha = max(max(max(a1, a2), max(a3, a4)),
                                          max(max(a5, a6), max(a7, a8)));

                // If current pixel is transparent but neighbor is not → outline
                float outline = step(0.01, neighborAlpha) * (1 - step(0.01, alpha));

                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

                // Blend outline
                col.rgb = lerp(col.rgb, _OutlineColor.rgb, outline);
                col.a = max(col.a, outline * _OutlineColor.a);

                return col;
            }

            ENDHLSL
        }
    }
}