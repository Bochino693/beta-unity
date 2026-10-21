// O REFLEXO NO VERNIZ DA PISTA (pinos): a peça desenhada espelhada embaixo
// do piso (Reflexos.cs). Só aparece onde fica ATRÁS do que já foi desenhado
// (ZTest Greater: a pista, fila 1990, vem antes; o resto da cena, depois,
// cobre). Clarinho, some com a profundidade e fora do deck. A luz também é
// espelhada (o reflexo mostra a peça acesa como ela é).
Shader "StrikeKids/Reflexo"
{
    Properties
    {
        _Tinta ("Tinta", Color) = (1, 1, 1, 1)
        _Forca ("Força", Range(0, 1)) = 0.34
        _Fundo ("Some a (m)", Float) = 0.45
        _Largura ("Meia largura do deck", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry-5" "IgnoreProjector" = "True" }
        Pass
        {
            ZWrite Off
            ZTest Greater
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "StrikeLuz.cginc"

            half4 _Tinta;
            half _Forca;
            float _Fundo;
            float _Largura;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; half4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; half4 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 n = (half3)UnityObjectToWorldNormal(v.normal);
                // luz espelhada (sol e céu vindos "de baixo")
                half3 L = half3(_SkLuzDir.x, -_SkLuzDir.y, _SkLuzDir.z);
                half ndl = dot(n, L);
                half dif = saturate((ndl + 0.08) / 0.32);
                dif = 0.72 * dif * dif * (3.0 - 2.0 * dif) + 0.28 * saturate(ndl);
                half t = saturate(-n.y * 0.5 + 0.5);
                half3 c = v.color.rgb * _Tinta.rgb * (lerp(_SkChao.rgb, _SkCeu.rgb, t) + _SkLuzCor.rgb * dif);
                half fundo = saturate(1.0 + pm.y / _Fundo);
                half dentro = saturate((_Largura - abs(pm.x)) / 0.06);
                o.cor = half4(c, _Forca * fundo * fundo * dentro);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target { return i.cor; }
            ENDCG
        }
    }
}
