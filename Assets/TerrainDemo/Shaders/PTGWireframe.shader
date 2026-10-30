Shader "PTG/Wireframe"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _OffsetFactor ("Offset Factor", Float) = -1
        _OffsetUnits ("Offset Units", Float) = -1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+1" }

        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off
            Offset [_OffsetFactor], [_OffsetUnits]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
