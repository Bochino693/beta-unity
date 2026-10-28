using UnityEngine;
using UnityEngine.Rendering;

namespace StrikeKids
{
    /// <summary>
    /// O REFLEXO NO VERNIZ DA PISTA: os pinos e a bola desenhados de novo,
    /// espelhados embaixo do piso (y = 0), clarinhos e sumindo com a
    /// profundidade — como numa pista de boliche de verdade.
    ///
    /// Barato (bom para a Mali-450): 11 malhas pequenas por quadro, sem câmera
    /// nem textura extra. A pista é desenhada antes de tudo (fila 1990); o
    /// reflexo (fila 1995, Reflexo.shader) só pinta onde fica ATRÁS do que já
    /// está na tela — embaixo do verniz —; o resto da cena (pinos, bola,
    /// canaletas, bichinhos) vem depois e cobre o que estiver por cima.
    /// </summary>
    public class Reflexos : MonoBehaviour
    {
        public const int FILA_PISTA = 1990;
        const int FILA_REFLEXO = 1995;

        // espelho no piso, 4 mm mais fundo: o reflexo fica sempre ATRÁS do verniz (sem
        // empate de profundidade na linha onde o pino encosta na pista)
        static readonly Matrix4x4 ESPELHO = Matrix4x4.Translate(new Vector3(0f, -0.004f, 0f)) * Matrix4x4.Scale(new Vector3(1f, -1f, 1f));

        /// <summary>Desligado: nada de reflexo (qualidade baixa).</summary>
        public bool ligado = true;

        Pinos _pinos;
        Bola _bola;
        Camera _cam;
        Material _matPino, _matBola;
        Material _matDaBola;

        public static Reflexos Criar(Transform pai, Pinos pinos, Bola bola, Camera cam)
        {
            var go = new GameObject("Reflexos");
            go.transform.SetParent(pai, false);
            var r = go.AddComponent<Reflexos>();
            r._pinos = pinos;
            r._bola = bola;
            r._cam = cam;
            r._matPino = Materiais.Novo("Reflexo", "reflexo_pino");
            r._matPino.renderQueue = FILA_REFLEXO;
            r._matBola = Materiais.Novo("ReflexoBola", "reflexo_bola");
            r._matBola.renderQueue = FILA_REFLEXO;
            return r;
        }

        void LateUpdate()
        {
            if (!ligado || _cam == null || !_cam.isActiveAndEnabled) return;
            for (int n = 1; n <= 10; n++)
            {
                var p = _pinos.Numero(n);
                if (p == null || p.removido || !p.gameObject.activeInHierarchy) continue;
                var r = p.Corpo;
                if (r == null || !r.enabled) continue;
                // só os de cima da pista (os do poço e os voando longe, não)
                var b = p.BaseNaTela();
                if (b.y < -0.05f || b.y > 0.6f || Mathf.Abs(b.x) > 0.66f) continue;
                Desenhar(p.MalhaReflexo, r.localToWorldMatrix, _matPino);
            }
            if (_bola != null && _bola.Visivel)
            {
                var c = _bola.transform.position;
                if (c.y > Bola.RAIO * 0.6f && c.y < 0.6f && Mathf.Abs(c.x) < 0.66f)
                {
                    var mr = _bola.Corpo;
                    var mat = mr.sharedMaterial;
                    if (mat != _matDaBola)
                    {
                        // a textura do bichinho da vez (troca na vez de cada um)
                        _matDaBola = mat;
                        _matBola.SetTexture(Materiais.ID_TEX, mat != null && mat.HasProperty("_textura") ? mat.GetTexture("_textura") : null);
                    }
                    Desenhar(Bola.Malha, mr.localToWorldMatrix, _matBola);
                }
            }
        }

        void Desenhar(Mesh m, Matrix4x4 mundo, Material mat)
        {
            if (m == null) return;
            Graphics.DrawMesh(m, ESPELHO * mundo, mat, 0, _cam, 0, null, ShadowCastingMode.Off, false, null,
                LightProbeUsage.Off, null);
        }
    }
}
