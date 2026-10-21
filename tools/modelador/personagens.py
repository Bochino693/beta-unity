# -*- coding: utf-8 -*-
"""
BOB (cachorrinho) e MIA (gatinha) — edição Unity, lisos e em boa resolução.

Mascotes de cabeça grande (proporção de bebê), corpinho de pera, bracinhos
e perninhas curtos — agora em faces retas e quinas vivas, cada peça com a
sua cor (nada de massinha derretida): roupa com gola, mangas, bainha,
estampa em relevo e tênis com sola.

Esqueleto (o mesmo das edições Godot, com o X espelhado — ver
docs/ARQUITETURA.md): corpo (quadril) > cabeca, rabo, braco > antebraco,
coxa > canela > pe. Cada parte é modelada na posição de repouso e gravada
no espaço do próprio pivô.

Rosto: peças separadas, sem luz (desenho limpo), que o jogo liga e desliga
para cada expressão — olhos abertos, fechados (piscar), felizes (^ ^),
sobrancelhas e 5 bocas.
"""
import math

import numpy as np

from malha import (Forma, Modelo, Peca, caixa, caixa_arredondada, cor, esfera, extrudar, icosfera, juntar,
                   estrela2d, circulo2d, mais_escura, prisma, rot, torno, tubo, trs, eixo_para, aplicar)

# ------------------------------------------------------------- juntas (repouso, mundo; ele olha para +Z)
QUADRIL = np.array([0.0, 0.36, 0.0])
PESCOCO = np.array([0.0, 0.635, 0.0])
CABECA_C = np.array([0.0, 0.865, 0.0])
CABECA_R = np.array([0.272, 0.246, 0.244])
# lado: s = -1 esquerda (-X), +1 direita (+X)
OMBRO = np.array([0.150, 0.565, 0.0])
COTOVELO = np.array([0.198, 0.468, 0.006])
PULSO = np.array([0.226, 0.386, 0.014])
MAO = np.array([0.236, 0.352, 0.018])
COXA = np.array([0.086, 0.36, 0.0])
JOELHO = np.array([0.086, 0.215, 0.006])
TORNOZELO = np.array([0.086, 0.078, 0.0])
RABO = np.array([0.0, 0.40, -0.135])


def lado(v, s):
    return np.array([v[0] * s, v[1], v[2]])


# ------------------------------------------------------------- utilidades
def no_elipsoide(c, r, d):
    """Ponto da superfície do elipsoide (centro c, raios r) na direção d e a normal ali."""
    d = np.asarray(d, float)
    d = d / np.linalg.norm(d)
    t = 1.0 / math.sqrt(sum((d[i] / r[i]) ** 2 for i in range(3)))
    p = c + d * t
    n = (p - c) / (np.asarray(r) ** 2)
    return p, n / np.linalg.norm(n)


def orientar(forma, normal, giro=0.0, cima=(0, 1, 0)):
    """Gira uma forma feita virada para +Z para olhar na direção `normal`
    (mantendo o "cima" o mais para cima possível); giro: graus em volta da normal."""
    n = np.asarray(normal, float)
    n /= np.linalg.norm(n)
    up = np.asarray(cima, float)
    x = np.cross(up, n)
    if np.linalg.norm(x) < 1e-6:
        x = np.array([1.0, 0, 0])
    x /= np.linalg.norm(x)
    y = np.cross(n, x)
    R = np.stack([x, y, n], axis=1)
    if giro:
        R = R @ rot(0, 0, giro)
    forma.V = forma.V @ R.T
    return forma


def disco(r, espessura, lados=10, escala=(1, 1)):
    """Disco/lentilha virado para +Z (frente em z = espessura)."""
    return extrudar(circulo2d(r, lados, escala=escala), 0.0, espessura)


def arco2d(raio, espessura, a0, a1, n=8):
    """Faixa em arco (graus), para olhos fechados, sorrisos, sobrancelhas."""
    pts_fora, pts_dentro = [], []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        pts_fora.append((math.cos(a) * (raio + espessura / 2), math.sin(a) * (raio + espessura / 2)))
        pts_dentro.append((math.cos(a) * (raio - espessura / 2), math.sin(a) * (raio - espessura / 2)))
    return pts_fora + pts_dentro[::-1]


def curva_bezier(p0, p1, p2, p3, n=10):
    P = []
    for i in range(n + 1):
        t = i / n
        a = (1 - t) ** 3
        b = 3 * (1 - t) ** 2 * t
        c = 3 * (1 - t) * t * t
        d = t ** 3
        P.append(a * np.asarray(p0) + b * np.asarray(p1) + c * np.asarray(p2) + d * np.asarray(p3))
    return np.array(P)


def membro(a, b, ra, rb, gomos=7, fase=0.0):
    """Tubo reto facetado de a até b (com as pontas fechadas)."""
    return tubo([a, b], [ra, rb], gomos=gomos, tampas=True)


def bola_junta(c, r, sub=1):
    return icosfera(r, sub, centro=c)


# ------------------------------------------------------------- paletas
class Paleta(dict):
    def __getattr__(self, k):
        return self[k]


BOB = Paleta(
    pelo=cor("#EE9A45"), pelo_escuro=cor("#D77F2E"), focinho=cor("#FFF0DC"), nariz=cor("#2E1D1A"),
    orelha=cor("#9C5426"), camisa=cor("#1E9BD7"), camisa_escura=cor("#167CB0"), estampa=cor("#FFD23F"),
    lenco=cor("#F04A3E"), short=cor("#2B4C8C"), tenis=cor("#F04A3E"), sola=cor("#FFFFFF"), cadarco=cor("#FFFFFF"),
    olho=cor("#2A1610"), iris=cor("#7A4220"), brilho=cor("#FFFFFF"), boca=cor("#5A2318"), lingua=cor("#FF7A8A"),
    bochecha=cor("#FF9C8A"), pata=cor("#FFF0DC"), almofada=cor("#E7B79A"),
)

MIA = Paleta(
    pelo=cor("#B9BCD6"), pelo_escuro=cor("#8E92B8"), focinho=cor("#FFFFFF"), nariz=cor("#FF6F96"),
    orelha_dentro=cor("#FFB3C7"), listra=cor("#7B80A8"), vestido=cor("#FF6A1F"), vestido_escuro=cor("#E2560F"),
    bolinha=cor("#FFFFFF"), gola=cor("#FFFFFF"), laco=cor("#2E7BFF"), laco_escuro=cor("#1F5ED0"),
    sapato=cor("#8A4FE0"), sola=cor("#FFFFFF"), meia=cor("#FFFFFF"),
    olho=cor("#12202A"), iris=cor("#1FBF9F"), brilho=cor("#FFFFFF"), boca=cor("#5A2330"), lingua=cor("#FF7A8A"),
    bochecha=cor("#FF9CB0"), pata=cor("#FFFFFF"), bigode=cor("#5C6086"),
)


# ------------------------------------------------------------- cabeça
def cranio(p, largura=1.0, gato=False):
    """A cabeça: elipsoide facetado (14 gomos x 10 anéis) com bochechas
    fofas embaixo e a testa um pouco reta."""
    r = CABECA_R * np.array([largura, 1.0, 1.0])
    f = esfera(1.0, gomos=16, aneis=11)
    V = f.V.copy()

    def deforma(V):
        x, y, z = V[:, 0], V[:, 1], V[:, 2]
        # bochechas: embaixo e na frente fica mais largo
        bo = np.clip(-y + 0.15, 0, 1) * np.clip(z + 0.6, 0, 1.6)
        x2 = x * (1.0 + 0.10 * bo)
        # testa um pouco mais chata em cima (cabeça de mascote)
        y2 = np.where(y > 0, y * (0.94 + 0.06 * (1 - np.abs(x))), y * 0.98)
        # rosto um pouco achatado na frente
        z2 = np.where(z > 0, z * (0.93 + 0.05 * np.abs(y)), z)
        if gato:
            # gatinha: queixo mais fino
            x2 = x2 * (1.0 - 0.10 * np.clip(-y - 0.2, 0, 1))
        return np.stack([x2 * r[0], y2 * r[1], z2 * r[2]], axis=1)
    f.V = deforma(V)
    f.mover(CABECA_C)
    return f, r


def olho_aberto(pal, s, c, r, d, raio=(0.045, 0.058)):
    """Olho aberto: oval escuro com íris e dois brilhos (peças sem luz)."""
    p, n = no_elipsoide(c, r, d)
    base = disco(1.0, 0.004, 14, escala=(raio[0], raio[1]))
    iris = disco(1.0, 0.0045, 12, escala=(raio[0] * 0.62, raio[1] * 0.5)).mover((0, -raio[1] * 0.3, 0.002))
    b1 = disco(raio[0] * 0.38, 0.005, 10).mover((-s * raio[0] * 0.28 + raio[0] * 0.05, raio[1] * 0.35, 0.003))
    b2 = disco(raio[0] * 0.16, 0.005, 8).mover((s * raio[0] * 0.3, -raio[1] * 0.12, 0.003))
    pc = Peca("olho", "luz")
    for (fo, co) in ((base, pal.olho), (iris, pal.iris), (b1, pal.brilho), (b2, pal.brilho)):
        g = orientar(fo.copia(), n)
        g.mover(p + n * 0.002)
        pc.add(g, co)
    return pc, p, n


def olho_linha(pal, c, r, d, feliz, raio=0.04):
    """Olho fechado (piscar: arco para baixo ‿) ou feliz (^ ^: arco para cima)."""
    p, n = no_elipsoide(c, r, d)
    if feliz:
        pts = arco2d(raio, 0.016, 20, 160, 10)          # ∩
        desl = -raio * 0.55
    else:
        pts = arco2d(raio, 0.016, 200, 340, 10)         # ∪
        desl = raio * 0.55
    fo = extrudar(pts, 0.0, 0.005).mover((0, desl, 0))
    pc = Peca("olho_linha", "luz")
    g = orientar(fo, n)
    g.mover(p + n * 0.003)
    pc.add(g, pal.olho)
    return pc


def sobrancelha(pal, s, c, r, d, cor_s):
    p, n = no_elipsoide(c, r, d)
    fo = extrudar(arco2d(0.05, 0.014, 55, 125, 8), 0.0, 0.005).mover((0, -0.05, 0))
    pc = Peca("sobrancelha", "luz")
    g = orientar(fo, n)
    g.mover(p + n * 0.004)
    pc.add(g, cor_s)
    return pc, p, n


def boca(pal, tipo, p, n, largura=0.05):
    """As bocas (desenho sobre o focinho): sorriso, aberta, triste, o, reta."""
    pc = Peca("boca_" + tipo, "luz")
    if tipo == "sorriso":
        fo = extrudar(arco2d(largura * 0.55, 0.012, 205, 335, 10), 0, 0.005).mover((0, largura * 0.3, 0))
        pc.add(orientar(fo, n).mover(p), pal.boca)
    elif tipo == "aberta":
        # boca aberta em "D" deitado, com a linguinha
        pts = [(math.cos(math.radians(a)) * largura * 0.62, math.sin(math.radians(a)) * largura * 0.5)
               for a in np.linspace(180, 360, 12)]
        pts = [(x, y + largura * 0.12) for x, y in pts]
        fo = extrudar(pts, 0, 0.005)
        pc.add(orientar(fo, n).mover(p), pal.boca)
        lin = extrudar(circulo2d(largura * 0.28, 10, centro=(0, -largura * 0.22), escala=(1.0, 0.6)), 0, 0.006)
        pc.add(orientar(lin, n).mover(p + n * 0.001), pal.lingua)
    elif tipo == "triste":
        fo = extrudar(arco2d(largura * 0.45, 0.012, 25, 155, 10), 0, 0.005).mover((0, -largura * 0.45, 0))
        pc.add(orientar(fo, n).mover(p), pal.boca)
    elif tipo == "o":
        fo = extrudar(circulo2d(largura * 0.22, 12, escala=(0.85, 1.1)), 0, 0.005).mover((0, -largura * 0.1, 0))
        pc.add(orientar(fo, n).mover(p), pal.boca)
    elif tipo == "reta":
        fo = extrudar([(-largura * 0.3, -0.006), (largura * 0.3, -0.006), (largura * 0.3, 0.006), (-largura * 0.3, 0.006)],
                      0, 0.005).mover((0, -largura * 0.05, 0))
        pc.add(orientar(fo, n).mover(p), pal.boca)
    return pc


# ------------------------------------------------------------- corpo e membros
PERFIL_TRONCO = [(0.10, 0.300), (0.142, 0.318), (0.166, 0.352), (0.174, 0.40), (0.170, 0.45), (0.160, 0.50),
                 (0.143, 0.55), (0.118, 0.595), (0.085, 0.628), (0.05, 0.648), (0.0, 0.655)]


def tronco(cor_cima, cor_baixo, corte_y=0.40, gomos=12, cortes=()):
    """Corpinho de pera, um pouco achatado na frente-trás. Embaixo do corte, a
    cor de baixo (short/saia). cortes: alturas com um anel a mais (listras de
    borda reta)."""
    ys = [q[1] for q in PERFIL_TRONCO]
    rs = [q[0] for q in PERFIL_TRONCO]
    extra = [y for y in list(cortes) + [corte_y] if ys[0] < y < 0.64]
    perfil = sorted(set(PERFIL_TRONCO + [(float(np.interp(y, ys, rs)), float(y)) for y in extra]), key=lambda q: q[1])
    f = torno(perfil, gomos=gomos, fase=0.5)
    f.escalar((1.0, 1.0, 0.86))
    f.pintar(lambda c, n: cor_baixo if c[1] < corte_y else cor_cima)
    return f


def braco_sup(s, cor_manga, cor_pelo, manga=0.55):
    om, co = lado(OMBRO, s), lado(COTOVELO, s)
    sup = membro(om, co, 0.047, 0.042)
    m = om + (co - om) * manga
    luva = tubo([om + (om - co) * 0.15, m], [0.064, 0.058], gomos=8)
    junta = bola_junta(co, 0.041)
    return [(sup, cor_pelo), (luva, cor_manga), (junta, cor_pelo)]


def antebraco(s, cor_pelo, cor_pata):
    co, pu, mao = lado(COTOVELO, s), lado(PULSO, s), lado(MAO, s)
    inf = membro(co, pu, 0.041, 0.038)
    pata = icosfera(0.052, 1, centro=mao, escala=(1.0, 1.08, 0.95))
    # dedinhos (3 gominhos na frente da pata)
    dedos = []
    for k in (-1, 0, 1):
        dedos.append(icosfera(0.016, 0, centro=mao + np.array([k * 0.018, -0.03, 0.03]), escala=(1, 0.9, 1)))
    return [(inf, cor_pelo), (pata, cor_pata)] + [(d, cor_pata) for d in dedos]


def coxa(s, cor_short, cor_pelo, curto=True):
    q, j = lado(COXA, s), lado(JOELHO, s)
    sup = membro(q + np.array([0, 0.02, 0]), j, 0.058, 0.05)
    partes = [(sup, cor_pelo)]
    if curto:
        barra = tubo([q + np.array([0, 0.03, 0]), q + (j - q) * 0.55], [0.07, 0.066], gomos=8)
        partes.append((barra, cor_short))
    partes.append((bola_junta(j, 0.049), cor_pelo))
    return partes


def canela(s, cor_pelo, cor_meia=None):
    j, t = lado(JOELHO, s), lado(TORNOZELO, s)
    inf = membro(j, t + np.array([0, 0.012, 0]), 0.049, 0.045)
    partes = [(inf, cor_pelo)]
    if cor_meia is not None:
        partes.append((tubo([t + np.array([0, 0.004, 0]), t + np.array([0, 0.07, 0])], [0.057, 0.055], gomos=8), cor_meia))
        partes.append((tubo([t + np.array([0, 0.066, 0]), t + np.array([0, 0.078, 0])], [0.059, 0.059], gomos=8), cor_meia))
    return partes


def tenis(s, cor_tenis, cor_sola, cor_cadarco=None, bico_redondo=True):
    """Tênis de sola chata (pisa firme): sola branca, corpo colorido, bico e cadarço."""
    t = lado(TORNOZELO, s)
    x = t[0]
    sola = caixa_arredondada(0.114, 0.024, 0.19, 0.012, 1, centro=(x, 0.012, 0.03))
    corpo = caixa_arredondada(0.11, 0.07, 0.17, 0.03, 2, centro=(x, 0.058, 0.03))
    bico = caixa_arredondada(0.104, 0.052, 0.07, 0.024, 2, centro=(x, 0.05, 0.1))
    cano = prisma(8, 0.05, 0.05, 0.0, centro=(x, 0.075, 0.0))
    partes = [(sola, cor_sola), (corpo, cor_tenis), (bico, cor_sola if cor_cadarco else cor_tenis), (cano, cor_tenis)]
    if cor_cadarco is not None:
        for k in range(3):
            partes.append((caixa(0.06, 0.008, 0.012, 0.0, centro=(x, 0.094 - k * 0.006, 0.045 + k * 0.022)), cor_cadarco))
    return partes


# ------------------------------------------------------------- montagem
def _peca(nome, partes, material=""):
    p = Peca(nome, material)
    for item in partes:
        fo, c = item[0], item[1]
        suave = item[2] if len(item) > 2 else None
        p.add(fo, c, suave)
    return p


def _local(peca, pivo):
    """Leva a peça (feita na posição de repouso no mundo) para o espaço do pivô."""
    if peca is None:
        return None
    P = np.array(peca.pos) - np.asarray(pivo)
    peca.pos = [tuple(v) for v in P]
    return peca


def montar(nome, partes_por_junta, rosto, extras=()):
    """partes_por_junta: junta -> [(forma, cor[, suave])...] no mundo (repouso)."""
    m = Modelo(nome)
    piv = {
        "corpo": QUADRIL, "cabeca": PESCOCO, "rabo": RABO,
        "braco_e": lado(OMBRO, -1), "antebraco_e": lado(COTOVELO, -1),
        "braco_d": lado(OMBRO, 1), "antebraco_d": lado(COTOVELO, 1),
        "coxa_e": lado(COXA, -1), "canela_e": lado(JOELHO, -1), "pe_e": lado(TORNOZELO, -1),
        "coxa_d": lado(COXA, 1), "canela_d": lado(JOELHO, 1), "pe_d": lado(TORNOZELO, 1),
    }
    pais = {"corpo": None, "cabeca": "corpo", "rabo": "corpo", "braco_e": "corpo", "antebraco_e": "braco_e",
            "braco_d": "corpo", "antebraco_d": "braco_d", "coxa_e": "corpo", "canela_e": "coxa_e", "pe_e": "canela_e",
            "coxa_d": "corpo", "canela_d": "coxa_d", "pe_d": "canela_d"}
    for j in ["corpo", "cabeca", "rabo", "braco_e", "antebraco_e", "braco_d", "antebraco_d", "coxa_e", "canela_e",
              "pe_e", "coxa_d", "canela_d", "pe_d"]:
        pai = pais[j]
        pos = piv[j] - (piv[pai] if pai else np.zeros(3))
        peca = _peca(j, partes_por_junta.get(j, [])) if partes_por_junta.get(j) else None
        m.parte(j, pai, pos, _local(peca, piv[j]))
    for (nome_r, peca_r) in rosto:
        m.parte(nome_r, "cabeca", (0, 0, 0), _local(peca_r, PESCOCO))
    for (nome_e, pai_e, pos_e, peca_e) in extras:
        m.parte(nome_e, pai_e, np.asarray(pos_e) - piv[pai_e], _local(peca_e, pos_e))
    return m


# ------------------------------------------------------------- rosto (comum)
def rosto_completo(pal, c, r, olho_dy=0.005, olho_dx=0.092, boca_pos=None, boca_n=None, cor_sobr=None, raio_olho=(0.045, 0.058)):
    pecas = []
    for s, nm in ((-1, "e"), (1, "d")):
        d = np.array([s * olho_dx, olho_dy, 0.21])
        o, p, n = olho_aberto(pal, s, c, r, d, raio_olho)
        pecas.append(("olho_" + nm, o))
        pecas.append(("fechado_" + nm, olho_linha(pal, c, r, d, False)))
        pecas.append(("feliz_" + nm, olho_linha(pal, c, r, d, True)))
        sb, _, _ = sobrancelha(pal, s, c, r, np.array([s * olho_dx * 1.02, olho_dy + 0.1, 0.2]), cor_sobr or pal.olho)
        pecas.append(("sobrancelha_" + nm, sb))
    for tipo in ("sorriso", "aberta", "triste", "o", "reta"):
        pecas.append(("boca_" + tipo, boca(pal, tipo, boca_pos, boca_n)))
    return pecas


# ------------------------------------------------------------- BOB
def cabeca_bob():
    """A cabeça do Bob (lista de formas no mundo) e o rosto."""
    pal = BOB
    cab, r = cranio(pal, 1.0)
    c = CABECA_C
    # focinho: bloco facetado creme, com o narizinho por cima
    foc_c = c + np.array([0.0, -0.075, 0.175])
    focinho = esfera(1.0, 10, 7).escalar((0.118, 0.078, 0.092)).mover(foc_c)
    focinho.deformar(lambda V: V + np.stack([V[:, 0] * 0, np.where(V[:, 1] < foc_c[1], (V[:, 1] - foc_c[1]) * 0.25, 0), V[:, 0] * 0], 1))
    nariz = icosfera(1.0, 1, escala=(0.05, 0.034, 0.034)).deformar(
        lambda V: V * np.stack([1.0 - np.clip(-V[:, 1] / 0.034, 0, 1) * 0.35, np.ones(len(V)), np.ones(len(V))], 1)).mover(
        foc_c + np.array([0, 0.042, 0.078]))
    # orelhas caídas: abas facetadas, achatadas, presas no alto dos lados
    orelhas = []
    for s in (-1, 1):
        pts = [c + np.array([s * 0.175, 0.17, -0.02]), c + np.array([s * 0.262, 0.07, 0.0]),
               c + np.array([s * 0.30, -0.06, 0.02]), c + np.array([s * 0.285, -0.17, 0.035])]
        ore = tubo(curva_bezier(*pts, n=6), [0.055, 0.07, 0.075, 0.07, 0.06, 0.045, 0.0], gomos=6, achatar=(0.38, 1.0),
                   cima=(0, 0, 1))
        orelhas.append(ore)
    # topete: três pontinhas de pelo no alto da testa
    topete = []
    for k, (dx, h) in enumerate(((-0.04, 0.05), (0.0, 0.07), (0.045, 0.045))):
        base = c + np.array([dx, r[1] * 0.93, 0.06])
        topete.append(prisma(4, 0.028, h, 0.0, raio_topo=0.0, centro=base).girar(18, 0, -dx * 300, centro=base))
    bochechas = []
    for s in (-1, 1):
        p, n = no_elipsoide(c, r * np.array([1.08, 1, 1]), np.array([s * 0.17, -0.06, 0.17]))
        bochechas.append(orientar(disco(0.032, 0.004, 10, escala=(1.0, 0.7)), n).mover(p))
    cabeca = [(cab, pal.pelo), (focinho, pal.focinho), (nariz, pal.nariz)] + [(o, pal.orelha) for o in orelhas] + \
        [(t, pal.pelo_escuro) for t in topete] + [(b, pal.bochecha) for b in bochechas]
    # boca: no focinho, embaixo do nariz
    p_boca, n_boca = no_elipsoide(foc_c, np.array([0.118, 0.078, 0.092]), np.array([0, -0.25, 1.0]))
    rosto = rosto_completo(pal, c, r, boca_pos=p_boca + n_boca * 0.003, boca_n=n_boca)
    return cabeca, rosto


def rabo_bob(pal=BOB):
    rabo = curva_bezier(RABO, RABO + np.array([0, 0.05, -0.06]), RABO + np.array([0, 0.16, -0.06]), RABO + np.array([0, 0.17, 0.0]), n=7)
    return tubo(rabo, [0.034, 0.033, 0.031, 0.028, 0.025, 0.021, 0.016, 0.0], gomos=6)


def corpo_bob_padrao():
    """A roupa de sempre do Bob: camiseta com estrela, lenço, short e tênis."""
    pal = BOB
    corpo = [(tronco(pal.camisa, pal.short), pal.camisa)]
    # gola em V do lenço vermelho: anel + pontinha no peito
    lenco = tubo([[0, 0.618, 0]], [0], gomos=3) if False else None
    anel = torno([(0.112, 0.598), (0.122, 0.612), (0.104, 0.636), (0.09, 0.64)], gomos=12, fase=0.5, tampa_baixo=False,
                 tampa_cima=False).escalar((1, 1, 0.9))
    ponta = extrudar([(-0.06, 0.0), (0.06, 0.0), (0.0, -0.085)], 0.0, 0.016, chanfro=0.004)
    ponta.girar(-24, 0, 0).mover((0, 0.612, 0.088))
    estrela = extrudar(estrela2d(5, 0.052, 0.024), 0.0, 0.016, chanfro=0.005).girar(-12, 0, 0).mover((0, 0.475, 0.135))
    corpo += [(anel, pal.lenco), (ponta, pal.lenco), (estrela, pal.estampa)]
    # barra da camiseta (faixa mais escura em volta)
    barra = torno([(0.176, 0.405), (0.178, 0.425)], gomos=12, fase=0.5, tampa_baixo=False, tampa_cima=False).escalar((1, 1, 0.87))
    corpo.append((barra, pal.camisa_escura))
    juntas = {"corpo": corpo, "rabo": [(rabo_bob(), pal.pelo)]}
    for s, nm in ((-1, "e"), (1, "d")):
        juntas["braco_" + nm] = braco_sup(s, pal.camisa, pal.pelo)
        juntas["antebraco_" + nm] = antebraco(s, pal.pelo, pal.pata)
        juntas["coxa_" + nm] = coxa(s, pal.short, pal.pelo)
        juntas["canela_" + nm] = canela(s, pal.pelo)
        juntas["pe_" + nm] = tenis(s, pal.tenis, pal.sola, pal.cadarco)
    return juntas


def bob(roupa=""):
    """O Bob com a roupa de sempre ("") ou a roupinha de uma fase (roupas.py)."""
    cabeca, rosto = cabeca_bob()
    extras = ()
    if roupa:
        import roupas
        juntas, extras = roupas.vestir("bob", roupa, cabeca)
    else:
        juntas = corpo_bob_padrao()
        juntas["cabeca"] = cabeca
    return montar("bob" if not roupa else "bob_" + roupa, juntas, rosto, extras)


# ------------------------------------------------------------- MIA
def cabeca_mia(com_laco=True):
    """A cabeça da Mia (lista de formas no mundo) e o rosto."""
    pal = MIA
    cab, r = cranio(pal, 1.0, gato=True)
    c = CABECA_C
    foc_c = c + np.array([0.0, -0.085, 0.168])
    focinho = esfera(1.0, 10, 7).escalar((0.105, 0.07, 0.08)).mover(foc_c)
    nariz = extrudar([(-0.024, 0.012), (0.024, 0.012), (0.0, -0.014)], 0.0, 0.02, chanfro=0.005).girar(-30, 0, 0).mover(
        foc_c + np.array([0, 0.045, 0.06]))
    # orelhas pontudas (pirâmides de 4 lados), rosadas por dentro
    orelhas = []
    for s in (-1, 1):
        base = c + np.array([s * 0.15, 0.17, -0.01])
        ore = prisma(4, 0.088, 0.17, 0.0, raio_topo=0.0, fase=0.5).escalar((1.0, 1.0, 0.55))
        ore.girar(0, 0, -s * 24).mover(base)
        den = prisma(3, 0.05, 0.11, 0.0, raio_topo=0.0, fase=0.25).escalar((1.0, 1.0, 0.3))
        den.girar(-8, 0, -s * 24).mover(base + np.array([s * 0.002, 0.012, 0.032]))
        orelhas += [(ore, pal.pelo), (den, pal.orelha_dentro)]
    # listrinhas da testa (três cunhas)
    listras = []
    for k, dx in enumerate((-0.05, 0.0, 0.05)):
        p, n = no_elipsoide(c, r, np.array([dx, 0.2, 0.12]))
        h = 0.07 if dx == 0 else 0.055
        cunha = extrudar([(-0.013, 0.0), (0.013, 0.0), (0.0, -h)], 0.0, 0.006)
        listras.append(orientar(cunha, n, giro=dx * 160).mover(p + n * 0.001))
    # bigodes: palitinhos dos lados do focinho
    bigodes = []
    for s in (-1, 1):
        for k, ang in enumerate((8, -6)):
            a = foc_c + np.array([s * 0.07, 0.0 - k * 0.022, 0.05])
            b = a + np.array([s * 0.12, math.radians(ang) * 0.12, -0.02])
            bigodes.append(tubo([a, b], [0.0045, 0.0035], gomos=4))
    bochechas = []
    for s in (-1, 1):
        p, n = no_elipsoide(c, r, np.array([s * 0.17, -0.07, 0.17]))
        bochechas.append(orientar(disco(0.03, 0.004, 10, escala=(1.0, 0.7)), n).mover(p))
    # laço azul na orelha direita
    laco = []
    lc = c + np.array([0.135, 0.215, 0.07])
    for s in (-1, 1):
        aba = extrudar([(0.0, 0.0), (s * 0.075, 0.04), (s * 0.085, -0.035)], -0.018, 0.018, chanfro=0.006)
        laco.append((aba.girar(-15, 0, -20).mover(lc), pal.laco))
    laco.append((icosfera(0.022, 1, centro=lc + np.array([0, 0, 0.005])), pal.laco_escuro))
    cabeca = [(cab, pal.pelo), (focinho, pal.focinho), (nariz, pal.nariz)] + orelhas + [(l, pal.listra) for l in listras] + \
        [(b, pal.bigode) for b in bigodes] + [(b, pal.bochecha) for b in bochechas] + (laco if com_laco else [])
    p_boca, n_boca = no_elipsoide(foc_c, np.array([0.105, 0.07, 0.08]), np.array([0, -0.2, 1.0]))
    rosto = rosto_completo(pal, c, r, boca_pos=p_boca + n_boca * 0.003, boca_n=n_boca, raio_olho=(0.044, 0.056))
    return cabeca, rosto


def rabo_mia(pal=MIA, listras=True):
    rabo = curva_bezier(RABO, RABO + np.array([0, -0.06, -0.14]), RABO + np.array([0.02, 0.12, -0.26]), RABO + np.array([0.06, 0.28, -0.2]), n=9)
    rr = [0.03, 0.03, 0.029, 0.028, 0.027, 0.026, 0.024, 0.022, 0.019, 0.0]
    rabo_f = tubo(rabo, rr, gomos=6)
    if listras:
        # anéis mais escuros no rabo
        rabo_f.pintar(lambda cc, n: pal.listra if int(((cc[2] - RABO[2]) * -1 + cc[1]) / 0.07) % 2 == 0 else None)
    return rabo_f


PERFIL_SAIA = [(0.172, 0.44), (0.184, 0.40), (0.198, 0.36), (0.212, 0.322), (0.214, 0.31)]
PERFIL_T = [(0.300, 0.10), (0.318, 0.142), (0.352, 0.166), (0.40, 0.174), (0.45, 0.170), (0.50, 0.160),
            (0.55, 0.143), (0.595, 0.118)]


def bolinhas_vestido(cor_b, so_frente=True):
    bolinhas = []
    for (ang, y, sobre) in ([(a, 0.50, "t") for a in range(-80, 90, 32)] + [(a + 16, 0.555, "t") for a in range(-80, 70, 32)] +
                             [(a, 0.38, "s") for a in range(0, 360, 30)] + [(a + 15, 0.335, "s") for a in range(0, 360, 30)]):
        a = math.radians(ang + 90)
        if sobre == "t":
            rr = np.interp(y, [p[0] for p in PERFIL_T], [p[1] for p in PERFIL_T]) + 0.001
        else:
            ys = [p[1] for p in PERFIL_SAIA][::-1]
            rs = [p[0] for p in PERFIL_SAIA][::-1]
            rr = np.interp(y, ys, rs) + 0.001
        p = np.array([math.cos(a) * rr, y, math.sin(a) * rr * (0.86 if sobre == "t" else 0.88)])
        n = np.array([math.cos(a), 0.3 if sobre == "s" else 0.1, math.sin(a)])
        bolinhas.append((orientar(disco(0.019, 0.003, 8), n).mover(p), cor_b))
    return bolinhas


def vestido(cor_v, cor_escura, cor_gola, cor_bolinha=None):
    """O vestido da Mia (corpinho + saia rodada + golinha), com bolinhas opcionais."""
    corpo_vest = tronco(cor_v, cor_v)
    saia = torno(PERFIL_SAIA, gomos=14, fase=0.5, tampa_baixo=False, tampa_cima=False).escalar((1, 1, 0.88))
    forro = torno([(0.205, 0.31), (0.12, 0.31)], gomos=14, fase=0.5, tampa_baixo=False, tampa_cima=False).escalar((1, 1, 0.88))
    forro.inverter()
    gola = []
    for s in (-1, 1):
        g = extrudar(circulo2d(0.05, 8, escala=(1.0, 0.62)), 0.0, 0.012, chanfro=0.003)
        gola.append((g.girar(-35, s * 18, 0).mover((s * 0.045, 0.603, 0.083)), cor_gola))
    corpo = [(corpo_vest, cor_v), (saia, cor_v), (forro, cor_escura)] + gola
    if cor_bolinha is not None:
        corpo += bolinhas_vestido(cor_bolinha)
    barra = torno([(0.2165, 0.308), (0.2165, 0.324)], gomos=14, fase=0.5, tampa_baixo=False, tampa_cima=False).escalar((1, 1, 0.88))
    corpo.append((barra, cor_gola))
    return corpo


def corpo_mia_padrao():
    """A roupa de sempre da Mia: vestido laranja de bolinhas, meias e sapatinhos."""
    pal = MIA
    juntas = {"corpo": vestido(pal.vestido, pal.vestido_escuro, pal.gola, pal.bolinha), "rabo": [(rabo_mia(), pal.pelo)]}
    for s, nm in ((-1, "e"), (1, "d")):
        juntas["braco_" + nm] = braco_sup(s, pal.vestido, pal.pelo, manga=0.4)
        juntas["antebraco_" + nm] = antebraco(s, pal.pelo, pal.pata)
        juntas["coxa_" + nm] = coxa(s, pal.pelo, pal.pelo, curto=False)
        juntas["canela_" + nm] = canela(s, pal.pelo, pal.meia)
        juntas["pe_" + nm] = tenis(s, pal.sapato, pal.sola, None)
    return juntas


def mia(roupa=""):
    """A Mia com a roupa de sempre ("") ou a roupinha de uma fase (roupas.py)."""
    cabeca, rosto = cabeca_mia(com_laco=(roupa in ("", "parque")))
    extras = ()
    if roupa:
        import roupas
        juntas, extras = roupas.vestir("mia", roupa, cabeca)
    else:
        juntas = corpo_mia_padrao()
        juntas["cabeca"] = cabeca
    return montar("mia" if not roupa else "mia_" + roupa, juntas, rosto, extras)
