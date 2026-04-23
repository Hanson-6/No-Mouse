Shader "Hidden/DarkVisionMask"
{
    Properties
    {
        _CenterWorld ("Center World", Vector) = (0, 0, 0, 0)
        _RadiusWorld ("Radius World", Float) = 2
        _SoftnessWorld ("Softness World", Float) = 0.05
        _LampCount ("Lamp Count", Float) = 0
        _BlackColor ("BlackColor", Color) = (0, 0, 0, 1)
        _DarkFade ("DarkFade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _CenterWorld;
            float _RadiusWorld;
            float _SoftnessWorld;
            float _LampCount;
            float4 _LampCentersWorldArr[16];
            float4 _LampRadiiSoftnessArr[16];
            float4 _BlackColor;
            float _DarkFade;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = world.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 delta = i.worldPos - _CenterWorld.xy;
                float dist = length(delta);
                float radius = max(_RadiusWorld, 0.0001);
                float softness = max(_SoftnessWorld, 0.0001);
                float outsideMask = smoothstep(radius - softness, radius + softness, dist);

                int lampCount = (int)clamp(_LampCount, 0.0, 16.0);
                [loop]
                for (int idx = 0; idx < 16; idx++)
                {
                    if (idx >= lampCount)
                        break;

                    float2 lampDelta = i.worldPos - _LampCentersWorldArr[idx].xy;
                    float lampDist = length(lampDelta);
                    float lampRadius = max(_LampRadiiSoftnessArr[idx].x, 0.0001);
                    float lampSoftness = max(_LampRadiiSoftnessArr[idx].y, 0.0001);
                    float lampOutside = smoothstep(lampRadius - lampSoftness, lampRadius + lampSoftness, lampDist);
                    outsideMask *= lampOutside;
                }

                float alpha = saturate(_BlackColor.a * outsideMask * saturate(_DarkFade));
                return fixed4(_BlackColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
