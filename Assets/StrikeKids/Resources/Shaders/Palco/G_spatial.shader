// O SPATIALMATERIAL SEM LUZ DA EDIÇÃO GODOT (as logos, o fundo escuro do
// gabinete): textura x cor (x cor do vértice, se pedido), transparente ou
// recortado. O Palco.cs ajusta mistura, fundura e lados pelo que o Godot
// usava (transparente, soma, corte, cull).
Shader "StrikeKids/G/spatial"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Cor ("Cor", Vector) = (1, 1, 1, 1)
        _Corte ("Corte (< 0: sem)", Float) = -1
        _CorVertice ("Cor do vértice", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _Origem ("Mistura origem", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Destino ("Mistura destino", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Lados ("Lados", Float) = 2
        _Grava ("Grava fundura", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        Pass
        {
            Blend [_Origem] [_Destino]
            ZWrite [_Grava]
            Cull [_Lados]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Cor;
            half _Corte, _CorVertice;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.cor = _Cor * lerp(half4(1, 1, 1, 1), (half4)v.color, _CorVertice);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv) * i.cor;
                // (o corte desligado fica em -1: nada some)
                clip(c.a - _Corte - 0.0001);
                return c;
            }
            ENDCG
        }
    }
}
