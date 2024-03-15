Shader "WorleyNoiseGenerator/DebugWorley3D"
{
    Properties
    {
        _BaseMap("Base Map", 3D) = ""
        _SliceDepth ("Slice Depth", Float) = 0.5
        [KeywordEnum(UVSlice,WorldPos)] _Mapping("Mapping",int) = 0
    }
           
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag

            #pragma  shader_feature _MAPPING_UVSLICE _MAPPING_WORLDPOS

            //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UnityCG.cginc"

            float _SliceDepth;
            sampler3D _BaseMap;

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float3 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            
            
            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.vertex = UnityObjectToClipPos(IN.vertex);
                o.worldPos = mul(unity_ObjectToWorld, IN.vertex).xyz;
                o.uv = IN.uv;
                return o;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                #ifdef _MAPPING_UVSLICE
                return  tex3D(_BaseMap, float3(IN.uv.x, IN.uv.y, _SliceDepth));
                #elif _MAPPING_WORLDPOS
                return  tex3D(_BaseMap, IN.worldPos);
                #endif
            }
            
            ENDHLSL
        }
    }
}
