using System;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// A TELA DO JOGO — RESPONSIVA.
    ///
    /// O jogo se adapta a qualquer tela, em pé ou deitada, sem esticar:
    ///   * monitor em pé ligado no HDMI deitado (a máquina de boliche): a imagem
    ///     inteira é girada 90° (giro 1 ou -1) — direto na projeção das câmeras
    ///     (nenhuma imagem extra: o mais leve para a Mali-450);
    ///   * TV deitada, PC: sem giro (giro 0), o layout se reorganiza.
    ///
    /// O lado menor da tela vale sempre 1080 unidades. Em pé, 1080 x (1920 ou
    /// o que a proporção der); deitada, (1920...) x 1080.
    ///
    /// Sentido do giro (Ajustes do operador; variável STRIKE_GIRO no PC):
    ///   1  = topo do jogo na ESQUERDA do HDMI
    ///  -1  = topo do jogo na DIREITA do HDMI
    ///   0  = sem giro
    /// </summary>
    public static class Tela
    {
        public const float LADO_MENOR = 1080f;
        /// <summary>Giro de fábrica na TV Box (a máquina de boliche tem o monitor em pé).</summary>
        public const int GIRO_TV_BOX = 1;

        public static int Giro { get; private set; }
        /// <summary>Tamanho lógico (já em pé, se girado): o lado menor vale 1080.</summary>
        public static Vector2 Tamanho { get; private set; } = new Vector2(1080, 1920);
        /// <summary>Tamanho em pixels reais da imagem do jogo (já em pé, se girada).</summary>
        public static Vector2 Fisico { get; private set; } = new Vector2(1080, 1920);
        public static event Action Mudou;

        static int _w, _h;
        static bool _iniciada;

        public static bool EmPe => Tamanho.y >= Tamanho.x;

        public static void Iniciar()
        {
            if (_iniciada) return;
            _iniciada = true;
            Giro = GiroConfigurado();
            Aplicar();
        }

        /// <summary>Confere se a janela mudou de tamanho (chame a cada quadro).</summary>
        public static void Atualizar()
        {
            if (!_iniciada) Iniciar();
            if (Screen.width != _w || Screen.height != _h) Aplicar();
        }

        static int GiroConfigurado()
        {
            string amb = Environment.GetEnvironmentVariable("STRIKE_GIRO");
            if (!string.IsNullOrEmpty(amb) && int.TryParse(amb, out int g)) return Mathf.Clamp(g, -1, 1);
            object aj = Ajustes.Valor("tela", "giro");
            if (aj != null) return Mathf.Clamp(Json.Int(aj), -1, 1);
            // o giro escolhido na geração do APK (GERAR_APK.bat -Giro)
            var fab = Resources.Load<TextAsset>("Dados/fabrica");
            if (fab != null)
            {
                var d = Json.Dic(Json.Ler(fab.text));
                var gf = d != null ? Json.Pegar(d, "giro") : null;
                if (gf != null) return Mathf.Clamp(Json.Int(gf), -1, 1);
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            return GIRO_TV_BOX;
#else
            return 0;
#endif
        }

        /// <summary>O operador troca o giro na tela de ajustes (sem gerar outro APK).</summary>
        public static void DefinirGiro(int novo)
        {
            Giro = Mathf.Clamp(novo, -1, 1);
            Ajustes.Definir("tela", "giro", Giro);
            Aplicar();
        }

        static void Aplicar()
        {
            _w = Screen.width;
            _h = Screen.height;
            if (_w <= 0 || _h <= 0) return;
            float w = _w, h = _h;
            if (Giro != 0)
            {
                float t = w;
                w = h;
                h = t;
            }
            Fisico = new Vector2(w, h);
            if (w <= h) Tamanho = new Vector2(LADO_MENOR, Mathf.Round(LADO_MENOR * h / w));
            else Tamanho = new Vector2(Mathf.Round(LADO_MENOR * w / h), LADO_MENOR);
            Mudou?.Invoke();
        }

        /// <summary>Gira uma projeção (espaço de recorte) para o monitor em pé.</summary>
        public static Matrix4x4 Girar(Matrix4x4 p)
        {
            if (Giro == 0) return p;
            var r = Matrix4x4.identity;
            if (Giro > 0)
            {
                // topo do jogo na esquerda do HDMI: (x, y) -> (-y, x)
                r.m00 = 0; r.m01 = -1; r.m10 = 1; r.m11 = 0;
            }
            else
            {
                r.m00 = 0; r.m01 = 1; r.m10 = -1; r.m11 = 0;
            }
            return r * p;
        }

        /// <summary>Projeção da câmera 3D: `fov` vertical (graus) no lado de cima a baixo da imagem LÓGICA.</summary>
        public static Matrix4x4 Perspectiva(float fov, float perto, float longe)
        {
            return Girar(Matrix4x4.Perspective(fov, Tamanho.x / Mathf.Max(Tamanho.y, 1f), perto, longe));
        }

        /// <summary>Projeção das telas (uGUI, Canvas "World Space" de Tamanho.x x Tamanho.y,
        /// centrado na origem, câmera olhando para +Z).</summary>
        public static Matrix4x4 Ortografica()
        {
            return Girar(Matrix4x4.Ortho(-Tamanho.x / 2f, Tamanho.x / 2f, -Tamanho.y / 2f, Tamanho.y / 2f, 0.1f, 200f));
        }

        /// <summary>Ponto da tela lógica (x para a direita, y para BAIXO, como no Godot) a partir
        /// de uma posição de viewport (0..1, y para cima) da imagem lógica.</summary>
        public static Vector2 DeViewport(Vector2 vp)
        {
            return new Vector2(vp.x * Tamanho.x, (1f - vp.y) * Tamanho.y);
        }
    }
}
