Shader "CraneSignalAI/PredictionOverlay"
{
    Properties
    {
        _Color ("Color", Color) = (0,1,1,1)
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil Pass", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            // Swept mesh uses NotEqual/Replace so overlapping intervals blend once.
            Stencil { Ref 64 ReadMask 64 WriteMask 64 Comp [_StencilComp] Pass [_StencilPass] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; };
            float4 _Color;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); return o; }
            half4 frag(v2f i) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
