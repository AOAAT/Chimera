// --- START OF FILE UIHealthGrid.shader ---
Shader "UI/HealthGrid"
{
    Properties
    {
        _GridCount ("Grid Count", Float) = 10
        _LineThickness ("Line Thickness (厚度调小点)", Range(0.001, 0.5)) = 0.05
        _LineColor ("Line Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        // 强制叠加在最上层
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            float _GridCount;
            float _LineThickness;
            float4 _LineColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // 提取 UV
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 核心数学：把 0~1 的 UV 乘以格子数，取小数部分 (frac)
                // 比如 GridCount=10，那么 frac 的结果会在 0~1 之间循环 10 次！
                float gridUV = frac(i.uv.x * _GridCount);

                // 如果跑到了每个循环的末尾（比如 0.95 ~ 1.0），就涂成黑色！
                if (gridUV > 1.0 - _LineThickness)
                {
                    return _LineColor;
                }
                
                // 其他地方，强制变成完全透明，把底下的红血条露出来！
                return fixed4(0, 0, 0, 0); 
            }
            ENDCG
        }
    }
}