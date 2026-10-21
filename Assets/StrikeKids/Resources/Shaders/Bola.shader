// A BOLA DE BOLICHE: a textura do bichinho com luz (como o Textura) + o
// verniz bem lustroso: o reflexo do MATCAP da fase (céu, luzes do teto, a
// pista, o neon) e os pontos de brilho das luzes. _Aceso: o brilho da bola
// no strike/replay.
Shader "StrikeKids/Bola"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Tinta ("Tinta", Color) = (1, 1, 1, 1)
        _Aceso ("Aceso", Range(0, 1)) = 0
        _Brilho ("Brilho do sol", Range(0, 1)) = 0.6
        _Reflexo ("Reflexo", Range(0, 1)) = 0.55
        _Luzes ("Brilho das luzes", Range(0, 1)) = 0.85
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
            sampler2D _SkMatcap;
            half4 _Tinta;
            half _Aceso;
            half _Brilho;
            half _Reflexo;
            half _Luzes;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 mc : TEXCOORD1;
                half4 luz : COLOR0;      // rgb = luz (o pixel multiplica pela textura); a = quanto reflete
                half4 extra : COLOR1;    // rgb = somado no fim (neblina, sol, aceso); a = força das luzes do matcap
            };

            v2f vert(entrada v)
            {
                v2f o;
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 n = (half3)UnityObjectToWorldNormal(v.normal);
                half nevoa;
                half3 l = SkIluminar(pm, n, v.color.rgb * _Tinta.rgb, nevoa);
                half3 vn = (half3)normalize(_WorldSpaceCameraPos.xyz - pm);
                half3 h = normalize((half3)_SkLuzDir.xyz + vn);
                half esp = pow(saturate(dot(n, h)), 32.0) * _Brilho;
                float3 nv = normalize(mul((float3x3)UNITY_MATRIX_V, (float3)n));
                o.mc = nv.xy * 0.5 + 0.5;
                half f = 1.0 - saturate((half)nv.z);
                f = _Reflexo * (0.16 + 0.84 * f * f);
                o.luz = half4(l * nevoa, f * nevoa);
                o.extra = half4(_SkNeblina.rgb * (1.0 - nevoa) + esp * _SkLuzCor.rgb * 0.6 + _Aceso * half3(0.35, 0.3, 0.12),
                                _Luzes * nevoa);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 t = tex2D(_MainTex, i.uv);
                half4 m = tex2D(_SkMatcap, i.mc);
                half3 c = lerp(t.rgb * i.luz.rgb, m.rgb, i.luz.a) + i.extra.rgb + m.a * i.extra.a;
                return half4(c, 1.0);
            }
            ENDCG
        }
    }
}
