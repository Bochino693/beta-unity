// A PISTA: a decoração parada (madeira, setas, trilha de estrelas, marcas)
// vem pronta na textura (já na cor da tela; A = máscara das estrelas). Aqui
// só o que mexe: as estrelas acendendo em sequência até os pinos, o NEON das
// bordas com as luzes correndo, o brilho que acompanha a bola e o reflexo do
// portal no verniz. A coordenada da textura vai direto do vértice para a
// leitura (na Mali-450 assim ela não perde precisão).
Shader "StrikeKids/Pista"
{
    Properties
    {
        _MainTex ("Pista pronta", 2D) = "white" {}
        _CorLuz ("Neon", Color) = (0.35, 0.85, 1, 1)
        _CorReflexo ("Reflexo do portal", Color) = (1, 0.76, 0.1, 1)
        _LuzCena ("Luz da cena", Color) = (1, 1, 1, 1)
        _Luz ("Acesa (jogada)", Range(0, 1)) = 0
        _Fase ("Fase das luzes", Float) = 0
        _FaseLenta ("Fase das estrelas", Float) = 0
        _Pulso ("Pulso", Range(0, 1)) = 0
        _BolaZ ("Bola z", Float) = 100
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
            half4 _CorLuz;
            half4 _CorReflexo;
            half4 _LuzCena;
            half _Luz;
            float _Fase;
            float _FaseLenta;
            half _Pulso;
            float _BolaZ;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 xd : TEXCOORD1;    // x: |x| (0..0.675), y: distância da linha de falta, z: brilho da bola, w: estrela acesa
                half4 ref : COLOR0;      // reflexo + neon prontos (por vértice)
            };

            v2f vert(entrada v)
            {
                v2f o;
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                float d = -pm.z;
                o.uv = v.uv;
                // estrelas: 6 grupos acendendo em sequência (onda que corre até os pinos)
                float k = clamp(floor((d - 2.3) / 1.35 + 0.5), 0.0, 5.0);
                half onda = step(0.72, frac(_FaseLenta - k * 0.14));
                half acesa = lerp(0.35, 1.0, max(onda * (1.0 - _Luz), _Luz * 0.7));
                // a bola: um brilho que anda junto dela
                half perto = saturate(1.0 - abs(d - _BolaZ) / 1.2);
                o.xd = half4(abs(pm.x), d, perto * perto, acesa);
                // reflexo do portal lá no fundo (perto dos pinos, olhando deitado)
                float3 vv = normalize(_WorldSpaceCameraPos.xyz - pm);
                half fres = pow(1.0 - saturate(vv.y), 4.0);
                half fundo = saturate((d - 4.5) / 6.0);
                o.ref = half4(_CorReflexo.rgb * fres * fundo * 0.35, 0.0);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 t = tex2D(_MainTex, i.uv);
                half3 c = t.rgb;
                // estrela: mais clara quando acesa
                c *= 1.0 + t.a * (i.xd.w * 0.9 - 0.25);
                c *= _LuzCena.rgb;
                c += i.ref.rgb;
                // neon das bordas: faixa fina perto da canaleta, com luzes correndo
                half borda = saturate((i.xd.x - 0.6) / 0.075);
                half corre = step(0.5, frac(i.xd.y * 0.8 - _Fase));
                half neon = borda * borda * (0.55 + 0.25 * _Pulso + 0.35 * _Luz) * (0.75 + 0.25 * corre);
                c = lerp(c, _CorLuz.rgb, neon);
                // brilho embaixo da bola rolando
                c += half3(0.25, 0.22, 0.12) * i.xd.z * _Luz;
                return half4(c, 1.0);
            }
            ENDCG
        }
    }
}
