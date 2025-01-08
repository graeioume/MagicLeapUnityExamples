Shader "Unlit/DepthSensorShader"
{
    Properties
    {
        [KeywordEnum(Depth, Confidence, Flags)] _Buffer("Buffer", Integer) = 0
        _MinDepth("Min Depth", Float) = 0
        _MaxDepth("Max Depth", Float) = 5
        [HideInInspector] _MainTex("Texture", 2D) = "white" {}
        _MapTex("Frame Data Map", 2D) = "white" {}
        [HideInInspector] _FlagTex("FlagColorTexture", 2D) = "white"
        [HideInInspector] _MetadataTex("Metadata Texture", 2D) = "white"
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _MetadataTex;
            sampler2D _MapTex;
            sampler2D _FlagTex;
            
            float4 _MainTex_ST;
            float _MinDepth;
            float _MaxDepth;
            int _Buffer;
            

            float InverseLerp(float v, float min, float max)
            {
                return clamp((v - min) / (max - min), 0.0, 1.0);
            }

            float Normalize(float v, float min, float end)
            {
                return InverseLerp(v, min, end);
            }

            float NormalizeDepth(float depth_meters)
            {
                return InverseLerp(depth_meters, _MinDepth, _MaxDepth);
            }

            float NormalizeConfidence(float confidence)
            {
                float conf = clamp(abs(confidence), 0.0, 0.5);
                return Normalize(conf, 0.0, 0.5);
            }

            fixed3 GetColorVisualization(float x) {
                return tex2D(_MapTex, fixed2(x, 0.5)).rgb;
            }

            fixed3 GetConfidenceVisualization(float x)
            {
                if(x <= 0.5f)
                {
                    return fixed3(1,1,1);
                }
                return fixed3(0,0,0);
            }

            float3 GetFlagColor(int conf)
            {
                //if flag is valid, then return white
                if(conf & 1)
                {
                    return float3(1,1,1);
                }

                const int valid_bits = (conf & (~3));
                fixed3 color = fixed3(0,0,0);
                [unroll]
                for(int j = 0; j < 8; j++)
                {
                    int i = 4 << j;
                    if(valid_bits & i)
                    {
                        int x = log2(valid_bits);
                        color += tex2D(_FlagTex, fixed2(x,0)).rgb;
                    }
                }
                return color;
            }

            float3 GetConfidenceColor(float conf)
            {
                return float3(conf, 1.0 - conf, 0.0);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float depth = tex2D(_MainTex, i.uv).r;
                float normalized_depth = NormalizeDepth(depth);
                if(_Buffer == 0)
                {
                    fixed4 depth_color = fixed4(GetColorVisualization(normalized_depth), 1.0);
                    // Values outside of range mapped to black.
                    if (depth < _MinDepth || depth > _MaxDepth)
                    {
                        depth_color.rgb *= 0.0;
                    }

                    return depth_color;
                }

                if(_Buffer == 1)
                {
                    //Confidence texture is applied
                    float confidence = tex2D(_MetadataTex, i.uv).r;
                    float normalized_confidence = NormalizeConfidence(confidence);
                    fixed4 depth_color = fixed4(GetConfidenceColor(normalized_confidence), 1.0);
                    depth_color *= (1.0 - depth / 2.0);
                    return depth_color;
                }

                if(_Buffer == 2)
                {
                    int flag = tex2D(_MetadataTex, i.uv).r;
                    fixed4 depth_color = fixed4(GetFlagColor(flag), 1.0);
                    depth_color *= (1.0 - depth / 2.0);
                    return depth_color;
                }

                return fixed4(1, 1,1,1);
            }
            ENDCG
        }
    }
}