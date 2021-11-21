Shader "Custom/AntiAliasing"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        LOD 200
        pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex VertMotionVectors
            #pragma fragment FragMotionVectors
            #include "UnityCG.cginc"
            #include "../TAA/MoveVectors.cginc"

            ENDCG
        }

        pass
        {
            CGPROGRAM
            #pragma target 4.5
		    #pragma vertex vert
		    #pragma fragment frag
		    #include "UnityCG.cginc"
		    #include "Lighting.cginc"
		    #include "AutoLight.cginc"
            #include "../TAA/TemporalAA.cginc"

            ENDCG
        }
    }
}