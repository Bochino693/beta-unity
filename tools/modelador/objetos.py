# -*- coding: utf-8 -*-
"""
PINOS, BOLA e peças pequenas do jogo (edição Unity, facetados).

PINO: 0,48 m (pista infantil ~1,25x a de adulto), duas listras vermelhas no
pescoço e uma CARINHA na barriga. Cada expressão é uma malha inteira (pino +
rosto): trocar de expressão é trocar a malha — um desenho por pino na tela.
Expressões: normal, piscando, medo, tonto, feliz, surpreso, triste, bravo.

BOLA: esfera lisa com UV de mapa-múndi (as texturas bola_bob/bola_mia/
bola_alvo das edições Godot).
"""
import math

import numpy as np

from malha import (Forma, Modelo, Peca, cor, extrudar, circulo2d, fecho_convexo, icosfera, torno, tubo,
                   modelo_simples)
from personagens import arco2d, disco, orientar

ALTURA_PINO = 0.48
K = ALTURA_PINO / 15.0
# perfil do pino (o mesmo das edições Godot): (y, raio) em 15 avos da altura
_YS = [0, 0.35, 1.5, 3.0, 4.5, 6.0, 7.5, 9.0, 10.0, 11.0, 12.0, 13.0, 13.8, 14.5, 14.85, 15.0]
_RS = [0.98, 1.18, 1.78, 2.26, 2.40, 2.31, 1.96, 1.38, 0.98, 0.92, 1.06, 1.26, 1.29, 1.08, 0.62, 0.0]
# o centro de massa (como na edição Godot): 17 cm acima da base
COM_Y = 0.17

BRANCO = cor("#FFFBF4")
VERMELHO = cor("#FF3B4E")
BASE = cor("#E9E2D8")
TINTA = cor("#2A1A22")
BOCHECHA = cor("#FFB0B8")
LINGUA = cor("#FF7A8A")
BRANCO_OLHO = cor("#FFFFFF")


def perfil_pino(sub=4):
    """Perfil alisado (Catmull-Rom) — [(raio, y)] de baixo para cima."""
    pts = []
    n = len(_YS)
    for i in range(n - 1):
        p0 = (_YS[max(i - 1, 0)], _RS[max(i - 1, 0)])
        p1 = (_YS[i], _RS[i])
        p2 = (_YS[i + 1], _RS[i + 1])
        p3 = (_YS[min(i + 2, n - 1)], _RS[min(i + 2, n - 1)])
        for t in np.linspace(0, 1, sub, endpoint=False):
            t2, t3 = t * t, t * t * t
            y = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 +
                       (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3)
            r = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 +
                       (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)
            pts.append((max(r, 0.0) * K * 1.12, y * K))
    pts.append((0.0, ALTURA_PINO))
    return pts


def raio_pino(y):
    p = perfil_pino(8)
    ys = [q[1] for q in p]
    rs = [q[0] for q in p]
    return float(np.interp(y, ys, rs))


def no_pino(dx, y):
    """Ponto da superfície do pino na frente (+Z) deslocado `dx` (arco) e a normal ali."""
    r = raio_pino(y)
    a = dx / max(r, 1e-6)
    # inclinação do perfil (para a normal olhar um pouco para cima/baixo)
    dr = (raio_pino(y + 0.005) - raio_pino(y - 0.005)) / 0.01
    p = np.array([math.sin(a) * r, y, math.cos(a) * r])
    n = np.array([math.sin(a), -dr, math.cos(a)])
    return p, n / np.linalg.norm(n)


LISTRAS = (10.25 * K, 10.85 * K, 11.2 * K, 11.8 * K)


def corpo_pino(gomos=16):
    """O pino liso nas curvas (suave). As listras vermelhas têm anéis
    exatamente nas bordas (risco reto, sem serrilhado)."""
    base = perfil_pino(3)
    ys = [q[1] for q in base]
    rs = [q[0] for q in base]
    extra = list(LISTRAS) + [0.012]
    perfil = sorted(set([(r, y) for r, y in base] + [(float(np.interp(y, ys, rs)), y) for y in extra]), key=lambda q: q[1])
    f = torno(perfil, gomos=gomos, fase=0.5)
    y1, y2, y3, y4 = LISTRAS
    f.pintar(lambda c, n: VERMELHO if (y1 < c[1] < y2) or (y3 < c[1] < y4) else (BASE if c[1] < 0.012 else None))
    return f


def _peca_rosto(pc, forma, dx, y, cor_f, sobe=0.0012):
    p, n = no_pino(dx, y)
    pc.add(orientar(forma, n).mover(p + n * sobe), cor_f)


def rosto_pino(expr):
    """Peças do rosto (sem luz não: o pino é uma malha só, então o rosto é
    desenhado com cores escuras, que leem bem com luz)."""
    pc = Peca("rosto")
    ye = 0.236      # altura dos olhos
    yb = 0.196      # altura da boca
    dxo = 0.026     # meia distância entre os olhos
    def olho_aberto(dx, esc=1.0, pupila=False):
        if pupila:
            # medo/surpreso: olho grande branco com a pupila pequena
            _peca_rosto(pc, disco(1.0, 0.003, 12, escala=(0.017 * esc, 0.021 * esc)), dx, ye, BRANCO_OLHO, 0.0010)
            _peca_rosto(pc, disco(1.0, 0.003, 10, escala=(0.008, 0.010)), dx, ye - 0.002, TINTA, 0.0016)
        else:
            _peca_rosto(pc, disco(1.0, 0.003, 12, escala=(0.0125 * esc, 0.0165 * esc)), dx, ye, TINTA, 0.0010)
            _peca_rosto(pc, disco(0.0048 * esc, 0.003, 8).mover((-0.003, 0.005, 0)), dx, ye, BRANCO_OLHO, 0.0016)

    def arco(dx, para_cima):
        pts = arco2d(0.011, 0.0042, 20, 160, 8) if para_cima else arco2d(0.011, 0.0042, 200, 340, 8)
        _peca_rosto(pc, extrudar(pts, 0, 0.003).mover((0, -0.004 if para_cima else 0.004, 0)), dx, ye, TINTA)

    def sobrancelha(dx, giro, dy=0.024):
        f = extrudar([(-0.011, -0.002), (0.011, -0.002), (0.011, 0.002), (-0.011, 0.002)], 0, 0.003)
        f.girar(0, 0, giro)
        _peca_rosto(pc, f, dx, ye + dy, TINTA)

    def boca(tipo):
        if tipo == "sorriso":
            _peca_rosto(pc, extrudar(arco2d(0.016, 0.0045, 200, 340, 10), 0, 0.003).mover((0, 0.008, 0)), 0.0, yb, TINTA)
        elif tipo == "aberta":
            pts = [(math.cos(math.radians(a)) * 0.017, math.sin(math.radians(a)) * 0.014 + 0.002) for a in np.linspace(180, 360, 12)]
            _peca_rosto(pc, extrudar(pts, 0, 0.003), 0.0, yb, TINTA)
            _peca_rosto(pc, extrudar(circulo2d(0.007, 10, centro=(0, -0.006), escala=(1.0, 0.6)), 0, 0.003), 0.0, yb, LINGUA, 0.0017)
        elif tipo == "triste":
            _peca_rosto(pc, extrudar(arco2d(0.013, 0.0045, 25, 155, 10), 0, 0.003).mover((0, -0.011, 0)), 0.0, yb, TINTA)
        elif tipo == "o":
            _peca_rosto(pc, extrudar(circulo2d(0.0075, 10, escala=(0.85, 1.15)), 0, 0.003), 0.0, yb, TINTA)
        elif tipo == "ondulada":
            pts_c, pts_b = [], []
            for i in range(9):
                x = -0.016 + 0.004 * i
                yy = 0.0035 * math.sin(i * math.pi / 2)
                pts_c.append((x, yy + 0.0022))
                pts_b.append((x, yy - 0.0022))
            _peca_rosto(pc, extrudar(pts_c + pts_b[::-1], 0, 0.003), 0.0, yb, TINTA)
        elif tipo == "brava":
            _peca_rosto(pc, extrudar(arco2d(0.012, 0.0045, 30, 150, 8), 0, 0.003).mover((0, -0.01, 0)), 0.0, yb, TINTA)

    def bochechas():
        for s in (-1, 1):
            _peca_rosto(pc, disco(1.0, 0.002, 10, escala=(0.009, 0.006)), s * 0.036, yb + 0.012, BOCHECHA, 0.0006)

    def x_olho(dx):
        for g in (45, -45):
            f = extrudar([(-0.011, -0.0022), (0.011, -0.0022), (0.011, 0.0022), (-0.011, 0.0022)], 0, 0.003).girar(0, 0, g)
            _peca_rosto(pc, f, dx, ye, TINTA)

    if expr == "normal":
        olho_aberto(-dxo); olho_aberto(dxo); boca("sorriso"); bochechas()
    elif expr == "piscando":
        arco(-dxo, False); arco(dxo, False); boca("sorriso"); bochechas()
    elif expr == "feliz":
        arco(-dxo, True); arco(dxo, True); boca("aberta"); bochechas()
    elif expr == "medo":
        olho_aberto(-dxo, 1.0, True); olho_aberto(dxo, 1.0, True); boca("ondulada")
        sobrancelha(-dxo, -18, 0.03); sobrancelha(dxo, 18, 0.03)
    elif expr == "surpreso":
        olho_aberto(-dxo, 1.25); olho_aberto(dxo, 1.25); boca("o")
        sobrancelha(-dxo, 8, 0.031); sobrancelha(dxo, -8, 0.031)
    elif expr == "tonto":
        x_olho(-dxo); x_olho(dxo); boca("ondulada")
    elif expr == "triste":
        olho_aberto(-dxo, 0.9); olho_aberto(dxo, 0.9); boca("triste")
        sobrancelha(-dxo, -16); sobrancelha(dxo, 16)
    elif expr == "bravo":
        olho_aberto(-dxo, 0.9); olho_aberto(dxo, 0.9); boca("brava")
        sobrancelha(-dxo, 24, 0.02); sobrancelha(dxo, -24, 0.02)
    return pc


EXPRESSOES_PINO = ["normal", "piscando", "medo", "tonto", "feliz", "surpreso", "triste", "bravo"]


def pinos():
    """Um modelo com uma parte por expressão ("pino_normal"...), todas no
    espaço do CENTRO DE MASSA (o pivô do corpo físico), mais "colisao":
    o casco convexo grosseiro (12 lados) para a PhysX."""
    m = Modelo("pinos")
    corpo = corpo_pino()
    for e in EXPRESSOES_PINO:
        pc = Peca("pino_" + e)
        pc.add(corpo, BRANCO, suave=34)
        rosto = rosto_pino(e)
        # o rosto entra na MESMA peça (um desenho por pino)
        pc.pos += rosto.pos
        pc.nrm += rosto.nrm
        pc.cor += rosto.cor
        pc.uv += rosto.uv
        pc.pos = [tuple(np.asarray(v) - np.array([0, COM_Y, 0])) for v in pc.pos]
        m.parte("pino_" + e, None, (0, 0, 0), pc)
    # casco de colisão: o perfil grosseiro girado (12 lados), sem o pescoço fino (o
    # convexo cobre o pescoço — como nas edições Godot)
    perfil = perfil_pino(2)
    pts = []
    for (r, y) in perfil[::2] + [perfil[-2]]:
        for i in range(12):
            a = 2 * math.pi * i / 12
            pts.append((math.cos(a) * r, y - COM_Y, math.sin(a) * r))
    casco = fecho_convexo(np.array(pts))
    pcc = Peca("colisao")
    pcc.add(casco, (255, 255, 255, 255))
    m.parte("colisao", None, (0, 0, 0), pcc)
    return m


# ------------------------------------------------------------- bola
RAIO_BOLA = 0.14


def esfera_uv(raio, gomos=32, aneis=16):
    """Esfera com UV de mapa-múndi (costura duplicada), normais lisas. Devolve Peca."""
    pc = Peca("bola", "textura")
    pc.tem_uv = True
    def p(i, j):
        u = i / gomos
        v = j / aneis
        a = 2 * math.pi * u
        b = math.pi * v
        n = np.array([-math.cos(a) * math.sin(b), math.cos(b), math.sin(a) * math.sin(b)])
        return n * raio, n, (u, 1.0 - v)
    for j in range(aneis):
        for i in range(gomos):
            A, B, C, D = p(i, j), p(i + 1, j), p(i, j + 1), p(i + 1, j + 1)
            for tri in ((A, B, D), (A, D, C)):
                # pólos: pula o triângulo degenerado
                P0, P1, P2 = tri[0][0], tri[1][0], tri[2][0]
                if np.linalg.norm(np.cross(P1 - P0, P2 - P0)) < 1e-12:
                    continue
                # orientação para fora
                nn = np.cross(P1 - P0, P2 - P0)
                if np.dot(nn, P0 + P1 + P2) < 0:
                    tri = (tri[0], tri[2], tri[1])
                for (pos, nrm, uv) in tri:
                    pc.pos.append(pos)
                    pc.nrm.append(nrm)
                    pc.cor.append((255, 255, 255, 255))
                    pc.uv.append(uv)
    return pc


def bola():
    return modelo_simples("bola", esfera_uv(RAIO_BOLA, 32, 16))
