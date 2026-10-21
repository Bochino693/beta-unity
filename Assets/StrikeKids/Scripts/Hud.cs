using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StrikeKids
{
    /// <summary>
    /// TUDO O QUE É 2D POR CIMA DO 3D: placar, mapa dos pinos, o convite
    /// "ROLE A BOLA!", letreiros (STRIKE!...), faixa "VEZ DE...", nome do
    /// cenário, modo replay e as telas de atração, escolha, fotos, fim e
    /// inatividade. Não há setinhas nem alvos na tela: a mira é sempre o STRIKE.
    ///
    /// Responsivo: tudo é reposicionado em Layout() conforme a tela (em pé ou
    /// deitada). Unidade "u" = lado menor / 1080 (a tela lógica já tem o lado
    /// menor de 1080: u = 1).
    ///
    /// Visual: placas com volume (Placa: degradê, verniz, lateral e sombra,
    /// como os objetos 3D) e contorno azul-marinho.
    /// </summary>
    public partial class Hud : MonoBehaviour
    {
        public static readonly Color COR_BOB = Materiais.Hex("#169BD5");
        public static readonly Color COR_MIA = Materiais.Hex("#FF7A21");
        public static readonly Color DESTAQUE = Materiais.Hex("#FFC21A");
        public const float ESCALA_ARTE = 1.6f;      // letreiros gerados em 1,6x (nítidos em 4K)
        public static readonly Color MARINHO = Materiais.Hex("#0B1F45");
        public static readonly Color AZUL = Materiais.Hex("#1A4FA8");
        public static readonly Color CREME = Materiais.Hex("#FFF6A8");
        static readonly string[] ZONAS = { "Z", "X", "C", "V", "B" };

        public float u = 1f;
        public bool retrato = true;
        public Vector2 T;
        public Figura tela3d;
        RectTransform _raiz;
        Image _flash;
        Image _tarjaCima, _tarjaBaixo;
        Figura _replay;
        Caixa _replayPonto;

        class Cartao
        {
            public Placa painel;
            public Figura avatar, selo;
            public Rotulo nome, pontos;
            public RectTransform rodadas;
            public Color cor;
            public float valor;
            public int alvo;
            public bool usado, foto;
        }

        readonly List<Cartao> _cartoes = new List<Cartao>();
        Rotulo _info;
        RectTransform _bolasBox;
        Placa _mapa;
        readonly Dictionary<int, Caixa> _mapaPontos = new Dictionary<int, Caixa>();
        Rotulo _aviso, _avisoSub;
        Figura _cenario;
        Rotulo _sequencia;
        RectTransform _centro;
        Placa _faixa;
        Rotulo _faixaTexto;
        Figura _faixaAvatar;
        Tw _twFaixa, _twCenario, _twTarjas, _twFlash;
        float _tempo;
        bool _convite;
        readonly Dictionary<string, Texture2D> _retratos = new Dictionary<string, Texture2D>();

        public static Hud Criar(Transform canvas)
        {
            var rt = Ui.Novo("Hud", canvas);
            var h = rt.gameObject.AddComponent<Hud>();
            h._raiz = rt;
            h.Montar();
            return h;
        }

        public RectTransform Raiz => _raiz;

        void Montar()
        {
            foreach (var p in new[] { "bob", "mia" }) _retratos[p] = Materiais.UI("retrato_" + p);
            tela3d = Ui.Figura(_raiz, null, false);
            tela3d.name = "tela3d";
            MontarPlacar();
            MontarMapa();
            MontarToque();
            _centro = Ui.Camada("centro", _raiz);
            MontarFaixa();
            MontarReplay();
            MontarAtracao();
            MontarJogadores();
            MontarSelecao();
            _fim = Ui.Camada("fim", _raiz);
            Ui.Ver(_fim, false);
            MontarInatividade();
            MontarAvisoAndroid();
            MontarFotos();
            MontarEtapas();
            MontarMarca();
            _flash = Ui.Retangulo(_raiz, new Color(1, 1, 1, 0));
            _flash.name = "flash";
            // escondido quando transparente: um retângulo da tela inteira desenhado à toa custa caro
            Ui.Ver(_flash, false);
            Layout();
            Tela.Mudou += Layout;
        }

        void OnDestroy() => Tela.Mudou -= Layout;

        Rotulo R(Transform pai, string texto, float tam, Color cor, float contorno = 0f, Color? corContorno = null, bool titulo = false)
            => Ui.Rotulo(pai, texto, tam, cor, contorno, corContorno, titulo);

        Texture2D Retrato(string p) => _retratos.TryGetValue(p ?? "bob", out var t) ? t : _retratos["bob"];

        // ================================================================ placar
        void MontarPlacar()
        {
            var camada = Ui.Camada("placar", _raiz);
            _camadaPlacar = camada;
            for (int i = 0; i < 2; i++)
            {
                var cor = i == 0 ? COR_BOB : COR_MIA;
                var p = Ui.Placa(camada, cor, 34, 12, 6);
                var av = Ui.Figura(p.transform, null);
                var selo = Ui.Figura(p.transform, null);
                Ui.Ver(selo, false);
                var nome = R(p.transform, "", 34, Color.white, 8);
                nome.Alinhar(-1);
                var pts = R(p.transform, "0", 64, CREME, 12, MARINHO, true);
                pts.Alinhar(1);
                var rod = Ui.Novo("rodadas", p.transform);
                _cartoes.Add(new Cartao { painel = p, avatar = av, selo = selo, nome = nome, pontos = pts, rodadas = rod, cor = cor });
            }
            _info = R(camada, "", 34, Color.white, 9);
            _bolasBox = Ui.Novo("bolas", camada);
            MostrarPlacar(false);
        }

        RectTransform _camadaPlacar;

        public void MostrarPlacar(bool sim)
        {
            foreach (var c in _cartoes) Ui.Ver(c.painel, sim && c.usado);
            Ui.Ver(_info, sim);
            Ui.Ver(_bolasBox, sim);
            if (_mapa != null) Ui.Ver(_mapa, sim);
        }

        /// <summary>Os personagens de quem joga (1 ou 2), na ordem.</summary>
        public void ConfigurarPlacar(IList<string> personagens)
        {
            for (int i = 0; i < 2; i++)
            {
                var c = _cartoes[i];
                c.usado = i < personagens.Count;
                if (!c.usado) continue;
                string p = personagens[i];
                c.nome.text = p.ToUpperInvariant();
                var cor = p == "bob" ? COR_BOB : COR_MIA;
                var ft = Fotos.Foto("j" + i);
                c.foto = ft != null;
                c.avatar.texture = ft != null ? (Texture)ft : Retrato(p);
                c.avatar.material = ft != null ? Ui.MaterialFoto(Color.white) : null;
                c.avatar.manter = ft == null;
                c.selo.texture = Retrato(p);
                Ui.Ver(c.selo, ft != null);
                c.cor = cor;
                c.painel.Pintar(cor);
                c.valor = 0f;
                c.alvo = 0;
                c.pontos.text = "0";
            }
            Layout();
        }

        public void AtualizarPlacar(Placar placar)
        {
            for (int i = 0; i < placar.jogadores.Count; i++)
            {
                var c = _cartoes[i];
                c.alvo = placar.Total(i);
                for (int k = c.rodadas.childCount - 1; k >= 0; k--) Destroy(c.rodadas.GetChild(k).gameObject);
                var j = placar.jogadores[i];
                float tc = 46 * u, sep = 8 * u;
                for (int r = 0; r < placar.rodadas; r++)
                {
                    var bolas = j.rodadas[r];
                    int soma = 0;
                    foreach (int b in bolas) soma += b;
                    var corCel = new Color(1, 1, 1, 0.22f);
                    string txt = "";
                    bool estrela = false;
                    if (bolas.Count > 0)
                    {
                        corCel = new Color(1, 1, 1, 0.95f);
                        txt = soma.ToString();
                        if (soma >= 10 && bolas.Count == 1)
                        {
                            corCel = Materiais.Hex("#FFD43B");
                            txt = "";
                            estrela = true;
                        }
                        else if (soma >= 10) corCel = Materiais.Hex("#7FE7FF");
                    }
                    bool atual = i == placar.jogadorAtual && r == placar.rodadaAtual;
                    var cel = Ui.Caixa(c.rodadas, corCel, 23 * u - 1f, atual ? 4 * u : 0f, DESTAQUE, 0);
                    Ui.Ret(cel, r * (tc + sep), (Ui.Tam(c.rodadas).y - tc) * 0.5f, tc, tc);
                    if (txt != "")
                    {
                        var l = R(cel.transform, txt, 26 * u, MARINHO);
                        Ui.Ret(l, 0, 0, tc, tc);
                    }
                    else if (estrela)
                    {
                        var e = Ui.Figura(cel.transform, Materiais.UI("estrela_placar"));
                        Ui.Ret(e, 0, 0, tc, tc);
                    }
                }
                bool ativo = i == placar.jogadorAtual;
                bool so = placar.jogadores.Count == 1;
                c.painel.color = ativo || so ? Color.white : new Color(0.78f, 0.78f, 0.85f, 0.9f);
                Ui.Alfa(c.painel, ativo || so ? 1f : 0.9f);
                Ui.Escala(c.painel, ativo ? 1f : 0.94f);
            }
            _info.text = "RODADA " + (placar.rodadaAtual + 1) + " de " + placar.rodadas;
            for (int k = _bolasBox.childCount - 1; k >= 0; k--) Destroy(_bolasBox.GetChild(k).gameObject);
            float tb = 34 * u, sb = 14 * u;
            float largura = placar.tentativas * tb + (placar.tentativas - 1) * sb;
            float x0 = (Ui.Tam(_bolasBox).x - largura) * 0.5f;
            for (int k = 0; k < placar.tentativas; k++)
            {
                bool usada = k < placar.bolaAtual;
                var b = Ui.Caixa(_bolasBox, usada ? new Color(1, 1, 1, 0.25f) : CorAtual(placar), 17 * u - 1f, 3 * u, Color.white, 4);
                Ui.Ret(b, x0 + k * (tb + sb), (Ui.Tam(_bolasBox).y - tb) * 0.5f, tb, tb);
            }
        }

        Color CorAtual(Placar placar) => placar.Atual.personagem == "bob" ? COR_BOB : COR_MIA;

        // ================================================================ mapa dos pinos
        void MontarMapa()
        {
            _mapa = Ui.Placa(_camadaPlacar, AZUL, 26, 8, 4);
            for (int n = 1; n <= 10; n++) _mapaPontos[n] = Ui.Caixa(_mapa.transform, Color.white, 99, 3, DESTAQUE, 0);
            MostrarMapa(new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            Ui.Ver(_mapa, false);
        }

        public void MostrarMapa(List<int> emPe)
        {
            foreach (var kv in _mapaPontos)
            {
                bool dePe = emPe.Contains(kv.Key);
                kv.Value.Definir(dePe ? Color.white : new Color(1, 1, 1, 0.12f), 99, dePe ? 3 : 0, DESTAQUE, 0);
            }
        }

        // ================================================================ convite
        void MontarToque()
        {
            _aviso = R(_raiz, "ROLE A BOLA!", 64, CREME, 16, MARINHO, true);
            Ui.Ver(_aviso, false);
            _avisoSub = R(_raiz, "mire no STRIKE!", 40, Color.white, 10, MARINHO);
            Ui.Ver(_avisoSub, false);
            _cenario = Ui.Figura(_raiz, null);
            Ui.Ver(_cenario, false);
            _sequencia = R(_raiz, "", 40, CREME, 10, Materiais.Hex("#7A1E00"), true);
            Ui.Ver(_sequencia, false);
        }

        public void Convite(bool ligado, string texto = "ROLE A BOLA!")
        {
            _convite = ligado;
            _aviso.text = texto;
            Ui.Ver(_aviso, ligado);
            Ui.Ver(_avisoSub, ligado);
        }

        public void MostrarSensores(bool sim)
        {
            if (!sim)
            {
                Ui.Ver(_aviso, false);
                Ui.Ver(_avisoSub, false);
            }
        }

        public void SensorAcendeu(string zona)
        {
            _convite = false;
            Ui.Ver(_aviso, false);
            Ui.Ver(_avisoSub, false);
        }

        /// <summary>Nome do cenário (letreiro grande que entra girando e sai).</summary>
        public void AnunciarCenario(string tema, float dur = 2.2f)
        {
            var tex = Materiais.UI("cen_" + tema);
            if (tex == null) return;
            _cenario.texture = tex;
            float w = Mathf.Min(T.x * 0.86f, 900f * u);
            float h = w * tex.height / tex.width;
            Ui.Ret(_cenario, (T.x - w) / 2, T.y * (retrato ? 0.3f : 0.24f) - h / 2, w, h);
            Ui.Ver(_cenario, true);
            Ui.Escala(_cenario, 0f);
            Ui.Giro(_cenario, -0.5f * Mathf.Rad2Deg);
            Ui.Alfa(_cenario, 1f);
            _twCenario?.Matar();
            var tw = _twCenario = Tw.Novo(_cenario);
            tw.Escala(_cenario, 1f, 0.55f, Facil.BackOut);
            tw.Junto().Giro(_cenario, 0f, 0.55f, Facil.BackOut);
            tw.Espera(dur);
            tw.Escala(_cenario, 1.25f, 0.3f);
            tw.Junto().Alfa(_cenario, 0f, 0.3f);
            tw.Chamar(() => Ui.Ver(_cenario, false));
        }

        /// <summary>Sequência de strikes (aparece embaixo do placar do jogador).</summary>
        public void Sequencia(int n)
        {
            Ui.Ver(_sequencia, n >= 2);
            if (n >= 2)
            {
                _sequencia.text = n + " STRIKES SEGUIDOS!";
                Pular(_sequencia);
            }
        }

        // ================================================================ letreiros
        /// <summary>Letreiro grande no centro (STRIKE!, SPARE!...). sub: texto embaixo.</summary>
        public void Letreiro(string nome, string sub = "", float dur = 1.7f, float altura = 0.38f)
        {
            var tex = Materiais.UI(nome);
            if (tex == null) return;
            var tr = Ui.Figura(_centro, tex);
            float larg = Mathf.Min(T.x * 0.9f, tex.width / ESCALA_ARTE * u * 0.95f);
            float alt = larg * tex.height / tex.width;
            float y = T.y * altura - alt / 2;
            Ui.Ret(tr, (T.x - larg) / 2, y, larg, alt);
            Ui.Escala(tr, 0f);
            Ui.Giro(tr, -12f);
            var tw = Tw.Novo(tr);
            tw.Escala(tr, 1.18f, 0.22f, Facil.BackOut);
            tw.Junto().Giro(tr, 3f, 0.22f);
            tw.Escala(tr, 1f, 0.18f, Facil.Seno);
            tw.Junto().Giro(tr, 0f, 0.18f);
            tw.Escala(tr, 1.05f, dur * 0.5f, Facil.Seno);
            tw.PosY(tr, y - 120 * u, 0.3f, Facil.BackIn);
            tw.Junto().Alfa(tr, 0f, 0.3f);
            tw.Chamar(() => Destroy(tr.gameObject));
            Estrelas(Ui.Centro(tr), 26);
            if (!string.IsNullOrEmpty(sub))
            {
                var l = R(_centro, sub, 58 * u, Color.white, 14 * u, MARINHO, true);
                Ui.Ret(l, 0, y + alt + 10 * u, T.x, 90 * u);
                Ui.Escala(l, 0f);
                var t2 = Tw.Novo(l);
                t2.Espera(0.18f);
                t2.Escala(l, 1f, 0.3f, Facil.BackOut);
                t2.Espera(dur * 0.55f);
                t2.Alfa(l, 0f, 0.3f);
                t2.Chamar(() => Destroy(l.gameObject));
            }
        }

        /// <summary>Texto grande simples (ex.: "INSIRA FICHA").</summary>
        public void TextoGrande(string texto, Color? cor = null, float dur = 1.4f, float altura = 0.38f)
        {
            var l = R(_centro, texto, 110 * u, cor ?? CREME, 22 * u, MARINHO, true);
            Ui.Ret(l, 0, T.y * altura - 80 * u, T.x, 160 * u);
            Ui.Escala(l, 0f);
            var tw = Tw.Novo(l);
            tw.Escala(l, 1.12f, 0.2f, Facil.BackOut);
            tw.Escala(l, 1f, 0.15f);
            tw.Espera(dur);
            tw.Alfa(l, 0f, 0.3f);
            tw.Chamar(() => Destroy(l.gameObject));
        }

        /// <summary>"+120" voando até o placar do jogador.</summary>
        public void PontosVoando(int pontos, int jogador)
        {
            if (jogador >= _cartoes.Count) return;
            var p = _cartoes[jogador].painel;
            var l = R(_centro, "+" + pontos, 84 * u, CREME, 18 * u, MARINHO, true);
            var tam = new Vector2(400, 120) * u;
            Ui.Ret(l, T.x / 2 - 200 * u, T.y * 0.5f, tam.x, tam.y);
            var destino = Ui.Pos(p) + Ui.Tam(p) / 2 - tam / 2;
            var tw = Tw.Novo(l);
            tw.Escala(l, 1.2f, 0.15f);
            tw.Espera(0.35f);
            tw.Pos(l, destino, 0.55f, Facil.QuadIn);
            tw.Junto().Escala(l, 0.5f, 0.55f);
            tw.Chamar(() =>
            {
                Destroy(l.gameObject);
                Pular(p);
            });
        }

        void Pular(Component no)
        {
            var tw = Tw.Novo(no);
            tw.Escala(no, 1.08f, 0.08f);
            tw.Escala(no, 1f, 0.3f, Facil.ElasticoOut);
        }

        void Estrelas(Vector2 centro, int n) => Estrelas2D.Explodir(_centro, centro, n, u);

        public void Flash(Color? cor = null, float forca = 0.7f, float dur = 0.35f)
        {
            var c = cor ?? Color.white;
            _flash.color = new Color(c.r, c.g, c.b, forca);
            Ui.Ver(_flash, true);
            _twFlash?.Matar();
            var tw = _twFlash = Tw.Novo(_flash);
            tw.CorAlfa(_flash, 0f, dur);
            tw.Chamar(() => Ui.Ver(_flash, false));
        }

        // ================================================================ faixa "VEZ DE"
        void MontarFaixa()
        {
            _faixa = Ui.Placa(_raiz, COR_BOB, 40, 14, 7);
            Ui.Ver(_faixa, false);
            _faixaAvatar = Ui.Figura(_faixa.transform, null);
            _faixaTexto = R(_faixa.transform, "", 76, Color.white, 16, MARINHO, true);
        }

        public void Faixa(string personagem, string texto, string sub = "", Texture foto = null)
        {
            var cor = personagem == "bob" ? COR_BOB : COR_MIA;
            _faixa.Pintar(cor);
            _faixaAvatar.texture = foto != null ? foto : Retrato(personagem);
            _faixaAvatar.material = foto != null ? Ui.MaterialFoto(Color.white) : null;
            _faixaAvatar.manter = foto == null;
            _faixaTexto.text = texto + (string.IsNullOrEmpty(sub) ? "" : "\n" + sub);
            float h = 210 * u;
            float w = Mathf.Min(T.x * 0.92f, 980 * u);
            float y = T.y * (retrato ? 0.3f : 0.36f);
            Ui.Ret(_faixa, -w - 50 * u, y, w, h);
            if (foto != null) Ui.Ret(_faixaAvatar, -h * 0.05f, -h * 0.12f, h * 1.05f, h * 1.05f);
            else Ui.Ret(_faixaAvatar, -h * 0.15f, -h * 0.35f, h * 1.25f, h * 1.25f);
            Ui.Ret(_faixaTexto, h * 1.05f, 0, w - h * 1.1f, h);
            _faixaTexto.Tamanho((string.IsNullOrEmpty(sub) ? 70 : 58) * u, 16 * u);
            Ui.Ver(_faixa, true);
            _twFaixa?.Matar();
            var tw = _twFaixa = Tw.Novo(_faixa);
            tw.PosX(_faixa, (T.x - w) / 2, 0.38f, Facil.BackOut);
            tw.Espera(1.25f);
            tw.PosX(_faixa, T.x + 50 * u, 0.3f, Facil.BackIn);
            tw.Chamar(() => Ui.Ver(_faixa, false));
        }

        // ================================================================ replay
        void MontarReplay()
        {
            _tarjaCima = Ui.Retangulo(_raiz, new Color(0.01f, 0.03f, 0.09f, 1));
            _tarjaBaixo = Ui.Retangulo(_raiz, new Color(0.01f, 0.03f, 0.09f, 1));
            _replay = Ui.Figura(_raiz, Materiais.UI("replay"));
            Ui.Ver(_replay, false);
            _replayPonto = Ui.Caixa(_raiz, Materiais.Hex("#FF2E4D"), 99, 0, null, 10);
            Ui.Ver(_replayPonto, false);
        }

        void Tarjas(float h, float dur)
        {
            _twTarjas?.Matar();
            var tw = _twTarjas = Tw.Novo(_tarjaCima).Paralelo();
            tw.TamY(_tarjaCima, h, dur, Facil.Quad);
            tw.TamY(_tarjaBaixo, h, dur, Facil.Quad);
            tw.PosY(_tarjaBaixo, T.y - h, dur, Facil.Quad);
        }

        /// <summary>Faixas pretas de cinema (abertura), sem o selo de replay.</summary>
        public void TarjasCinema(bool ligado) => Tarjas(ligado ? T.y * (retrato ? 0.09f : 0.1f) : 0f, 0.4f);

        public void ModoReplay(bool ligado)
        {
            Tarjas(ligado ? T.y * (retrato ? 0.1f : 0.12f) : 0f, 0.35f);
            Ui.Ver(_replay, ligado);
            Ui.Ver(_replayPonto, ligado);
            // na demonstração (tela inicial) o logo grande sai da frente do selo REPLAY
            Tw.Novo(_logo).Alfa(_logo, ligado ? 0f : 1f, 0.3f);
            float w = 420 * u;
            Ui.Ret(_replay, T.x - w - 40 * u, T.y * (retrato ? 0.1f : 0.12f) * 0.5f - w * 0.118f, w, w * 0.236f);
            Ui.Ret(_replayPonto, Ui.Pos(_replay).x - 50 * u, Ui.Pos(_replay).y + w * 0.118f - 17 * u, 34 * u, 34 * u);
            if (ligado)
            {
                MostrarPlacar(false);
                MostrarSensores(false);
            }
        }

        // ================================================================ atração
        RectTransform _atracao;
        Figura _logo;
        Rotulo _aperte;
        Placa _aperteBotao;
        Rotulo _recorde;
        Placa _recordePlaca;
        Rotulo _creditos;
        Figura _recFoto;
        Placa _melhores;
        string _chaveMelhores = "";
        List<Classificacao.Entrada> _melhoresHoje = new List<Classificacao.Entrada>();

        void MontarAtracao()
        {
            _atracao = Ui.Camada("atracao", _raiz);
            _logo = Ui.Figura(_atracao, Materiais.UI("logo"));
            // botão amarelo com volume
            _aperteBotao = Ui.Placa(_atracao, DESTAQUE, 70, 18, 8);
            _aperte = R(_aperteBotao.transform, "APERTE START", 88, MARINHO, 0, MARINHO, true);
            _aperte.corSombra = new Color(1, 1, 1, 0.55f);
            // placa do recorde
            _recordePlaca = Ui.Placa(_atracao, AZUL, 40, 10, 5);
            _recorde = R(_recordePlaca.transform, "", 40, CREME, 8);
            _recorde.Alinhar(-1);
            _recFoto = Ui.Figura(_recordePlaca.transform, null, false);
            _recFoto.material = Ui.MaterialFoto(DESTAQUE);
            Ui.Ver(_recFoto, false);
            _creditos = R(_atracao, "", 30, Color.white, 8);
            Ui.Ver(_atracao, false);
        }

        public bool AtracaoVisivel => Ui.Visivel(_atracao);

        public void Atracao(bool ligado, string recorde = "", string creditos = "", Texture fotoRecorde = null,
            List<Classificacao.Entrada> melhores = null)
        {
            Ui.Ver(_atracao, ligado);
            melhores = melhores ?? new List<Classificacao.Entrada>();
            _melhoresHoje = melhores;
            _ultimoRecorde = recorde;
            _ultimosCreditos = creditos;
            _ultimaFotoRecorde = fotoRecorde;
            // o quadro dos melhores só é montado de novo quando muda
            var chave = new System.Text.StringBuilder();
            foreach (var e in melhores) chave.Append(e.pontos).Append(e.personagem).Append(e.foto).Append('|');
            chave.Append(recorde).Append(T.ToString()).Append(retrato);
            if (chave.ToString() != _chaveMelhores || (_melhores == null && melhores.Count > 0))
            {
                _chaveMelhores = chave.ToString();
                MontarMelhores(melhores);
            }
            if (melhores.Count > 0) recorde = "";
            _recorde.text = recorde;
            _creditos.text = creditos;
            _recFoto.texture = fotoRecorde;
            Ui.Ver(_recFoto, fotoRecorde != null && recorde != "");
            Ui.Ver(_recordePlaca, recorde != "");
            if (recorde != "")
            {
                float larg = _recorde.Largura(recorde);
                float h = 100 * u, d = 124 * u;
                float fotoL = Ui.Visivel(_recFoto) ? d * 0.82f : 0f;
                float w = Mathf.Min(larg + fotoL + 80 * u, T.x * 0.94f);
                float cx = Ui.Centro(_aperteBotao).x;
                Ui.Ret(_recordePlaca, Mathf.Clamp(cx - w / 2, 16 * u, T.x - w - 16 * u), Ui.Pos(_aperteBotao).y + Ui.Tam(_aperteBotao).y + 34 * u, w, h);
                Ui.Ret(_recorde, fotoL + 30 * u, 0, w - fotoL - 50 * u, h - 10 * u);
                Ui.Ret(_recFoto, -d * 0.3f, (h - d) / 2 - 6 * u, d, d);
            }
            if (ligado)
            {
                MostrarPlacar(false);
                MostrarSensores(false);
            }
        }

        string _ultimoRecorde = "", _ultimosCreditos = "";
        Texture _ultimaFotoRecorde;

        /// <summary>"MELHORES DE HOJE" na atração: os 3 primeiros do dia, com a foto de cada um.</summary>
        void MontarMelhores(List<Classificacao.Entrada> lista)
        {
            if (_melhores != null)
            {
                Destroy(_melhores.gameObject);
                _melhores = null;
            }
            if (lista.Count == 0) return;
            float d = 150 * u, gap = 34 * u;
            float w = lista.Count * d + (lista.Count - 1) * gap + 80 * u;
            float cabH = 70 * u;
            float h = cabH + d + 96 * u;
            w = Mathf.Max(w, 560 * u);
            var p = Ui.Placa(_atracao, Materiais.Hex("#0E2A5C"), 40, 14, 7);
            if (retrato)
            {
                float y = Ui.Pos(_aperteBotao).y + Ui.Tam(_aperteBotao).y + 34 * u;
                Ui.Ret(p, (T.x - w) / 2, Mathf.Min(y, T.y - h - 110 * u), w, h);
            }
            else Ui.Ret(p, T.x - w - 40 * u, T.y * 0.06f, w, h);
            _melhores = p;
            var cab = R(p.transform, "MELHORES DE HOJE", 42 * u, CREME, 10 * u, MARINHO, true);
            Ui.Ret(cab, 0, 0, w, cabH);
            cab.Caber(42 * u, w - 40 * u);
            float x0 = (w - (lista.Count * d + (lista.Count - 1) * gap)) / 2;
            for (int k = 0; k < lista.Count; k++)
            {
                var e = lista[k];
                var ft = Fotos.Carregar(e.foto);
                var fr = Ui.Figura(p.transform, ft != null ? (Texture)ft : Retrato(e.personagem), false);
                fr.material = Ui.MaterialFoto(CorMedalha(k + 1));
                Ui.Ret(fr, x0 + k * (d + gap), cabH, d, d);
                float md = 54 * u;
                var med = Ui.Placa(p.transform, CorMedalha(k + 1), md / 2f / u, 5, 4);
                Ui.Ret(med, Ui.Pos(fr).x - md * 0.2f, Ui.Pos(fr).y - md * 0.15f, md, md);
                var ln = R(med.transform, (k + 1).ToString(), md * 0.56f, MARINHO, 0, MARINHO, true);
                Ui.Ret(ln, 0, 0, md, md - 4 * u);
                var lp = R(p.transform, e.pontos.ToString(), 46 * u, Color.white, 10 * u, MARINHO, true);
                Ui.Ret(lp, Ui.Pos(fr).x - gap / 2, cabH + d + 8 * u, d + gap, 60 * u);
            }
        }

        // ================================================================ etapas
        /// <summary>Faixa "1 JOGADORES › 2 AMIGO › 3 FOTO › 4 JOGAR" no alto das telas de preparação.</summary>
        static readonly string[] ETAPAS = { "JOGADORES", "AMIGO", "FOTO", "JOGAR" };
        RectTransform _etapas;
        readonly List<Placa> _etapasPilulas = new List<Placa>();
        readonly List<Rotulo> _etapasTextos = new List<Rotulo>();

        void MontarEtapas()
        {
            _etapas = Ui.Camada("etapas", _raiz);
            Ui.Ver(_etapas, false);
            for (int i = 0; i < ETAPAS.Length; i++)
            {
                var p = Ui.Placa(_etapas, AZUL, 30, 7, 4);
                var l = R(p.transform, (i + 1) + "  " + ETAPAS[i], 30, Color.white, 7, MARINHO);
                _etapasPilulas.Add(p);
                _etapasTextos.Add(l);
            }
        }

        /// <summary>n = passo atual (1..4); 0 esconde.</summary>
        public void Etapa(int n)
        {
            Ui.Ver(_etapas, n > 0);
            for (int i = 0; i < _etapasPilulas.Count; i++)
            {
                var p = _etapasPilulas[i];
                var t = _etapasTextos[i];
                int passo = i + 1;
                if (passo == n)
                {
                    p.Pintar(DESTAQUE);
                    Ui.Alfa(p, 1f);
                    t.Estilo(-1f, 0f, MARINHO);
                }
                else if (passo < n)
                {
                    p.Pintar(Materiais.Hex("#2DBE4E"));
                    Ui.Alfa(p, 0.9f);
                    t.Estilo(-1f, 7 * u, Color.white);
                }
                else
                {
                    p.Pintar(AZUL);
                    Ui.Alfa(p, 0.75f);
                    t.Estilo(-1f, 7 * u, Color.white);
                }
            }
        }

        void LayoutEtapas()
        {
            float gap = 12 * u;
            float w = Mathf.Min((T.x - 40 * u - gap * 3) / 4f, 250 * u);
            float h = 66 * u;
            float x0 = (T.x - (w * 4 + gap * 3)) / 2;
            for (int i = 0; i < _etapasPilulas.Count; i++)
            {
                Ui.Ret(_etapasPilulas[i], x0 + i * (w + gap), 24 * u, w, h);
                var l = _etapasTextos[i];
                Ui.Ret(l, 0, 0, w, h - 8 * u);
                l.Caber(30 * u, w - 20 * u);
            }
        }

        /// <summary>Os 5 sensores da máquina em miniatura, cada um com a cor do que ele faz nesta tela.</summary>
        RectTransform Sensores(Transform pai, Color[] cores, (string texto, int de, int ate)[] legendas, float largura)
        {
            var c = Ui.Novo("sensores", pai);
            float d = 40 * u;
            float passo = Mathf.Min(largura / 5f, 76 * u);
            float x0 = (largura - passo * 4 - d) / 2;
            for (int i = 0; i < 5; i++)
            {
                var b = Ui.Caixa(c, cores[i], 99, 4, Color.white, 4);
                Ui.Ret(b, x0 + i * passo, 0, d, d);
            }
            foreach (var lg in legendas)
            {
                var l = R(c, lg.texto, 26 * u, Color.white, 7 * u, MARINHO);
                float xa = x0 + lg.de * passo, xb = x0 + lg.ate * passo + d;
                float lw = Mathf.Max(xb - xa, 120 * u);
                Ui.Ret(l, (xa + xb) / 2 - lw / 2, d + 4 * u, lw, 36 * u);
            }
            Ui.Tam(c, largura, d + 44 * u);
            return c;
        }

        // ================================================================ layout responsivo
        public void Layout()
        {
            T = Tela.Tamanho;
            Ui.Ret(_raiz, -T.x / 2f, -T.y / 2f, T.x, T.y);
            // (a raiz fica centrada no Canvas: o canto de cima à esquerda da tela lógica)
            _raiz.anchorMin = _raiz.anchorMax = new Vector2(0.5f, 0.5f);
            _raiz.anchoredPosition = Vector2.zero;
            _raiz.sizeDelta = T;
            retrato = T.y >= T.x;
            u = Mathf.Min(T.x, T.y) / 1080f;
            Ui.Ret(tela3d, 0, 0, T.x, T.y);
            Ui.Ret(_flash, 0, 0, T.x, T.y);
            Ui.Ret(_centro, 0, 0, T.x, T.y);
            Ui.Ret(_camadaPlacar, 0, 0, T.x, T.y);
            Ui.Ret(_atracao, 0, 0, T.x, T.y);
            Ui.Ret(_etapas, 0, 0, T.x, T.y);
            Ui.Ret(_fim, 0, 0, T.x, T.y);
            Ui.Ret(_tarjaCima, 0, 0, T.x, Ui.Tam(_tarjaCima).y);
            float hb = Ui.Tam(_tarjaBaixo).y;
            Ui.Ret(_tarjaBaixo, 0, T.y - hb, T.x, hb);
            // placar
            int usados = 0;
            foreach (var c in _cartoes) if (c.usado) usados++;
            float margem = 26 * u;
            float larg = retrato ? (usados == 2 ? (T.x - margem * 3) / 2 : T.x * 0.62f) : 560 * u;
            float alt = 200 * u;
            for (int i = 0; i < 2; i++)
            {
                var c = _cartoes[i];
                float x = usados == 1 ? (retrato ? (T.x - larg) / 2 : margem) : (i == 0 ? margem : T.x - larg - margem);
                Ui.Ret(c.painel, x, margem + 40 * u, larg, alt);
                if (c.foto)
                {
                    // foto da criança dentro do cartão + o mascote de "selo"
                    Ui.Ret(c.avatar, alt * 0.07f, alt * 0.07f, alt * 0.86f, alt * 0.86f);
                    Ui.Ret(c.selo, alt * 0.55f, alt * 0.52f, alt * 0.5f, alt * 0.5f);
                }
                else Ui.Ret(c.avatar, -alt * 0.12f, -alt * 0.32f, alt * 1.15f, alt * 1.15f);
                c.nome.Tamanho(42 * u, 10 * u);
                Ui.Ret(c.nome, alt * 0.98f, 10 * u, larg - alt, 54 * u);
                c.pontos.Tamanho(78 * u, 14 * u);
                c.pontos.Alinhar(-1);
                Ui.Ret(c.pontos, alt * 0.98f, 52 * u, larg - alt - 20 * u, 88 * u);
                Ui.Ret(c.rodadas, alt * 0.98f, alt - 64 * u, larg - alt, 50 * u);
            }
            float baseY = margem + 40 * u + alt + 16 * u;
            _info.Tamanho(34 * u, 9 * u);
            if (retrato)
            {
                Ui.Ret(_info, 0, baseY, T.x, 48 * u);
                Ui.Ret(_bolasBox, 0, baseY + 52 * u, T.x, 40 * u);
            }
            else
            {
                Ui.Ret(_info, margem, baseY, larg, 48 * u);
                Ui.Ret(_bolasBox, margem, baseY + 52 * u, larg, 40 * u);
            }
            // mapa dos pinos (canto)
            float ms = 190 * u;
            if (retrato) Ui.Ret(_mapa, T.x - ms - margem, baseY + 8 * u, ms, ms * 0.86f);
            else Ui.Ret(_mapa, T.x - ms - margem, margem + 300 * u, ms, ms * 0.86f);
            foreach (var kv in _mapaPontos)
            {
                var pos = Pinos.PosicaoDoPino(kv.Key);
                float r = 24 * u;
                // na tela, a esquerda de quem joga fica à esquerda (o x da Unity é espelhado)
                float fx = -pos.x / (Medidas.ESPACO_PINOS * 1.5f);
                float fy = (Medidas.Z_PINO1 - pos.z) / (Medidas.FILEIRA * 3f);
                Ui.Ret(kv.Value, ms / 2 + fx * ms * 0.36f - r / 2, ms * 0.86f * (0.78f - fy * 0.56f) - r / 2, r, r);
            }
            // convite
            float yAviso = T.y - (retrato ? 330 : 210) * u;
            _aviso.Tamanho(84 * u, 20 * u);
            Ui.Ret(_aviso, 0, yAviso, T.x, 120 * u);
            _avisoSub.Tamanho(44 * u, 11 * u);
            Ui.Ret(_avisoSub, 0, yAviso + 112 * u, T.x, 64 * u);
            _sequencia.Tamanho(40 * u, 10 * u);
            Ui.Ret(_sequencia, retrato ? 0 : margem, baseY + 100 * u, retrato ? T.x : larg, 56 * u);
            // atração
            float lw = retrato ? Mathf.Min(T.x * 0.9f, 980 * u) : T.x * 0.36f;
            var lt = _logo.texture;
            float lh = lw * (lt != null ? (float)lt.height / lt.width : 0.4f);
            if (retrato) Ui.Ret(_logo, (T.x - lw) / 2, T.y * 0.05f, lw, lh);
            else Ui.Ret(_logo, T.x * 0.03f, T.y * 0.05f, lw, lh);
            float bw = retrato ? Mathf.Min(T.x * 0.78f, 780 * u) : T.x * 0.34f;
            float bh = 150 * u;
            if (retrato) Ui.Ret(_aperteBotao, (T.x - bw) / 2, T.y * 0.69f, bw, bh);
            else Ui.Ret(_aperteBotao, T.x * 0.03f + (lw - bw) / 2, T.y * 0.6f, bw, bh);
            _aperte.Tamanho(84 * u);
            _aperte.sombraDy = 3 * u;
            Ui.Ret(_aperte, 0, 0, bw, bh - 18 * u);
            _aperte.Caber(84 * u, bw - 90 * u);
            _aperte.sombraDy = 3 * u;
            _recorde.Tamanho(40 * u, 8 * u);
            _creditos.Tamanho(30 * u, 8 * u);
            Ui.Ret(_creditos, 0, T.y - (retrato ? 90 : 64) * u, T.x, 46 * u);
            LayoutJogadores();
            LayoutEtapas();
            LayoutSelecao();
            LayoutInatividade();
            if (Ui.Visivel(_fotoTela)) LayoutFotos();
            if (Ui.Visivel(_marcaTela)) LayoutMarca();
            if (Ui.Visivel(_atracao)) Atracao(true, _ultimoRecorde, _ultimosCreditos, _ultimaFotoRecorde, _melhoresHoje);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _tempo += dt;
            // placar: pontos contando
            foreach (var c in _cartoes)
            {
                if (c.usado && c.valor < c.alvo)
                {
                    c.valor = Mathf.Min(c.alvo, c.valor + dt * Mathf.Max(60f, (c.alvo - c.valor) * 3f));
                    c.pontos.text = ((int)c.valor).ToString();
                }
            }
            if (Ui.Visivel(_aviso))
            {
                Ui.Escala(_aviso, 1f + Mathf.Sin(_tempo * 5f) * 0.05f);
                _avisoSub.color = new Color(1, 1, 1, 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(_tempo * 2.6f)));
            }
            if (Ui.Visivel(_atracao))
            {
                Ui.Escala(_logo, 1f + Mathf.Sin(_tempo * 2.2f) * 0.025f);
                Ui.Giro(_logo, Mathf.Sin(_tempo * 1.3f) * 0.02f * Mathf.Rad2Deg);
                Ui.Escala(_aperteBotao, 1f + Mathf.Sin(_tempo * 3f) * 0.035f);
                _aperteBotao.Realcar(0.35f + 0.35f * Mathf.Abs(Mathf.Sin(_tempo * 3f)));
            }
            if (Ui.Visivel(_jog))
            {
                _jogCartoes[_jogN - 1].Realcar(0.7f + 0.3f * Mathf.Sin(_tempo * 6f));
                Ui.Escala(_jogCartoes[_jogN - 1], 1.05f + Mathf.Sin(_tempo * 6f) * 0.015f);
                Ui.Escala(_jogCartoes[2 - _jogN], 0.94f);
            }
            if (Ui.Visivel(_selecao)) _selPlacas[_selDestaque].Realcar(0.7f + 0.3f * Mathf.Sin(_tempo * 6f));
            if (Ui.Visivel(_replayPonto)) Ui.Alfa(_replayPonto, Mathf.Repeat(_tempo, 1f) < 0.6f ? 1f : 0.15f);
        }
    }
}
