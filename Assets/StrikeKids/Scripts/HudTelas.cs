using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StrikeKids
{
    /// <summary>As telas do Hud: quantos vão jogar, escolha do amigo, fotos,
    /// fim de jogo com a classificação do dia, inatividade e a marca.</summary>
    public partial class Hud
    {
        // ================================================================ quantos vão jogar
        // Igual ao Dragon Bowling (mesma máquina): START 1 vez = 1 jogador; START de novo
        // (dentro do tempo) = 2 jogadores. A tela mostra isso com o botão START desenhado em
        // cada cartão e a contagem para começar.
        RectTransform _jog;
        Image _jogVeu;
        Placa _jogPainel;
        Rotulo _jogTitulo, _jogDica;
        readonly List<Placa> _jogCartoes = new List<Placa>();
        Placa _jogTrilho, _jogBarra;
        int _jogN = 1;

        class PecasCartao
        {
            public Rotulo num, leg, meio, botaoTexto, vezes;
            public Figura[] retratos = new Figura[2];
            public Placa botao;
        }

        readonly List<PecasCartao> _jogPecas = new List<PecasCartao>();

        void MontarJogadores()
        {
            _jog = Ui.Camada("jogadores", _raiz);
            Ui.Ver(_jog, false);
            // véu escuro por cima do 3D (que fica parado enquanto o modal está aberto)
            _jogVeu = Ui.Retangulo(_jog, new Color(0.02f, 0.05f, 0.14f, 0.42f));
            _jogPainel = Ui.Placa(_jog, AZUL, 50, 18, 8);
            _jogTitulo = R(_jogPainel.transform, "QUANTOS VÃO JOGAR?", 70, CREME, 16, MARINHO, true);
            for (int i = 0; i < 2; i++)
            {
                var c = Ui.Placa(_jogPainel.transform, Materiais.Hex("#F2F7FF"), 40, 16, 6);
                var pc = new PecasCartao();
                pc.num = R(c.transform, (i + 1).ToString(), 170, i == 0 ? COR_BOB : COR_MIA, 22, MARINHO, true);
                pc.leg = R(c.transform, i == 0 ? "JOGADOR" : "JOGADORES", 46, MARINHO, 0, MARINHO, true);
                for (int k = 0; k < 2; k++) pc.retratos[k] = Ui.Figura(c.transform, Retrato(k == 0 ? "bob" : "mia"));
                pc.meio = R(c.transform, i == 0 ? "ou" : "x", 40, MARINHO, 0, MARINHO, true);
                // como escolher: o botão START desenhado + quantas vezes
                pc.botao = Ui.Placa(c.transform, Materiais.Hex("#E8384F"), 26, 7, 4);
                pc.botaoTexto = R(pc.botao.transform, "START", 34, Color.white, 8, MARINHO, true);
                pc.vezes = R(c.transform, i == 0 ? "1 VEZ" : "2 VEZES", 40, MARINHO, 0, MARINHO, true);
                pc.vezes.Alinhar(-1);
                _jogCartoes.Add(c);
                _jogPecas.Add(pc);
            }
            _jogDica = R(_jogPainel.transform, "", 36, Color.white, 9);
            _jogTrilho = Ui.Placa(_jogPainel.transform, Ui.Clarear(MARINHO, 0.08f), 17, 3, 3);
            _jogTrilho.Sombra(0f);
            _jogBarra = Ui.Placa(_jogTrilho.transform, DESTAQUE, 17, 5, 3);
            _jogBarra.Sombra(0f);
        }

        /// <summary>Tela "QUANTOS VÃO JOGAR?": dois cartões (1 ou 2), o escolhido brilha.
        /// fracao: tempo que falta (1 → 0); segundos: para a contagem escrita.</summary>
        public void Jogadores(bool ligado, int n = 1, float fracao = 0f, int segundos = 0)
        {
            if (ligado && !Ui.Visivel(_jog))
            {
                Ui.Escala(_jogPainel, 0.75f);
                Tw.Novo(_jogPainel).Escala(_jogPainel, 1f, 0.3f, Facil.BackOut);
            }
            Ui.Ver(_jog, ligado);
            if (!ligado) return;
            if (n != _jogN) Pular(_jogCartoes[n - 1]);
            _jogN = n;
            _jogDica.text = n == 1
                ? "Vão jogar em 2? Aperte START de novo agora!\nSe não, começa com 1 jogador em " + Mathf.Max(segundos, 1) + "..."
                : "2 JOGADORES: BOB x MIA!\nPreparem-se...";
            var tt = Ui.Tam(_jogTrilho);
            Ui.Ret(_jogBarra, 0, 0, Mathf.Max(tt.x * Mathf.Clamp01(fracao), tt.y), tt.y);
            Ui.Ver(_jogBarra, fracao > 0f);
            for (int i = 0; i < 2; i++)
            {
                var c = _jogCartoes[i];
                c.color = i == n - 1 ? Color.white : new Color(0.78f, 0.82f, 0.9f, 0.9f);
                Ui.Alfa(c, i == n - 1 ? 1f : 0.9f);
                if (i != n - 1) c.Realcar(0f);
            }
        }

        void LayoutJogadores()
        {
            Ui.Ret(_jog, 0, 0, T.x, T.y);
            Ui.Ret(_jogVeu, 0, 0, T.x, T.y);
            float mw = Mathf.Min(T.x * 0.92f, 980 * u);
            float mh = 830 * u;
            Ui.Ret(_jogPainel, (T.x - mw) / 2, (T.y - mh) / 2 + (retrato ? 50 : 30) * u, mw, mh);
            _jogTitulo.Tamanho(70 * u, 16 * u);
            Ui.Ret(_jogTitulo, 0, 22 * u, mw, 120 * u);
            _jogTitulo.Caber(70 * u, mw - 60 * u);
            float gap = 36 * u;
            float cw = (mw - gap * 3) / 2;
            float ch = 470 * u;
            for (int i = 0; i < 2; i++)
            {
                var c = _jogCartoes[i];
                var pc = _jogPecas[i];
                Ui.Ret(c, gap + i * (cw + gap), 150 * u, cw, ch);
                pc.num.Tamanho(170 * u, 22 * u);
                Ui.Ret(pc.num, 0, 0, cw, 180 * u);
                pc.leg.Tamanho(44 * u);
                Ui.Ret(pc.leg, 0, 172 * u, cw, 56 * u);
                pc.leg.Caber(44 * u, cw - 30 * u);
                float rs = 112 * u;
                for (int k = 0; k < 2; k++)
                    Ui.Ret(pc.retratos[k], cw / 2 + (k == 0 ? -rs - 20 * u : 20 * u), 232 * u, rs, rs);
                pc.meio.Tamanho(38 * u);
                Ui.Ret(pc.meio, 0, 232 * u, cw, rs);
                // botão START + "1 VEZ"/"2 VEZES", centralizados
                float bw = 150 * u, bh = 60 * u;
                pc.vezes.Tamanho(38 * u);
                float vw = pc.vezes.Largura();
                float total = bw + 14 * u + vw;
                float x0 = (cw - total) / 2;
                Ui.Ret(pc.botao, x0, 372 * u, bw, bh);
                pc.botaoTexto.Tamanho(32 * u, 7 * u);
                Ui.Ret(pc.botaoTexto, 0, 0, bw, bh - 7 * u);
                Ui.Ret(pc.vezes, x0 + bw + 14 * u, 372 * u - 4 * u, vw + 10 * u, bh);
            }
            _jogDica.Tamanho(34 * u, 9 * u);
            Ui.Ret(_jogDica, 20 * u, 636 * u, mw - 40 * u, 100 * u);
            Ui.Ret(_jogTrilho, 70 * u, 752 * u, mw - 140 * u, 34 * u);
            Ui.Ret(_jogBarra, 0, 0, Ui.Tam(_jogBarra).x, 34 * u);
        }

        // ================================================================ escolha do personagem
        RectTransform _selecao;
        Placa _selTitulo, _selDica;
        Rotulo _selTituloTexto, _selTexto;
        readonly List<Placa> _selPlacas = new List<Placa>();
        readonly List<Rotulo> _selNomes = new List<Rotulo>();
        int _selDestaque;
        RectTransform _selSensores;
        readonly bool[] _selPosta = new bool[2];

        void MontarSelecao()
        {
            _selecao = Ui.Camada("selecao", _raiz);
            Ui.Ver(_selecao, false);
            _selTitulo = Ui.Placa(_selecao, AZUL, 44, 14, 7);
            _selTituloTexto = R(_selTitulo.transform, "ESCOLHA SEU AMIGO!", 76, CREME, 16, MARINHO, true);
            for (int i = 0; i < 2; i++)
            {
                var placa = Ui.Placa(_selecao, i == 0 ? COR_BOB : COR_MIA, 36, 14, 7);
                var l = R(placa.transform, i == 0 ? "BOB" : "MIA", 70, Color.white, 16, MARINHO, true);
                _selPlacas.Add(placa);
                _selNomes.Add(l);
            }
            _selDica = Ui.Placa(_selecao, Ui.Clarear(MARINHO, 0.12f), 30, 8, 5);
            Ui.Alfa(_selDica, 0.94f);
            _selTexto = R(_selDica.transform, "", 36, Color.white, 9);
        }

        void LayoutSelecao()
        {
            Ui.Ret(_selecao, 0, 0, T.x, T.y);
            float sw = Mathf.Min(T.x * 0.9f, 900 * u);
            Ui.Ret(_selTitulo, (T.x - sw) / 2, 112 * u, sw, 130 * u);
            _selTituloTexto.Tamanho(74 * u, 16 * u);
            Ui.Ret(_selTituloTexto, 0, 0, sw, 116 * u);
            for (int i = 0; i < 2; i++)
            {
                var p = _selPlacas[i];
                var tam = new Vector2(330, 116) * u;
                if (!_selPosta[i]) Ui.Ret(p, T.x * (i == 0 ? 0.27f : 0.73f) - tam.x / 2, T.y * 0.64f, tam.x, tam.y);
                else Ui.Tam(p, tam);
                var l = _selNomes[i];
                l.Tamanho(70 * u, 16 * u);
                Ui.Ret(l, 0, 0, tam.x, tam.y - 14 * u);
            }
            float dw = Mathf.Min(T.x * 0.9f, 860 * u);
            float dh = 230 * u;
            Ui.Ret(_selDica, (T.x - dw) / 2, T.y - dh - (retrato ? 60 : 24) * u, dw, dh);
            _selTexto.Tamanho(34 * u, 9 * u);
            Ui.Ret(_selTexto, 0, 8 * u, dw, 100 * u);
            // os 5 sensores: esquerda = Bob, meio = confirma, direita = Mia
            if (_selSensores != null) Destroy(_selSensores.gameObject);
            float sl = Mathf.Min(dw - 80 * u, 420 * u);
            _selSensores = Sensores(_selDica.transform, new[] { COR_BOB, COR_BOB, DESTAQUE, COR_MIA, COR_MIA },
                new[] { ("BOB", 0, 1), ("OK", 2, 2), ("MIA", 3, 4) }, sl);
            Ui.Pos(_selSensores, (dw - sl) / 2, 112 * u);
        }

        /// <summary>pes: onde ficam os pés de Bob e Mia na tela (a placa fica logo abaixo).</summary>
        public void Selecao(bool ligado, int destaque = 0, string texto = "", Vector2[] pes = null)
        {
            bool entrou = ligado && !Ui.Visivel(_selecao);
            Ui.Ver(_selecao, ligado);
            _selTexto.text = texto;
            if (!ligado) return;
            if (entrou) Pular(_selTitulo);
            if (destaque != _selDestaque) Pular(_selPlacas[destaque]);
            _selDestaque = destaque;
            for (int i = 0; i < 2; i++)
            {
                var p = _selPlacas[i];
                var tam = Ui.Tam(p);
                if (pes != null && pes.Length == 2)
                {
                    Ui.Pos(p, Mathf.Clamp(pes[i].x - tam.x / 2, 12 * u, T.x - tam.x - 12 * u), pes[i].y + 30 * u);
                    _selPosta[i] = true;
                }
                float alvo = i == destaque ? 1.12f : 0.9f;
                Ui.Escala(p, Mathf.Lerp(Ui.Escala(p), alvo, 0.25f));
                p.color = i == destaque ? Color.white : new Color(0.8f, 0.84f, 0.92f, 0.85f);
                Ui.Alfa(p, i == destaque ? 1f : 0.85f);
                if (i != destaque) p.Realcar(0f);
            }
        }

        // ================================================================ fim de jogo
        RectTransform _fim;
        int _fimVez;
        Placa _fimBotao;
        Rotulo _fimAperte;

        public class JogadorFim
        {
            public string personagem;
            public int pontos, estrelas, strikes, spares, posicao;
            public Texture foto;
        }

        public class DadosFim
        {
            public List<JogadorFim> jogadores = new List<JogadorFim>();
            public int vencedor = -9;
            public bool recorde;
            public int recordeValor;
            public List<Classificacao.Entrada> classificacao = new List<Classificacao.Entrada>();
        }

        /// <summary>FIM DE JOGO + CLASSIFICAÇÃO: o cartão de cada jogador com a FOTO grande dele
        /// (ou o mascote), pontos, estrelas e a posição no dia; embaixo (em pé) ou ao lado
        /// (deitada) o quadro "CLASSIFICAÇÃO DE HOJE" — quem acabou de jogar aparece brilhando.</summary>
        public void TelaFim(bool ligado, DadosFim dados = null)
        {
            for (int k = _fim.childCount - 1; k >= 0; k--) Destroy(_fim.GetChild(k).gameObject);
            _fimBotao = null;
            _fimAperte = null;
            Ui.Ver(_fim, ligado);
            _fimVez++;
            if (!ligado || dados == null) return;
            int vez = _fimVez;
            MostrarPlacar(false);
            MostrarSensores(false);
            float bh = 116 * u;
            float yBotao = T.y - bh - (retrato ? 110 : 34) * u;
            var titulo = Ui.Figura(_fim, Materiais.UI("fim_de_jogo"));
            float twl = retrato ? Mathf.Min(T.x * 0.42f, 440 * u) : 340 * u;
            Ui.Ret(titulo, (T.x - twl) / 2, (retrato ? 40 : 14) * u, twl, twl * 0.61f);
            Ui.Escala(titulo, 0f);
            Tw.Novo(titulo).Escala(titulo, 1f, 0.45f, Facil.BackOut);
            float y0 = Ui.Pos(titulo).y + Ui.Tam(titulo).y + 6 * u;
            Rect areaC, areaQ;
            if (retrato)
            {
                areaC = new Rect(36 * u, y0, T.x - 72 * u, 590 * u);
                areaQ = new Rect(36 * u, y0 + 620 * u, T.x - 72 * u, yBotao - (y0 + 620 * u) - 26 * u);
            }
            else
            {
                float meio = T.x * 0.5f;
                areaC = new Rect(30 * u, y0, meio - 50 * u, yBotao - y0 - 20 * u);
                areaQ = new Rect(meio + 20 * u, y0, meio - 50 * u, yBotao - y0 - 20 * u);
            }
            var js = dados.jogadores;
            for (int i = 0; i < js.Count; i++) CartaoFim(js[i], i, js.Count, areaC, dados.vencedor == i && js.Count > 1);
            QuadroClassificacao(dados.classificacao, js, areaQ);
            // (só se ainda for este fim de jogo: com START logo, a partida nova já começou)
            if (dados.vencedor == -1 && js.Count > 1)
                Tarefas.DepoisReal(3f, () => { if (vez == _fimVez) Letreiro("empate", "", 2.5f, 0.55f); });
            if (dados.recorde)
            {
                string sub = dados.recordeValor + " PONTOS";
                float alt = retrato ? 0.42f : 0.3f;
                Tarefas.DepoisReal(3.4f, () => { if (vez == _fimVez) Letreiro("novo_recorde", sub, 3f, alt); });
            }
            // botão "jogar de novo"
            float bw = Mathf.Min(T.x * 0.92f, 940 * u);
            _fimBotao = Ui.Placa(_fim, DESTAQUE, 58, 14, 7);
            Ui.Ret(_fimBotao, (T.x - bw) / 2, yBotao, bw, bh);
            _fimAperte = R(_fimBotao.transform, "APERTE START PARA JOGAR DE NOVO", 44 * u, MARINHO, 0, MARINHO, true);
            _fimAperte.corSombra = new Color(1, 1, 1, 0.55f);
            Ui.Ret(_fimAperte, 0, 0, bw, bh - 14 * u);
            _fimAperte.Caber(44 * u, bw - 70 * u);
        }

        /// <summary>Cartão de um jogador: foto grande em cima, nome, pontos, estrelas e o lugar no dia.</summary>
        void CartaoFim(JogadorFim j, int i, int n, Rect area, bool venceu)
        {
            var cor = j.personagem == "bob" ? COR_BOB : COR_MIA;
            float gap = 26 * u;
            float w = Mathf.Min((area.width - gap * (n - 1)) / n, 620 * u);
            float d = Mathf.Min(w * (n == 1 ? 0.5f : 0.56f), 250 * u);
            float h = Mathf.Min(area.height - d * 0.42f, 472 * u);
            float x = area.x + (area.width - (w * n + gap * (n - 1))) / 2 + i * (w + gap);
            float y = area.y + d * 0.42f;
            var c = Ui.Placa(_fim, cor, 44, 18, 8);
            Ui.Ret(c, x, y + 900 * u, w, h);
            var tw = Tw.Novo(c);
            tw.Espera(0.3f + i * 0.2f);
            tw.PosY(c, y, 0.5f, Facil.BackOut);
            // foto (da câmera; sem câmera, o mascote)
            var fr = Ui.Figura(c.transform, j.foto != null ? j.foto : Retrato(j.personagem), false);
            fr.material = Ui.MaterialFoto(venceu ? DESTAQUE : Color.white);
            Ui.Ret(fr, (w - d) / 2, -d * 0.42f, d, d);
            if (j.foto != null)
            {
                // o mascote com quem jogou, no cantinho da foto
                var selo = Ui.Figura(c.transform, Retrato(j.personagem));
                Ui.Ret(selo, Ui.Pos(fr).x + d * 0.7f, Ui.Pos(fr).y + d * 0.62f, d * 0.4f, d * 0.4f);
            }
            float yy = d * 0.6f;
            var nome = R(c.transform, j.personagem.ToUpperInvariant(), 46 * u, Color.white, 10 * u, MARINHO, true);
            Ui.Ret(nome, 0, yy, w, 58 * u);
            var pts = R(c.transform, "0", 96 * u, CREME, 20 * u, MARINHO, true);
            Ui.Ret(pts, 0, yy + 44 * u, w, 100 * u);
            var tw2 = Tw.Novo(pts);
            tw2.Espera(0.8f + i * 0.2f);
            tw2.Metodo(v => pts.text = ((int)v).ToString(), 0f, j.pontos, 1.4f, Facil.QuadOut);
            var leg = R(c.transform, "PONTOS", 30 * u, Color.white, 7 * u);
            Ui.Ret(leg, 0, yy + 136 * u, w, 34 * u);
            for (int k = 0; k < 3; k++)
            {
                var e = Ui.Figura(c.transform, Materiais.UI(k < j.estrelas ? "estrela_placar" : "estrela_vazia"));
                float es = k == 1 ? 64 * u : 54 * u;
                Ui.Ret(e, w / 2 - es / 2 + (k - 1) * 72 * u, yy + 176 * u - (k == 1 ? 6 * u : 0f), es, es);
                Ui.Escala(e, 0f);
                var t3 = Tw.Novo(e);
                t3.Espera(2.2f + k * 0.25f + i * 0.1f);
                t3.Escala(e, 1f, 0.35f, Facil.BackOut);
            }
            // lugar no dia (e quem venceu a partida de 2)
            int pos = j.posicao;
            if (pos > 0 || venceu)
            {
                float fitaH = 58 * u;
                var fita = Ui.Placa(c.transform, venceu ? DESTAQUE : CorMedalha(pos), 28, 8, 5);
                Ui.Ret(fita, w * 0.06f, h - fitaH - 16 * u, w * 0.88f, fitaH);
                string txt = pos <= 3 ? pos + "º LUGAR HOJE!" : pos + "º LUGAR HOJE";
                if (venceu)
                {
                    txt = pos > 0 ? "VENCEU!  " + pos + "º HOJE" : "VENCEU!";
                    pos = pos > 0 ? Mathf.Min(pos, 3) : 1;
                }
                var lf = R(fita.transform, txt, 34 * u, pos <= 3 ? MARINHO : Color.white, pos <= 3 ? 0 : 7 * u, MARINHO, true);
                Ui.Ret(lf, 0, 0, w * 0.86f, fitaH - 6 * u);
                lf.Caber(34 * u, w * 0.8f);
                Ui.Escala(fita, 0f);
                var t5 = Tw.Novo(fita);
                t5.Espera(2.8f + i * 0.15f);
                t5.Escala(fita, 1f, 0.4f, Facil.BackOut);
            }
            if (venceu) c.Realcar(0.55f);
        }

        Color CorMedalha(int pos)
        {
            switch (pos)
            {
                case 1: return Materiais.Hex("#FFC21A");
                case 2: return Materiais.Hex("#DCE6F2");
                case 3: return Materiais.Hex("#F0A25A");
            }
            return AZUL;
        }

        /// <summary>Quadro "CLASSIFICAÇÃO DE HOJE": os 5 primeiros do dia, cada um com a sua foto;
        /// quem jogou agora e ficou mais para trás aparece numa linha a mais.</summary>
        void QuadroClassificacao(List<Classificacao.Entrada> lista, List<JogadorFim> js, Rect area)
        {
            if (area.height < 200 * u) return;
            var linhas = new List<(int pos, Classificacao.Entrada e, Texture foto)>();
            for (int k = 0; k < Mathf.Min(5, lista.Count); k++) linhas.Add((k + 1, lista[k], null));
            foreach (var j in js)
                if (j.posicao > 5)
                    linhas.Add((j.posicao, new Classificacao.Entrada { pontos = j.pontos, personagem = j.personagem, hora = "" }, j.foto));
            var atuais = new HashSet<int>();
            foreach (var j in js) atuais.Add(j.posicao);
            float cabH = 86 * u, gap = 12 * u;
            int n = Mathf.Max(linhas.Count, 1);
            float lh = Mathf.Clamp((area.height - cabH - gap * (n + 1)) / n, 70 * u, 112 * u);
            float altura = cabH + gap + n * (lh + gap);
            var quadro = Ui.Placa(_fim, Materiais.Hex("#0E2A5C"), 40, 14, 7);
            Ui.Ret(quadro, area.x, area.y, area.width, altura);
            Ui.Alfa(quadro, 0f);
            Tw.Novo(quadro).Alfa(quadro, 1f, 0.4f).Atraso(1f);
            var cab = R(quadro.transform, "CLASSIFICAÇÃO DE HOJE", 50 * u, CREME, 12 * u, MARINHO, true);
            Ui.Ret(cab, 0, 0, area.width, cabH);
            cab.Caber(50 * u, area.width - 60 * u);
            if (lista.Count == 0 && linhas.Count == 0) return;
            float lw = area.width - 36 * u;
            for (int k = 0; k < linhas.Count; k++)
            {
                var (pos, e, fotoJ) = linhas[k];
                bool minha = atuais.Contains(pos);
                var cor = minha ? Materiais.Hex("#FFB627") : Materiais.Hex("#2A63C4");
                var lin = Ui.Placa(quadro.transform, cor, 26, 8, 4);
                Ui.Ret(lin, 18 * u, cabH + gap + k * (lh + gap), lw, lh);
                Ui.Alfa(lin, 0f);
                var tw = Tw.Novo(lin);
                tw.Espera(1.2f + k * 0.12f);
                tw.Alfa(lin, 1f, 0.25f);
                // número (medalha para os 3 primeiros)
                float md = lh * 0.74f;
                var med = Ui.Placa(lin.transform, CorMedalha(pos), md / 2f / u, 6, 4);
                Ui.Ret(med, 12 * u, (lh - md) / 2 - 3 * u, md, md);
                var ln = R(med.transform, pos.ToString(), md * 0.55f, pos <= 3 ? MARINHO : Color.white, pos <= 3 ? 0 : 6 * u, MARINHO, true);
                Ui.Ret(ln, 0, 0, md, md - 4 * u);
                // foto da criança (ou o mascote)
                float fd = lh * 0.92f;
                Texture ft = fotoJ != null ? fotoJ : Fotos.Carregar(e.foto);
                var fr = Ui.Figura(lin.transform, ft != null ? ft : Retrato(e.personagem), false);
                fr.material = Ui.MaterialFoto(Color.white);
                Ui.Ret(fr, 24 * u + md, (lh - fd) / 2 - 3 * u, fd, fd);
                float px = 24 * u + md + fd + 14 * u;
                string quem = "com " + (e.personagem ?? "").ToUpperInvariant();
                if (!string.IsNullOrEmpty(e.hora)) quem += "  ·  " + e.hora;
                var lq = R(lin.transform, quem, 30 * u, Color.white, 7 * u, MARINHO);
                lq.Alinhar(-1);
                Ui.Ret(lq, px, 0, lw * 0.5f, lh - 6 * u);
                if (minha)
                {
                    var voce = R(lin.transform, "VOCÊ!", 34 * u, MARINHO, 0, MARINHO, true);
                    voce.Alinhar(-1);
                    Ui.Ret(voce, px, -2 * u, lw * 0.3f, lh * 0.5f);
                    Ui.Ret(lq, px, lh * 0.36f, lw * 0.5f, lh * 0.6f);
                    lq.Estilo(-1f, 0f, MARINHO);
                }
                var lp = R(lin.transform, e.pontos.ToString(), lh * 0.6f, minha ? MARINHO : CREME, minha ? 0 : 10 * u, MARINHO, true);
                lp.Alinhar(1);
                Ui.Ret(lp, lw * 0.7f - 24 * u, 0, lw * 0.3f, lh - 6 * u);
            }
        }

        public void FimContagem(int segundos)
        {
            if (_fimAperte != null)
            {
                _fimAperte.text = "APERTE START PARA JOGAR DE NOVO  (" + segundos + ")";
                _fimAperte.Caber(44 * u, Ui.Tam(_fimAperte).x - 70 * u);
            }
            if (_fimBotao != null) Ui.Escala(_fimBotao, 1f + Mathf.Sin(_tempo * 4f) * 0.025f);
        }

        // ================================================================ inatividade e avisos
        Placa _inativo, _avisoAndroid;
        Rotulo _inativoTexto, _avisoAndroidTexto;

        void MontarInatividade()
        {
            _inativo = Ui.Placa(_raiz, AZUL, 48, 18, 8);
            Ui.Ver(_inativo, false);
            _inativoTexto = R(_inativo.transform, "", 52, Color.white, 12);
            _inativoTexto.Quebrar = true;
        }

        /// <summary>Aviso no alto da tela ANTES de uma janela de permissão do Android (ela congela
        /// a imagem do jogo; o aviso fica visível em volta dela). "" = some.</summary>
        void MontarAvisoAndroid()
        {
            _avisoAndroid = Ui.Placa(_raiz, Materiais.Hex("#E8384F"), 36, 12, 7);
            Ui.Ver(_avisoAndroid, false);
            _avisoAndroidTexto = R(_avisoAndroid.transform, "", 40, Color.white, 10);
            _avisoAndroidTexto.Quebrar = true;
        }

        public void AvisoAndroid(string texto)
        {
            Ui.Ver(_avisoAndroid, !string.IsNullOrEmpty(texto));
            _avisoAndroidTexto.text = texto ?? "";
            Ui.Frente(_avisoAndroid);
        }

        public void Inatividade(bool ligado, int segundos = 10)
        {
            Ui.Ver(_inativo, ligado);
            _inativoTexto.text = "AINDA ESTÁ AÍ?\n\nRole a bola para continuar!\n\n" + segundos;
        }

        void LayoutInatividade()
        {
            float mw = Mathf.Min(T.x * 0.86f, 860 * u);
            Ui.Ret(_inativo, (T.x - mw) / 2, (T.y - 600 * u) / 2, mw, 600 * u);
            _inativoTexto.Tamanho(54 * u, 12 * u);
            Ui.Ret(_inativoTexto, 30 * u, 0, mw - 60 * u, 600 * u);
            float aw = Mathf.Min(T.x - 60 * u, 1500 * u);
            Ui.Ret(_avisoAndroid, (T.x - aw) / 2, 30 * u, aw, 230 * u);
            _avisoAndroidTexto.Tamanho(40 * u, 10 * u);
            Ui.Ret(_avisoAndroidTexto, 30 * u, 10 * u, aw - 60 * u, 210 * u);
        }

        // ================================================================ fotos (câmera da máquina)
        RectTransform _fotoTela;
        Image _fotoFundo;
        Rotulo _fotoTitulo, _fotoSub, _fotoNumero;
        Figura _fotoCirculo, _fotoSelo, _fotoDestaque;
        Tw _twNumero, _twDestaque;

        void MontarFotos()
        {
            _fotoTela = Ui.Camada("fotos", _raiz);
            Ui.Ver(_fotoTela, false);
            _fotoFundo = Ui.Retangulo(_fotoTela, new Color(0.01f, 0.05f, 0.14f, 0.55f));
            _fotoTitulo = R(_fotoTela, "SORRIA!", 110, CREME, 24, MARINHO, true);
            _fotoCirculo = Ui.Figura(_fotoTela, null, false);
            _fotoSelo = Ui.Figura(_fotoTela, null);
            _fotoNumero = R(_fotoTela, "", 280, Color.white, 44, MARINHO, true);
            _fotoSub = R(_fotoTela, "", 46, Color.white, 12, MARINHO);
            _fotoSub.Quebrar = true;
            _fotoDestaque = Ui.Figura(_raiz, null, false);
            Ui.Ver(_fotoDestaque, false);
        }

        void LayoutFotos()
        {
            Ui.Ret(_fotoTela, 0, 0, T.x, T.y);
            Ui.Ret(_fotoFundo, 0, 0, T.x, T.y);
            float d = Mathf.Min(T.x * 0.7f, T.y * 0.44f);
            float cy = T.y * (retrato ? 0.47f : 0.52f);
            Ui.Ret(_fotoCirculo, (T.x - d) / 2, cy - d / 2, d, d);
            float sd = d * 0.42f;
            Ui.Ret(_fotoSelo, Ui.Pos(_fotoCirculo).x + d * 0.74f, Ui.Pos(_fotoCirculo).y + d * 0.66f, sd, sd);
            _fotoTitulo.Tamanho(110 * u, 24 * u);
            Ui.Ret(_fotoTitulo, 0, cy - d / 2 - 190 * u, T.x, 150 * u);
            _fotoNumero.Tamanho(280 * u, 44 * u);
            Ui.Ret(_fotoNumero, 0, cy - 180 * u, T.x, 360 * u);
            _fotoSub.Tamanho(46 * u, 12 * u);
            Ui.Ret(_fotoSub, T.x * 0.05f, cy + d / 2 + 30 * u, T.x * 0.9f, 140 * u);
        }

        /// <summary>Tela "SORRIA!": a câmera ao vivo num círculo, com o mascote do lado.</summary>
        public void FotoTela(bool ligado, Texture textura = null, string personagem = "", string titulo = "SORRIA!", string sub = "")
        {
            Ui.Ver(_fotoTela, ligado);
            if (!ligado) return;
            LayoutFotos();
            var cor = personagem == "bob" ? COR_BOB : COR_MIA;
            _fotoCirculo.texture = textura;
            _fotoCirculo.Quadrada();
            _fotoCirculo.material = Ui.MaterialFoto(Ui.Clarear(cor, 0.35f));
            _fotoSelo.texture = Retrato(personagem);
            _fotoTitulo.text = titulo;
            _fotoSub.text = sub;
            _fotoNumero.text = "";
            Ui.Escala(_fotoCirculo, 0f);
            Tw.Novo(_fotoCirculo).Escala(_fotoCirculo, 1f, 0.45f, Facil.BackOut);
            Pular(_fotoTitulo);
        }

        public void FotoContagem(int n)
        {
            _fotoNumero.text = n > 0 ? n.ToString() : "";
            if (n <= 0) return;
            Ui.Escala(_fotoNumero, 1.6f);
            _fotoNumero.color = Color.white;
            _twNumero?.Matar();
            var tw = _twNumero = Tw.Novo(_fotoNumero);
            tw.Escala(_fotoNumero, 1f, 0.35f, Facil.BackOut);
            tw.Junto().CorAlfa(_fotoNumero, 0.75f, 0.6f);
        }

        /// <summary>A foto tirada (ou o mascote, se a câmera não mandou imagem).</summary>
        public void FotoPronta(Texture foto, string personagem)
        {
            _fotoNumero.text = "";
            if (foto != null)
            {
                _fotoCirculo.texture = foto;
                _fotoCirculo.uvRect = new Rect(0, 0, 1, 1);
                _fotoTitulo.text = "QUE FOTO LINDA!";
                _fotoSub.text = "";
            }
            else
            {
                _fotoCirculo.texture = Retrato(personagem);
                _fotoCirculo.uvRect = new Rect(0, 0, 1, 1);
                _fotoTitulo.text = "VAMOS JOGAR!";
                _fotoSub.text = "(a câmera não mandou imagem)";
            }
            Pular(_fotoTitulo);
            Ui.Escala(_fotoCirculo, 1.12f);
            Tw.Novo(_fotoCirculo).Escala(_fotoCirculo, 1f, 0.4f, Facil.ElasticoOut);
            Estrelas(Ui.Centro(_fotoCirculo), 26);
        }

        /// <summary>Foto da criança pulando na tela (no strike).</summary>
        public void FotoDestaque(Texture foto, Color cor, float dur = 1.6f)
        {
            if (foto == null) return;
            float d = Mathf.Min(T.x * 0.36f, 360f * u);
            _fotoDestaque.texture = foto;
            _fotoDestaque.material = Ui.MaterialFoto(Ui.Clarear(cor, 0.3f));
            Ui.Ret(_fotoDestaque, (T.x - d) / 2, T.y * (retrato ? 0.56f : 0.58f) - d / 2, d, d);
            Ui.Ver(_fotoDestaque, true);
            Ui.Frente(_fotoDestaque);
            Ui.Escala(_fotoDestaque, 0f);
            Ui.Giro(_fotoDestaque, -0.6f * Mathf.Rad2Deg);
            Ui.Alfa(_fotoDestaque, 1f);
            _twDestaque?.Matar();
            var tw = _twDestaque = Tw.Novo(_fotoDestaque);
            tw.Escala(_fotoDestaque, 1f, 0.5f, Facil.BackOut);
            tw.Junto().Giro(_fotoDestaque, 0.08f * Mathf.Rad2Deg, 0.5f, Facil.BackOut);
            tw.Giro(_fotoDestaque, -0.08f * Mathf.Rad2Deg, 0.4f, Facil.Seno);
            tw.Giro(_fotoDestaque, 0f, 0.3f, Facil.Seno);
            tw.Espera(Mathf.Max(dur - 1.2f, 0.1f));
            tw.Escala(_fotoDestaque, 0f, 0.3f, Facil.BackIn);
            tw.Chamar(() => Ui.Ver(_fotoDestaque, false));
            Estrelas(Ui.Centro(_fotoDestaque), 22);
        }

        // ================================================================ marca Lazer & Sport
        // posição das peças dentro da logo inteira (tools/arte/gerar_marca.py)
        static readonly Vector2 CENTRO_ALVO = new Vector2(0.4752f, 0.2780f);
        static readonly Vector2 CENTRO_PLACA = new Vector2(0.5f, 0.7792f);
        const float PROPORCAO_LOGO = 1698f / 2560f;
        RectTransform _marcaTela;
        Figura _marcaFundo, _marcaGames, _marcaVeu, _marcaAlvo, _marcaPlaca;
        Rotulo _marcaTexto;
        Tw _twMarca;

        void MontarMarca()
        {
            _marcaTela = Ui.Camada("marca", _raiz);
            Ui.Ver(_marcaTela, false);
            _marcaFundo = Ui.Figura(_marcaTela, Materiais.UI("marca_fundo"), false);
            // véu escuro redondo atrás da logo (quando ela aparece sobre o 3D)
            _marcaVeu = Ui.Figura(_marcaTela, VeuRedondo(), false);
            // o alvo fica atrás da placa (como na logo)
            _marcaAlvo = Ui.Figura(_marcaTela, Materiais.UI("marca_alvo"), false);
            _marcaAlvo.material = Ui.MaterialBrilho();
            _marcaPlaca = Ui.Figura(_marcaTela, Materiais.UI("marca_placa"), false);
            _marcaPlaca.material = Ui.MaterialBrilho();
            // selo GAMES embaixo da placa: LAZER & SPORT GAMES
            _marcaGames = Ui.Figura(_marcaTela, Materiais.UI("marca_games"));
            _marcaTexto = R(_marcaTela, "apresenta", 52, new Color(1, 1, 1, 0.92f), 0, MARINHO);
        }

        static Texture2D _veu;

        static Texture2D VeuRedondo()
        {
            if (_veu != null) return _veu;
            const int n = 128;
            _veu = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "veu" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    px[y * n + x] = new Color(0f, 0.03f, 0.12f, 0.72f * (1f - r));
                }
            _veu.SetPixels32(px);
            _veu.Apply(false, true);
            return _veu;
        }

        void LayoutMarca()
        {
            Ui.Ret(_marcaTela, 0, 0, T.x, T.y);
            Ui.Ret(_marcaFundo, 0, 0, T.x, T.y);
            float lw = Mathf.Min(T.x * 0.82f, T.y * 0.62f / PROPORCAO_LOGO);
            float lh = lw * PROPORCAO_LOGO;
            float centroY = Ui.Visivel(_marcaVeu) ? 0.4f : 0.46f;
            var origem = new Vector2((T.x - lw) / 2, T.y * centroY - lh / 2);
            var tp = _marcaPlaca.texture;
            float refL = tp != null ? tp.width : 1f;   // a placa tem a largura da logo inteira
            foreach (var (t, centro) in new[] { (_marcaAlvo, CENTRO_ALVO), (_marcaPlaca, CENTRO_PLACA) })
            {
                if (t.texture == null) continue;
                var tam = new Vector2(t.texture.width, t.texture.height) * (lw / refL);
                var c = origem + new Vector2(lw * centro.x, lh * centro.y) - tam / 2;
                Ui.Ret(t, c.x, c.y, tam.x, tam.y);
            }
            float gw = lw * 0.46f, gh = gw * 188f / 616f;
            Ui.Ret(_marcaGames, (T.x - gw) / 2, origem.y + lh + 10 * u, gw, gh);
            float vw = Mathf.Max(T.x, T.y) * 1.25f;
            var mc = origem + new Vector2(lw, lh) * 0.5f;
            Ui.Ret(_marcaVeu, mc.x - vw / 2, mc.y - vw / 2, vw, vw);
            _marcaTexto.Tamanho(52 * u);
            Ui.Ret(_marcaTexto, 0, origem.y + lh + gh + 26 * u, T.x, 80 * u);
        }

        /// <summary>Abertura: a logo aparece (sobre o fundo azul ou, com `sobre3d`, por cima do
        /// strike da abertura), o dardo "acerta" o alvo (tremidinha + estrelas), sobe o selo
        /// GAMES, passa um brilho e entra "apresenta".</summary>
        public void Marca(bool ligado, bool naHora = false, bool sobre3d = false)
        {
            if (!ligado)
            {
                if (!Ui.Visivel(_marcaTela)) return;
                _twMarca?.Matar();
                if (naHora)
                {
                    Ui.Ver(_marcaTela, false);
                    return;
                }
                var tw0 = _twMarca = Tw.Novo(_marcaTela);
                tw0.Alfa(_marcaTela, 0f, 0.4f);
                tw0.Chamar(() => Ui.Ver(_marcaTela, false));
                return;
            }
            _twMarca?.Matar();
            Ui.Ver(_marcaFundo, !sobre3d);
            Ui.Ver(_marcaVeu, sobre3d);
            Ui.Ver(_marcaTela, true);
            Ui.Frente(_marcaTela);
            Ui.Frente(_flash);
            LayoutMarca();
            Ui.Alfa(_marcaTela, 1f);
            _marcaTexto.color = new Color(1, 1, 1, 0f);
            Ui.Escala(_marcaAlvo, 1f);
            Ui.Ver(_marcaGames, true);
            Ui.Escala(_marcaGames, 0f);
            var tg = Tw.Novo(_marcaGames);
            tg.Espera(0.55f);
            tg.Escala(_marcaGames, 1.12f, 0.2f, Facil.BackOut);
            tg.Escala(_marcaGames, 1f, 0.18f);
            if (sobre3d)
            {
                // logo "estoura" na tela
                foreach (var t in new[] { _marcaAlvo, _marcaPlaca })
                {
                    Ui.Escala(t, 0f);
                    var tp = Tw.Novo(t);
                    tp.Espera(t == _marcaPlaca ? 0f : 0.08f);
                    tp.Escala(t, 1f, 0.42f, Facil.BackOut);
                }
                _marcaVeu.color = new Color(1, 1, 1, 0f);
                Tw.Novo(_marcaVeu).CorAlfa(_marcaVeu, 1f, 0.3f);
                Tarefas.DepoisReal(0.12f, () => Estrelas2D.Explodir(_marcaTela, Ui.Centro(_marcaPlaca), 36, u));
            }
            else
            {
                Ui.Escala(_marcaPlaca, 1f);
                _marcaVeu.color = Color.white;
            }
            var tw = Tw.Novo(_marcaAlvo);
            tw.Espera(0.3f);
            tw.Escala(_marcaAlvo, new Vector2(1.07f, 0.94f), 0.07f);
            tw.Escala(_marcaAlvo, 1f, 0.5f, Facil.ElasticoOut);
            Tarefas.DepoisReal(0.32f, () =>
                Estrelas2D.Explodir(_marcaTela, Ui.Pos(_marcaAlvo) + Vector2.Scale(Ui.Tam(_marcaAlvo), new Vector2(0.43f, 0.5f)), 30, u));
            foreach (var t in new[] { _marcaPlaca, _marcaAlvo })
            {
                var m = t.material;
                m.SetFloat("_Pos", -1f);
                var tb = Tw.Novo(t);
                tb.Espera(t == _marcaPlaca ? 0.7f : 0.6f);
                tb.Metodo(v => m.SetFloat("_Pos", v), -0.4f, 1.6f, 0.9f, Facil.Seno);
            }
            var tt = Tw.Novo(_marcaTexto);
            tt.Espera(1f);
            tt.CorAlfa(_marcaTexto, 0.92f, 0.4f);
        }

        public bool MarcaVisivel => Ui.Visivel(_marcaTela);
    }
}
