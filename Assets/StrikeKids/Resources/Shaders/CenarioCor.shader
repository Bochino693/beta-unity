// TOON LEVE DE COR (o toon_leve.shader da edição Godot): a cor do vértice x
// tinta, luz do sol em duas faixas, ambiente, brilho de borda, partes que
// brilham — por VÉRTICE nas duas faixas (a do sol e a da sombra, já com a cor
// pronta); por PIXEL só a passagem de uma para a outra (a sombra corta a
// forma numa curva lisa, sem seguir os triângulos). As NUVENS (luz própria
// de céu, sem neblina) e o fio dos balões. A luz vem no próprio material
// (Temas): cores LINEARES; só a direção do sol é a do cenário (_CnSolDir).
Shader "StrikeKids/CenarioCor"
{
    Properties
    {
        _Tinta ("Tinta", Color) = (1, 1, 1, 1)
        _Faixa ("Largura da divisa sol/sombra", Float) = 0.1
        _LuzMinima ("Luz mínima na sombra", Float) = 0
        _Borda ("Borda clara", Float) = 0.35
        _CorBorda ("Cor da borda", Color) = (1, 0.95, 0.9, 1)
        _Emissao ("Emissão", Color) = (0, 0, 0, 1)
        _BrilhoLuz ("Brilho das partes acesas", Float) = 1.4
        _SolCor ("Sol (linear)", Vector) = (1, 1, 1, 0)
        _Ambiente ("Ambiente (linear)", Vector) = (0.4, 0.4, 0.4, 0)
        _NeblinaCor ("Neblina (linear)", Vector) = (0.7, 0.8, 0.95, 0)
        _NeblinaDensidade ("Densidade da neblina", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "StrikeCenario.cginc"

            float4 _Tinta, _CorBorda, _Emissao;
            float _Faixa, _LuzMinima, _Borda, _BrilhoLuz, _NeblinaDensidade;
            float4 _SolCor, _Ambiente, _NeblinaCor;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; half3 sol : TEXCOORD0; half3 sombra : TEXCOORD1; half virada : TEXCOORD2; };

            v2f vert(entrada v)
            {
                v2f o;
                float3 base = CnS2L(v.color.rgb) * CnS2L(_Tinta.rgb);
                float4 pm = mul(unity_ObjectToWorld, v.vertex);
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float3 ate = _WorldSpaceCameraPos.xyz - pm.xyz;
                float dist = max(length(ate), 0.0001);
                float neblina = 1.0 - exp(-_NeblinaDensidade * dist);
                o.virada = (dot(n, _CnSolDir.xyz) - 0.02) * (0.5 / _Faixa) + 0.5;
                float rim = 1.0 - saturate(dot(n, ate) / dist);
                float acesa = 1.0 - saturate(abs(v.color.a - 0.5) * 5.0);
                float3 resto = CnS2L(_CorBorda.rgb) * _Borda * rim * rim * rim * base + CnS2L(_Emissao.rgb) + base * acesa * _BrilhoLuz;
                o.sol = CnTela(lerp(base * (_Ambiente.rgb + _SolCor.rgb) + resto, _NeblinaCor.rgb, neblina));
                o.sombra = CnTela(lerp(base * (_Ambiente.rgb + _SolCor.rgb * _LuzMinima) + resto, _NeblinaCor.rgb, neblina));
                o.pos = mul(UNITY_MATRIX_VP, pm);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(lerp(i.sombra, i.sol, saturate(i.virada)), 1.0);
            }
            ENDCG
        }
    }
}
