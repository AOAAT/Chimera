Shader "Chimera/SolidAlpha"
{
    Properties {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Outline Color", Color) = (1,1,1,1)
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata_t { float4 vertex : POSITION; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 texcoord : TEXCOORD0; };
            sampler2D _MainTex; float4 _Color;
            v2f vert (appdata_t v) { 
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.texcoord = v.texcoord; return o; 
            }
            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.texcoord);
                // 核心：丢弃原图颜色，只保留透明度形状，并染上描边颜色！
                return fixed4(_Color.rgb, col.a * _Color.a); 
            }
            ENDCG
        }
    }
}