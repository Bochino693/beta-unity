using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// SONS E MÚSICAS.
    ///
    /// Efeitos: Resources/Sons/&lt;nome&gt; (vários tocam juntos, pelo "pool").
    /// Músicas: Resources/Sons/musica_*, em loop, com troca suave.
    /// Vozes opcionais: se existir Resources/Sons/voz/&lt;nome&gt; (ex.: strike,
    /// spare, vez_bob), ela toca junto do efeito — basta pôr o arquivo na pasta.
    /// Todos os efeitos são carregados na abertura (o primeiro "STRIKE!" não
    /// espera o disco).
    /// </summary>
    public class Sons : MonoBehaviour
    {
        public static Sons I { get; private set; }

        static readonly Dictionary<string, int> VARIACOES = new Dictionary<string, int> { ["pino_batida"] = 3 };
        const int TAMANHO_POOL = 14;

        readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        AudioSource _musicaA, _musicaB, _rolando;
        string _musicaAtual = "";
        Coroutine _troca;

        /// <summary>Na demo o som pode ficar desligado (ajuste do operador).</summary>
        public static bool MudoDemo;

        public static void Criar()
        {
            if (I != null) return;
            var go = new GameObject("Sons");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Sons>();
        }

        void Awake()
        {
            for (int i = 0; i < TAMANHO_POOL; i++) _pool.Add(NovaFonte());
            _musicaA = NovaFonte();
            _musicaB = NovaFonte();
            _musicaA.loop = _musicaB.loop = true;
            _rolando = NovaFonte();
            _rolando.loop = true;
            foreach (var c in Resources.LoadAll<AudioClip>("Sons"))
            {
                if (!c.name.StartsWith("musica")) _cache[c.name] = c;
            }
        }

        AudioSource NovaFonte()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        AudioClip Carregar(string nome)
        {
            if (_cache.TryGetValue(nome, out var c)) return c;
            c = Resources.Load<AudioClip>("Sons/" + nome);
            _cache[nome] = c;
            return c;
        }

        static float VolEfeitos()
        {
            return MudoDemo ? 0f : Ajustes.Float("som", "efeitos");
        }

        /// <summary>Toca um efeito. volume: 0..1 (multiplica o volume do operador).</summary>
        public static void Tocar(string nome, float volume = 1f, float tom = 1f)
        {
            if (I == null) return;
            I.TocarEfeito(nome, volume, tom);
        }

        void TocarEfeito(string nome, float volume, float tom)
        {
            string baseNome = nome;
            if (VARIACOES.TryGetValue(nome, out int n)) nome = nome + "_" + Random.Range(1, n + 1);
            var clip = Carregar(nome);
            if (clip == null) return;
            float vol = VolEfeitos() * volume;
            if (vol <= 0.001f) return;
            var p = Livre();
            p.clip = clip;
            p.volume = Mathf.Clamp01(vol);
            p.pitch = tom;
            p.Play();
            var voz = Carregar("voz/" + baseNome);
            if (voz != null)
            {
                var pv = Livre();
                pv.clip = voz;
                pv.volume = Mathf.Clamp01(vol);
                pv.pitch = 1f;
                pv.Play();
            }
        }

        AudioSource Livre()
        {
            foreach (var p in _pool)
                if (!p.isPlaying) return p;
            // todos ocupados: reaproveita o que está tocando há mais tempo
            var velho = _pool[0];
            foreach (var p in _pool)
                if (p.time > velho.time) velho = p;
            return velho;
        }

        // ------------------------------------------------------------ música
        static float VolMusica()
        {
            return Ajustes.Float("som", "musica") * (MudoDemo ? 0f : 1f) * 0.75f;
        }

        public static void Musica(string nome, float troca = 0.8f)
        {
            if (I == null || nome == I._musicaAtual) return;
            I._musicaAtual = nome;
            if (I._troca != null) I.StopCoroutine(I._troca);
            I._troca = I.StartCoroutine(I.TrocarMusica(nome, troca));
        }

        public static void PararMusica(float troca = 0.6f) => Musica("", troca);

        IEnumerator TrocarMusica(string nome, float troca)
        {
            AudioSource sai = _musicaA.isPlaying ? _musicaA : (_musicaB.isPlaying ? _musicaB : null);
            AudioSource entra = sai == _musicaA ? _musicaB : _musicaA;
            AudioClip clip = nome != "" ? Resources.Load<AudioClip>("Sons/" + nome) : null;
            float v0 = sai != null ? sai.volume : 0f;
            if (clip != null)
            {
                entra.clip = clip;
                entra.volume = 0f;
                entra.Play();
            }
            float t = 0f;
            while (t < troca)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / troca);
                if (sai != null) sai.volume = v0 * (1f - u);
                if (clip != null) entra.volume = VolMusica() * u;
                yield return null;
            }
            if (sai != null)
            {
                sai.Stop();
                var velho = sai.clip;
                sai.clip = null;
                if (velho != null && velho != clip) Resources.UnloadAsset(velho);
            }
            if (clip != null) entra.volume = VolMusica();
            _troca = null;
        }

        /// <summary>Reaplica o volume (depois de mudar ajustes ou entrar/sair da demo).</summary>
        public static void AtualizarVolume()
        {
            if (I == null || I._troca != null) return;
            foreach (var p in new[] { I._musicaA, I._musicaB })
                if (p.isPlaying) p.volume = VolMusica();
        }

        // ------------------------------------------------------------ bola rolando
        public static void Rolando(bool ligar, float velocidade = 0f)
        {
            if (I == null) return;
            var r = I._rolando;
            if (ligar)
            {
                if (r.clip == null) r.clip = I.Carregar("bola_rolando");
                float f = Mathf.Clamp(velocidade / 7f, 0f, 1.2f);
                r.volume = Mathf.Clamp01(VolEfeitos() * f * 0.9f);
                r.pitch = 0.75f + 0.35f * f;
                if (!r.isPlaying && r.clip != null) r.Play();
            }
            else if (r.isPlaying) r.Stop();
        }
    }
}
