# -*- coding: utf-8 -*-
"""
A PISTA E A MÁQUINA (edição Unity, facetadas).

Mesmas medidas das edições Godot (scripts/mundo.gd e maquina.gd), com o X
espelhado (coordenadas da Unity: ver docs/ARQUITETURA.md). Linha de falta em
z = 0, pinos em z = -10,5, a pista vai para -Z.

Dois tipos de pacote:
  * "pista" — o que é igual em todas as fases: o deck (textura da pista), o
    piso de madeira de quem joga, o fosso e o poço, o túnel da caçapa, a
    máquina de pinos (mesa, barra, portinhas), o retorno de bolas, os pódios,
    um para-choque e uma tira da cortina da caçapa (o jogo copia), o terreno.
  * "pista_<fase>" — o que muda de cor/desenho em cada fase (parque, espaco,
    mar, dinos): canaletas, divisórias, paredes, gabinete, colunas, letreiro,
    arco de raios, cortina do fundo, mureta e letreiro dos jogadores.

Nomes começando com "lampadas" são PONTOS (o jogo põe as lâmpadas que piscam);
"colisao_*" são só para a física (o jogo cria os colisores e não desenha).
"""
import math

import numpy as np

from malha import (Forma, Modelo, Peca, caixa, caixa_arredondada, cor, esfera, extrudar, icosfera, juntar, plano,
                   prisma, retangulo2d, circulo2d, torno, tubo, mais_escura, misturar)

MEIA_LARGURA = 0.675
CANALETA = 0.25
Z_PINO1 = -10.5
ESPACO_PINOS = 0.38
FILEIRA = 0.329
Z_FIM_DECK = -11.95
Z_FUNDO = -13.7
N_BUMPERS = 10
ALCAPAO_X = 0.9
ALCAPAO_Z0 = -12.0
ALCAPAO_Z1 = -13.62
POCO_FUNDO = -1.75
Y_BOCA = 0.28
LARGURA_BOCA = 1.5
N_TIRAS = 7
Y_CHAO_FOSSO = -0.14
RETORNO = np.array([-2.35, 0.0, 5.2])     # X espelhado (no Godot x = +2,35)

# cores das fases (Temas.CONFIG das edições Godot)
FASES = {
    "parque": {"fundo": ["#3A1C8C", "#7A3FD6"], "halo": "#FFD6F4", "portal": ["#FFC21A", "#E8384F"], "gabinete": "#E8384F",
               "coluna": "#1E62C8", "neon": "#FF4FC8", "parede": "#2A63C4", "placa": "#1E3FA0", "divisoria": "#FFB627",
               "faixa": "#E8384F", "canaleta_modo": 0, "canaleta": ["#FF6FAE", "#FFF3E2", "#FFD23F"],
               "bumper": ["#FFFFFF", "#FF3B50"]},
    "espaco": {"fundo": ["#0B1450", "#2A3FB0"], "halo": "#9FE8FF", "portal": ["#4FD8FF", "#3B2FA8"], "gabinete": "#3B3FA8",
               "coluna": "#6A3FD0", "neon": "#4FE3FF", "parede": "#3B4FC8", "placa": "#141E66", "divisoria": "#4FD8FF",
               "faixa": "#FF9F2E", "canaleta_modo": 1, "canaleta": ["#26357A", "#9FB4FF", "#4FE3FF"],
               "bumper": ["#FFFFFF", "#3B8BFF"]},
    "mar": {"fundo": ["#064A70", "#0FA0B8"], "halo": "#C8FFF4", "portal": ["#2EE6C8", "#FF7A5C"], "gabinete": "#0E8C9C",
            "coluna": "#FF7A5C", "neon": "#5CFFD8", "parede": "#18A6B8", "placa": "#0B5A80", "divisoria": "#FF8A5C",
            "faixa": "#2EC4B6", "canaleta_modo": 2, "canaleta": ["#0C86A6", "#56E0EA", "#E9FFFB"],
            "bumper": ["#FFFFFF", "#FF7A5C"]},
    "dinos": {"fundo": ["#14501E", "#3E9A2E"], "halo": "#FFF2A8", "portal": ["#9BE04A", "#FF8A1E"], "gabinete": "#8A5228",
              "coluna": "#2E9A3E", "neon": "#B6FF3B", "parede": "#3E9A3A", "placa": "#1E5A28", "divisoria": "#FF9F2E",
              "faixa": "#8BD33F", "canaleta_modo": 3, "canaleta": ["#6DBB45", "#D4F58A", "#F2D27A"],
              "bumper": ["#FFE65A", "#2E9A3E"]},
}
FRISO = cor("#FFB627")


# ------------------------------------------------------------------ utilidades
def peca_de(nome, itens, material=""):
    p = Peca(nome, material)
    for it in itens:
        p.add(it[0], it[1], it[2] if len(it) > 2 else 0.0)
    return p


def bloco(tam, centro, chanfro=0.0):
    return caixa(tam[0], tam[1], tam[2], chanfro, centro=centro)


def perfil_z(contorno, z0, z1, chanfro=0.0):
    """Contorno 2D (x, y) puxado ao longo de Z de z0 a z1 (z0 < z1)."""
    return extrudar(contorno, z0, z1, chanfro)


def ret(w, h, r, centro, segs=2):
    return retangulo2d(w, h, r, segs, centro)


def perfil_canaleta(r, borda=0.018, fundo=0.05, segs=14):
    """A meia-cana (como Perfis.canaleta das edições Godot), com menos gomos
    (facetada): lábios no nível da pista, calha de raio r."""
    p = []
    yc = -borda
    xe = r + 2.0 * borda
    p.append((xe, yc - r - fundo))
    for i in range(5):
        a = math.pi * i / 4.0
        p.append((r + borda + math.cos(a) * borda, yc + math.sin(a) * borda))
    for i in range(1, segs):
        a = -math.pi * i / segs
        p.append((math.cos(a) * r, yc + math.sin(a) * r))
    for i in range(5):
        a = math.pi * i / 4.0
        p.append((-r - borda + math.cos(a) * borda, yc + math.sin(a) * borda))
    p.append((-xe, yc - r - fundo))
    return p


def canaleta(fase, x, z0, z1, passo=0.3):
    """A canaleta da fase, em gomos de `passo` metros ao longo da pista
    (os desenhos de cada fase são anéis de cor — bordas retas)."""
    cfg = FASES[fase]
    ca, cb, cc = (cor(h) for h in cfg["canaleta"])
    modo = cfg["canaleta_modo"]
    perfil = perfil_canaleta(CANALETA / 2.0)
    n = max(1, int(round((z1 - z0) / passo)))
    formas = []
    for i in range(n):
        za = z0 + (z1 - z0) * i / n
        zb = z0 + (z1 - z0) * (i + 1) / n
        f = extrudar(perfil, za, zb).mover((x, 0, 0))
        if modo == 0:      # doce: anéis rosa e creme, lábios amarelos
            base = ca if i % 2 == 0 else cb
        elif modo == 1:    # nave: metal escuro com faixas de luz
            base = ca if i % 4 != 0 else mais_escura(ca, 0.75)
        elif modo == 2:    # água: azul com ondas claras
            base = ca if i % 3 else misturar(ca, cb, 0.45)
        else:              # bambu: gomos verdes com nó escuro
            base = ca if i % 3 else mais_escura(ca, 0.7)
        def pinta(c, nn, base=base, i=i):
            lado_lab = abs(c[0] - x) > CANALETA / 2.0 - 0.005 and c[1] > -0.05
            if lado_lab:
                return cc if modo in (0, 3) else (cb if modo == 2 else cc)
            if modo == 1 and abs(c[0] - x) < 0.02 and c[1] < -0.1:
                return cc      # a faixa de luz no fundo da calha
            if modo == 2 and c[1] < -0.12 and i % 2 == 0:
                return misturar(base, cc, 0.25)
            return base
        f.pintar(pinta)
        formas.append((f, base))
    return formas


def lampadas_pontos(nome, pontos):
    """Os pontos das lâmpadas viram uma "peça" de pontos (o jogo lê só as posições)."""
    p = Peca(nome)
    for q in pontos:
        p.pos += [tuple(q), tuple(q), tuple(q)]
        p.nrm += [(0, 1, 0)] * 3
        p.cor += [(255, 255, 255, 255)] * 3
        p.uv += [(0.0, 0.0)] * 3
    return p


def posicao_do_pino(n):
    fileira = 0
    primeiro = 1
    while n >= primeiro + fileira + 1:
        primeiro += fileira + 1
        fileira += 1
    k = n - primeiro
    x = -(k - fileira * 0.5) * ESPACO_PINOS       # X espelhado
    return np.array([x, 0.0, Z_PINO1 - fileira * FILEIRA])


# ------------------------------------------------------------------ terreno
MORROS = [[-9, -20, 6, 2.2], [10, -26, 7, 2.6], [-14, -42, 9, 3.4], [15, -48, 10, 3.8], [0, -64, 16, 5.0],
          [-20, -10, 6, 1.8], [21, -8, 7, 2.2], [-26, -30, 9, 4.0], [27, -34, 10, 4.5], [-6, -52, 6, 2.0], [6, -58, 7, 2.4]]


def altura_terreno(x, z):
    """A MESMA fórmula das edições Godot (x espelhado nos morros)."""
    h = 0.0
    for m in MORROS:
        mx = -m[0]
        h += m[3] * math.exp(-((x - mx) ** 2 + (z - m[1]) ** 2) / (2.0 * m[2] ** 2))
    p = min(max((abs(x) - 3.2) / 4.0, 0.0), 1.0)
    p = p * p * (3.0 - 2.0 * p)
    return h * p + 0.06 * math.sin(-x * 0.7) * math.sin(z * 0.55) * p - 0.02 - 0.22 * (1.0 - p)


def terreno():
    """Chão facetado com morros: grade irregular (jitter), furo onde ficam a
    pista/fosso/área de jogo. Cor do vértice = mancha (R: 0..3) que o shader
    do chão troca pelas 4 cores da fase."""
    rnd = np.random.RandomState(5)
    xs = np.concatenate([np.arange(-60, -12, 4.0), np.arange(-12, 12, 1.5), np.arange(12, 61, 4.0)])
    zs = np.concatenate([np.arange(-90, -30, 4.0), np.arange(-30, 14, 1.5), np.arange(14, 41, 4.0)])
    V = []
    idx = {}
    for j, z in enumerate(zs):
        for i, x in enumerate(xs):
            jx = x + (rnd.uniform(-0.35, 0.35) * (1.5 if abs(x) < 12 else 4.0) if 0 < i < len(xs) - 1 else 0)
            jz = z + (rnd.uniform(-0.35, 0.35) * (1.5 if -30 < z < 14 else 4.0) if 0 < j < len(zs) - 1 else 0)
            idx[(i, j)] = len(V)
            V.append((jx, altura_terreno(jx, jz), jz))
    V = np.array(V)

    def dentro_furo(x, z):
        # área da pista, do fosso e de quem joga (o chão de lá é outra peça)
        return abs(x) < 3.5 and -15.2 < z < 8.4
    F = []
    for j in range(len(zs) - 1):
        for i in range(len(xs) - 1):
            a, b, c, d = idx[(i, j)], idx[(i + 1, j)], idx[(i, j + 1)], idx[(i + 1, j + 1)]
            cx = (V[a, 0] + V[d, 0]) / 2
            cz = (V[a, 2] + V[d, 2]) / 2
            if dentro_furo(cx, cz):
                continue
            # alterna a diagonal (facetas mais bonitas)
            if (i + j) % 2 == 0:
                F += [(a, c, b), (b, c, d)]
            else:
                F += [(a, c, d), (a, d, b)]
    f = Forma(V, F)
    # orientação: para cima
    A, B, C = f.V[f.F[:, 0]], f.V[f.F[:, 1]], f.V[f.F[:, 2]]
    n = np.cross(B - A, C - A)
    trocar = n[:, 1] < 0
    f.F[trocar] = f.F[trocar][:, [0, 2, 1]]
    # manchas: 0 = base, 1 = base escura, 2 = detalhe (flor/cratera/concha), 3 = detalhe 2
    def mancha(c, nn):
        h = (math.sin(c[0] * 0.37 + c[2] * 0.21) + math.sin(c[0] * 0.13 - c[2] * 0.29) * 0.8 + rnd.uniform(-0.5, 0.5))
        k = 1 if h > 0.55 else 0
        if rnd.uniform() < 0.035:
            k = 2
        elif rnd.uniform() < 0.025:
            k = 3
        return (int(k * 85), 0, 0, 255)
    f.pintar(mancha)
    p = Peca("terreno", "chao")
    p.add(f, (0, 0, 0, 255))
    return p


# ------------------------------------------------------------------ peças comuns (pacote "pista")
def deck():
    """O deck da pista: uma caixa com a textura (UV pelo mundo: u = x, v = z)."""
    comp = 0.1 - Z_FIM_DECK
    f = caixa(MEIA_LARGURA * 2, 0.2, comp, 0.0, centro=(0, -0.1, 0.1 - comp / 2))
    p = Peca("deck", "pista")
    def uv(V):
        u = V[:, 0] / (MEIA_LARGURA * 2.0) + 0.5
        v = (V[:, 2] - 0.1) / (Z_FIM_DECK - 0.1)
        return np.stack([1.0 - u, 1.0 - v], axis=1)
    p.add(f, (255, 255, 255, 255), uv=uv)
    return p


def aproximacao():
    f = caixa(6.6, 0.2, 6.2, 0.0, centro=(0, -0.101, 3.2))
    p = Peca("aproximacao", "madeira")
    p.add(f, (255, 255, 255, 255), uv=lambda V: np.stack([V[:, 0] / 1.6, V[:, 2] / 3.2], axis=1))
    return p


def fosso_e_poco():
    escuro = cor("#16336E")
    y_f = Y_CHAO_FOSSO - 0.1
    meio_z = (ALCAPAO_Z0 + ALCAPAO_Z1) / 2.0
    comp = ALCAPAO_Z0 - ALCAPAO_Z1
    itens = [
        (bloco((2.0, 0.2, Z_FIM_DECK - ALCAPAO_Z0), (0, y_f, (Z_FIM_DECK + ALCAPAO_Z0) / 2)), escuro),
        (bloco((2.0, 0.2, ALCAPAO_Z1 - Z_FUNDO), (0, y_f, (ALCAPAO_Z1 + Z_FUNDO) / 2)), escuro),
    ]
    for s in (-1, 1):
        itens.append((bloco((1.0 - ALCAPAO_X, 0.2, comp), (s * (1.0 + ALCAPAO_X) / 2.0, y_f, meio_z)), escuro))
    # poço: caixa do avesso, escurecendo com a fundura (cor do vértice)
    x = ALCAPAO_X - 0.003
    z0 = ALCAPAO_Z0 - 0.003
    z1 = ALCAPAO_Z1 + 0.003
    topo = Y_CHAO_FOSSO + 0.0025
    poco = caixa(x * 2.0, topo - POCO_FUNDO, z0 - z1, 0.0, centro=(0, (topo + POCO_FUNDO) / 2.0, (z0 + z1) / 2.0)).inverter()
    poco.pintar(lambda c, n: misturar(cor("#0A1430"), cor("#2A4C8E"), min(max((c[1] - POCO_FUNDO) / (topo - POCO_FUNDO), 0), 1) ** 1.5))
    itens.append((poco, cor("#0A1430")))
    # teto escuro do fosso (por dentro do gabinete)
    itens.append((bloco((2.3, 0.1, -Z_FUNDO + Z_PINO1 + 0.7), (0, 2.12, (Z_PINO1 + 0.7 + Z_FUNDO) / 2)), cor("#0B1B3A")))
    return peca_de("fosso", itens)


def cacapa():
    """O túnel escuro da caçapa das bolas (sem luz) + a boca."""
    escuro = cor("#05070D")
    meia = LARGURA_BOCA / 2.0
    alto = Y_BOCA - Y_CHAO_FOSSO
    fundo = 1.0
    itens = [
        (bloco((LARGURA_BOCA + 0.2, 0.2, fundo), (0, Y_CHAO_FOSSO - 0.1, Z_FUNDO - fundo / 2)), escuro),
        (bloco((LARGURA_BOCA + 0.2, 0.1, fundo), (0, Y_BOCA + 0.05, Z_FUNDO - fundo / 2)), escuro),
        (bloco((LARGURA_BOCA + 0.2, alto + 0.2, 0.1), (0, Y_CHAO_FOSSO + alto / 2, Z_FUNDO - fundo - 0.05)), escuro),
    ]
    for s in (-1, 1):
        itens.append((bloco((0.1, alto + 0.1, fundo), (s * (meia + 0.05), Y_CHAO_FOSSO + alto / 2, Z_FUNDO - fundo / 2)), escuro))
    return peca_de("cacapa", itens, "luz")


def borracha_boca():
    borracha = cor("#20263A")
    meia = LARGURA_BOCA / 2.0
    alto = Y_BOCA - Y_CHAO_FOSSO
    itens = []
    for s in (-1, 1):
        x0 = meia + 0.06
        x1 = MEIA_LARGURA + CANALETA + 0.06
        itens.append((bloco((x1 - x0, alto + 0.14, 0.12), (s * (x0 + x1) / 2.0, Y_CHAO_FOSSO + (alto + 0.14) / 2.0 - 0.07, Z_FUNDO - 0.02)), borracha))
    # moldura amarela em volta da boca
    itens.append((perfil_z(ret(LARGURA_BOCA + 0.26, 0.12, 0.05, (0, Y_BOCA + 0.06)), Z_FUNDO - 0.04, Z_FUNDO + 0.1, 0.012), FRISO))
    for s in (-1, 1):
        itens.append((perfil_z(ret(0.13, alto + 0.12, 0.05, (s * (meia + 0.065), Y_CHAO_FOSSO + (alto + 0.12) / 2.0 - 0.02)),
                               Z_FUNDO - 0.04, Z_FUNDO + 0.1, 0.012), FRISO))
    return peca_de("boca", itens)


def tira():
    """Uma tira de borracha da cortina da caçapa: pivô em cima (o jogo copia e gira)."""
    alto = Y_BOCA - Y_CHAO_FOSSO - 0.03
    larg = LARGURA_BOCA / N_TIRAS - 0.012
    return peca_de("tira", [(bloco((larg, alto, 0.012), (0, -alto / 2.0, 0)), cor("#151A28"))])


def bumper():
    """Um para-choque (cápsula deitada ao longo de Z, 1,14 m); o jogo põe 10 de cada lado.
    Gomos em anéis alternados: as duas cores da fase vêm da cor do vértice (R=1ª, G=2ª)."""
    perfil = []
    r = 0.07
    L = 1.14 - 2 * r
    for i in range(4):
        a = -math.pi / 2 + (math.pi / 2) * i / 3
        perfil.append((math.cos(a) * r if i else 0.0, -L / 2 + math.sin(a) * r))
    k = 7
    for i in range(1, k):
        perfil.append((r, -L / 2 + L * i / k))
    for i in range(4):
        a = (math.pi / 2) * i / 3
        perfil.append((math.cos(a) * r if i < 3 else 0.0, L / 2 + math.sin(a) * r))
    f = torno(perfil, gomos=10, fase=0.5)
    # listras: anéis alternados (marcados em vermelho/verde no vértice = cor 1/cor 2)
    f.pintar(lambda c, n: (255, 0, 0, 255) if int(math.floor((c[1] + 1.0) / (1.14 / 8))) % 2 == 0 else (0, 255, 0, 255))
    f.girar(90, 0, 0)    # deitado ao longo de Z
    return peca_de("bumper", [(f, (255, 0, 0, 255), 30)], "bumper")


def maquina_mesa():
    maq = cor("#E4EAF4")
    det = cor("#FFD23F")
    z_mesa = Z_PINO1 - FILEIRA * 1.5
    itens = [(caixa(1.28, 0.07, 1.32, 0.02), maq)]
    for n in range(1, 11):
        p = posicao_do_pino(n)
        copo = prisma(10, 0.06, 0.12, 0.0, raio_topo=0.075, centro=(p[0], -0.035 - 0.12, p[2] - z_mesa))
        itens.append((copo, det))
    for s in (-1, 1):
        itens.append((prisma(8, 0.03, 1.4, 0.0, centro=(s * 0.55, 0.0, 0)), mais_escura(maq, 0.75)))
    return peca_de("mesa", itens)


def maquina_barra():
    maq = cor("#E4EAF4")
    tam = (MEIA_LARGURA * 2 + 0.3, 0.17, 0.07)
    itens = [(caixa(tam[0], tam[1], tam[2], 0.015), cor("#FFD23F"))]
    # listras de aviso
    for k in range(-5, 6):
        itens.append((caixa(0.06, 0.12, 0.004, 0.0, centro=(k * 0.15, 0, 0.037)).girar(0, 0, 30, centro=(k * 0.15, 0, 0.037)), cor("#2A2430")))
    for s in (-1, 1):
        itens.append((caixa(0.05, 1.3, 0.05, 0.008, centro=(s * (MEIA_LARGURA + 0.12), 0.65, 0)), mais_escura(maq, 0.75)))
    return peca_de("barra", itens)


def maquina_porta():
    """Uma portinha do alçapão (lado +1): pivô na dobradiça de FORA, a peça vai para dentro (-X)."""
    comp = ALCAPAO_Z0 - ALCAPAO_Z1
    tam = (ALCAPAO_X - 0.004, 0.04, comp - 0.008)
    itens = [(caixa(tam[0], tam[1], tam[2], 0.006, centro=(-tam[0] / 2.0, -tam[1] / 2.0, 0)), cor("#1D4290")),
             (caixa(0.045, 0.004, tam[2] - 0.05, 0.0, centro=(-(tam[0] - 0.035), 0.002, 0)), cor("#FFD23F"))]
    return peca_de("porta", itens)


def retorno_bolas():
    marinho = cor("#123A7A")
    azul = cor("#1E62C8")
    itens = [
        (perfil_z(ret(0.56, 0.5, 0.1, (0, 0.25)), -0.8, 0.8, 0.02), marinho),
        (perfil_z(ret(0.6, 0.5, 0.24, (0, 0.62), 3), 0.25, 0.8, 0.02), azul),
        (perfil_z(ret(0.5, 0.06, 0.03, (0, 0.53)), -0.78, 0.3), FRISO),
    ]
    cores = [cor("#169BD5"), cor("#FF7A21"), cor("#2DBE4E")]
    for i in range(3):
        itens.append((icosfera(0.13, 2, centro=(0, 0.69, -0.62 + i * 0.29)), cores[i], 40))
    return peca_de("retorno", itens)


def podio(cor_h):
    c = cor(cor_h)
    itens = [(prisma(16, 0.45, 0.14, 0.012, raio_topo=0.4), c),
             (prisma(16, 0.37, 0.014, 0.0, centro=(0, 0.14, 0)), cor("#F4F8FF"))]
    p = peca_de("podio", itens)
    anel = Peca("podio_anel", "luz")
    anel.add(torno([(0.37, 0.142), (0.405, 0.142), (0.405, 0.152), (0.37, 0.152)], gomos=24, tampa_baixo=False, tampa_cima=False),
             cor("#FFD43B"))
    return p, anel


def sofa():
    vermelho = cor("#E8384F")
    marinho = cor("#123A7A")
    itens = [(extrudar(ret(0.66, 0.42, 0.12, (0.0, 0.21)), -1.1, 1.1, 0.02), vermelho),
             (extrudar(ret(0.2, 0.86, 0.09, (-0.27, 0.43)), -1.1, 1.1, 0.02), vermelho)]
    for zz in (-1.2, 1.2):
        itens.append((extrudar(ret(0.66, 0.6, 0.1, (0.0, 0.3)), zz - 0.12, zz + 0.12, 0.02), marinho))
    return itens


def area_jogadores_comum():
    """Tapete, friso, sofás e postes (iguais em todas as fases)."""
    z_parede = 7.7
    itens = [(bloco((9.6, 0.06, z_parede - 6.3), (0, -0.03, (6.3 + z_parede) / 2)), cor("#1B3E86")),
             (bloco((6.6, 0.065, 0.07), (0, -0.03, 6.33)), FRISO)]
    for s in (-1, 1):
        itens.append((bloco((0.12, 0.04, 6.2), (s * 3.3, 0.0, 3.2)), cor("#FF9F2E")))
        for (f, c) in sofa():
            g = f.copia().girar(0, 90, 0).mover((s * 3.4, 0, z_parede - 0.42))
            itens.append((g, c))
    return peca_de("area_jogadores", itens)


# ------------------------------------------------------------------ peças de cada fase (pacote "pista_<fase>")
def perfil_arco(r, y0, yc, alto, n=24):
    p = [(r, y0)]
    for i in range(n + 1):
        a = math.pi * i / n
        p.append((math.cos(a) * r, yc + math.sin(a) * alto))
    p.append((-r, y0))
    return p


def borda_do_arco(ang, r, y0, yc, alto):
    d = (math.cos(ang), math.sin(ang))
    if abs(d[0]) > 1e-4:
        t_lado = r / abs(d[0])
        y_lado = y0 + t_lado * d[1]
        if y_lado <= yc:
            return (t_lado * d[0], y_lado)
    a = (d[0] * d[0]) / (r * r) + (d[1] * d[1]) / (alto * alto)
    b = 2.0 * (y0 - yc) * d[1] / (alto * alto)
    c = (y0 - yc) * (y0 - yc) / (alto * alto) - 1.0
    t = (-b + math.sqrt(max(b * b - 4.0 * a * c, 0.0))) / (2.0 * a)
    return (t * d[0], y0 + t * d[1])


def leque_raios(r, y0, yc, alto, setores, z, ca, cb):
    """O leque de raios do arco: setores alternando as duas cores, mais claros na borda."""
    V, F, C = [], [], []
    sub = 3
    for k in range(setores):
        cc = ca if k % 2 == 0 else cb
        for j in range(sub):
            a0 = math.pi * (k + j / sub) / setores
            a1 = math.pi * (k + (j + 1) / sub) / setores
            p0 = borda_do_arco(a0, r, y0, yc, alto)
            p1 = borda_do_arco(a1, r, y0, yc, alto)
            base = len(V)
            V += [(0.0, y0, z), (p1[0], p1[1], z), (p0[0], p0[1], z)]
            # virado para +Z (para quem joga): cross(b - a, c - a) aponta para +Z
            F.append((base, base + 2, base + 1))
            C.append(cc)
    f = Forma(V, F, C)
    a, b, c = f.V[f.F[0]]
    if np.cross(b - a, c - a)[2] < 0:
        f.inverter()
    return f


def cortina(fase):
    """A cortina pregueada do fundo dos pinos, com as cores da fase em faixas
    verticais (dobras) e estrelinhas claras."""
    cfg = FASES[fase]
    ca, cb = cor(cfg["fundo"][0]), cor(cfg["fundo"][1])
    halo = cor(cfg["halo"])
    larg, alt, dobras, fundura = 2.2, 1.95, 11, 0.07
    cols = dobras * 4
    V, F, C = [], [], []
    k = 2 * math.pi * dobras / larg
    for i in range(cols + 1):
        x = -larg / 2 + larg * i / cols
        z = math.sin(x * k) * fundura * 0.5
        V.append((x, -alt / 2, z))
        V.append((x, alt / 2, z))
    for i in range(cols):
        a, b, c, d = 2 * i, 2 * i + 2, 2 * i + 1, 2 * i + 3
        F += [(a, c, b), (b, c, d)]
        t = i / cols
        brilho = 0.5 + 0.5 * math.cos(((i + 0.5) / 4.0) * 2 * math.pi)
        cc = misturar(ca, cb, brilho)
        # o halo atrás dos pinos (no meio, embaixo)
        dm = abs(t - 0.5) * 2
        if dm < 0.35:
            cc = misturar(cc, halo, (0.35 - dm) / 0.35 * 0.35)
        C += [cc, cc]
    f = Forma(V, F, C)
    a, b, c = f.V[f.F[0]]
    if np.cross(b - a, c - a)[2] < 0:
        f.inverter()
    f.mover((0, Y_BOCA + 0.08 + 0.975, Z_FUNDO + 0.04))
    itens = [(f, ca)]
    # estrelinhas
    rnd = np.random.RandomState(9)
    for _ in range(26):
        x = rnd.uniform(-1.0, 1.0)
        y = rnd.uniform(Y_BOCA + 0.25, Y_BOCA + 1.95)
        z = Z_FUNDO + 0.04 + math.sin(x * k) * fundura * 0.5 + 0.012
        e = extrudar(circulo2d(0.018, 4), 0, 0.004).mover((x, y, z))
        itens.append((e, misturar(halo, cor("#FFFFFF"), 0.5)))
    return itens


def pecas_fase(fase):
    cfg = FASES[fase]
    gabinete = cor(cfg["gabinete"])
    coluna = cor(cfg["coluna"])
    parede = cor(cfg["parede"])
    placa = cor(cfg["placa"])
    divisoria = cor(cfg["divisoria"])
    faixa = cor(cfg["faixa"])
    m = Modelo("pista_" + fase)
    # canaletas
    can = []
    for s in (-1, 1):
        can += canaleta(fase, s * (MEIA_LARGURA + CANALETA / 2), Z_FIM_DECK, 0.1)
    m.parte("canaletas", None, (0, 0, 0), peca_de("canaletas", can))
    # divisórias (capping) e as faixas de luz em cima
    div = []
    luz = Peca("luz_divisoria", "luz")
    for s in (-1, 1):
        xc = s * (MEIA_LARGURA + CANALETA + 0.036 + 0.16)
        div.append((perfil_z(ret(0.32, 0.34, 0.06, (xc, -0.07), 2), Z_PINO1 + 0.2, 0.1, 0.02), divisoria))
        luz.add(bloco((0.05, 0.012, -Z_PINO1 - 0.3), (xc, 0.104, (Z_PINO1 + 0.2) / 2.0 + 0.05)), cor(cfg["neon"]))
    m.parte("divisorias", None, (0, 0, 0), peca_de("divisorias", div))
    m.parte("luz_divisoria", None, (0, 0, 0), luz)
    # paredes da caçapa (kickbacks) + friso
    z_frente = Z_PINO1 + 0.9
    par = []
    for s in (-1, 1):
        x = s * (MEIA_LARGURA + CANALETA + 0.06)
        par.append((perfil_z(ret(0.12, 1.0, 0.05, (x, 0.35)), Z_FUNDO, z_frente, 0.015), parede))
        par.append((perfil_z(ret(0.17, 0.07, 0.034, (x, 0.875)), Z_FUNDO, z_frente + 0.02, 0.01), FRISO))
    m.parte("paredes", None, (0, 0, 0), peca_de("paredes", par))
    # gabinete por fora (laterais, teto, fundo) + friso
    z_tras = Z_FUNDO - 0.95
    z_masc = Z_PINO1 + 0.68
    gab = []
    for s in (-1, 1):
        gab.append((perfil_z(ret(0.34, 3.7, 0.12, (s * 1.23, 1.55), 3), z_tras + 0.3, z_masc, 0.03), gabinete))
    gab.append((perfil_z(ret(2.8, 0.32, 0.1, (0, 3.24), 2), z_tras + 0.3, z_masc, 0.03), gabinete))
    gab.append((perfil_z(ret(2.9, 3.75, 0.22, (0, 1.53), 3), z_tras, Z_FUNDO - 0.02, 0.03), gabinete))
    gab.append((perfil_z(ret(2.96, 0.09, 0.04, (0, 3.42)), z_tras - 0.03, z_masc, 0.01), FRISO))
    # letreiro acima dos pinos (o logo é outra peça, com textura)
    gab.append((perfil_z(ret(2.7, 0.95, 0.07, (0, 1.85), 2), Z_PINO1 + 0.68, Z_PINO1 + 0.82, 0.02), placa))
    gab.append((perfil_z(ret(2.8, 1.05, 0.09, (0, 1.85), 2), Z_PINO1 + 0.66, Z_PINO1 + 0.78, 0.02), FRISO))
    # arco: chapa escura + moldura (o leque de raios brilha: peça "luz")
    gab.append((perfil_z(perfil_arco(1.42, 2.22, 2.45, 0.86), Z_PINO1 + 0.69, Z_PINO1 + 0.79), cor("#2A1E3A")))
    gab.append((perfil_z(perfil_arco(1.52, 2.2, 2.45, 0.96), Z_PINO1 + 0.66, Z_PINO1 + 0.76), FRISO))
    # colunas dos lados do letreiro
    zc = Z_PINO1 + 0.75
    for s in (-1, 1):
        x = s * 1.93
        gab.append((perfil_z(ret(1.16, 3.0, 0.14, (x, 1.15), 3), zc - 0.07, zc + 0.07, 0.025), coluna))
        gab.append((perfil_z(ret(1.26, 0.12, 0.05, (x, 2.7)), zc - 0.09, zc + 0.09, 0.012), FRISO))
    m.parte("gabinete", None, (0, 0, 0), peca_de("gabinete", gab))
    raios = Peca("raios", "luz")
    raios.add(leque_raios(1.42, 2.22, 2.45, 0.86, 22, Z_PINO1 + 0.792, cor(cfg["portal"][0]), cor(cfg["portal"][1])),
              cor(cfg["portal"][0]))
    m.parte("raios", None, (0, 0, 0), raios)
    m.parte("cortina", None, (0, 0, 0), peca_de("cortina", cortina(fase)))
    # mureta dos jogadores e letreiro STRIKE KIDS virado para a pista
    z_parede = 7.7
    mur = [(perfil_z(ret(9.6, 1.1, 0.12, (0, 0.55), 2), z_parede, z_parede + 0.3, 0.025), gabinete),
           (perfil_z(ret(9.7, 0.1, 0.05, (0, 1.12)), z_parede - 0.03, z_parede + 0.33, 0.01), FRISO)]
    for s in (-1, 1):
        mur.append((perfil_z(ret(0.16, 3.0, 0.07, (s * 1.9, 1.5)), z_parede - 0.2, z_parede - 0.04, 0.012), coluna))
    mur.append((perfil_z(ret(4.0, 1.75, 0.16, (0, 2.55), 2), z_parede - 0.18, z_parede - 0.06, 0.02), placa))
    m.parte("mureta", None, (0, 0, 0), peca_de("mureta", mur))
    # faixas laterais da aproximação (cor da fase)
    fx = []
    for s in (-1, 1):
        fx.append((bloco((0.12, 0.042, 6.2), (s * 3.3, 0.001, 3.2)), faixa))
    m.parte("faixas", None, (0, 0, 0), peca_de("faixas", fx))
    return m


def lampadas():
    """As posições de todas as lâmpadas (letreiro, arco, colunas, letreiro dos jogadores)."""
    pts = []
    zc = Z_PINO1 + 0.83
    for i in range(26):
        pts.append((-1.3 + i * 0.104, 1.41, zc))
    for i in range(9):
        pts.append((-1.33, 1.41 + i * 0.11, zc))
        pts.append((1.33, 1.41 + i * 0.11, zc))
    for i in range(31):
        a = math.pi * i / 30.0
        pts.append((math.cos(a) * 1.47, 2.45 + math.sin(a) * 0.91, zc - 0.02))
    zc2 = Z_PINO1 + 0.75
    for s in (-1, 1):
        x = s * 1.93
        for i in range(23):
            pts.append((x - s * 0.5, 0.2 + i * 0.1, zc2 + 0.08))
            pts.append((x + s * 0.5, 0.2 + i * 0.1, zc2 + 0.08))
    z_parede = 7.7
    for i in range(30):
        x = -1.85 + i * (3.7 / 29.0)
        pts.append((x, 3.35, z_parede - 0.21))
        pts.append((x, 1.75, z_parede - 0.21))
    return pts


def modelo_pista():
    m = Modelo("pista")
    m.parte("deck", None, (0, 0, 0), deck())
    m.parte("aproximacao", None, (0, 0, 0), aproximacao())
    m.parte("fosso", None, (0, 0, 0), fosso_e_poco())
    m.parte("cacapa", None, (0, 0, 0), cacapa())
    m.parte("boca", None, (0, 0, 0), borracha_boca())
    m.parte("area_jogadores", None, (0, 0, 0), area_jogadores_comum())
    m.parte("retorno", None, tuple(RETORNO), retorno_bolas())
    m.parte("terreno", None, (0, 0, 0), terreno())
    m.parte("lampadas", None, (0, 0, 0), lampadas_pontos("lampadas", lampadas()))
    # peças que o jogo copia/mexe (no próprio pivô)
    m.parte("tira", None, (0, 0, 0), tira())
    m.parte("bumper", None, (0, 0, 0), bumper())
    m.parte("mesa", None, (0, 0, 0), maquina_mesa())
    m.parte("barra", None, (0, 0, 0), maquina_barra())
    m.parte("porta", None, (0, 0, 0), maquina_porta())
    for i, h in enumerate(("#169BD5", "#FF7A21")):
        p, anel = podio(h)
        m.parte("podio_%d" % i, None, (0, 0, 0), p)
        m.parte("podio_anel_%d" % i, "podio_%d" % i, (0, 0, 0), anel)
    return m


MODELOS = {"pista": modelo_pista}
for _f in FASES:
    MODELOS["pista_" + _f] = (lambda f=_f: pecas_fase(f))
