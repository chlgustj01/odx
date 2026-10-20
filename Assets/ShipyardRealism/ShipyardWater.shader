Shader "Shipyard/CoastalWater"
{
 Properties { _BaseColor("Deep water",Color)=(0.025,0.16,0.22,1) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor;
   CBUFFER_END
   struct A { float4 p:POSITION; };
   struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float fog:TEXCOORD1; };
   V vert(A a) { V o; o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.fog=ComputeFogFactor(o.p.z);return o; }
   half4 frag(V i):SV_Target
   {
    float2 q=i.w.xz; float t=_Time.y;
    float a=q.x*.13+q.y*.09+t*.4,b=q.x*.23-q.y*.16-t*.5,c=q.x*.031+q.y*.09+t*.2;
    float3 n=normalize(float3(.008*cos(a)+.005*cos(b),1,.007*sin(a)+.004*cos(c)));
    float3 v=GetWorldSpaceNormalizeViewDir(i.w); Light l=GetMainLight();
    float fres=pow(1-saturate(dot(n,v)),4);
    float spec=pow(saturate(dot(n,normalize(l.direction+v))),60)*.12;
    float ripple=sin(a)*sin(b)*.002+sin(c)*.0015;
    float3 col=lerp(_BaseColor.rgb,float3(.36,.56,.66),fres*.8)+ripple+spec*l.color;
    return half4(MixFog(col,i.fog),1);
   }
   ENDHLSL
  }
 }
}
