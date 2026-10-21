// Peça com textura e luz (a bola, o piso de madeira): textura x cor do
// vértice x tinta. _Aceso: o brilho da bola no strike/replay.
Shader "StrikeKids/Textura"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Tinta ("Tinta", Color) = (1, 1, 1, 1)
        _Aceso ("Aceso", Range(0, 1)) = 0
        _Brilho ("Brilho especular", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "StrikeLuz.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Tinta;
            half _Aceso;
            half _Brilho;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 luz : COLOR0; half4 extra : COLOR1; };

            v2f vert(entrada v)
            {
                v2f o;
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 n = (half3)UnityObjectToWorldNormal(v.normal);
                half nevoa;
                // a luz (sem o albedo): o pixel multiplica pela textura
                half3 l = SkIluminar(pm, n, v.color.rgb * _Tinta.rgb, nevoa);
                half3 vn = (half3)normalize(_WorldSpaceCameraPos.xyz - pm);
                half3 h = normalize((half3)_SkLuzDir.xyz + vn);
                half esp = pow(saturate(dot(n, h)), 24.0) * _Brilho;
                o.luz = half4(l * nevoa, nevoa);
                o.extra = half4(_SkNeblina.rgb * (1.0 - nevoa) + esp * _SkLuzCor.rgb * 0.6 + _Aceso * half3(0.35, 0.3, 0.12), 1.0);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 t = tex2D(_MainTex, i.uv);
                return half4(t.rgb * i.luz.rgb + i.extra.rgb, 1.0);
            }
            ENDCG
        }
    }
}
