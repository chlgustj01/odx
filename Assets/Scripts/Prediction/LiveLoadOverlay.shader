Shader "CraneSignalAI/LiveLoadOverlay"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,0,.3)
        _StencilRef ("Stencil Reference", Float) = 16
        _StencilMask ("Stencil Mask", Float) = 16
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 6
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil Pass", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent+102" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            Stencil {Ref [_StencilRef] ReadMask [_StencilMask] WriteMask [_StencilMask] Comp [_StencilComp] Pass [_StencilPass]}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;};
            struct v2f {float4 vertex:SV_POSITION;};
            float4 _Color;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);return o;}
            half4 frag(v2f i):SV_Target{return _Color;}
            ENDHLSL
        }
    }
}
