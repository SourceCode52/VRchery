// A dark oval over the edges of the view. The quad is drawn straight in clip space, so it covers the whole
// eye image and looks the same in both eyes (no depth, no parallax).
Shader "VRchery/FocusVignette"
{
    Properties
    {
        _Color ("Colour (alpha = darkest the edges get)", Color) = (0, 0, 0, 0.85)
        _Inner ("Clear radius (1 = edge of the image)", Float) = 1.6
        _Feather ("Fade distance", Float) = 0.5
        _Strength ("Strength", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FocusVignette"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Inner;
                float _Feather;
                float _Strength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 ndc : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // Unity's Quad mesh spans -0.5..0.5, so x2 gives the full -1..1 clip square
                float2 p = input.positionOS.xy * 2.0;
                output.positionCS = float4(p, UNITY_NEAR_CLIP_VALUE, 1.0);
                output.ndc = p;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float r = length(input.ndc);
                float edge = smoothstep(_Inner, _Inner + max(_Feather, 0.001), r);
                return half4(_Color.rgb, edge * _Strength * _Color.a);
            }
            ENDHLSL
        }
    }
}
