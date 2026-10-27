Shader "NotebookDodge/Trace Effect Tint"
{
    Properties
    {
        _MainTex ("Sheet", 2D) = "white" {}
        _Color ("Effect Color", Color) = (1,1,1,1)
        _FrameBlend ("Frame Blend", Range(0,1)) = 0
        _NextFrameOffset ("Next Frame UV Offset", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _FrameBlend;
            float4 _NextFrameOffset;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, i.uv);
                fixed brightness = max(source.r, max(source.g, source.b));
                fixed4 next = tex2D(_MainTex, i.uv + _NextFrameOffset.xy);
                fixed nextBrightness = max(next.r, max(next.g, next.b));
                // Alpha-weighted crossfade avoids dark fringes at transparent edges.
                float alpha = lerp(source.a, next.a, _FrameBlend);
                float weightedBrightness = lerp(brightness * source.a, nextBrightness * next.a, _FrameBlend);
                brightness = weightedBrightness / max(alpha, 0.00001);
                // Replace yellow rather than multiplying purple into yellow.
                // Keep the original shape, brightness and soft alpha edges.
                return fixed4(_Color.rgb * brightness, alpha * _Color.a);
            }
            ENDCG
        }
    }
}
