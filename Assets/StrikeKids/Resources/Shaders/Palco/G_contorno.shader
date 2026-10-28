// CONTORNO (o contorno.shader da edição Godot OpenGL ES 2.0): casca
// invertida — a malha é inflada ao longo da normal e desenhada só por
// dentro (um material à parte: a peça é desenhada de novo com ele). A
// espessura acompanha a distância, para o traço ficar com a mesma grossura
// na tela de perto e de longe.
// RECUO: a casca é empurrada um pouquinho para longe da câmera. Contra o
// fundo o traço continua igual, mas onde uma peça encosta em outra (manga
// no ombro, lenço na camiseta) o traço fica escondido atrás dela.
// A cor sai pronta do vértice.
Shader "StrikeKids/G/contorno"
{
    Properties
    {
        _espessura ("Espessura", Float) = 0.0035
        _recuo ("Recuo", Float) = 0.015
        _cor_contorno ("Cor do contorno", Vector) = (0.17, 0.1, 0.27, 1)
        _usa_cor_vertice ("Usa a cor do vértice", Float) = 0.55
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "../StrikeCenario.cginc"

            float _espessura, _recuo, _usa_cor_vertice;
            float4 _cor_contorno;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; half3 cor : TEXCOORD0; };

            v2f vert(entrada v)
            {
                v2f o;
                // (o espaço da câmera é o mesmo nas duas edições; e inflar
                // pela normal dá o mesmo ponto com o X ao contrário: aqui não
                // precisa converter. O ponto inflado = o ponto + a normal
                // levada para a câmera, a mesma conta numa matriz só)
                float4 pv = mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)));
                float d = clamp(-pv.z, 0.8, 9.0);
                float3 nv = mul((float3x3)UNITY_MATRIX_V, mul((float3x3)unity_ObjectToWorld, v.normal));
                float4 p = float4(pv.xyz + nv * (_espessura * d), pv.w);
                p.xyz += normalize(p.xyz) * _recuo * d;
                o.pos = mul(UNITY_MATRIX_P, p);
                // a cor CRUA, como o shader do Godot 4 a entregava, até a tela
                o.cor = CnTela(lerp(CnS2L(_cor_contorno.rgb), CnS2L(v.color.rgb) * 0.28, _usa_cor_vertice));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(i.cor, 1.0);
            }
            ENDCG
        }
    }
}
