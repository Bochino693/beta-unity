# -*- coding: utf-8 -*-
"""
AS ROUPINHAS DAS FASES (Bob e Mia), lisas, com as MESMAS juntas do
bichinho: o jogo só troca a malha de cada junta (animações, rosto e IK valem
igual). Peças extras viram partes próprias:
  * "helice" (filha da cabeça, gira sozinha) — boné de hélice do PARQUE;
  * "capacete" (filha da cabeça, material "vidro") — bolha do ASTRONAUTA.

  parque     — camiseta listrada de arco-íris / vestido turquesa + boné de hélice
  pirata     — camisa listrada de marinheiro, faixa vermelha, chapéu de três
               pontas com caveirinha, botas
  dino       — fantasia de dinossauro: capuz com olhinhos e dentinhos,
               barriga clara, espinhos laranja nas costas e um rabo de dino
  astronauta — macacão branco com painel no peito, mochila, botas e o
               capacete de vidro
"""
import math

import numpy as np

from malha import (Peca, caixa, caixa_arredondada, cor, esfera, extrudar, icosfera, prisma, torno, tubo, juntar,
                   circulo2d, estrela2d, Forma)
from personagens import (BOB, MIA, CABECA_C, CABECA_R, PESCOCO, RABO, OMBRO, COTOVELO, PULSO, MAO, COXA, JOELHO,
                         TORNOZELO, antebraco, braco_sup, canela, coxa, curva_bezier, disco, lado, no_elipsoide, orientar,
                         rabo_bob, rabo_mia, tenis, tronco, vestido, membro)

ARCO_IRIS = [cor("#FF4B5C"), cor("#FF9F2E"), cor("#FFD43B"), cor("#5ED16A"), cor("#2E9BFF"), cor("#8A5CFF")]


def faixas(y0, passo, cores):
    """Pintura por faixas horizontais (alturas a partir de y0)."""
    def f(c, n):
        k = int(math.floor((c[1] - y0) / passo))
        return cores[k % len(cores)]
    return f


def cortes_faixas(y0, passo, ate=0.66):
    """As alturas das bordas das faixas (para o tronco ter um anel em cada uma)."""
    r = []
    y = y0
    while y < ate:
        r.append(y)
        y += passo
    return r


# ------------------------------------------------------------- chapéus e capuzes
def casca_cabeca(escala, filtro, gomos=16, aneis=11, gato=False):
    """Uma casca por cima da cabeça (capuz, gorro): a esfera da cabeça um pouco
    maior, só as faces que `filtro(direção)` aceita, com o lado de dentro."""
    c = CABECA_C
    r = CABECA_R * escala
    f = esfera(1.0, gomos, aneis)
    A, B, C = f.V[f.F[:, 0]], f.V[f.F[:, 1]], f.V[f.F[:, 2]]
    cen = (A + B + C) / 3.0
    ok = np.array([filtro(x) for x in cen])
    f.F = f.F[ok]
    f.V = f.V * r + c
    dentro = f.copia().inverter()
    dentro.escalar(0.985, centro=c)
    return juntar(f, dentro)


def gorro_helice(cores):
    """Boné de hélice: gomos coloridos em cima da cabeça + aba na frente.
    Devolve (formas para a cabeça, Peca da hélice, pivô da hélice)."""
    c = CABECA_C
    gorro = casca_cabeca(1.045, lambda d: d[1] > 0.52, gomos=12, aneis=10)
    gorro.pintar(lambda cc, n: cores[int((math.degrees(math.atan2(cc[2] - c[2], cc[0] - c[0])) + 360) // 90) % 4])
    topo_y = c[1] + CABECA_R[1] * 1.045
    botao = prisma(8, 0.03, 0.035, 0.006, centro=(0, topo_y - 0.012, 0))
    haste = prisma(6, 0.008, 0.05, 0.0, centro=(0, topo_y + 0.015, 0))
    aba = extrudar(circulo2d(0.15, 14, escala=(1.0, 0.62)), -0.006, 0.006, chanfro=0.002)
    aba.girar(-90 + 14, 0, 0).mover((0, c[1] + CABECA_R[1] * 0.62, c[2] + CABECA_R[2] * 0.83))
    formas = [(gorro, cores[0]), (botao, cor("#FFD43B")), (haste, cor("#FFFFFF")), (aba, cores[0])]
    # a hélice: duas pás em X, pivô no eixo
    piv = np.array([0.0, topo_y + 0.06, 0.0])
    pc = Peca("helice")
    for k, cc in enumerate((cor("#FF4B5C"), cor("#2E9BFF"))):
        pa = extrudar([(0.0, -0.016), (0.12, -0.022), (0.13, 0.0), (0.12, 0.022), (0.0, 0.016)], -0.004, 0.004)
        pa.girar(90, 0, 0)          # deitada (no plano XZ)
        pa.girar(8 * (1 if k == 0 else -1), 0, 0)
        for s in (1, -1):
            g = pa.copia()
            if s < 0:
                g.girar(0, 180, 0)
            g.girar(0, 90 * k, 0)
            pc.add(g.mover(piv), cc)
    pc.add(icosfera(0.016, 1, centro=piv), cor("#FFD43B"))
    return formas, pc, piv


def chapeu_pirata(com_pena=False):
    c = CABECA_C
    y0 = c[1] + CABECA_R[1] * 0.80
    copa = prisma(10, 0.19, 0.12, 0.012, raio_topo=0.17, centro=(0, y0, -0.01))
    # aba de três pontas, levantada nas pontas
    tri = []
    for k in range(3):
        a = math.radians(90 + 120 * k)
        tri.append((math.cos(a) * 0.36, math.sin(a) * 0.36))
    # cantos arredondados (2 pontos por canto)
    pts = []
    for k in range(3):
        p0 = np.array(tri[k - 1])
        p1 = np.array(tri[k])
        p2 = np.array(tri[(k + 1) % 3])
        pts.append(tuple(p1 + (p0 - p1) * 0.12))
        pts.append(tuple(p1 + (p2 - p1) * 0.12))
    def levanta(V):
        d = np.sqrt(V[:, 0] ** 2 + V[:, 2] ** 2)
        V = V.copy()
        V[:, 1] += np.clip(d - 0.17, 0, 1) * 0.55
        return V
    aba = extrudar(pts, -0.012, 0.012).girar(90, 0, 0).deformar(levanta)
    aba.girar(0, 180, 0).mover((0, y0 + 0.005, -0.01))
    borda = extrudar(pts, -0.008, 0.008).girar(90, 0, 0).escalar((1.06, 1, 1.06)).deformar(levanta)
    borda.girar(0, 180, 0).mover((0, y0 - 0.006, -0.01))
    formas = [(copa, cor("#2A2430")), (aba, cor("#2A2430")), (borda, cor("#FFC531"))]
    # caveirinha (simpática) na frente da copa
    cav_c = np.array([0, y0 + 0.07, 0.18])
    formas.append((orientar(disco(0.035, 0.006, 10, escala=(1.0, 0.9)), (0, 0.1, 1)).mover(cav_c), cor("#FFFFFF")))
    for s in (-1, 1):
        formas.append((orientar(disco(0.009, 0.004, 8), (0, 0.1, 1)).mover(cav_c + np.array([s * 0.013, 0.004, 0.006])),
                       cor("#2A2430")))
    ossos = extrudar([(-0.05, -0.006), (0.05, -0.006), (0.05, 0.006), (-0.05, 0.006)], 0, 0.005)
    for g in (30, -30):
        formas.append((orientar(ossos.copia().girar(0, 0, g), (0, 0.1, 1)).mover(cav_c + np.array([0, -0.035, -0.004])),
                       cor("#FFFFFF")))
    if com_pena:
        pena = tubo(curva_bezier(np.array([0.12, y0 + 0.08, -0.06]), np.array([0.2, y0 + 0.2, -0.1]),
                                 np.array([0.26, y0 + 0.28, -0.16]), np.array([0.22, y0 + 0.36, -0.24]), n=6),
                    [0.012, 0.03, 0.035, 0.03, 0.02, 0.01, 0.0], gomos=5, achatar=(0.25, 1.0))
        formas.append((pena, cor("#FF4B5C")))
    return formas


def capuz_dino(cor_capuz, cor_espinho, cor_olho_fundo=cor("#FFFFFF")):
    """Capuz de dinossauro: cobre o alto e a nuca, olhinhos em cima, uma
    fileira de dentinhos na testa e espinhos no meio."""
    c = CABECA_C
    capuz = casca_cabeca(1.07, lambda d: (d[1] > 0.30 and d[2] < 0.82) or (d[2] < -0.25 and d[1] > -0.55), gomos=14, aneis=11)
    formas = [(capuz, cor_capuz)]
    # dentinhos na beirada da frente (arco na testa)
    for k in range(-3, 4):
        a = math.radians(k * 14)
        p, n = no_elipsoide(c, CABECA_R * 1.07, np.array([math.sin(a) * 0.7, 0.33, math.cos(a) * 0.62]))
        dente = prisma(3, 0.017, 0.032, 0.0, raio_topo=0.0).girar(180, 0, 0)
        formas.append((dente.mover(p + np.array([0, -0.004, 0])), cor("#FFFFFF")))
    # olhinhos do dino em cima
    for s in (-1, 1):
        p, n = no_elipsoide(c, CABECA_R * 1.07, np.array([s * 0.3, 0.85, 0.35]))
        formas.append((icosfera(0.036, 1, centro=p + n * 0.012), cor_olho_fundo))
        formas.append((icosfera(0.017, 1, centro=p + n * 0.035 + np.array([0, 0.004, 0.012])), cor("#2A1A22")))
    # espinhos do alto da cabeça até a nuca
    for k, ang in enumerate((60, 30, 0, -30, -60)):
        a = math.radians(ang)
        d = np.array([0.0, math.sin(math.radians(90 - abs(ang) * 0.0)) * math.cos(a) + 0.0, -math.sin(a)])
        d = np.array([0.0, math.cos(a), -math.sin(a) - 0.1])
        p, n = no_elipsoide(c, CABECA_R * 1.07, d)
        h = 0.07 if abs(ang) < 40 else 0.055
        esp = prisma(4, 0.036, h, 0.0, raio_topo=0.0, fase=0.5).escalar((0.45, 1, 1))
        R = np.eye(4)
        formas.append((orientar(esp.girar(-90, 0, 0), n, cima=(1, 0, 0)).mover(p - n * 0.01), cor_espinho))
    return formas


def espinhos_costas(cor_espinho, n_esp=4):
    """Espinhos nas costas do corpinho (fileira no meio)."""
    formas = []
    for k in range(n_esp):
        y = 0.58 - k * 0.07
        z = -0.14 + k * 0.004 - (0.01 if k == 0 else 0)
        esp = prisma(4, 0.04, 0.06, 0.0, raio_topo=0.0, fase=0.5).escalar((0.42, 1, 1))
        esp.girar(-90 - 18, 0, 0).mover((0, y, z))
        formas.append((esp, cor_espinho))
    return formas


def rabo_dino(cor_rabo, cor_espinho):
    pts = curva_bezier(RABO + np.array([0, 0.0, 0.03]), RABO + np.array([0, -0.06, -0.12]),
                       RABO + np.array([0, -0.2, -0.26]), RABO + np.array([0, -0.28, -0.42]), n=7)
    f = tubo(pts, [0.075, 0.07, 0.062, 0.05, 0.038, 0.026, 0.014, 0.0], gomos=7)
    formas = [(f, cor_rabo)]
    for k in range(1, 6):
        p = pts[k]
        t = pts[k + 1] - pts[k - 1]
        t /= np.linalg.norm(t)
        cima = np.cross(t, np.array([1.0, 0, 0]))
        if cima[1] < 0:
            cima = -cima
        raio = [0.075, 0.07, 0.062, 0.05, 0.038, 0.026, 0.014][k]
        esp = prisma(4, 0.03 * (1.2 - k * 0.12), 0.05 * (1.2 - k * 0.12), 0.0, raio_topo=0.0, fase=0.5).escalar((0.42, 1, 1))
        formas.append((orientar(esp.girar(-90, 0, 0), cima, cima=(1, 0, 0)).mover(p + cima * (raio * 0.8)), cor_espinho))
    return formas


def capacete_vidro():
    """A bolha do astronauta (material "vidro") e a gola de metal."""
    c = CABECA_C + np.array([0, 0.03, 0])
    bolha = esfera(0.37, 18, 12).mover(c)
    pc = Peca("capacete", "vidro")
    pc.add(bolha, cor("#BFE8FF", 70), suave=60)
    gola = torno([(0.16, 0.6), (0.2, 0.615), (0.205, 0.65), (0.19, 0.675), (0.165, 0.68)], gomos=16, fase=0.5,
                 tampa_baixo=False, tampa_cima=False)
    return pc, [(gola, cor("#AEB8C8"))]


# ------------------------------------------------------------- corpos
def _membros(juntas, cor_manga, cor_pelo, cor_pata, cor_coxa, cor_canela, pe, manga=0.55, coxa_curta=True, meia=None,
             antebraco_cor=None):
    for s, nm in ((-1, "e"), (1, "d")):
        juntas["braco_" + nm] = braco_sup(s, cor_manga, cor_pelo if manga < 1.0 else cor_manga, manga=min(manga, 1.0))
        juntas["antebraco_" + nm] = antebraco(s, antebraco_cor or cor_pelo, cor_pata)
        juntas["coxa_" + nm] = coxa(s, cor_coxa, cor_pelo if coxa_curta else cor_coxa, curto=coxa_curta)
        juntas["canela_" + nm] = canela(s, cor_canela, meia)
        juntas["pe_" + nm] = pe(s)


def bota(cor_bota, cor_sola, cor_detalhe=None, cano=0.07):
    def f(s):
        t = lado(TORNOZELO, s)
        x = t[0]
        partes = [
            (caixa_arredondada(0.118, 0.026, 0.19, 0.012, 1, centro=(x, 0.012, 0.03)), cor_sola),
            (caixa_arredondada(0.112, 0.075, 0.17, 0.03, 2, centro=(x, 0.06, 0.03)), cor_bota),
            (prisma(9, 0.058, cano + 0.06, 0.008, centro=(x, 0.06, 0.0)), cor_bota),
        ]
        if cor_detalhe is not None:
            partes.append((prisma(9, 0.061, 0.018, 0.0, centro=(x, 0.06 + cano + 0.035, 0.0)), cor_detalhe))
        return partes
    return f


def pantufa_dino(cor_pe, cor_garra):
    def f(s):
        t = lado(TORNOZELO, s)
        x = t[0]
        partes = [
            (caixa_arredondada(0.12, 0.024, 0.2, 0.012, 1, centro=(x, 0.012, 0.035)), mais_escuro(cor_pe)),
            (caixa_arredondada(0.118, 0.08, 0.19, 0.035, 2, centro=(x, 0.06, 0.035)), cor_pe),
        ]
        for k in (-1, 0, 1):
            g = prisma(4, 0.014, 0.03, 0.0, raio_topo=0.0).girar(80, 0, 0)
            partes.append((g.mover((x + k * 0.034, 0.03, 0.128)), cor_garra))
        return partes
    return f


def mais_escuro(c, f=0.8):
    return (int(c[0] * f), int(c[1] * f), int(c[2] * f), c[3])


def vestir(nome, roupa, cabeca):
    """(juntas, extras) do bichinho `nome` com a roupinha `roupa`; `cabeca` é a
    lista de formas da cabeça (os chapéus entram nela)."""
    bob = nome == "bob"
    pal = BOB if bob else MIA
    juntas = {}
    extras = []
    cab = list(cabeca)
    if roupa == "parque":
        formas, helice, piv = gorro_helice([cor("#FF4B5C"), cor("#FFD43B"), cor("#2E9BFF"), cor("#5ED16A")])
        cab += formas
        extras.append(("helice", "cabeca", piv, helice))
        if bob:
            t = tronco(ARCO_IRIS[0], cor("#2B4C8C"), cortes=cortes_faixas(0.40, 0.042))
            t.pintar(lambda c, n: faixas(0.40, 0.042, ARCO_IRIS)(c, n) if c[1] >= 0.40 else None)
            corpo = [(t, ARCO_IRIS[0])]
            gola = torno([(0.11, 0.6), (0.122, 0.612), (0.106, 0.634), (0.092, 0.64)], gomos=12, fase=0.5,
                         tampa_baixo=False, tampa_cima=False).escalar((1, 1, 0.9))
            corpo.append((gola, cor("#FFFFFF")))
            juntas["corpo"] = corpo
            juntas["rabo"] = [(rabo_bob(), pal.pelo)]
            _membros(juntas, cor("#FFFFFF"), pal.pelo, pal.pata, cor("#2B4C8C"), pal.pelo,
                     lambda s: tenis(s, cor("#2E9BFF"), cor("#FFFFFF"), cor("#FFD43B")))
        else:
            juntas["corpo"] = vestido(cor("#19B8C9"), cor("#1296A6"), cor("#FFFFFF"), cor("#FFD43B"))
            juntas["rabo"] = [(rabo_mia(), pal.pelo)]
            _membros(juntas, cor("#19B8C9"), pal.pelo, pal.pata, pal.pelo, pal.pelo,
                     lambda s: tenis(s, cor("#FF4B5C"), cor("#FFFFFF"), None), manga=0.4, coxa_curta=False, meia=cor("#FFD43B"))
    elif roupa == "pirata":
        cab += chapeu_pirata(com_pena=not bob)
        listras = faixas(0.30, 0.036, [cor("#1F3A6E"), cor("#FFFFFF")])
        if bob:
            t = tronco(cor("#FFFFFF"), cor("#7A4A22"), cortes=cortes_faixas(0.30, 0.036))
            t.pintar(lambda c, n: listras(c, n) if c[1] >= 0.43 else None)
            corpo = [(t, cor("#FFFFFF"))]
        else:
            corpo = vestido(cor("#E8384F"), cor("#B82A3E"), cor("#FFFFFF"))
            t = tronco(cor("#FFFFFF"), cor("#FFFFFF"), cortes=cortes_faixas(0.30, 0.036))
            t.pintar(lambda c, n: listras(c, n))
            t.escalar((1.012, 1.0, 1.012))
            corpo = [(f, c) for (f, c) in corpo if f is not None]
            corpo.insert(1, (t.copia().deformar(lambda V: V), cor("#FFFFFF")))
        faixa = torno([(0.181, 0.40), (0.184, 0.415), (0.184, 0.44), (0.181, 0.452)], gomos=12, fase=0.5,
                      tampa_baixo=False, tampa_cima=False).escalar((1, 1, 0.88))
        no_f = icosfera(0.03, 1, centro=(0.12, 0.425, 0.13))
        corpo += [(faixa, cor("#E8384F")), (no_f, cor("#C42638"))]
        juntas["corpo"] = corpo
        juntas["rabo"] = [(rabo_bob() if bob else rabo_mia(), pal.pelo)]
        _membros(juntas, cor("#FFFFFF"), pal.pelo, pal.pata, cor("#7A4A22") if bob else pal.pelo, pal.pelo,
                 bota(cor("#2A2430"), cor("#7A4A22"), cor("#FFC531")), manga=0.75, coxa_curta=bob)
        # mangas listradas: pinta a manga longa
        for nm in ("braco_e", "braco_d"):
            juntas[nm] = [(f.copia().pintar(lambda c, n: listras(c, n)) if c == cor("#FFFFFF") else f, c) for (f, c) in juntas[nm]]
    elif roupa == "dino":
        verde = cor("#4CB04C") if bob else cor("#3FB37A")
        barriga = cor("#D8F08A")
        espinho = cor("#FF8A1E") if bob else cor("#B06CFF")
        cab += capuz_dino(verde, espinho)
        t = tronco(verde, verde)
        t.pintar(lambda c, n: barriga if (n[2] > 0.45 and 0.33 < c[1] < 0.6) else None)
        juntas["corpo"] = [(t, verde)] + espinhos_costas(espinho)
        juntas["rabo"] = rabo_dino(verde, espinho)
        _membros(juntas, verde, pal.pelo, pal.pata, verde, verde, pantufa_dino(verde, cor("#FFFFFF")), manga=1.0,
                 coxa_curta=False, antebraco_cor=verde)
    elif roupa == "astronauta":
        branco = cor("#F2F5FA")
        acento = cor("#2E7BFF") if bob else cor("#FF8A1E")
        capacete, gola = capacete_vidro()
        cab += gola
        extras.append(("capacete", "cabeca", np.array(PESCOCO), capacete))
        t = tronco(branco, branco, cortes=(0.40, 0.425))
        t.pintar(lambda c, n: acento if 0.40 <= c[1] < 0.425 else None)
        painel = caixa(0.13, 0.08, 0.02, 0.006, centro=(0, 0.5, 0.15)).girar(-10, 0, 0, centro=(0, 0.5, 0.15))
        corpo = [(t, branco), (painel, cor("#3A4458"))]
        for k, cc in enumerate((cor("#FF4B5C"), cor("#FFD43B"), cor("#5ED16A"))):
            corpo.append((prisma(8, 0.012, 0.012, 0.0, centro=(0, 0, 0)).girar(80, 0, 0).mover((-0.035 + k * 0.035, 0.505, 0.163)), cc))
        mochila = caixa_arredondada(0.24, 0.24, 0.1, 0.03, 2, centro=(0, 0.5, -0.17))
        corpo += [(mochila, branco), (caixa(0.16, 0.03, 0.02, 0.004, centro=(0, 0.55, -0.224)), acento),
                  (caixa(0.16, 0.03, 0.02, 0.004, centro=(0, 0.47, -0.224)), acento)]
        juntas["corpo"] = corpo
        juntas["rabo"] = [(rabo_bob() if bob else rabo_mia(listras=False), branco)]
        _membros(juntas, branco, branco, cor("#DDE3EE"), branco, branco, bota(branco, cor("#8A94A8"), acento, cano=0.09),
                 manga=1.0, coxa_curta=False, antebraco_cor=branco)
    else:
        raise ValueError("roupa desconhecida: " + roupa)
    juntas["cabeca"] = cab
    return juntas, extras
