Shader "Custom/AntiAliasing"
{
    Properties
    {
        _MainTex("_MainTex", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 200
        pass
        {
            CGPROGRAM
            #pragma target 4.5
		    #pragma vertex vert
		    #pragma fragment frag
		    #include "UnityCG.cginc"
		    #include "Lighting.cginc"
		    #include "AutoLight.cginc"

            struct input{
                float4 vertex : POSITION;
                float2 uv : TEXCOORD;
            };

            struct v2f{
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            uniform uint _Sample;
            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert(input v){
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f o) : SV_TARGET{
                return float4(tex2D(_MainTex, o.uv).rgb, 1.0f / (_Sample + 1.0f));
            }
            ENDCG
        }
    }
}