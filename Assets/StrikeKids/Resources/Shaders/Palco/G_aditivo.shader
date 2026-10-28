// ADITIVO (o aditivo.shader da edição Godot): brilhos, chamas dos jatos,
// faíscas — soma luz.
Shader "StrikeKids/G/aditivo"
{
    Properties
    {
        _textura ("Textura", 2D) = "white" {}
        _cor ("Cor", Vector) = (1, 1, 1, 1)
        _forca ("Força", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _textura;
            half4 _cor;
            half _forca;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 cor : TEXCOORD1; };

            v2f vert(entrada v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.cor = v.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = tex2D(_textura, i.uv) * _cor * i.cor;
                return half4(c.rgb * c.a * _forca, 1.0);
            }
            ENDCG
        }
    }
}
