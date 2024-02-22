Shader"Moonflow/MFFaceSDFExp"
{
    Properties
    {
        _FaceSDF ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
               float4 vertex : POSITION;
               float2 uv : TEXCOORD0;
            };

            struct v2f
            {
               float4 vertex : SV_POSITION;
               float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_FaceSDF);
            SAMPLER(sampler_FaceSDF);

            float CalculateFaceShadow(float3 lightDir, float3 forward, float2 uv)
            {
                float LR = cross(forward, -lightDir).y;
                // 左右翻转
                float2 flipUV = float2(1 - uv.x, uv.y);
                float lightMap = 0;
                float lightMapL = SAMPLE_TEXTURE2D(_FaceSDF, sampler_FaceSDF, uv).r;
                float lightMapR = SAMPLE_TEXTURE2D(_FaceSDF, sampler_FaceSDF, flipUV).r;

                lightMap = LR < 0 ? lightMapL : lightMapR;
                lightDir.y = 0;
                forward.y = 0;
                float s = dot(-lightDir, forward);
                return step(1-lightMap , s );
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                Light l = GetMainLight();
                float3 forward = unity_ObjectToWorld._m20_m21_m22;
                return CalculateFaceShadow(l.direction, forward, i.uv);
            }
            ENDHLSL
        }
    }
}
