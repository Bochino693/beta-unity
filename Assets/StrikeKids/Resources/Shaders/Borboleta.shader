// BORBOLETA (detalhes extras, Temas.Borboletas — o borboleta.shader da
// edição Godot): poucos triângulos com a cor nos vértices. As asas BATEM
// aqui no vértice: as do lado +X e as do lado -X giram em volta do corpo
// (o eixo Z) — o voo só move a borboleta inteira. Cada uma bate no seu
// tempo: a fase sai da posição dela no mundo.
Shader "StrikeKids/Borboleta"
{
    Properties
    {
        _Bater ("Batidas (rad/s)", Float) = 11
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "StrikeCenario.cginc"

            float _Bater;

            struct entrada { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; half3 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                float3 p = v.vertex.xyz;
                float lado = sign(p.x);
                float d = abs(p.x);
                float2 lugar = float2(-unity_ObjectToWorld._m03, unity_ObjectToWorld._m23);
                float fase = dot(lugar, float2(1.7, 2.3));
                float a = 0.2 + 1.05 * (0.5 + 0.5 * sin(_Time.y * _Bater + fase));
                p.x = lado * d * cos(a);
                p.y += d * sin(a);
                // a asa de pé pega menos luz que a aberta
                o.cor = v.color.rgb * (1.0 - 0.22 * a);
                o.pos = UnityObjectToClipPos(float4(p, 1.0));
                return o;
            }

            half4 frag(v2f i) : SV_Target { return half4(i.cor, 1.0); }
            ENDCG
        }
    }
}
