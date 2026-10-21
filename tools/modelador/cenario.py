# -*- coding: utf-8 -*-
"""
O CENÁRIO DAS 4 FASES (edição Unity, FACETADO)
==============================================

Os objetos de decoração de cada fase, no mesmo estilo dos mascotes: faces
retas e quinas vivas, blocos de cor limpos, detalhes como peças à parte
(pintas, listras, janelas, folhas) — nada de massinha derretida. As
composições, medidas e cores vêm das edições Godot (tools/modelos/temas.py e
modelos.py); quem põe e mexe cada um é o Scripts/Temas.cs (e o Mundo.cs, nas
marcas de cada fase).

  * PARQUE ... arvore_verde, arvore_laranja, arvore_escura, pirulito, cupcake,
               donut, presente, bengala, cogumelo, nuvem, balao, arco_iris,
               torre, tenda, carrossel, roda_base, roda, cabine
  * ESPAÇO ... planeta_anel, foguete, ovni, planeta, cristal, pedra_lua,
               estrela_3d
  * MAR ...... coral, coral_roxo, alga, concha, estrela_mar, peixe, agua_viva,
               bau, navio, tartaruga
  * DINOS .... palmeira, samambaia, vulcao, ovo_dino, pedra, flor_gigante e os
               dinossauros: dino (pescoçudo), trex, triceratops, estegossauro,
               ptero, ovo_chocando

CONVENÇÕES
  * metros, coordenadas da Unity (mão esquerda, Y para cima), frente para +Z;
  * origem no centro da BASE, no chão (y = 0) — menos os que voam ou giram
    no próprio centro: planeta_anel, ovni, planeta, estrela_3d, peixe,
    agua_viva, ptero e roda (no eixo). A cabine tem a origem no ponto onde
    pendura (em cima) e fica toda para baixo;
  * TINTA (balao, cabine, planeta, peixe, agua_viva): o corpo é quase
    branco, o jogo multiplica pela cor de cada cópia; os olhos ficam em peças
    "luz" (sem luz e sem tinta);
  * rostos: olhos e bocas em peças "luz" (como os mascotes), filhas da
    cabeça; bochechas e narinas na própria peça;
  * partes que mexem (dinossauros, peixe, tartaruga, ptero, carrossel) são
    modeladas na posição de repouso e gravadas no espaço do próprio pivô.
"""
import math

import numpy as np

from malha import (Forma, Modelo, Peca, caixa, caixa_arredondada, circulo2d, cor, cristal, eixo_para, esfera, estrela2d,
                   extrudar, fecho_convexo, icosfera, mais_escura, modelo_simples, pedra, prisma, retangulo2d, rot, torno,
                   triangular, tubo)

PI = math.pi

# cores da casa (as mesmas dos rostos das edições Godot)
OLHO = cor("#2B1F3F")
BRILHO = cor("#FFFFFF")
BOCA = cor("#5A2330")
BOCHECHA = cor("#FF9BB0")
LINGUA = cor("#FF7A8A")
UNHA = cor("#FFF3DC")


class Paleta(dict):
    """Dicionário de cores que também se lê como atributo (pal.pele)."""

    def __getattr__(self, k):
        return self[k]


# =================================================================== utilidades


def _norm(v):
    """Vetor unitário."""
    v = np.asarray(v, float)
    return v / max(float(np.linalg.norm(v)), 1e-12)


def _area2(poly):
    """Duas vezes a área com sinal do contorno 2D (> 0: anti-horário)."""
    s = 0.0
    for i in range(len(poly)):
        x0, y0 = poly[i]
        x1, y1 = poly[(i + 1) % len(poly)]
        s += x0 * y1 - x1 * y0
    return s


def _convexo(poly):
    """O contorno (anti-horário) é convexo?"""
    n = len(poly)
    for i in range(n):
        a, b, c = np.asarray(poly[i - 1]), np.asarray(poly[i]), np.asarray(poly[(i + 1) % n])
        if (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0]) < -1e-12:
            return False
    return True


def base_tangente(n, cima=(0, 1, 0), giro=0.0):
    """Eixos (x, y, n) de um adesivo virado para n, com o y o mais para `cima`
    possível; giro em graus em volta de n. (x, y, n) é destro: o contorno
    anti-horário em (x, y) fica de frente para quem olha de n."""
    n = _norm(n)
    up = np.asarray(cima, float)
    x = np.cross(up, n)
    if np.linalg.norm(x) < 1e-6:
        x = np.cross((0.0, 0.0, 1.0) if abs(n[2]) < 0.9 else (1.0, 0.0, 0.0), n)
    x = _norm(x)
    y = np.cross(n, x)
    if giro:
        a = math.radians(giro)
        x, y = x * math.cos(a) + y * math.sin(a), -x * math.sin(a) + y * math.cos(a)
    return x, y, n


def orientar(forma, normal, giro=0.0, cima=(0, 1, 0)):
    """Gira uma forma feita virada para +Z (contorno no plano XY) para olhar
    na direção `normal` (o +Y dela o mais para `cima` possível)."""
    x, y, n = base_tangente(normal, cima, giro)
    R = np.stack([x, y, n], axis=1)
    forma.V = forma.V @ R.T
    return forma


def placa(pts, p, n, esp, giro=0.0, cima=(0, 1, 0), chanfro=0.0, z0=0.0):
    """Contorno 2D puxado em espessura `esp` (de z0), virado para n e posto em p."""
    f = extrudar(pts, z0, z0 + esp, chanfro)
    return orientar(f, n, giro, cima).mover(p)


def arco2d(raio, espessura, a0, a1, n=8):
    """Faixa em arco (graus): sorrisos, olhos fechados, sobrancelhas."""
    fora, dentro = [], []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        fora.append((math.cos(a) * (raio + espessura / 2), math.sin(a) * (raio + espessura / 2)))
        dentro.append((math.cos(a) * (raio - espessura / 2), math.sin(a) * (raio - espessura / 2)))
    return fora + dentro[::-1]


def bezier(p0, p1, p2, p3, n=10):
    """n + 1 pontos da curva de Bézier cúbica (pescoços, rabos, caules)."""
    P = []
    for i in range(n + 1):
        t = i / n
        P.append((1 - t) ** 3 * np.asarray(p0, float) + 3 * (1 - t) ** 2 * t * np.asarray(p1, float) +
                 3 * (1 - t) * t * t * np.asarray(p2, float) + t ** 3 * np.asarray(p3, float))
    return np.array(P)


def parametro_linha(P, Q):
    """Para cada ponto Q, o parâmetro (0..1, pelo comprimento) do ponto mais
    perto na polilinha P — para pintar faixas em tubos."""
    P = np.asarray(P, float)
    seg = P[1:] - P[:-1]
    L = np.linalg.norm(seg, axis=1)
    acum = np.concatenate([[0.0], np.cumsum(L)])
    tot = acum[-1]
    Q = np.atleast_2d(Q)
    melhor = np.full(len(Q), np.inf)
    t = np.zeros(len(Q))
    for i in range(len(seg)):
        h = np.clip(((Q - P[i]) @ seg[i]) / max(L[i] ** 2, 1e-12), 0, 1)
        d = np.linalg.norm(Q - (P[i] + h[:, None] * seg[i]), axis=1)
        m = d < melhor
        melhor[m] = d[m]
        t[m] = (acum[i] + h[m] * L[i]) / tot
    return t


# ------------------------------------------------------------------ elipsoide (malha + superfície para adesivos)


class Elip:
    """Elipsoide (centro c, raios r, rotação R local -> mundo). Dá a malha
    facetada (esfera UV) e a superfície "ideal" onde se colam os adesivos
    (olhos, pintas, bocas): os vértices da malha estão nela e as faces ficam
    um pouco para dentro, então um adesivo nunca afunda nas facetas."""

    def __init__(self, c, r, R=None):
        self.c = np.asarray(c, float)
        self.r = np.asarray(r, float)
        self.R = np.eye(3) if R is None else np.asarray(R, float)

    def malha(self, gomos=12, aneis=8, polo="y"):
        f = esfera(1.0, gomos, aneis)
        if polo == "z":
            f.girar(90, 0, 0)
        elif polo == "x":
            f.girar(0, 0, 90)
        f.V = (f.V * self.r) @ self.R.T + self.c
        return f

    def local(self, p):
        return (np.asarray(p, float) - self.c) @ self.R

    def normal(self, p):
        q = self.local(p) / (self.r ** 2)
        return _norm(self.R @ q)

    def superficie(self, d):
        """Ponto e normal da superfície na direção d (a partir do centro, no mundo)."""
        dl = _norm(np.asarray(d, float)) @ self.R
        t = 1.0 / math.sqrt(float(np.sum((dl / self.r) ** 2)))
        p = self.c + self.R @ (dl * t)
        return p, self.normal(p)

    def raio(self, o, d):
        """Maior t com o + d t na superfície (o raio que vem de fora); None se erra."""
        ol = self.local(o) / self.r
        dl = (np.asarray(d, float) @ self.R) / self.r
        A = float(dl @ dl)
        B = 2.0 * float(ol @ dl)
        C = float(ol @ ol) - 1.0
        disc = B * B - 4 * A * C
        if disc < 0:
            return None
        return (-B + math.sqrt(disc)) / (2 * A)

class SupMalha:
    """Superfície de uma malha qualquer (casco convexo, torno...) para colar
    adesivos: os raios batem nas próprias facetas."""

    def __init__(self, forma, centro=None):
        V, F = forma.V, forma.F
        self.A = V[F[:, 0]]
        self.E1 = V[F[:, 1]] - self.A
        self.E2 = V[F[:, 2]] - self.A
        N = np.cross(self.E1, self.E2)
        self.N = N / np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)
        self.c = V.mean(axis=0) if centro is None else np.asarray(centro, float)
        self._n = np.array([0.0, 1.0, 0.0])

    def _batidas(self, o, d):
        o = np.asarray(o, float)
        d = np.asarray(d, float)
        p = np.cross(d, self.E2)
        det = np.einsum("ij,ij->i", self.E1, p)
        ok = np.abs(det) > 1e-12
        inv = np.where(ok, 1.0 / np.where(ok, det, 1.0), 0.0)
        tv = o - self.A
        u = np.einsum("ij,ij->i", tv, p) * inv
        q = np.cross(tv, self.E1)
        v = (q @ d) * inv
        t = np.einsum("ij,ij->i", self.E2, q) * inv
        bate = ok & (u >= -1e-9) & (v >= -1e-9) & (u + v <= 1 + 1e-9)
        return t, bate

    def superficie(self, d):
        d = _norm(d)
        t, bate = self._batidas(self.c, d)
        t = np.where(bate, t, -np.inf)
        i = int(np.argmax(t))
        self._n = self.N[i]
        return self.c + d * t[i], self.N[i]

    def raio(self, o, d):
        t, bate = self._batidas(o, d)
        t = np.where(bate, t, -np.inf)
        i = int(np.argmax(t))
        if not np.isfinite(t[i]):
            return None
        self._n = self.N[i]
        return float(t[i])

    def normal(self, p):
        """A normal da faceta que o último raio acertou (o adesivo pergunta logo depois)."""
        return self._n


def adesivo(sup, pts, d, esp=0.005, afundar=0.012, giro=0.0, cima=(0, 1, 0), fundo=False, leque=None, paredes=True):
    """Um contorno 2D (x, y no plano tangente) colado na superfície `sup` na
    direção d: cada vértice é projetado na superfície e o adesivo vira uma
    lâmina de `afundar` para dentro até `esp` para fora (sem fresta, sem
    brigar com a face de baixo). Contorno convexo: leque a partir do centro
    (acompanha a curva por dentro também)."""
    p, n0 = sup.superficie(d)
    x, y, n = base_tangente(n0, cima, giro)
    pts = [(float(u), float(v)) for (u, v) in pts]
    if _area2(pts) < 0:
        pts = pts[::-1]
    usar_leque = _convexo(pts) if leque is None else leque

    def proj(u, v):
        q = p + x * u + y * v
        t = sup.raio(q, n)
        s = q + n * (t if t is not None else 0.0)
        ns = sup.normal(s)
        return s + ns * esp, s - ns * afundar

    m = len(pts)
    cima_v, baixo_v = [], []
    for (u, v) in pts:
        a, b = proj(u, v)
        cima_v.append(a)
        baixo_v.append(b)
    V = cima_v + baixo_v
    F = []
    if usar_leque:
        cu = sum(q[0] for q in pts) / m
        cv = sum(q[1] for q in pts) / m
        a, b = proj(cu, cv)
        ic = len(V)
        V += [a, b]
        for i in range(m):
            F.append((ic, i, (i + 1) % m))
            if fundo:
                F.append((ic + 1, m + (i + 1) % m, m + i))
    else:
        for (i0, i1, i2) in triangular(pts):
            F.append((i0, i1, i2))
            if fundo:
                F.append((m + i0, m + i2, m + i1))
    if paredes:
        for i in range(m):
            j = (i + 1) % m
            F.append((m + i, m + j, j))
            F.append((m + i, j, i))
    return Forma(np.array(V), F)


# ------------------------------------------------------------------ montagem


def _peca(nome, itens, material=""):
    """Peca com os itens [(forma, cor[, suave])]."""
    p = Peca(nome, material)
    for it in itens:
        p.add(it[0], it[1], it[2] if len(it) > 2 else 0.0)
    return p


def _local(peca, pivo):
    """Leva a peça (feita na posição de repouso no mundo) para o espaço do pivô."""
    if peca is None or not peca.pos:
        return peca
    P = np.array(peca.pos) - np.asarray(pivo, float)
    peca.pos = [tuple(v) for v in P]
    return peca


def montar(nome, partes):
    """partes: [(nome, pai, pivô no mundo, itens [(forma, cor[, suave])], material)],
    pais antes dos filhos. Cada parte é gravada no espaço do próprio pivô."""
    m = Modelo(nome)
    piv = {}
    for (pn, pai, pivo, itens, mat) in partes:
        pivo = np.asarray(pivo, float)
        piv[pn] = pivo
        pos = pivo - (piv[pai] if pai is not None else np.zeros(3))
        peca = _peca(pn, itens, mat) if itens else None
        m.parte(pn, pai, pos, _local(peca, pivo))
    return m


def simples(nome, itens, luz=None):
    """Modelo de uma peça só (e, se houver, a peça "luz" — rosto, lâmpadas — filha dela)."""
    if not luz:
        return modelo_simples(nome, _peca(nome, itens))
    return montar(nome, [(nome, None, (0, 0, 0), itens, ""), ("luz", nome, (0, 0, 0), luz, "luz")])


# =================================================================== rostos


def olho(sup, d, raio, s, cor_iris=None, giro=0.0, cima=(0, 1, 0), esp=0.004, simples_=False):
    """Olho de desenho como o dos mascotes: oval escuro, íris embaixo e dois
    brilhos (o grande do lado de dentro, em cima). s = -1 esquerda (-X), +1
    direita. Para peças "luz". simples_: sem as paredinhas e com menos lados
    (bichinhos pequenos e numerosos). Devolve [(forma, cor)]."""
    rx, ry = raio
    k = max(rx, ry)
    par = not simples_
    n0, n1, n2 = (14, 10, 8) if par else (11, 7, 5)
    itens = [(adesivo(sup, circulo2d(1.0, n0, escala=(rx, ry)), d, esp, 0.01, giro, cima, paredes=par), OLHO)]
    if cor_iris is not None:
        itens.append((adesivo(sup, circulo2d(1.0, 12, centro=(0, -ry * 0.3), escala=(rx * 0.62, ry * 0.48)), d,
                              esp + 0.0025 * k / 0.05, 0.01, giro, cima, paredes=par), cor_iris))
    # o brilho grande do lado de dentro (para o nariz), como nos mascotes
    b1 = circulo2d(rx * 0.4, n1, centro=(-s * rx * 0.28 + rx * 0.04, ry * 0.36))
    b2 = circulo2d(rx * 0.17, n2, centro=(s * rx * 0.32, -ry * 0.14))
    itens.append((adesivo(sup, b1, d, esp + 0.005 * k / 0.05, 0.01, giro, cima, paredes=par), BRILHO))
    itens.append((adesivo(sup, b2, d, esp + 0.005 * k / 0.05, 0.01, giro, cima, paredes=par), BRILHO))
    return itens


def sorriso(sup, d, largura, grossura, abertura=130.0, esp=0.004, cima=(0, 1, 0), cor_=BOCA, giro=0.0):
    """Sorriso em arco (∪) colado na superfície."""
    a0 = 270 - abertura / 2
    pts = arco2d(largura / 2, grossura, a0, a0 + abertura, 10)
    pts = [(u, v + largura / 2 * 0.75) for (u, v) in pts]
    return adesivo(sup, pts, d, esp, 0.01, giro, cima, leque=False), cor_


def oval(sup, d, rx, ry, cor_, esp=0.004, giro=0.0, lados=10, cima=(0, 1, 0), afundar=0.012):
    """Pinta/bochecha/narina oval colada na superfície. Devolve (forma, cor)."""
    return adesivo(sup, circulo2d(1.0, lados, escala=(rx, ry)), d, esp, afundar, giro, cima), cor_


# =================================================================== SELVA DOS DINOS

DINO = Paleta(pele=cor("#8FD14F"), pele_escura=cor("#79BE3C"), barriga=cor("#E8F6AE"), pinta=cor("#5FB33C"),
              unha=UNHA, iris=cor("#3F6B22"), nariz=cor("#3B5A22"))


def pe_dino(c, r, h, pele, unha, unhas=3, frente=(0, 0, 1)):
    """Pé de coluna (elefante): base um pouco mais larga e unhas na frente."""
    c = np.asarray(c, float)
    itens = [(prisma(9, r, h, chanfro=min(0.05, r * 0.2), raio_topo=r * 0.9, fase=0.5).mover(c), pele)]
    f = _norm(frente)
    lado = np.cross((0, 1, 0), f)
    w = r * 0.24
    d_ = [(-w, 0.0), (w, 0.0), (w * 0.95, h * 0.32), (w * 0.55, h * 0.5), (0.0, h * 0.56), (-w * 0.55, h * 0.5),
          (-w * 0.95, h * 0.32)]
    for k in range(unhas):
        a = (k - (unhas - 1) / 2) * 0.62
        dirn = f * math.cos(a) + lado * math.sin(a)
        un = orientar(extrudar(d_, -r * 0.14, r * 0.14), dirn)
        un.mover(c + dirn * (r * 0.9) + np.array([0, 0.004, 0]))
        itens.append((un, unha))
    return itens


def dino():
    """O pescoçudo (braquiossauro) ~3,5 m: corpo de feijão inclinado (a
    frente mais alta), patas curtas de coluna, pescoço que sobe para a
    frente, cabeção redonda de olhos grandes e um rabo que enrola de leve.
    Partes: corpo > pescoco > cabeca (> rosto), corpo > rabo."""
    pal = DINO
    corpo_e = Elip((0, 1.2, -0.25), (0.76, 0.68, 1.04), rot(-9, 0, 0))
    corpo = corpo_e.malha(16, 10, polo="z")
    corpo.pintar(lambda c, n: pal.barriga if (n[1] < -0.4 or (n[2] > 0.6 and n[1] < 0.15)) else None)
    itens_corpo = [(corpo, pal.pele)]
    # patas curtas e grossas (as da frente um pouco mais altas)
    for s in (-1, 1):
        for (z, topo_y, r) in ((0.4, 1.0, 0.25), (-0.88, 0.86, 0.265)):
            x = s * 0.42
            coxa = Elip((x * 1.02, topo_y, z - 0.04), (0.27, 0.36, 0.34))
            itens_corpo.append((coxa.malha(10, 7), pal.pele))
            itens_corpo.append((tubo([(x, 0.14, z), (x, topo_y, z)], [r, r * 1.04], gomos=10), pal.pele))
            itens_corpo += pe_dino((x, 0.0, z + 0.03), r * 1.14, 0.2, pal.pele_escura, pal.unha)
    # pintas nas costas (adesivos na superfície do corpo)
    for (d, rx, ry, g) in (((0.45, 0.8, 0.25), 0.18, 0.14, 20), ((-0.5, 0.75, -0.2), 0.2, 0.15, -15),
                           ((0.2, 0.9, -0.55), 0.16, 0.12, 35), ((-0.15, 0.95, 0.35), 0.13, 0.1, 0),
                           ((0.62, 0.55, -0.6), 0.14, 0.11, -25), ((-0.7, 0.5, 0.3), 0.12, 0.09, 10),
                           ((-0.45, 0.8, -0.75), 0.13, 0.1, 30), ((0.75, 0.45, 0.25), 0.11, 0.08, 0)):
        itens_corpo.append(oval(corpo_e, d, rx, ry, pal.pinta, esp=0.012, giro=g, lados=9))
    # pescoço
    pts = bezier((0, 1.3, 0.45), (0, 1.95, 0.85), (0, 2.45, 1.02), (0, 2.88, 1.12), n=8)
    raios = np.linspace(0.42, 0.25, len(pts))
    pescoco = tubo(pts, raios, gomos=10)
    pescoco.pintar(lambda c, n: pal.barriga if float(np.dot(n, _norm((0, -0.3, 1)))) > 0.55 else None)
    itens_pesc = [(pescoco, pal.pele)]
    for (k, rr) in ((3, 0.09), (5, 0.075)):
        e = Elip(pts[k], (raios[k], raios[k], raios[k]))
        itens_pesc.append(oval(e, (0.5, 0.35, -0.75), rr, rr * 0.75, pal.pinta, esp=0.01, lados=8))
        itens_pesc.append(oval(e, (-0.55, 0.3, -0.7), rr * 0.8, rr * 0.6, pal.pinta, esp=0.01, lados=8))
    PIV_PESC = np.array([0.0, 1.5, 0.55])
    # cabeça (pivô no alto do pescoço): crânio redondo + focinho largo embaixo
    PIV_CAB = np.array([0.0, 2.86, 1.12])
    cranio = Elip((0, 3.1, 1.2), (0.4, 0.37, 0.4))
    focinho = Elip((0, 2.94, 1.42), (0.31, 0.22, 0.27))
    cab = cranio.malha(16, 10, polo="z")
    foc = focinho.malha(14, 8, polo="z")
    foc.pintar(lambda c, n: pal.barriga if n[1] < -0.35 else None)
    cab.pintar(lambda c, n: pal.barriga if n[1] < -0.65 else None)
    itens_cab = [(cab, pal.pele), (foc, pal.pele)]
    # calombo no alto da testa (o "nariz" do braquiossauro)
    crista = Elip((0, 3.38, 1.14), (0.15, 0.1, 0.26), rot(-18, 0, 0))
    itens_cab.append((crista.malha(10, 6, polo="z"), pal.pele_escura))
    itens_cab.append((tubo([PIV_CAB + (0, -0.3, -0.12), PIV_CAB + (0, 0.15, 0.05)], [0.25, 0.25], gomos=10), pal.pele))
    for s in (-1, 1):
        itens_cab.append(oval(focinho, (s * 0.32, 0.8, 0.55), 0.034, 0.024, pal.nariz, esp=0.004, giro=-s * 25, lados=8))
        itens_cab.append(oval(cranio, (s * 0.66, -0.4, 0.66), 0.075, 0.05, BOCHECHA, esp=0.004, lados=10))
    rosto = []
    for s in (-1, 1):
        rosto += olho(cranio, (s * 0.43, 0.22, 0.88), (0.085, 0.105), s, cor_iris=pal.iris)
    rosto.append(sorriso(focinho, (0, -0.02, 1.0), 0.22, 0.026, abertura=125))
    # rabo
    PIV_RABO = np.array([0.0, 1.2, -1.05])
    pts_r = bezier((0, 1.32, -0.95), (0, 1.15, -1.62), (0.1, 0.62, -1.95), (0.5, 0.42, -2.38), n=9)
    rr = 0.4 * (1 - np.linspace(0, 1, len(pts_r))) ** 1.35 + 0.045
    rabo = tubo(pts_r, rr, gomos=10)
    rabo.pintar(lambda c, n: pal.barriga if n[1] < -0.55 else None)
    itens_rabo = [(rabo, pal.pele)]
    for k in (2, 4, 6):
        e = Elip(pts_r[k], (rr[k], rr[k], rr[k]))
        itens_rabo.append(oval(e, (0, 1, 0.15), rr[k] * 0.55, rr[k] * 0.4, pal.pinta, esp=0.008, cima=(0, 0, 1), lados=8))
    return montar("dino", [
        ("corpo", None, (0, 0, 0), itens_corpo, ""),
        ("pescoco", "corpo", PIV_PESC, itens_pesc, ""),
        ("cabeca", "pescoco", PIV_CAB, itens_cab, ""),
        ("rosto", "cabeca", PIV_CAB, rosto, "luz"),
        ("rabo", "corpo", PIV_RABO, itens_rabo, ""),
    ])


def secao(z, meia, yb, yt, rb=0.05, rt=0.1, segs=2, x0=0.0):
    """Anel de uma seção (retângulo de cantos redondos, raio de baixo rb e de
    cima rt) no plano z — para cascos convexos feitos de seções."""
    pts = []
    for (cx, cy, r, a0) in ((meia - rb, yb + rb, rb, -90), (meia - rt, yt - rt, rt, 0), (-meia + rt, yt - rt, rt, 90),
                            (-meia + rb, yb + rb, rb, 180)):
        for i in range(segs + 1):
            a = math.radians(a0 + 90.0 * i / segs)
            pts.append((x0 + cx + math.cos(a) * r, cy + math.sin(a) * r, z))
    return pts


def casco(secoes):
    """Casco convexo de várias seções (cabeça do T-rex, bicos)."""
    P = []
    for sc in secoes:
        P += sc
    return fecho_convexo(np.unique(np.round(np.array(P), 7), axis=0))


def dente(base, ponta, r):
    """Dentinho redondo (cone rombudo de 5 lados) de `base` até `ponta`."""
    return tubo([base, ponta], [r, r * 0.45], gomos=5)


def garra(base, dirn, comp, r):
    """Garrinha (cone de 5 lados) saindo de `base` na direção dirn."""
    dirn = _norm(dirn)
    return tubo([np.asarray(base, float), np.asarray(base, float) + dirn * comp], [r, 0.0], gomos=5)


TREX = Paleta(pele=cor("#FF7657"), escura=cor("#E2574A"), barriga=cor("#FFE3B3"), boca=cor("#A3324A"),
              lingua=cor("#FF93A6"), dente=cor("#FFFFFF"), unha=UNHA, iris=cor("#7A3A1E"), nariz=cor("#8A2E2A"))


def trex():
    """T-rex amigo ~2,25 m: corpo de pera inclinado, coxonas, pés de três
    garras, cabeção com olhos em calombos, sorriso de dentinhos redondos,
    bracinhos e rabo listrado. Partes: corpo > cabeca (> mandibula, rosto),
    corpo > rabo, braco_e, braco_d."""
    pal = TREX
    corpo_e = Elip((0, 1.12, -0.12), (0.5, 0.64, 0.52), rot(28, 0, 0))
    corpo = corpo_e.malha(14, 11)
    corpo.pintar(lambda c, n: pal.barriga if float(np.dot(n, _norm((0, -0.35, 1)))) > 0.45 else None)
    itens_corpo = [(corpo, pal.pele)]
    # listras nas costas
    for (d, g, L) in (((0, 0.75, -0.66), 0, 0.3), ((0, 0.3, -0.95), 0, 0.34), ((0, 1.0, -0.15), 0, 0.26)):
        pts = [(-L, 0.0), (-L * 0.4, 0.045), (L * 0.4, 0.045), (L, 0.0), (L * 0.4, -0.045), (-L * 0.4, -0.045)]
        itens_corpo.append((adesivo(corpo_e, pts, d, 0.01, 0.012, g, cima=(0, 0, 1)), pal.escura))
    # pernas: coxona, canela e pé de três garras
    for s in (-1, 1):
        x = s * 0.34
        coxa = Elip((x, 0.8, -0.16), (0.24, 0.36, 0.33), rot(-12, 0, 0))
        itens_corpo.append((coxa.malha(12, 8), pal.pele))
        itens_corpo.append((tubo([(x * 1.04, 0.62, -0.04), (x * 1.06, 0.14, -0.1)], [0.15, 0.125], gomos=8), pal.pele))
        pe = caixa_arredondada(0.3, 0.17, 0.44, 0.07, 2, centro=(x * 1.06, 0.085, 0.02))
        itens_corpo.append((pe, pal.escura))
        for k in (-1, 0, 1):
            b = np.array([x * 1.06 + k * 0.095, 0.06, 0.22 - abs(k) * 0.03])
            itens_corpo.append((garra(b, (k * 0.25, -0.15, 1), 0.09, 0.04), pal.unha))
    # cabeça: casco de seções (o céu da boca é reto), calombos dos olhos e o pescoço
    PIV_CAB = np.array([0.0, 1.58, 0.16])
    sup = casco([secao(0.0, 0.22, 1.74, 2.05, 0.06, 0.14), secao(0.16, 0.29, 1.72, 2.19, 0.06, 0.16),
                 secao(0.42, 0.3, 1.72, 2.2, 0.06, 0.16), secao(0.7, 0.255, 1.72, 2.07, 0.06, 0.13),
                 secao(0.9, 0.215, 1.72, 1.99, 0.07, 0.11), secao(0.98, 0.15, 1.73, 1.93, 0.06, 0.08)])
    sup.pintar(lambda c, n: pal.boca if n[1] < -0.9 else None)
    sup_s = SupMalha(sup, (0, 1.9, 0.5))
    itens_cab = [(sup, pal.pele)]
    itens_cab.append((tubo([(0, 1.36, -0.02), (0, 1.86, 0.24)], [0.27, 0.27], gomos=10), pal.pele))
    olhos = []
    for s in (-1, 1):
        bossa = Elip((s * 0.19, 2.1, 0.46), (0.125, 0.12, 0.115))
        olhos.append(bossa)
        itens_cab.append((bossa.malha(10, 7), pal.pele))
        itens_cab.append((adesivo(sup_s, circulo2d(1.0, 8, escala=(0.03, 0.02)), (s * 0.35, 0.25, 1.0), 0.004, 0.01,
                                  -s * 20), pal.nariz))
        itens_cab.append((adesivo(sup_s, circulo2d(1.0, 10, escala=(0.07, 0.045)), (s * 1.0, -0.35, 0.35), 0.004, 0.01),
                          BOCHECHA))
    # dentinhos de cima, na beirada do céu da boca (por fora da mandíbula)
    beira = [(0.16, 0.225), (0.42, 0.235), (0.7, 0.19), (0.9, 0.14), (0.975, 0.075)]
    zs = [q[0] for q in beira]
    xs = [q[1] for q in beira]
    for (z, sx) in ((0.975, 0.0), (0.955, 1), (0.9, 1), (0.8, 1)):
        x = float(np.interp(z, zs, xs)) * sx
        for sg in ((-1, 1) if sx else (1,)):
            b = np.array([sg * x * 0.92, 1.735, z - 0.01])
            itens_cab.append((dente(b, b + (0, -0.058, 0.006), 0.03), pal.dente))
    rosto = []
    for s, bossa in zip((-1, 1), olhos):
        rosto += olho(bossa, (s * 0.5, 0.2, 0.85), (0.075, 0.09), s, cor_iris=pal.iris)
    # cantinhos do sorriso
    for s in (-1, 1):
        pts = arco2d(0.035, 0.014, 200 if s < 0 else 280, 260 if s < 0 else 340, 6)
        rosto.append((adesivo(sup_s, pts, (s * 1.0, -0.55, 0.12), 0.004, 0.01, leque=False), BOCA))
    # mandíbula (pivô na dobradiça, atrás da boca)
    PIV_MAND = np.array([0.0, 1.7, 0.16])
    mand = casco([secao(0.12, 0.215, 1.56, 1.715, 0.07, 0.03), secao(0.4, 0.215, 1.54, 1.715, 0.07, 0.03),
                  secao(0.7, 0.17, 1.56, 1.715, 0.07, 0.03), secao(0.86, 0.12, 1.59, 1.715, 0.06, 0.03),
                  secao(0.92, 0.07, 1.62, 1.715, 0.04, 0.03)])
    mand.pintar(lambda c, n: pal.boca if n[1] > 0.9 else (pal.barriga if n[1] < -0.6 else None))
    itens_mand = [(mand, pal.pele)]
    lingua = Elip((0, 1.70, 0.52), (0.12, 0.022, 0.2))
    itens_mand.append((lingua.malha(10, 4), pal.lingua))
    for k in (-1.5, -0.5, 0.5, 1.5):
        a = k / 1.5
        b = np.array([0.085 * a, 1.70, 0.86 - 0.06 * a * a])
        itens_mand.append((dente(b, b + (0, 0.045, 0), 0.02), pal.dente))
    # bracinhos (pivô no ombro)
    bracos = {}
    for s, nm in ((-1, "braco_e"), (1, "braco_d")):
        om = np.array([s * 0.3, 1.34, 0.24])
        co = np.array([s * 0.36, 1.18, 0.4])
        pu = np.array([s * 0.34, 1.17, 0.54])
        it = [(icosfera(0.085, 1, centro=om), pal.pele), (tubo([om, co], [0.075, 0.065], gomos=7), pal.pele),
              (tubo([co, pu], [0.062, 0.055], gomos=7), pal.pele), (icosfera(0.065, 1, centro=pu), pal.pele)]
        for k in (-1, 1):
            it.append((garra(pu + (k * 0.025, -0.01, 0.04), (k * 0.2, -0.3, 1), 0.06, 0.022), pal.unha))
        bracos[nm] = (om, it)
    # rabo listrado
    PIV_RABO = np.array([0.0, 0.92, -0.48])
    pts_r = bezier((0, 1.0, -0.4), (0, 0.78, -0.98), (0.04, 0.5, -1.38), (0.26, 0.3, -1.74), n=9)
    rr = 0.41 * (1 - np.linspace(0, 1, len(pts_r))) ** 1.1 + 0.04
    rabo = tubo(pts_r, rr, gomos=10)

    def listra_rabo(c, n, P=pts_r):
        t = parametro_linha(P, c)[0]
        if n[1] > -0.1 and 0.14 < t < 0.9 and int(t * 9) % 2 == 1:
            return pal.escura
        return pal.barriga if n[1] < -0.6 else None
    rabo.pintar(listra_rabo)
    return montar("trex", [
        ("corpo", None, (0, 0, 0), itens_corpo, ""),
        ("cabeca", "corpo", PIV_CAB, itens_cab, ""),
        ("mandibula", "cabeca", PIV_MAND, itens_mand, ""),
        ("rosto", "cabeca", PIV_CAB, rosto, "luz"),
        ("rabo", "corpo", PIV_RABO, [(rabo, pal.pele)], ""),
        ("braco_e", "corpo", bracos["braco_e"][0], bracos["braco_e"][1], ""),
        ("braco_d", "corpo", bracos["braco_d"][0], bracos["braco_d"][1], ""),
    ])


def coxa_e_perna(x, z, topo_y, r, pal_pele, pal_pe, unha, coxa=(0.24, 0.3, 0.28), h_pe=0.17):
    """Pata de quadrúpede: coxa redonda, perna de coluna e pé com unhas."""
    itens = [(Elip((x * 0.92, topo_y + 0.04, z), coxa).malha(10, 7), pal_pele),
             (tubo([(x, 0.12, z), (x, topo_y, z)], [r, r * 1.05], gomos=9), pal_pele)]
    itens += pe_dino((x, 0.0, z + 0.03), r * 1.15, h_pe, pal_pe, unha)
    return itens


TRICE = Paleta(pele=cor("#5AACF7"), escura=cor("#3E88DA"), barriga=cor("#DDF0FF"), placa=cor("#FFD43B"),
               borda=cor("#FF9F2E"), pinta=cor("#FFF1A8"), chifre=UNHA, bico=cor("#3570C2"), iris=cor("#1F4E8A"))


def triceratops():
    """Tricerátops ~1,45 m (com o babado): corpo de barril, patinhas curtas, cabeça de bico
    com o babado amarelo de borda laranja e três chifres creme.
    Partes: corpo > cabeca (> rosto), corpo > rabo."""
    pal = TRICE
    corpo_e = Elip((0, 0.74, -0.2), (0.56, 0.48, 0.8), rot(4, 0, 0))
    corpo = corpo_e.malha(16, 10, polo="z")
    corpo.pintar(lambda c, n: pal.barriga if n[1] < -0.45 else None)
    itens_corpo = [(corpo, pal.pele)]
    for (d, rx, ry, g) in (((0.4, 0.85, 0.1), 0.13, 0.1, 15), ((-0.45, 0.8, -0.25), 0.15, 0.11, -20),
                           ((0.3, 0.75, -0.6), 0.12, 0.09, 30), ((-0.2, 0.95, 0.35), 0.1, 0.08, 0),
                           ((0.65, 0.5, -0.35), 0.1, 0.08, 0), ((-0.6, 0.55, 0.3), 0.09, 0.07, 20)):
        itens_corpo.append(oval(corpo_e, d, rx, ry, pal.escura, esp=0.01, giro=g, lados=9))
    for s in (-1, 1):
        for (z, topo_y) in ((0.3, 0.55), (-0.66, 0.6)):
            itens_corpo += coxa_e_perna(s * 0.36, z, topo_y, 0.165, pal.pele, pal.escura, pal.chifre,
                                        coxa=(0.22, 0.28, 0.26), h_pe=0.15)
    # cabeça (pivô no pescoço)
    PIV_CAB = np.array([0.0, 0.8, 0.46])
    cab_e = Elip((0, 0.8, 0.82), (0.34, 0.31, 0.36))
    cab = cab_e.malha(14, 9, polo="z")
    cab.pintar(lambda c, n: pal.barriga if n[1] < -0.55 else None)
    itens_cab = [(cab, pal.pele), (tubo([(0, 0.76, 0.3), (0, 0.8, 0.62)], [0.27, 0.27], gomos=10), pal.pele)]
    bico = casco([secao(1.02, 0.18, 0.58, 0.84, 0.07, 0.1), secao(1.14, 0.115, 0.56, 0.76, 0.05, 0.07),
                  secao(1.24, 0.045, 0.58, 0.66, 0.02, 0.03)])
    itens_cab.append((bico, pal.bico))
    # babado: placa laranja atrás e a amarela na frente, de borda ondulada
    def babado(raio, ondas, amp):
        pts = []
        n = ondas * 4
        for i in range(n + 1):
            a = math.radians(-118 + 236 * i / n)
            r = raio * (1 + amp * math.cos(i * 2 * PI / 4))
            pts.append((math.sin(a) * r, math.cos(a) * r))
        pts.append((0.0, -raio * 0.2))
        return pts
    centro_b = np.array([0.0, 0.93, 0.6])
    tras = extrudar(babado(0.62, 7, 0.06), -0.035, 0.035, chanfro=0.012)
    frente = extrudar(babado(0.52, 7, 0.06), 0.0, 0.02, chanfro=0.006).mover((0, 0, 0.03))
    for f in (tras, frente):
        f.girar(-38, 0, 0).mover(centro_b)
    itens_cab += [(tras, pal.borda), (frente, pal.placa)]
    nb = rot(-38, 0, 0) @ np.array([0, 0, 1.0])
    for (u, v, rr) in ((-0.33, 0.17, 0.055), (0.33, 0.17, 0.055), (-0.19, 0.37, 0.05), (0.19, 0.37, 0.05),
                       (0.0, 0.44, 0.045), (-0.42, -0.05, 0.04), (0.42, -0.05, 0.04)):
        c = centro_b + rot(-38, 0, 0) @ np.array([u, v, 0.055])
        itens_cab.append((placa(circulo2d(rr, 8), c, nb, 0.008, cima=rot(-38, 0, 0) @ np.array([0, 1.0, 0])),
                          pal.borda))
    # chifres: dois na testa, um no nariz
    for s in (-1, 1):
        b = np.array([s * 0.15, 1.02, 0.82])
        pts = bezier(b, b + (s * 0.04, 0.12, 0.08), b + (s * 0.06, 0.22, 0.2), b + (s * 0.05, 0.26, 0.36), n=4)
        itens_cab.append((tubo(pts, [0.07, 0.06, 0.045, 0.025, 0.0], gomos=7), pal.chifre))
    b = np.array([0.0, 0.82, 1.06])
    itens_cab.append((tubo(bezier(b, b + (0, 0.06, 0.02), b + (0, 0.11, 0.05), b + (0, 0.15, 0.1), n=3),
                           [0.055, 0.04, 0.022, 0.0], gomos=6), pal.chifre))
    for s in (-1, 1):
        itens_cab.append(oval(cab_e, (s * 0.75, -0.3, 0.6), 0.06, 0.04, BOCHECHA, lados=10))
    rosto = []
    for s in (-1, 1):
        rosto += olho(cab_e, (s * 0.62, 0.22, 0.75), (0.06, 0.075), s, cor_iris=pal.iris)
    rosto.append(sorriso(cab_e, (0, -0.5, 1.0), 0.17, 0.022, abertura=120))
    PIV_RABO = np.array([0.0, 0.78, -0.88])
    pts_r = bezier((0, 0.8, -0.8), (0, 0.7, -1.12), (0.03, 0.52, -1.32), (0.12, 0.4, -1.46), n=6)
    rr = 0.3 * (1 - np.linspace(0, 1, len(pts_r))) ** 1.1 + 0.03
    rabo = tubo(pts_r, rr, gomos=9)
    rabo.pintar(lambda c, n: pal.barriga if n[1] < -0.55 else None)
    return montar("triceratops", [
        ("corpo", None, (0, 0, 0), itens_corpo, ""),
        ("cabeca", "corpo", PIV_CAB, itens_cab, ""),
        ("rosto", "cabeca", PIV_CAB, rosto, "luz"),
        ("rabo", "corpo", PIV_RABO, [(rabo, pal.pele)], ""),
    ])


ESTEGO = Paleta(pele=cor("#A27CFF"), escura=cor("#8460E6"), barriga=cor("#ECE2FF"), placa=cor("#FF9F2E"),
                borda=cor("#FFD43B"), espinho=UNHA, iris=cor("#4A2C8A"))


def placa_costas(base, altura, largura, inclina, cor_placa, cor_borda, esp=0.05):
    """Placa das costas do estegossauro (folha pontuda, de lado para X), com a
    borda chanfrada amarela."""
    w, h = largura, altura
    pts = [(0, h), (0.36 * w, 0.72 * h), (0.5 * w, 0.36 * h), (0.42 * w, 0.0), (-0.42 * w, 0.0), (-0.5 * w, 0.36 * h),
           (-0.36 * w, 0.72 * h)]
    f = extrudar(pts, -esp / 2, esp / 2, chanfro=esp * 0.32)
    f.pintar(lambda c, n: cor_placa if abs(n[2]) > 0.97 else cor_borda)
    f.girar(0, 90, 0)
    f.girar(0, 0, inclina)
    return f.mover(base)


def estegossauro():
    """Estegossauro ~1,7 m (com as placas): costas em arco com duas fileiras de placas
    laranja (borda amarela), cabecinha de olhos grandes e o rabo com quatro
    espinhos. Partes: corpo > cabeca (> rosto), corpo > rabo."""
    pal = ESTEGO
    corpo_e = Elip((0, 0.84, -0.12), (0.47, 0.52, 0.74), rot(8, 0, 0))
    corpo = corpo_e.malha(16, 10, polo="z")
    corpo.pintar(lambda c, n: pal.barriga if n[1] < -0.42 else None)
    itens_corpo = [(corpo, pal.pele)]
    for s in (-1, 1):
        for (z, topo_y) in ((0.32, 0.52), (-0.55, 0.66)):
            itens_corpo += coxa_e_perna(s * 0.31, z, topo_y, 0.15, pal.pele, pal.escura, pal.espinho,
                                        coxa=(0.18, 0.25, 0.23), h_pe=0.14)
    # placas: duas fileiras desencontradas ao longo da espinha (maiores no meio)
    n_pl = 9
    for i in range(n_pl):
        t = i / (n_pl - 1)
        ang = math.radians(-50 + 118 * t)              # da frente (cima-frente) para trás
        d = rot(8, 0, 0) @ np.array([0.0, math.cos(ang), -math.sin(ang)])
        p, n = corpo_e.superficie(d)
        h = 0.2 + 0.24 * math.sin(PI * (0.15 + 0.8 * t)) ** 1.5
        s = -1 if i % 2 == 0 else 1
        base = p - n * 0.06 + np.array([s * 0.075, 0, 0])
        itens_corpo.append((placa_costas(base, h, 0.22 + 0.2 * h, -s * 11, pal.placa, pal.borda, 0.055), pal.placa))
    # cabeça (o pescoço sai do peito e sobe um pouco)
    PIV_CAB = np.array([0.0, 0.76, 0.46])
    cab_e = Elip((0, 0.72, 0.98), (0.23, 0.21, 0.26))
    foc_e = Elip((0, 0.65, 1.15), (0.17, 0.125, 0.14))
    cab = cab_e.malha(12, 8, polo="z")
    foc = foc_e.malha(10, 6, polo="z")
    foc.pintar(lambda c, n: pal.barriga if n[1] < -0.4 else None)
    pescoco = tubo(bezier((0, 0.78, 0.34), (0, 0.78, 0.6), (0, 0.74, 0.78), (0, 0.72, 0.92), n=4),
                   [0.26, 0.24, 0.22, 0.2, 0.19], gomos=10)
    pescoco.pintar(lambda c, n: pal.barriga if n[1] < -0.5 else None)
    itens_cab = [(cab, pal.pele), (foc, pal.pele), (pescoco, pal.pele)]
    for s in (-1, 1):
        itens_cab.append(oval(cab_e, (s * 0.72, -0.35, 0.62), 0.05, 0.034, BOCHECHA, lados=10))
        itens_cab.append(oval(foc_e, (s * 0.3, 0.72, 0.62), 0.02, 0.014, pal.iris, lados=8, giro=-s * 25))
    rosto = []
    for s in (-1, 1):
        rosto += olho(cab_e, (s * 0.55, 0.3, 0.78), (0.055, 0.068), s, cor_iris=pal.iris)
    rosto.append(sorriso(foc_e, (0, -0.15, 1.0), 0.13, 0.018, abertura=125))
    # rabo com placas pequenas e os quatro espinhos
    PIV_RABO = np.array([0.0, 0.94, -0.62])
    pts_r = bezier((0, 0.97, -0.5), (0, 0.97, -1.05), (0, 0.8, -1.45), (0.05, 0.64, -1.8), n=8)
    rr = 0.3 * (1 - np.linspace(0, 1, len(pts_r))) ** 0.85 + 0.045
    rabo = tubo(pts_r, rr, gomos=9)
    rabo.pintar(lambda c, n: pal.barriga if n[1] < -0.55 else None)
    itens_rabo = [(rabo, pal.pele)]
    for (k, h, s) in ((1, 0.18, 1), (3, 0.13, -1)):
        base = pts_r[k] + np.array([s * 0.05, rr[k] * 0.72, 0])
        itens_rabo.append((placa_costas(base, h, 0.2, -s * 12, pal.placa, pal.borda, 0.045), pal.placa))
    for (k, s, sobe) in ((6, -1, 0.55), (6, 1, 0.55), (7, -1, 0.4), (7, 1, 0.4)):
        c = pts_r[k]
        dirn = _norm((s * 1.0, sobe, -0.4 if k == 7 else 0.05))
        itens_rabo.append((tubo([c + dirn * rr[k] * 0.4, c + dirn * (rr[k] + 0.24)], [0.05, 0.0], gomos=6), pal.espinho))
    return montar("estegossauro", [
        ("corpo", None, (0, 0, 0), itens_corpo, ""),
        ("cabeca", "corpo", PIV_CAB, itens_cab, ""),
        ("rosto", "cabeca", PIV_CAB, rosto, "luz"),
        ("rabo", "corpo", PIV_RABO, itens_rabo, ""),
    ])


PTERO = Paleta(pele=cor("#FF9F2E"), asa=cor("#FF8657"), asa_clara=cor("#FFB08A"), osso=cor("#E8692E"),
               barriga=cor("#FFDCA6"), bico=cor("#FFD43B"), crista=cor("#2EC4B6"), unha=UNHA, iris=cor("#7A3A1E"))
OMBRO_PTERO = np.array([0.1, 0.045, 0.06])


def asa_ptero(s, pal):
    """Uma asa (s = -1 esquerda / +1 direita), saindo do ombro para os lados:
    membrana com a borda de trás em ondas, o braço na frente e dois dedos."""
    ox = OMBRO_PTERO[0]
    # contorno no plano XZ (x para fora, z para a frente)
    k = 0.87        # envergadura ~1,6 m
    borda_f = [(0.06, 0.1), (0.26 * k, 0.13), (0.48 * k, 0.09), (0.66 * k, 0.0), (0.86 * k, -0.13)]
    borda_t = [(0.7 * k, -0.18), (0.58 * k, -0.25), (0.46 * k, -0.21), (0.34 * k, -0.29), (0.2 * k, -0.24), (0.06, -0.2)]
    pts = borda_f + borda_t
    memb = extrudar([(x, z) for (x, z) in pts], -0.012, 0.012)
    memb.girar(90, 0, 0)                     # (x, z_2d) -> (x, y=0, z = z_2d)

    def diedro(V):
        V = V.copy()
        V[:, 1] += 0.1 * np.clip(V[:, 0] - 0.1, 0, None) ** 1.3
        return V
    memb.deformar(diedro)
    memb.pintar(lambda c, n: pal.asa_clara if n[1] < -0.3 else None)
    itens = [(memb, pal.asa)]
    braco = [np.array([x, 0.0, z - 0.012]) for (x, z) in borda_f]
    braco = [b + (0, 0.1 * max(b[0] - 0.1, 0) ** 1.3, 0) for b in braco]
    itens.append((tubo(braco, [0.032, 0.028, 0.024, 0.018, 0.006], gomos=6), pal.osso))
    for (fim, ini) in ((borda_t[1], 2), (borda_t[3], 2)):
        a = braco[ini]
        b = np.array([fim[0], 0.0, fim[1] + 0.02])
        b = b + (0, 0.1 * max(b[0] - 0.1, 0) ** 1.3, 0)
        itens.append((tubo([a + (0, 0.008, 0), b + (0, 0.008, 0)], [0.011, 0.006], gomos=4), pal.osso))
    out = []
    for (f, c) in itens:
        g = f.copia().mover((0, OMBRO_PTERO[1], OMBRO_PTERO[2] - 0.06))
        g.mover((ox - 0.06, 0, 0))
        if s < 0:
            g.espelhar_x()
        out.append((g, c))
    return out


def ptero():
    """Pterossauro ~1,6 m de asa a asa (origem no meio do corpo): corpo
    pequeno, cabeça de bico amarelo comprido e crista azul-turquesa, asas de
    membrana e os pezinhos de garras para baixo (segura a plaquinha da marca).
    Partes: corpo (> rosto), asa_e, asa_d (pivôs nos ombros)."""
    pal = PTERO
    corpo_e = Elip((0, 0, 0), (0.13, 0.12, 0.23))
    corpo = corpo_e.malha(12, 8, polo="z")
    corpo.pintar(lambda c, n: pal.barriga if n[1] < -0.35 else None)
    cab_e = Elip((0, 0.12, 0.25), (0.115, 0.108, 0.13))
    cab = cab_e.malha(12, 8, polo="z")
    itens = [(corpo, pal.pele), (cab, pal.pele), (tubo([(0, 0.0, 0.1), (0, 0.1, 0.24)], [0.075, 0.075], gomos=8), pal.pele)]
    bico = casco([secao(0.3, 0.07, 0.04, 0.15, 0.03, 0.04), secao(0.44, 0.042, 0.04, 0.1, 0.02, 0.025),
                  secao(0.58, 0.012, 0.045, 0.058, 0.004, 0.004)])
    itens.append((bico, pal.bico))
    # crista: lâmina no plano YZ (o x do contorno vira o z do mundo)
    crista = extrudar([(0.3, 0.19), (0.12, 0.25), (-0.2, 0.33), (-0.04, 0.22), (0.16, 0.15)], -0.016, 0.016, chanfro=0.006)
    crista.girar(0, -90, 0)
    itens.append((crista, pal.crista))
    rabo = tubo([(0, -0.01, -0.2), (0, -0.04, -0.32), (0, -0.07, -0.42)], [0.05, 0.025, 0.008], gomos=6)
    itens.append((rabo, pal.pele))
    ponta = extrudar([(0, 0.05), (0.035, 0), (0, -0.05), (-0.035, 0)], -0.008, 0.008).girar(90, 0, 0).mover((0, -0.075, -0.45))
    itens.append((ponta, pal.crista))
    # pernas e pés com garras (penduradas, para segurar a plaquinha)
    garras = []
    for s in (-1, 1):
        q = np.array([s * 0.06, -0.07, -0.1])
        j = np.array([s * 0.075, -0.17, -0.07])
        t = np.array([s * 0.08, -0.25, -0.05])
        itens += [(tubo([q, j], [0.035, 0.028], gomos=6), pal.pele), (tubo([j, t], [0.026, 0.022], gomos=6), pal.pele),
                  (icosfera(0.03, 1, centro=t), pal.bico)]
        for k in (-1, 0, 1):
            g = bezier(t + (k * 0.02, -0.01, 0.01), t + (k * 0.03, -0.04, 0.03), t + (k * 0.03, -0.07, 0.02),
                       t + (k * 0.025, -0.075, -0.01), n=3)
            itens.append((tubo(g, [0.012, 0.01, 0.007, 0.0], gomos=4), pal.unha))
        garras.append(t + (0, -0.075, 0.0))
    for s in (-1, 1):
        itens.append(oval(cab_e, (s * 0.75, -0.35, 0.55), 0.03, 0.02, BOCHECHA, lados=8))
    rosto = []
    for s in (-1, 1):
        rosto += olho(cab_e, (s * 0.62, 0.32, 0.72), (0.042, 0.052), s, cor_iris=pal.iris)
    rosto.append(sorriso(cab_e, (0, -0.3, 1.0), 0.07, 0.012, abertura=120))
    asas = {}
    for s, nm in ((-1, "asa_e"), (1, "asa_d")):
        piv = np.array([s * OMBRO_PTERO[0], OMBRO_PTERO[1], OMBRO_PTERO[2]])
        asas[nm] = (piv, asa_ptero(s, pal))
    m = montar("ptero", [
        ("corpo", None, (0, 0, 0), itens, ""),
        ("rosto", "corpo", (0, 0, 0), rosto, "luz"),
        ("asa_e", "corpo", asas["asa_e"][0], asas["asa_e"][1], ""),
        ("asa_d", "corpo", asas["asa_d"][0], asas["asa_d"][1], ""),
    ])
    m.garras = garras
    return m


OVO = Paleta(casca=cor("#FFF3DC"), dentro=cor("#F1DDB8"), pinta=cor("#2EC4B6"), palha=cor("#E8C170"),
             palha2=cor("#C99A4B"), graveto=cor("#A8743F"))


def raio_ovo(y, R=0.27, yc=0.3, a_baixo=0.3, a_cima=0.4):
    """Raio do ovo na altura y (meia elipse mais gorda embaixo, mais comprida em cima)."""
    a = a_baixo if y < yc else a_cima
    return R * math.sqrt(max(0.0, 1.0 - ((y - yc) / a) ** 2))


def casca_ovo(parte="inteiro", gomos=14, corte=0.47, zig=0.045, esp=0.02, semente=3, H=0.7):
    """Casca de ovo de revolução (14 gomos). parte: "inteiro", "baixo" (até a
    rachadura em zigue-zague, com a parede de dentro e a borda) ou "cima" (o
    chapeuzinho que sobra em cima)."""
    rnd = np.random.RandomState(semente)
    borda = [corte + zig * (1 if i % 2 == 0 else -1) * rnd.uniform(0.55, 1.0) for i in range(gomos)]
    angs = [2 * PI * (i + 0.5) / gomos for i in range(gomos)]
    V, F = [], []

    def anel(ys, encolher=0.0):
        idx = []
        for i in range(gomos):
            y = ys[i] if hasattr(ys, "__len__") else ys
            r = max(raio_ovo(y) - encolher, 0.004)
            idx.append(len(V))
            V.append((math.cos(angs[i]) * r, y, math.sin(angs[i]) * r))
        return idx

    def costura(A, B, fora=True):
        for i in range(gomos):
            j = (i + 1) % gomos
            a, b, c, d = A[i], A[j], B[i], B[j]
            if fora:
                F.extend([(a, c, b), (b, c, d)])
            else:
                F.extend([(a, b, c), (b, d, c)])

    def polo(A, y, baixo, fora=True):
        ic = len(V)
        V.append((0.0, y, 0.0))
        for i in range(gomos):
            j = (i + 1) % gomos
            t = (ic, A[i], A[j]) if baixo else (ic, A[j], A[i])
            F.append(t if fora else (t[0], t[2], t[1]))

    if parte == "inteiro":
        niveis = [0.03, 0.1, 0.2, 0.3, 0.41, 0.52, 0.61, 0.67]
        aneis = [anel(y) for y in niveis]
        polo(aneis[0], 0.0, True)
        for A, B in zip(aneis[:-1], aneis[1:]):
            costura(A, B)
        polo(aneis[-1], H, False)
        return Forma(V, F)
    if parte == "baixo":
        niveis = [0.02, 0.07, 0.14, 0.22, 0.3, 0.38]
        fora = [anel(y) for y in niveis] + [anel(borda)]
        polo(fora[0], 0.0, True)
        for A, B in zip(fora[:-1], fora[1:]):
            costura(A, B)
        dentro = [anel(borda, esp), anel(0.36, esp), anel(0.26, esp)]
        for A, B in zip(dentro[1:], dentro[:-1]):
            costura(A, B, fora=False)
        polo(dentro[-1], 0.2, True, fora=False)
        o, n = fora[-1], dentro[0]
        for i in range(gomos):
            j = (i + 1) % gomos
            F.extend([(o[i], n[i], o[j]), (o[j], n[i], n[j])])
        return Forma(V, F)
    # "cima": do zigue-zague até a ponta
    niveis = [0.56, 0.63, 0.68]
    fora = [anel(borda)] + [anel(y) for y in niveis]
    for A, B in zip(fora[:-1], fora[1:]):
        costura(A, B)
    polo(fora[-1], H, False)
    dentro = [anel(borda, esp), anel(0.56, esp), anel(0.63, esp)]
    for A, B in zip(dentro[:-1], dentro[1:]):
        costura(A, B, fora=False)
    polo(dentro[-1], 0.66, False, fora=False)
    o, n = fora[0], dentro[0]
    for i in range(gomos):
        j = (i + 1) % gomos
        F.extend([(o[i], o[j], n[i]), (o[j], n[j], n[i])])
    return Forma(V, F)


def ninho(R=0.25, r=0.085, y=0.07, gomos=12, semente=2, gravetos=4, lados=7):
    """Ninho de palha: um anel gordinho em gomos de duas cores e uns gravetos."""
    pal = OVO
    perfil = [(R + math.cos(a) * r, y + math.sin(a) * r * 0.8) for a in np.linspace(-PI / 2, 1.5 * PI, lados)]
    f = torno(perfil, gomos=gomos, tampa_baixo=False, tampa_cima=False)
    f.pintar(lambda c, n: pal.palha2 if int((math.atan2(c[2], c[0]) + PI) / (2 * PI) * gomos * 1.5) % 3 == 0 else None)
    itens = [(f, pal.palha)]
    rnd = np.random.RandomState(semente)
    for k in range(gravetos):
        a = 2 * PI * k / gravetos + rnd.uniform(-0.3, 0.3)
        c = np.array([math.cos(a) * R, y + r * 0.55, math.sin(a) * R])
        tg = np.array([-math.sin(a), rnd.uniform(-0.15, 0.15), math.cos(a)])
        itens.append((tubo([c - tg * 0.16, c + tg * 0.16], [0.014, 0.012], gomos=4), pal.graveto))
    return itens


def pintas_ovo(sup, dirs, raio=0.045, cor_=OVO.pinta, cima=(0, 1, 0), lados=8, paredes=True):
    """Pintas ovais coladas na casca: dirs = [(direção, tamanho, giro)]."""
    return [(adesivo(sup, circulo2d(1.0, lados, escala=(raio * k, raio * k * 0.85)), d, 0.003, 0.012, g, cima,
                     paredes=paredes), cor_) for (d, k, g) in dirs]


def ovo_dino():
    """Ovo de dinossauro ~0,7 m com pintas azul-turquesa, num ninho de palha."""
    ovo = casca_ovo("inteiro", gomos=11)
    sup = SupMalha(ovo, (0, 0.33, 0))
    itens = [(ovo, OVO.casca)] + ninho(gomos=10, gravetos=3, lados=6)
    dirs = [((0.8, 0.5, 0.3), 1.0, 10), ((-0.6, 0.7, 0.5), 0.9, -20), ((0.1, 0.9, -0.8), 1.1, 30), ((-0.9, 0.3, -0.4), 0.8, 0),
            ((0.4, 0.2, 0.95), 0.85, 15), ((-0.2, 0.1, -1.0), 0.75, 0), ((0.95, 0.05, -0.5), 0.7, 40), ((0.0, 1.0, 0.1), 0.7, 0),
            ((-0.75, 0.0, 0.8), 0.7, 0)]
    itens += pintas_ovo(sup, dirs, 0.05, lados=7, paredes=False)
    return simples("ovo_dino", itens)


BEBE = Paleta(pele=cor("#8EDB6A"), barriga=cor("#E2F7B5"), escura=cor("#6CC24E"), iris=cor("#2F6B2A"), nariz=cor("#3B6A2A"))


def ovo_chocando():
    """Ovo rachado em cima com o filhote espiando, de chapeuzinho de casca e
    as mãozinhas na borda. Partes: ovo > cabeca (> rosto) — o pivô da cabeça
    é o pescoço, dentro do ovo."""
    pb = BEBE
    baixo = casca_ovo("baixo")
    sup = SupMalha(baixo, (0, 0.25, 0))
    baixo.pintar(lambda c, n: OVO.dentro if (c[0] ** 2 + c[2] ** 2) < (raio_ovo(c[1]) - 0.01) ** 2 else None)
    itens_ovo = [(baixo, OVO.casca)] + ninho()
    itens_ovo += pintas_ovo(sup, [((0.8, -0.1, 0.4), 1.0, 10), ((-0.6, -0.2, 0.7), 0.9, -20), ((0.2, 0.0, -0.95), 1.1, 30),
                                  ((-0.9, 0.1, -0.4), 0.8, 0), ((0.5, 0.25, 0.85), 0.7, 0), ((-0.3, 0.3, -0.9), 0.7, 15),
                                  ((0.95, 0.3, -0.3), 0.7, 0)], 0.05)
    # mãozinhas na borda (na frente, dos lados)
    for s in (-1, 1):
        a = math.radians(90 - s * 38)
        r = raio_ovo(0.46)
        c = np.array([math.cos(a) * r, 0.47, math.sin(a) * r])
        itens_ovo.append((Elip(c, (0.055, 0.04, 0.05), rot(0, -s * 38, 0)).malha(8, 5), pb.pele))
        for k in (-1, 0, 1):
            dd = np.array([math.cos(a + k * 0.16) * (r + 0.03), 0.455, math.sin(a + k * 0.16) * (r + 0.03)])
            itens_ovo.append((icosfera(0.019, 0, centro=dd), pb.pele))
    # filhote
    PIV_CAB = np.array([0.0, 0.36, 0.0])
    cab_e = Elip((0, 0.69, 0.04), (0.175, 0.16, 0.165))
    foc_e = Elip((0, 0.635, 0.15), (0.125, 0.088, 0.095))
    cab = cab_e.malha(14, 9, polo="z")
    foc = foc_e.malha(12, 7, polo="z")
    foc.pintar(lambda c, n: pb.barriga if n[1] < -0.3 else None)
    pesc = tubo([(0, 0.3, -0.01), (0, 0.6, 0.02)], [0.12, 0.12], gomos=10)
    pesc.pintar(lambda c, n: pb.barriga if n[2] > 0.5 else None)
    itens_cab = [(cab, pb.pele), (foc, pb.pele), (pesc, pb.pele)]
    for s in (-1, 1):
        itens_cab.append(oval(cab_e, (s * 0.7, -0.38, 0.6), 0.04, 0.027, BOCHECHA, lados=10))
        itens_cab.append(oval(foc_e, (s * 0.3, 0.7, 0.62), 0.014, 0.01, pb.nariz, lados=8, giro=-s * 25))
    # chapeuzinho de casca (inclinado), com uma pinta
    chapeu = casca_ovo("cima")
    chapeu.escalar(0.84, centro=(0, 0.47, 0)).girar(-12, 0, 16, centro=(0, 0.47, 0)).mover((0.025, 0.31, 0.0))
    itens_cab.append((chapeu, OVO.casca))
    sup_ch = SupMalha(chapeu, (0.03, 0.8, 0.0))
    itens_cab += pintas_ovo(sup_ch, [((0.3, 0.8, 0.5), 0.75, 0), ((-0.6, 0.6, -0.3), 0.6, 20)], 0.05)
    rosto = []
    for s in (-1, 1):
        rosto += olho(cab_e, (s * 0.5, 0.18, 0.85), (0.046, 0.056), s, cor_iris=pb.iris)
    rosto.append(sorriso(foc_e, (0, -0.1, 1.0), 0.09, 0.014, abertura=130))
    return montar("ovo_chocando", [
        ("ovo", None, (0, 0, 0), itens_ovo, ""),
        ("cabeca", "ovo", PIV_CAB, itens_cab, ""),
        ("rosto", "cabeca", PIV_CAB, rosto, "luz"),
    ])


def tangentes(P):
    """Tangentes unitárias de uma polilinha (diferença central)."""
    P = np.asarray(P, float)
    T = np.zeros_like(P)
    T[0] = P[1] - P[0]
    T[-1] = P[-1] - P[-2]
    T[1:-1] = P[2:] - P[:-2]
    return T / np.maximum(np.linalg.norm(T, axis=1, keepdims=True), 1e-12)


def folha(pts, larg, cor_cima, cor_baixo, cima=(0, 1, 0), vinco=0.3, serra=0.0, cor_ponta=None, t_ponta=0.75):
    """Folha de duas faces ao longo da linha `pts` (larguras `larg` em cada
    ponto, 0 = ponta): dobrada no meio (a nervura mais alta), borda serrilhada
    (serra = quanto os pontos ímpares entram). Cima e baixo de cores
    diferentes (vista de baixo também: as palmeiras ficam no alto)."""
    P = np.asarray(pts, float)
    n = len(P)
    T = tangentes(P)
    up = np.asarray(cima, float)
    V, ref = [], []
    for i in range(n):
        l = np.cross(up, T[i])
        if np.linalg.norm(l) < 1e-6:
            l = np.cross((1.0, 0, 0), T[i])
        l = _norm(l)
        nn = _norm(np.cross(T[i], l))
        w = larg[i] * (1.0 - serra if (i % 2 == 1 and 0 < i < n - 1) else 1.0)
        V += [P[i] - l * w - nn * w * vinco * 0.5, P[i] + nn * w * vinco * 0.5, P[i] + l * w - nn * w * vinco * 0.5]
        ref.append(nn)
    F, C = [], []
    for i in range(n - 1):
        e0, c0, d0, e1, c1, d1 = 3 * i, 3 * i + 1, 3 * i + 2, 3 * i + 3, 3 * i + 4, 3 * i + 5
        rn = _norm(ref[i] + ref[i + 1])
        t = (i + 0.5) / (n - 1)
        for tri in ((e0, e1, c0), (c0, e1, c1), (c0, c1, d0), (d0, c1, d1)):
            a, b, c = (np.asarray(V[k]) for k in tri)
            nf = np.cross(b - a, c - a)
            if np.linalg.norm(nf) < 1e-12:
                continue
            if np.dot(nf, rn) < 0:
                tri = (tri[0], tri[2], tri[1])
            F.append(tri)
            C.append(cor_ponta if (cor_ponta is not None and t > t_ponta) else cor_cima)
            F.append((tri[0], tri[2], tri[1]))
            C.append(cor_baixo)
    return Forma(V, F, C)


SELVA = Paleta(tronco=cor("#C99560"), anel=cor("#A8743F"), folha=cor("#4CC46A"), folha_baixo=cor("#2F9A52"),
               folha_ponta=cor("#7EDB6E"), coco=cor("#8B5A2B"), copa=cor("#3E9E4E"))


def palmeira():
    """Palmeira ~3,8 m: tronco curvado em anéis (tronquinhos que alargam em
    cima, duas cores), oito folhas arqueadas de borda serrilhada e três cocos."""
    pal = SELVA
    H = 3.5
    ys = np.linspace(0.0, H, 10)
    xs = 0.45 * (ys / H) ** 2
    eixo = np.stack([xs, ys, np.zeros_like(ys)], 1)
    itens = []
    for i in range(len(eixo) - 1):
        r0 = 0.21 - 0.07 * ys[i] / H
        a = eixo[i] - (eixo[i + 1] - eixo[i]) * 0.08
        b = eixo[i + 1]
        seg = tubo([a, b], [r0 * 0.9, r0 * 1.16], gomos=7, tampas=(i == 0))
        itens.append((seg, pal.tronco if i % 2 == 0 else pal.anel))
    itens.append((prisma(7, 0.34, 0.22, chanfro=0.05, raio_topo=0.22, fase=0.5), pal.anel))
    topo = eixo[-1]
    itens.append((icosfera(0.24, 1, centro=topo + (0, 0.04, 0), escala=(1, 0.75, 1)), pal.copa))
    for i in range(8):
        az = 2 * PI * i / 8 + 0.35 + 0.15 * math.sin(i * 2.3)
        L = 1.75 + 0.18 * math.sin(i * 1.7)
        dr = np.array([math.cos(az), 0.0, math.sin(az)])
        sv = np.linspace(0, 1, 9)
        pts = topo + (0, 0.1, 0) + np.outer(sv * L, dr) + np.outer(L * (0.6 * sv - (1.15 + 0.12 * math.cos(i)) * sv * sv),
                                                                     (0, 1, 0))
        larg = 0.08 + 0.28 * np.sin(PI * sv ** 0.7) ** 0.7
        larg[-1] = 0.0
        itens.append((folha(pts, larg, pal.folha, pal.folha_baixo, serra=0.38, vinco=0.35, cor_ponta=pal.folha_ponta,
                            t_ponta=0.8), pal.folha))
    for a in (0.9, 0.9 + 2.1, 0.9 + 4.2):
        c = topo + np.array([0.19 * math.cos(a), -0.2, 0.19 * math.sin(a)])
        itens.append((icosfera(0.13, 0, centro=c, escala=(1, 1.05, 1)), pal.coco))
    return simples("palmeira", itens)


def fronde(pts, larg, n, cor_cima, cor_baixo, cor_ponta=None, cima=(0, 1, 0)):
    """Folha de samambaia em "espinha de peixe": n folhinhas triangulares de
    cada lado da linha `pts` (largura larg(t)), inclinadas para a ponta e
    caindo de leve; duas faces (de baixo mais escura)."""
    P = np.asarray(pts, float)
    T = tangentes(P)
    L = np.concatenate([[0.0], np.cumsum(np.linalg.norm(P[1:] - P[:-1], axis=1))])
    L /= L[-1]
    up = np.asarray(cima, float)
    V, F, C = [], [], []
    for k in range(n):
        t0 = max((k - 0.35) / n, 0.0)
        t1 = min((k + 1.0) / n, 1.0)
        tm = (t0 + t1) / 2
        a = np.array([np.interp(t0, L, P[:, i]) for i in range(3)])
        b = np.array([np.interp(t1, L, P[:, i]) for i in range(3)])
        tg = np.array([np.interp(tm, L, T[:, i]) for i in range(3)])
        lado = _norm(np.cross(up, tg))
        nn = _norm(np.cross(tg, lado))
        w = larg(tm)
        for sgn in (-1, 1):
            ponta = a * 0.35 + b * 0.65 + lado * sgn * w + _norm(tg) * w * 0.5 - nn * w * 0.25
            base = len(V)
            V += [a, b, ponta]
            tri = (base, base + 1, base + 2)
            nf = np.cross(V[base + 1] - V[base], V[base + 2] - V[base])
            if np.dot(nf, nn) < 0:
                tri = (base, base + 2, base + 1)
            F.append(tri)
            C.append(cor_ponta if (cor_ponta is not None and tm > 0.72) else cor_cima)
            F.append((tri[0], tri[2], tri[1]))
            C.append(cor_baixo)
    return Forma(V, F, C)


def samambaia():
    """Samambaia (~0,7 m de altura, ~2 m de largura): nove folhas arqueadas
    de folhinhas triangulares (espinha de peixe), de ponta clara."""
    pal = Paleta(verde=cor("#5ED16A"), baixo=cor("#2F9E5A"), ponta=cor("#A6E85F"), centro=cor("#2F8F4E"))
    itens = [(prisma(6, 0.15, 0.12, chanfro=0.04, raio_topo=0.08, fase=0.5), pal.centro)]
    for i in range(9):
        az = 2 * PI * i / 9 + 0.25 + 0.18 * math.sin(i * 3.1)
        externa = i % 2 == 0
        L = 0.95 if externa else 1.05
        sub, que = (1.2, 1.25) if externa else (1.55, 1.3)
        dr = np.array([math.cos(az), 0.0, math.sin(az)])
        sv = np.linspace(0, 1, 7)
        pts = np.array([0, 0.06, 0]) + np.outer(sv * L, dr) + np.outer(L * (sub * sv - que * sv * sv), (0, 1, 0))
        itens.append((fronde(pts, lambda t: 0.06 + 0.15 * math.sin(PI * min(t, 1.0) ** 0.8) ** 0.8, 9, pal.verde, pal.baixo,
                             pal.ponta), pal.verde))
    return simples("samambaia", itens)


def rocha(raio, achatar=(1.0, 0.75, 0.9), semente=1, sub=1, ruido=0.13, chao=None):
    """Rocha facetada: icosfera com os vértices empurrados (cada face é um
    triângulo: sempre plana e nítida). chao: corta embaixo nessa altura (local)."""
    f = icosfera(1.0, sub)
    rnd = np.random.RandomState(semente)
    # vértices iguais (a icosfera não repete) — empurra cada um pelo seu raio
    k = 1.0 + rnd.uniform(-ruido, ruido, size=(len(f.V), 1))
    f.V = f.V * k * np.asarray(achatar) * raio
    if chao is not None:
        f.V[:, 1] = np.maximum(f.V[:, 1], chao)
    return f


def pedra_():
    """Pedra facetada com musgo em cima (e uma pedrinha encostada)."""
    pal = Paleta(pedra=cor("#A9A6BC"), musgo=cor("#7ED957"), escura=cor("#8E8AA6"))
    a = rocha(0.52, (1.0, 0.74, 0.88), 4, chao=-0.25).mover((0, 0.26, 0))
    b = rocha(0.26, (1.0, 0.78, 0.95), 9, sub=0, ruido=0.1, chao=-0.08).mover((0.52, 0.09, 0.28))
    for f in (a, b):
        f.pintar(lambda c, n: pal.musgo if n[1] > 0.7 else (pal.escura if n[1] < -0.3 else None))
    return simples("pedra", [(a, pal.pedra), (b, pal.pedra)])


def flor_gigante():
    """Flor gigante ~1,3 m: caule curvo, duas folhas, cinco pétalas laranja
    com o miolo amarelo e o centro marrom de pontinhos, virada para a frente."""
    pal = Paleta(caule=cor("#4CB85A"), folha=cor("#6FD25E"), folha_baixo=cor("#3E9E4E"), petala=cor("#FF8A2A"),
                 clara=cor("#FFD43B"), miolo=cor("#7A4A2E"), ponto=cor("#C08A58"))
    caule_pts = bezier((0, 0, 0), (0.05, 0.35, 0.0), (0.0, 0.7, 0.0), (0.0, 0.9, 0.07), n=4)
    itens = [(tubo(caule_pts, [0.055, 0.05, 0.047, 0.045, 0.045], gomos=6), pal.caule)]
    for (y, az, L) in ((0.26, 0.15, 0.48), (0.46, PI - 0.25, 0.42)):
        base = np.array([0.03, y, 0.0])
        dr = np.array([math.cos(az), 0.0, math.sin(az)])
        sv = np.linspace(0, 1, 5)
        pts = base + np.outer(sv * L, dr) + np.outer(L * (0.75 * sv - 0.65 * sv * sv), (0, 1, 0))
        larg = 0.02 + 0.11 * np.sin(PI * sv ** 0.8)
        larg[-1] = 0.0
        itens.append((folha(pts, larg, pal.folha, pal.folha_baixo, vinco=0.3), pal.folha))
    centro = np.array([0.0, 0.94, 0.09])
    f = _norm((0.0, 0.42, 1.0))
    R = np.stack(base_tangente(f), axis=1)
    for i in range(5):
        a = 90 + 72 * i
        pet = extrudar(circulo2d(1.0, 7, escala=(0.13, 0.19)), -0.018, 0.018).mover((0, 0.2, 0))
        pet.girar(8, 0, 0, centro=(0, 0.02, 0)).girar(0, 0, a - 90)
        pet.V = pet.V @ R.T + centro
        itens.append((pet, pal.petala))
    estrela = extrudar(estrela2d(5, 0.2, 0.12, fase=90), -0.012, 0.012)
    estrela.V = estrela.V @ R.T + centro + f * 0.04
    itens.append((estrela, pal.clara))
    miolo = Elip(centro + f * 0.05, (0.11, 0.11, 0.06), R)
    itens.append((miolo.malha(9, 5, polo="z"), pal.miolo))
    for (u, v) in ((0.0, 0.0), (0.05, 0.03), (-0.05, 0.03), (0.0, -0.055), (0.05, -0.035), (-0.05, -0.035), (0.0, 0.06)):
        d = R @ np.array([u, v, 0.08])
        itens.append((adesivo(miolo, circulo2d(0.014, 5), d, 0.003, 0.01, paredes=False), pal.ponto))
    return simples("flor_gigante", itens)


DINO_LAVA = Paleta(terra=cor("#B97650"), escura=cor("#9C6043"), clara=cor("#CC8C62"), lava=cor("#FF7A1A"),
                   lava_clara=cor("#FFC93B"), pedra=cor("#8A6352"))

# perfil do vulcão (raio, altura), da base até a borda da cratera
PERFIL_VULCAO = [(4.75, -0.12), (4.45, 0.4), (3.75, 1.1), (2.95, 1.9), (2.25, 2.75), (1.68, 3.6), (1.3, 4.3), (1.12, 4.72)]


def _r_vulcao(y):
    """Raio da encosta do vulcão na altura y."""
    ys = [q[1] for q in PERFIL_VULCAO]
    rs = [q[0] for q in PERFIL_VULCAO]
    return float(np.interp(y, ys, rs))


def _no_vulcao(teta, y, sobe=0.0):
    """Ponto da encosta (ângulo, altura) e a normal ali, levantado `sobe`."""
    r = _r_vulcao(y)
    dr = (_r_vulcao(y + 0.01) - _r_vulcao(y - 0.01)) / 0.02
    n = _norm((math.cos(teta), -dr, math.sin(teta)))
    p = np.array([math.cos(teta) * r, y, math.sin(teta) * r])
    return p + n * sobe


def escorrido(teta0, L, w, sobe, passo=0.22):
    """Escorrido de lava descendo a encosta: faixa (esquerda, meio, direita)
    em linhas de altura, com a gota redonda no fim. Devolve uma Forma."""
    y0 = PERFIL_VULCAO[-1][1] - 0.02
    linhas = []
    n = max(3, int(L / passo))
    for k in range(n + 1):
        t = k / n
        y = y0 - t * L
        r = _r_vulcao(y)
        larg = w * (1.0 - 0.38 * t) * (1.0 + 0.08 * math.sin(t * 11 + teta0 * 3))
        tc = teta0 + 0.05 * math.sin(t * 5 + teta0)
        linhas.append((tc, y, larg / r))
    V, F = [], []
    for (tc, y, da) in linhas:
        for a in (tc - da, tc, tc + da):
            V.append(_no_vulcao(a, y, sobe))
    for k in range(len(linhas) - 1):
        a = 3 * k
        b = 3 * (k + 1)
        F += [(a, b, a + 1), (a + 1, b, b + 1), (a + 1, b + 1, a + 2), (a + 2, b + 1, b + 2)]
    # gota no fim (meio disco para baixo), em leque a partir do ponto do meio da última linha
    tc, y, da = linhas[-1]
    r = _r_vulcao(y)
    k_ult = 3 * (len(linhas) - 1)
    meio = k_ult + 1
    leque = [k_ult]
    for i in range(1, 6):
        a = PI * i / 6
        dy = -math.sin(a) * da * r * 1.15
        dt = -math.cos(a) * da * 1.15
        V.append(_no_vulcao(tc + dt, y + dy, sobe))
        leque.append(len(V) - 1)
    leque.append(k_ult + 2)
    for i in range(len(leque) - 1):
        F.append((meio, leque[i], leque[i + 1]))
    f = Forma(np.array(V), F)
    # todas para fora da encosta
    A, B, C = f.V[f.F[:, 0]], f.V[f.F[:, 1]], f.V[f.F[:, 2]]
    nf = np.cross(B - A, C - A)
    cen = (A + B + C) / 3
    fora = np.stack([cen[:, 0], np.full(len(cen), 0.3), cen[:, 2]], 1)
    trocar = np.einsum("ij,ij->i", nf, fora) < 0
    f.F[trocar] = f.F[trocar][:, [0, 2, 1]]
    return f


def vulcao():
    """Vulcão ~4,8 m (o jogo põe com escala 1,9 lá no fundo): cone facetado
    em faixas de terra, a cratera com o lago de lava e os escorridos (peças
    "luz": brilham sozinhos), pedras em volta do pé."""
    pal = DINO_LAVA
    gomos = 16
    V, F, aneis = [], [], []
    for j, (r, y) in enumerate(PERFIL_VULCAO):
        anel = []
        for i in range(gomos):
            a = 2 * PI * (i + 0.5 * (j % 2)) / gomos
            anel.append(len(V))
            V.append((math.cos(a) * r, y, math.sin(a) * r))
        aneis.append(anel)
    for A, B in zip(aneis[:-1], aneis[1:]):
        for i in range(gomos):
            j = (i + 1) % gomos
            F += [(A[i], B[i], A[j]), (A[j], B[i], B[j])]
    dentro = []
    for i in range(gomos):
        a = 2 * PI * (i + 0.5 * ((len(PERFIL_VULCAO) - 1) % 2)) / gomos
        dentro.append(len(V))
        V.append((math.cos(a) * 0.82, 4.5, math.sin(a) * 0.82))
    B = aneis[-1]
    for i in range(gomos):
        j = (i + 1) % gomos
        F += [(B[i], dentro[i], B[j]), (B[j], dentro[i], dentro[j])]
    cone = Forma(V, F)
    A_, B_, C_ = cone.V[cone.F[:, 0]], cone.V[cone.F[:, 1]], cone.V[cone.F[:, 2]]
    nf = np.cross(B_ - A_, C_ - A_)
    cen = (A_ + B_ + C_) / 3
    ref = np.stack([cen[:, 0], np.full(len(cen), 0.5), cen[:, 2]], 1)
    trocar = np.einsum("ij,ij->i", nf, ref) < 0
    cone.F[trocar] = cone.F[trocar][:, [0, 2, 1]]

    def faixas(c, n):
        if c[1] < 0.45:
            return pal.escura
        if 1.5 < c[1] < 2.0:
            return pal.escura
        if c[1] > 3.95:
            return pal.clara
        return None
    cone.pintar(faixas)
    itens = [(cone, pal.terra)]
    for k in range(8):
        a = 2 * PI * k / 8 + 0.4
        rr = 4.35 + 0.35 * math.sin(k * 2.1)
        p_ = pedra(0.5 + 0.15 * math.sin(k * 1.3), 12, (1.0, 0.7, 0.9), 20 + k, 0.15).mover((math.cos(a) * rr, 0.15, math.sin(a) * rr))
        itens.append((p_, pal.pedra))
    for k in range(5):
        a = 2 * PI * k / 5 + 1.0
        y = 1.3 + 0.9 * (k % 3)
        c = _no_vulcao(a, y, 0.05)
        itens.append((pedra(0.32, 10, (1.0, 0.55, 0.9), 40 + k, 0.15).mover(c), pal.escura))
    lava = [(torno([(0.0, 4.56), (0.84, 4.56)], gomos=gomos, tampa_baixo=False, tampa_cima=False).inverter(), pal.lava),
            (torno([(0.0, 4.58), (0.5, 4.58)], gomos=10, tampa_baixo=False, tampa_cima=False).inverter(), pal.lava_clara)]
    for (az, L, w) in ((0.12, 2.6, 0.46), (0.95, 1.6, 0.38), (-0.75, 2.1, 0.42), (1.9, 2.3, 0.44), (-1.75, 1.5, 0.36),
                       (2.75, 1.9, 0.4), (-2.6, 2.4, 0.44), (3.6, 1.6, 0.38)):
        lava.append((escorrido(-az + PI / 2, L, w, 0.09), pal.lava))
        lava.append((escorrido(-az + PI / 2, L * 0.62, w * 0.42, 0.13), pal.lava_clara))
    return simples("vulcao", itens, luz=lava)


# =================================================================== PARQUE DE DIVERSÕES


def tubo_torcido(pts, raios, gomos, giro_anel, cor_de_gomo, tampas=True):
    """Tubo cujos anéis giram `giro_anel` gomos a cada anel: as faces seguem
    uma hélice, então pintar pelo gomo dá listras em espiral de borda reta
    (bengala, poste de carrossel, torre). Moldura sem torção (transporte
    paralelo): não vira nas curvas. cor_de_gomo(k) -> cor ou None."""
    P = np.asarray(pts, float)
    n = len(P)
    raios = list(raios) if hasattr(raios, "__len__") else [raios] * n
    T = tangentes(P)
    ref = np.array([1.0, 0.0, 0.0]) if abs(T[0][0]) < 0.9 else np.array([0.0, 0.0, 1.0])
    X = [_norm(np.cross(T[0], ref))]
    for i in range(1, n):
        x = X[-1] - np.dot(X[-1], T[i]) * T[i]
        X.append(_norm(x))
    V, F, C = [], [], []
    for i in range(n):
        y = np.cross(T[i], X[i])
        for k in range(gomos):
            a = 2 * PI * (k + giro_anel * i) / gomos
            V.append(P[i] + (X[i] * math.cos(a) + y * math.sin(a)) * raios[i])
    for i in range(n - 1):
        for k in range(gomos):
            a, b = i * gomos + k, i * gomos + (k + 1) % gomos
            c, d = (i + 1) * gomos + k, (i + 1) * gomos + (k + 1) % gomos
            F += [(a, b, c), (b, d, c)]
            C += [cor_de_gomo(k)] * 2
    if tampas:
        for (i, sinal) in ((0, -1), (n - 1, 1)):
            ic = len(V)
            V.append(P[i])
            for k in range(gomos):
                a, b = i * gomos + k, i * gomos + (k + 1) % gomos
                F.append((ic, b, a) if sinal < 0 else (ic, a, b))
                C.append(cor_de_gomo(k))
    f = Forma(V, F, C)
    return _orientar_fora_linha(f, P)


def _orientar_fora_linha(f, P):
    """Cada face com a normal para longe da linha central (tubos)."""
    A, B, C = f.V[f.F[:, 0]], f.V[f.F[:, 1]], f.V[f.F[:, 2]]
    nf = np.cross(B - A, C - A)
    cen = (A + B + C) / 3
    t = parametro_linha(P, cen)
    seg = np.asarray(P)
    L = np.concatenate([[0.0], np.cumsum(np.linalg.norm(seg[1:] - seg[:-1], axis=1))])
    L /= L[-1]
    perto = np.stack([np.interp(t, L, seg[:, k]) for k in range(3)], 1)
    fora = cen - perto
    # tampas: a direção da ponta
    tg = tangentes(P)
    tampa0 = np.linalg.norm(fora, axis=1) < 1e-6
    fora[tampa0 & (t < 0.5)] = -tg[0]
    fora[tampa0 & (t >= 0.5)] = tg[-1]
    trocar = np.einsum("ij,ij->i", nf, fora) < 0
    f.F[trocar] = f.F[trocar][:, [0, 2, 1]]
    return f


PARQUE = Paleta(tronco=cor("#A86B3C"), tronco_escuro=cor("#8E5A32"), verde=cor("#5ED16A"), verde2=cor("#4BBE5C"),
                laranja=cor("#FFA23A"), laranja2=cor("#FF8A2A"), escura=cor("#2FA35A"), escura2=cor("#24894A"),
                vermelho=cor("#FF4B5C"), amarelo=cor("#FFD43B"), azul=cor("#4FC3F7"), branco=cor("#FFFFFF"),
                creme=cor("#FFF3DC"), rosa=cor("#FF6FAE"), turquesa=cor("#2EC4B6"), roxo=cor("#8A5CFF"))


def tronco_arvore(altura=1.35, r0=0.17, r1=0.09, curva=0.06, cor_=PARQUE.tronco, raizes=True):
    """Tronco curvinho afinando para cima, com três raízes no pé."""
    pts = [(0, 0, 0), (curva * 0.3, altura * 0.4, 0), (curva, altura * 0.75, 0.02), (curva * 0.6, altura, 0.0)]
    itens = [(tubo(pts, [r0, r0 * 0.82, r1 * 1.1, r1], gomos=7), cor_)]
    if raizes:
        for k in range(3):
            a = 2 * PI * k / 3 + 0.4
            d = np.array([math.cos(a), 0, math.sin(a)])
            itens.append((tubo([d * 0.05 + (0, 0.2, 0), d * 0.3 + (0, 0.0, 0)], [0.07, 0.03], gomos=5), cor_))
    return itens


def copa_bolas(bolas, cores):
    """Copa de árvore de bolas facetadas (icosferas) — cada uma de uma cor da paleta."""
    itens = []
    for i, (c, r) in enumerate(bolas):
        f = icosfera(r, 1, centro=c)
        itens.append((f, cores[i % len(cores)]))
    return itens


def arvore_verde():
    """Árvore redonda (~2,6 m): tronco curvinho e a copa de bolas facetadas verdes."""
    pal = PARQUE
    itens = tronco_arvore()
    itens += copa_bolas([((0.0, 1.78, 0.0), 0.62), ((0.45, 1.62, 0.18), 0.42), ((-0.42, 1.68, -0.15), 0.44),
                         ((0.12, 1.6, -0.45), 0.4), ((-0.2, 1.62, 0.42), 0.4), ((0.05, 2.22, 0.02), 0.4)],
                        [pal.verde, pal.verde2, pal.verde2, pal.verde, pal.verde, pal.verde])
    return simples("arvore_verde", itens)


def arvore_laranja():
    """Árvore de outono (~2,7 m): copa de bolas laranja, mais alta e oval."""
    pal = PARQUE
    itens = tronco_arvore(1.4, curva=-0.05)
    itens += copa_bolas([((0.0, 1.85, 0.0), 0.6), ((0.38, 1.66, 0.25), 0.4), ((-0.4, 1.7, 0.12), 0.42),
                         ((0.05, 1.68, -0.42), 0.42), ((0.12, 2.3, 0.05), 0.42), ((-0.18, 2.12, -0.2), 0.36)],
                        [pal.laranja, pal.laranja2, pal.laranja, pal.laranja2, pal.laranja, pal.laranja2])
    return simples("arvore_laranja", itens)


def arvore_escura():
    """Pinheiro verde-escuro (~2,8 m): três cones de borda em zigue-zague."""
    pal = PARQUE
    itens = tronco_arvore(0.9, 0.15, 0.1, curva=0.0)
    for k, (y0, r, h) in enumerate(((0.55, 0.9, 1.05), (1.15, 0.72, 0.95), (1.7, 0.52, 1.05))):
        n = 18
        V = [(0.0, y0 + h, 0.0)]
        for i in range(n):
            a = 2 * PI * i / n + k * 0.3
            rr = r * (1.0 if i % 2 == 0 else 0.8)
            V.append((math.cos(a) * rr, y0 - (0.08 if i % 2 == 0 else 0.0), math.sin(a) * rr))
        V.append((0.0, y0 + 0.08, 0.0))
        F = [(0, 1 + (i + 1) % n, 1 + i) for i in range(n)] + [(n + 1, 1 + i, 1 + (i + 1) % n) for i in range(n)]
        f = Forma(V, F)
        f.pintar(lambda c, nn, k=k: pal.escura2 if nn[1] < -0.2 else None)
        itens.append((f, pal.escura))
    return simples("arvore_escura", itens)


def pirulito():
    """Pirulito gigante (~2,6 m): disco azul com o redemoinho amarelo (as
    faces do disco giram com o raio: os gomos pintados viram espirais) e um
    lacinho vermelho no palito."""
    pal = PARQUE
    azul = cor("#1E88F0")
    R, esp, cy = 0.62, 0.22, 2.0
    setores = 16
    raios = [0.0, 0.12, 0.27, 0.42, 0.54, R]
    V, F, C = [], [], []
    torcao = 0.66          # quanto cada anel gira (em setores)
    for lado in (1, -1):
        base = len(V)
        z = lado * esp / 2
        V.append((0.0, cy, z))
        for j, r in enumerate(raios[1:], 1):
            for k in range(setores):
                a = 2 * PI * (k + torcao * j * lado) / setores
                V.append((math.cos(a) * r, cy + math.sin(a) * r, z))
        for k in range(setores):
            F.append((base, base + 1 + k, base + 1 + (k + 1) % setores))
            C.append(pal.amarelo if (k // 2) % 2 == 0 else azul)
        for j in range(1, len(raios) - 1):
            a0 = base + 1 + (j - 1) * setores
            a1 = base + 1 + j * setores
            for k in range(setores):
                k1 = (k + 1) % setores
                F += [(a0 + k, a1 + k, a0 + k1), (a0 + k1, a1 + k, a1 + k1)]
                cc = pal.amarelo if (k // 2) % 2 == 0 else azul
                C += [cc, cc]
    # borda: liga os dois aros de fora (cada lado tem 1 + anéis x setores vértices)
    por_lado = 1 + (len(raios) - 1) * setores
    o1 = [1 + (len(raios) - 2) * setores + k for k in range(setores)]
    o2 = [por_lado + 1 + (len(raios) - 2) * setores + k for k in range(setores)]
    # os aros de fora têm ângulos diferentes nos dois lados: costura pela ordem do ângulo
    ang1 = [math.atan2(V[i][1] - cy, V[i][0]) for i in o1]
    ang2 = [math.atan2(V[i][1] - cy, V[i][0]) for i in o2]
    ordem1 = [o1[i] for i in np.argsort(ang1)]
    ordem2 = [o2[i] for i in np.argsort(ang2)]
    for k in range(setores):
        k1 = (k + 1) % setores
        F += [(ordem1[k], ordem2[k], ordem1[k1]), (ordem1[k1], ordem2[k], ordem2[k1])]
        C += [azul, azul]
    disco = Forma(V, F, C)
    A_, B_, C_ = disco.V[disco.F[:, 0]], disco.V[disco.F[:, 1]], disco.V[disco.F[:, 2]]
    nf = np.cross(B_ - A_, C_ - A_)
    cen = (A_ + B_ + C_) / 3
    fora = cen - np.array([0, cy, 0])
    fora[:, 2] *= 6.0
    trocar = np.einsum("ij,ij->i", nf, fora) < 0
    disco.F[trocar] = disco.F[trocar][:, [0, 2, 1]]
    itens = [(disco, azul), (tubo([(0, 0, 0), (0, cy - 0.3, 0)], [0.06, 0.055], gomos=6), pal.creme)]
    # lacinho
    yl = cy - R - 0.08
    for s in (-1, 1):
        aba = extrudar([(0.0, 0.0), (s * 0.17, 0.08), (s * 0.19, -0.06)], -0.02, 0.02)
        itens.append((aba.mover((0, yl, 0.0)), pal.vermelho))
        ponta = extrudar([(0.0, 0.0), (s * 0.06, -0.16), (s * 0.12, -0.13)], -0.012, 0.012)
        itens.append((ponta.mover((0, yl, 0.0)), pal.vermelho))
    itens.append((icosfera(0.045, 0, centro=(0, yl, 0)), cor("#D93445")))
    return simples("pirulito", itens)


def cupcake():
    """Cupcake (~1,3 m): forminha azul pregueada (zigue-zague), bolo,
    três andares de creme, confeitos e a cereja."""
    pal = PARQUE
    gomos = 14
    perfil = [(0.32, 0.0), (0.42, 0.42)]
    V, F, C = [], [], []
    for (r, y) in perfil:
        for i in range(gomos):
            a = 2 * PI * i / gomos
            rr = r * (1.0 if i % 2 == 0 else 0.92)
            V.append((math.cos(a) * rr, y, math.sin(a) * rr))
    for i in range(gomos):
        i1 = (i + 1) % gomos
        F += [(i, gomos + i, i1), (i1, gomos + i, gomos + i1)]
        cc = pal.azul if i % 2 == 0 else cor("#36A6DC")
        C += [cc, cc]
    ic = len(V)
    V.append((0.0, 0.0, 0.0))
    for i in range(gomos):
        F.append((ic, i, (i + 1) % gomos))
        C.append(pal.azul)
    forminha = Forma(V, F, C)
    forminha = _orientar_fora_linha(forminha, [(0, -0.1, 0), (0, 0.5, 0)])
    itens = [(forminha, pal.azul)]
    itens.append((prisma(11, 0.44, 0.09, raio_topo=0.43, centro=(0, 0.4, 0)), cor("#C98A5A")))
    creme = cor("#FFF7E6")
    for (r, y, h) in ((0.41, 0.47, 0.2), (0.3, 0.64, 0.19), (0.18, 0.8, 0.17)):
        prof = [(r * 0.97, y - 0.02), (r, y + h * 0.3), (r * 0.92, y + h * 0.75), (r * 0.6, y + h)]
        anel = torno(prof, gomos=10, fase=0.5, tampa_baixo=False, tampa_cima=False)
        itens.append((anel, creme))
    itens.append((prisma(7, 0.12, 0.16, raio_topo=0.0, centro=(0, 0.95, 0)), creme))
    itens.append((esfera(0.095, 6, 5, centro=(0, 1.13, 0)), pal.vermelho))
    itens.append((tubo([(0, 1.2, 0), (0.03, 1.27, 0.0), (0.07, 1.31, 0)], [0.012, 0.011, 0.009], gomos=3), cor("#5A8A2A")))
    rnd = np.random.RandomState(5)
    cores = [pal.rosa, pal.amarelo, pal.azul, pal.turquesa]
    for k in range(5):
        a = 2 * PI * k / 5 + 0.3
        y = 0.6 + 0.06 * (k % 2)
        r = 0.36 - 0.06 * (k % 2)
        c = np.array([math.cos(a) * r, y + 0.03, math.sin(a) * r])
        g = caixa(0.07, 0.022, 0.022, centro=(0, 0, 0)).girar(rnd.uniform(-40, 40), math.degrees(-a) + 90, 0).mover(c)
        itens.append((g, cores[k % 4]))
    return simples("cupcake", itens)


def donut():
    """Rosquinha gigante (~1,6 m): massa dourada, cobertura rosa com a beirada
    escorrendo e confeitos coloridos."""
    pal = PARQUE
    R, r, y0 = 0.5, 0.28, 0.3
    gomos = 14
    perfil = [(R + math.cos(a) * r, y0 + math.sin(a) * r) for a in np.linspace(-PI / 2, 1.5 * PI, 8)]
    massa = torno(perfil, gomos=gomos, tampa_baixo=False, tampa_cima=False)
    itens = [(massa, cor("#E9A55B"))]
    # cobertura: de dentro, por cima, até a beirada de fora (que escorre em gotas)
    V, F = [], []
    angs = [2 * PI * (i + 0.5) / gomos for i in range(gomos)]
    perfil_c = [-0.92 * PI, -0.55 * PI, -0.25 * PI, 0.05 * PI, 0.32 * PI]   # do lado de dentro para fora
    perfil_c = [PI * 0.92, PI * 0.62, PI * 0.36, PI * 0.12]
    aneis = []
    for j, ph in enumerate(perfil_c):
        anel = []
        for i in range(gomos):
            rr = r * 1.06
            x = R + math.cos(ph) * rr
            y = y0 + math.sin(ph) * rr
            anel.append(len(V))
            V.append((math.cos(angs[i]) * x, y, math.sin(angs[i]) * x))
        aneis.append(anel)
    borda = []
    for i in range(gomos):
        ph = -0.12 * PI if i % 2 == 0 else 0.02 * PI
        rr = r * 1.06
        x = R + math.cos(ph) * rr
        y = y0 + math.sin(ph) * rr
        borda.append(len(V))
        V.append((math.cos(angs[i]) * x, y, math.sin(angs[i]) * x))
    aneis.append(borda)
    for A, B in zip(aneis[:-1], aneis[1:]):
        for i in range(gomos):
            j = (i + 1) % gomos
            F += [(A[i], B[i], A[j]), (A[j], B[i], B[j])]
    cob = Forma(V, F)
    A_, B_, C_ = cob.V[cob.F[:, 0]], cob.V[cob.F[:, 1]], cob.V[cob.F[:, 2]]
    nf = np.cross(B_ - A_, C_ - A_)
    cen = (A_ + B_ + C_) / 3
    rc = np.sqrt(cen[:, 0] ** 2 + cen[:, 2] ** 2)
    eixo = np.stack([cen[:, 0] / rc * R, np.full(len(cen), y0), cen[:, 2] / rc * R], 1)
    trocar = np.einsum("ij,ij->i", nf, cen - eixo) < 0
    cob.F[trocar] = cob.F[trocar][:, [0, 2, 1]]
    itens.append((cob, pal.rosa))
    cores = [pal.branco, pal.amarelo, pal.azul, pal.turquesa]
    for k in range(7):
        a = 2 * PI * k / 7 + 0.2
        ph = PI * (0.3 + 0.12 * (k % 3))
        x = R + math.cos(ph) * r * 1.1
        c = np.array([math.cos(a) * x, y0 + math.sin(ph) * r * 1.1 + 0.005, math.sin(a) * x])
        g = caixa(0.075, 0.022, 0.022).girar(0, math.degrees(-a) + 35 * (k % 3), 0).mover(c)
        itens.append((g, cores[k % 4]))
    return simples("donut", itens)


def presente():
    """Caixa de presente (~0,95 m): caixa azul, tampa, fita amarela cruzada e laço."""
    pal = PARQUE
    itens = [(caixa(0.72, 0.64, 0.72, chanfro=0.025, centro=(0, 0.32, 0)), pal.azul),
             (caixa(0.8, 0.14, 0.8, chanfro=0.025, centro=(0, 0.69, 0)), pal.azul)]
    for (sx, sz) in ((0.14, 0.734), (0.734, 0.14)):
        itens.append((caixa(sx, 0.62, sz, centro=(0, 0.32, 0)), pal.amarelo))
    for (sx, sz) in ((0.14, 0.814), (0.814, 0.14)):
        itens.append((caixa(sx, 0.145, sz, centro=(0, 0.69, 0)), pal.amarelo))
    yl = 0.79
    for s in (-1, 1):
        lobo = torno([(0.06, -0.035), (0.12, 0.0), (0.06, 0.035)], gomos=8, tampa_baixo=False, tampa_cima=False)
        lobo.V = np.stack([lobo.V[:, 0], lobo.V[:, 2], lobo.V[:, 1]], 1)       # de pé (anel no plano XY)
        lobo.inverter()
        lobo.escalar((1.15, 0.85, 1.0)).girar(0, 0, s * 28).mover((s * 0.13, yl + 0.07, 0))
        itens.append((lobo, pal.amarelo))
        rabo = extrudar([(0.0, 0.0), (0.05, 0.0), (0.09, -0.05), (0.04, -0.05)], -0.012, 0.012)
        if s < 0:
            rabo.espelhar_x()
        itens.append((rabo.girar(0, 0, -s * 10).mover((0, yl + 0.0, 0.0)), cor("#F2B92E")))
    itens.append((icosfera(0.06, 0, centro=(0, yl + 0.03, 0)), cor("#F2B92E")))
    return simples("presente", itens)


def bengala():
    """Bengala de doce (~2 m): o tubo gira um gomo a cada anel e as listras
    vermelhas seguem a hélice (borda reta)."""
    pal = PARQUE
    pts = [(0.0, y, 0.0) for y in np.arange(0.0, 1.6, 0.1)]
    for i in range(1, 10):
        a = PI * i / 9
        pts.append((0.28 - math.cos(a) * 0.28, 1.6 + math.sin(a) * 0.28, 0.0))
    pts.append((0.56, 1.5, 0.0))
    pts.append((0.56, 1.44, 0.0))
    f = tubo_torcido(pts, 0.085, 7, 0.8, lambda k: pal.vermelho if k in (0, 1, 4) else pal.branco)
    return simples("bengala", [(f, pal.branco)])


def cogumelo():
    """Cogumelo (~1 m): pé creme, chapéu vermelho de pintas brancas e a lamela embaixo."""
    pal = PARQUE
    pe = tubo([(0, 0, 0), (0, 0.3, 0), (0, 0.66, 0)], [0.21, 0.17, 0.15], gomos=9)
    chapeu = torno([(0.58, 0.6), (0.57, 0.7), (0.5, 0.84), (0.37, 0.96), (0.2, 1.03), (0.0, 1.05)], gomos=12, fase=0.5,
                   tampa_baixo=False)
    lamela = torno([(0.15, 0.62), (0.58, 0.6)], gomos=12, fase=0.5, tampa_baixo=False, tampa_cima=False)   # virada para baixo
    itens = [(pe, pal.creme), (chapeu, pal.vermelho), (lamela, cor("#F2DDBB"))]
    sup = SupMalha(chapeu, (0, 0.6, 0))
    for (d, rr) in (((0, 1, 0), 0.1), ((0.75, 0.55, 0.3), 0.085), ((-0.6, 0.6, 0.5), 0.08), ((0.2, 0.5, -0.85), 0.08),
                    ((-0.5, 0.5, -0.65), 0.07), ((0.6, 0.45, -0.55), 0.065), ((-0.1, 0.55, 0.85), 0.075),
                    ((0.85, 0.42, 0.42), 0.055), ((-0.9, 0.38, -0.1), 0.06)):
        itens.append((adesivo(sup, circulo2d(rr, 7), d, 0.004, 0.01, paredes=False), pal.branco))
    return simples("cogumelo", itens)


def nuvem():
    """Nuvem fofa facetada (~3,9 m de largura, como nas edições Godot), de
    base achatada e barriga azulada."""
    bolas = [((0.0, 0.62, 0.0), 0.75), ((-0.88, 0.48, 0.06), 0.56), ((0.92, 0.46, -0.05), 0.6), ((-0.35, 0.95, -0.02), 0.56),
             ((0.42, 0.9, 0.06), 0.52), ((1.58, 0.36, 0.0), 0.38), ((-1.55, 0.34, 0.02), 0.36)]
    itens = []
    for (c, r) in bolas:
        f = icosfera(r, 1, centro=c)
        f.V[:, 2] = f.V[:, 2] * 0.78
        f.V[:, 1] = np.maximum(f.V[:, 1], 0.22)
        f.pintar(lambda cc, n: cor("#DCE7F7") if n[1] < -0.5 else None)
        itens.append((f, cor("#FFFFFF")))
    return simples("nuvem", itens)


def balao():
    """Balão de festa (~0,55 m, com TINTA: branco), origem embaixo, no nó.
    Brilho "luz" no alto e o fio (parte "fio", sem luz) pendurado.
    (Na marca do Parque, Mundo.cs: a logo fica em z = 0,21 local; o balão vai
    até z = 0,19.)"""
    perfil = [(0.05, 0.065), (0.12, 0.11), (0.18, 0.17), (0.21, 0.24), (0.22, 0.31), (0.208, 0.39), (0.17, 0.46),
              (0.1, 0.515), (0.0, 0.54)]
    corpo = torno(perfil, gomos=12, fase=0.5)
    corpo.escalar((1.0, 1.0, 0.86))
    no = prisma(6, 0.04, 0.075, raio_topo=0.055)
    itens = [(corpo, cor("#F4F4F4")), (no, cor("#E2E2E2"))]
    sup = Elip((0, 0.3, 0), (0.22, 0.24, 0.19))
    brilho = [(adesivo(sup, circulo2d(1.0, 8, escala=(0.035, 0.06)), (0.55, 0.6, 0.6), 0.004, 0.01, 25, paredes=False),
               BRILHO)]
    fio = tubo(bezier((0, 0.01, 0), (0.04, -0.3, 0.0), (-0.05, -0.55, 0.02), (0.02, -0.85, 0.0), n=6),
               0.006, gomos=3, tampas=False)
    return montar("balao", [("balao", None, (0, 0, 0), itens, ""), ("brilho", "balao", (0, 0, 0), brilho, "luz"),
                            ("fio", "balao", (0, 0, 0), [(fio, cor("#F2F2F2"))], "luz")])


def orientar_fora(f, centro_fn):
    """Vira as faces cuja normal não aponta para longe de centro_fn(centro_da_face)."""
    A, B, C = f.V[f.F[:, 0]], f.V[f.F[:, 1]], f.V[f.F[:, 2]]
    nf = np.cross(B - A, C - A)
    cen = (A + B + C) / 3
    ref = np.array([centro_fn(c) for c in cen])
    trocar = np.einsum("ij,ij->i", nf, cen - ref) < 0
    f.F[trocar] = f.F[trocar][:, [0, 2, 1]]
    return f


def duas_faces(V, tris, cores, fora_fn, escurecer=0.75):
    """Lâmina de duas faces (sanefas, velas): cada triângulo vira dois, um
    para cada lado; o lado para onde fora_fn(centro) aponta leva a cor e o
    avesso a cor escurecida."""
    V = np.asarray(V, float)
    F, C = [], []
    for tri, cc in zip(tris, cores):
        a, b, c = V[list(tri)]
        if np.dot(np.cross(b - a, c - a), fora_fn((a + b + c) / 3.0)) < 0:
            tri = (tri[0], tri[2], tri[1])
        F += [tuple(tri), (tri[0], tri[2], tri[1])]
        C += [cc, mais_escura(cc, escurecer)]
    return Forma(V, F, C)


def lampada(c, n, r=0.04, lados=6):
    """Lâmpada: disquinho (para peças "luz") virado para n."""
    return placa(circulo2d(r, lados), np.asarray(c, float), n, 0.012)


def arco_iris():
    """Arco-íris (6,8 m x 3,4 m) atrás dos pinos (raio 3 ± 0,42, como nas
    edições Godot): seis arcos de cor em degraus (os do meio mais grossos:
    perfil quase redondo) e o trilho roxo de dentro com as lâmpadas (peça
    "luz", raio 2,5, viradas para +Z)."""
    cores = [cor(h) for h in ("#FF4B5C", "#FF9F2E", "#FFD43B", "#5ED16A", "#4FC3F7", "#2E5BE6")]
    R, r = 3.0, 0.42
    w = 2 * r / 6
    fundo = [0.22, 0.31, 0.37, 0.37, 0.31, 0.22]
    segs = 16
    angs = [PI * j / segs for j in range(segs + 1)]
    itens = []

    def faixa_arco(r_in, r_out, z0, z1, lados=("frente", "tras", "fora", "dentro")):
        """Pedaço de arco (meio anel de r_in a r_out, de z0 a z1) só com os lados pedidos."""
        V, F = [], []

        def p(rr, a, z):
            V.append((math.cos(a) * rr, math.sin(a) * rr, z))
            return len(V) - 1
        for j in range(segs):
            a0, a1 = angs[j], angs[j + 1]
            if "frente" in lados:
                q = [p(r_in, a0, z1), p(r_out, a0, z1), p(r_out, a1, z1), p(r_in, a1, z1)]
                F += [(q[0], q[1], q[2]), (q[0], q[2], q[3])]
            if "tras" in lados:
                q = [p(r_in, a0, z0), p(r_out, a0, z0), p(r_out, a1, z0), p(r_in, a1, z0)]
                F += [(q[0], q[2], q[1]), (q[0], q[3], q[2])]
            if "fora" in lados:
                q = [p(r_out, a0, z0), p(r_out, a0, z1), p(r_out, a1, z1), p(r_out, a1, z0)]
                F += [(q[0], q[1], q[2]), (q[0], q[2], q[3])]
            if "dentro" in lados:
                q = [p(r_in, a0, z0), p(r_in, a0, z1), p(r_in, a1, z1), p(r_in, a1, z0)]
                F += [(q[0], q[1], q[2]), (q[0], q[2], q[3])]
        f = orientar_fora(Forma(V, F), lambda c, zc=(z0 + z1) / 2, rc=(r_in + r_out) / 2: _centro_arco(c, rc, zc))
        if "pontas" in lados:
            # as duas pontas, no chão, viradas para baixo
            Vp, Fp = [], []
            for a in (angs[0], angs[-1]):
                k = len(Vp)
                Vp += [(math.cos(a) * rr, 0.0, zz) for (rr, zz) in ((r_in, z0), (r_out, z0), (r_out, z1), (r_in, z1))]
                Fp += [(k, k + 1, k + 2), (k, k + 2, k + 3)]
            pontas = orientar_fora(Forma(Vp, Fp), lambda c: c + np.array([0.0, 1.0, 0.0]))
            f = Forma(np.concatenate([f.V, pontas.V]), np.concatenate([f.F, pontas.F + len(f.V)]))
        return f
    for i in range(6):
        r_out = R + r - i * w
        r_in = r_out - w
        d = fundo[i]
        itens.append((faixa_arco(r_in, r_out, -d, d, ("frente", "tras", "pontas")), cores[i]))
        if i == 0:
            itens.append((faixa_arco(r_in, r_out, -d, d, ("fora",)), cores[i]))
        if i == 5:
            itens.append((faixa_arco(r_in, r_out, -d, d, ("dentro",)), cores[i]))
        if i > 0:
            dv = fundo[i - 1]
            if d > dv:          # degrau: a de dentro é mais funda — mostra o lado de fora dela
                for (z0, z1) in ((dv, d), (-d, -dv)):
                    itens.append((faixa_arco(r_in, r_out, z0, z1, ("fora",)), cores[i]))
            elif dv > d:
                ro = r_out + w
                for (z0, z1) in ((d, dv), (-dv, -d)):
                    itens.append((faixa_arco(r_out, ro, z0, z1, ("dentro",)), cores[i - 1]))
    # trilho roxo de dentro (o "anil" que faltava), com as lâmpadas
    roxo = cor("#8A5CFF")
    itens.append((faixa_arco(2.42, R - r, -0.17, 0.17, ("frente", "tras", "dentro", "pontas")), roxo))
    luzes = []
    for k in range(15):
        a = PI * (k + 0.5) / 15
        c = np.array([math.cos(a) * 2.5, math.sin(a) * 2.5, 0.172])
        luzes.append((lampada(c, (0, 0, 1), 0.05), cor("#FFE36A")))
    return simples("arco_iris", itens, luz=luzes)


def _centro_arco(c, rc, zc):
    """O ponto do arco (raio rc, no plano z = zc) no mesmo ângulo de c."""
    a = math.atan2(c[1], c[0])
    return np.array([math.cos(a) * rc, math.sin(a) * rc, zc])


def janela_arco(larg, alt, n=6):
    """Contorno de porta/janela em arco (retângulo com meio círculo em cima), base em y = 0."""
    pts = [(larg / 2, 0.0), (larg / 2, alt - larg / 2)]
    for i in range(1, n):
        a = PI * i / n
        pts.append((math.cos(a) * larg / 2, alt - larg / 2 + math.sin(a) * larg / 2))
    pts += [(-larg / 2, alt - larg / 2), (-larg / 2, 0.0)]
    return pts


def bandeirinha(topo, haste, comp, larg, cor_band, cor_haste=PARQUE.branco, cor_bola=PARQUE.amarelo):
    """Mastro + bolinha + bandeira triangular (para +X)."""
    topo = np.asarray(topo, float)
    itens = [(tubo([topo, topo + (0, haste, 0)], 0.025, gomos=5), cor_haste),
             (icosfera(0.055, 0, centro=topo + (0, haste + 0.04, 0)), cor_bola)]
    b = extrudar([(0.0, 0.0), (comp, -larg * 0.45), (0.0, -larg)], -0.012, 0.012)
    b.deformar(lambda V: V + np.stack([V[:, 0] * 0, V[:, 0] * 0, 0.05 * np.sin(V[:, 0] / comp * PI * 1.5)], 1))
    itens.append((b.mover(topo + (0.02, haste - 0.02, 0)), cor_band))
    return itens


def torre():
    """Torre de circo (~4,3 m) dos lados dos pinos: listras vermelhas em
    espiral (o tubo torce), janela e porta em arco, ameias e telhado azul
    em gomos, bandeirinha amarela."""
    pal = PARQUE
    branco, vermelho, azul = cor("#FFFFFF"), cor("#E8384F"), cor("#1E6FE0")
    pts = [(0, y, 0) for y in np.linspace(0.12, 2.5, 11)]
    corpo = tubo_torcido(pts, 0.55, 14, 0.3, lambda k: vermelho if k % 2 == 0 else branco, tampas=False)
    # o tubo torcido tem quadrinhos tortos: normais lisas em volta (como o pino), listras nítidas
    itens = [(corpo, branco, 40), (prisma(12, 0.62, 0.2, chanfro=0.03, fase=0.5), azul)]
    yc = 1.2
    cil = Elip((0, yc, 0), (0.55, 60.0, 0.55))
    for (y0, larg, alt) in ((1.55, 0.3, 0.5), (0.1, 0.42, 0.74)):
        moldura = [(x * (larg + 0.09) / larg, y * (alt + 0.05) / alt + y0 - 0.025 - yc) for (x, y) in janela_arco(larg, alt)]
        itens.append((adesivo(cil, moldura, (0, 0, 1), 0.006, 0.04), pal.amarelo))
        vao = [(x, y + y0 - yc) for (x, y) in janela_arco(larg, alt)]
        itens.append((adesivo(cil, vao, (0, 0, 1), 0.012, 0.04), cor("#0E2A5C")))
    # ameias
    itens.append((prisma(12, 0.66, 0.16, chanfro=0.025, fase=0.5, centro=(0, 2.48, 0)), branco))
    for k in range(6):
        a = 2 * PI * k / 6 + PI / 12
        c = (math.cos(a) * 0.58, 2.7, math.sin(a) * 0.58)
        m = caixa(0.2, 0.16, 0.14, chanfro=0.02, centro=(0, 0, 0)).girar(0, -math.degrees(a) + 90, 0).mover(c)
        itens.append((m, vermelho if k % 2 == 0 else branco))
    teto = prisma(12, 0.7, 1.22, raio_topo=0.0, fase=0.5, centro=(0, 2.62, 0))
    teto.pintar(lambda c, n: cor("#3D8BF0") if int((math.atan2(c[2], c[0]) + PI) / (2 * PI) * 12) % 2 == 0 else None)
    itens.append((teto, azul))
    itens += bandeirinha((0, 3.8, 0), 0.5, 0.36, 0.22, pal.amarelo, branco, pal.amarelo)
    return simples("torre", itens)


def tenda():
    """Tenda de circo (~3,4 m): lona em gomos turquesa e amarelo, telhado
    côncavo, sanefa de babados, porta em arco e a bandeirinha."""
    pal = PARQUE
    N = 16
    perfil = [(1.37, -0.1), (1.37, 0.65), (1.38, 1.3), (1.52, 1.5), (1.3, 1.62), (1.02, 1.8), (0.78, 2.0), (0.58, 2.2),
              (0.41, 2.4), (0.27, 2.56), (0.15, 2.68), (0.07, 2.76), (0.0, 2.8)]
    lona = torno(perfil, gomos=N, fase=0.0, tampa_baixo=False)

    def gomo(c):
        return int(((math.atan2(c[2], c[0]) + 2 * PI) % (2 * PI)) / (2 * PI) * N)
    lona.pintar(lambda c, n: pal.turquesa if gomo(c) % 2 == 0 else pal.amarelo)
    itens = [(lona, pal.turquesa)]
    # sanefa: um babado por gomo, das duas cores, com o avesso mais escuro
    V, T, C = [], [], []
    for i in range(N):
        a0, a1 = 2 * PI * i / N, 2 * PI * (i + 1) / N
        am = (a0 + a1) / 2
        rr = 1.535
        k = len(V)
        V += [(math.cos(a0) * rr, 1.5, math.sin(a0) * rr), (math.cos(a1) * rr, 1.5, math.sin(a1) * rr),
              (math.cos(a0) * rr, 1.32, math.sin(a0) * rr), (math.cos(a1) * rr, 1.32, math.sin(a1) * rr),
              (math.cos(am) * rr * 1.003, 1.17, math.sin(am) * rr * 1.003)]
        cc = pal.amarelo if i % 2 == 0 else pal.vermelho
        T += [(k, k + 2, k + 4), (k, k + 4, k + 1), (k + 1, k + 4, k + 3)]
        C += [cc] * 3
    sanefa = duas_faces(V, T, C, lambda c: np.array([c[0], 0.0, c[2]]))
    itens.append((sanefa, pal.amarelo))
    yc = 0.5
    cil = Elip((0, yc, 0), (1.37, 60.0, 1.37))
    porta = [(x, y - yc) for (x, y) in janela_arco(0.62, 0.92)]
    moldura = [(x * 0.78 / 0.62, (y + yc) * 1.02 / 0.92 - yc) for (x, y) in porta]
    itens.append((adesivo(cil, moldura, (0, 0, 1), 0.008, 0.04), pal.creme))
    itens.append((adesivo(cil, porta, (0, 0, 1), 0.016, 0.04), cor("#4A2C7A")))
    itens += bandeirinha((0, 2.74, 0), 0.6, 0.48, 0.3, cor("#FF6F61"))
    return simples("tenda", itens)


def ponei(c, frente, cor_crina, cor_sela):
    """Cavalinho de carrossel (branco, crina colorida, sela vermelha, cascos
    dourados), centro do corpo em c, olhando para `frente` (horizontal)."""
    c = np.asarray(c, float)
    f = _norm(frente)
    lado = np.cross((0, 1, 0), f)
    up = np.array([0.0, 1.0, 0.0])
    R = np.stack([lado, up, f], axis=1)

    def P(x, y, z):
        return c + lado * x + up * y + f * z
    branco = cor("#FFFFFF")
    ouro = cor("#FFD43B")
    itens = [(Elip(c, (0.11, 0.115, 0.22), R).malha(8, 6, polo="z"), branco)]
    itens.append((tubo([P(0, 0.04, 0.13), P(0, 0.2, 0.24)], [0.07, 0.06], gomos=6, tampas=False), branco))
    cab = Elip(P(0, 0.25, 0.3), (0.062, 0.068, 0.12), R @ rot(32, 0, 0))
    itens.append((cab.malha(6, 5, polo="z"), branco))
    for s in (-1, 1):
        itens.append((tubo([P(s * 0.035, 0.3, 0.25), P(s * 0.045, 0.37, 0.23)], [0.022, 0.0], gomos=4), branco))
        olho_ = adesivo(cab, circulo2d(0.014, 6), R @ np.array([s * 0.8, 0.45, 0.35]), 0.003, 0.008, paredes=False)
        itens.append((olho_, OLHO))
    crina = [P(0, 0.33, 0.22), P(0, 0.27, 0.13), P(0, 0.2, 0.07), P(0, 0.12, 0.02)]
    itens.append((tubo(crina, [0.035, 0.045, 0.045, 0.03], gomos=5), cor_crina))
    itens.append((tubo([P(0, 0.06, -0.2), P(0, -0.04, -0.3), P(0, -0.14, -0.33)], [0.045, 0.04, 0.02], gomos=5), cor_crina))
    itens.append((Elip(P(0, 0.105, -0.02), (0.115, 0.03, 0.09), R).malha(6, 3, polo="z"), cor_sela))
    for s in (-1, 1):
        # pernas da frente dobradas, as de trás esticadas (galope), cascos dourados
        j = P(s * 0.06, -0.12, 0.2)
        itens.append((tubo([P(s * 0.06, -0.05, 0.14), j, P(s * 0.06, -0.12, 0.27)], [0.035, 0.03, 0.03], gomos=5,
                           tampas=False), branco))
        itens.append((tubo([P(s * 0.06, -0.12, 0.26), P(s * 0.06, -0.12, 0.32)], [0.034, 0.03], gomos=5), ouro))
        itens.append((tubo([P(s * 0.06, -0.05, -0.14), P(s * 0.06, -0.19, -0.23)], [0.035, 0.03], gomos=5, tampas=False),
                      branco))
        itens.append((tubo([P(s * 0.06, -0.18, -0.225), P(s * 0.06, -0.23, -0.255)], [0.034, 0.03], gomos=5), ouro))
    return itens


def carrossel():
    """Carrossel (~3,1 m). Partes: "base" (o tablado parado) > "topo" (o
    que gira em volta do eixo Y: piso, coluna, postes listrados, quatro
    cavalinhos, teto em gomos, sanefa e bandeirinha) > "luzes" (lâmpadas
    do beiral, peça "luz"). O pivô do topo fica no eixo, em cima da base."""
    pal = PARQUE
    turquesa, creme, roxo = pal.turquesa, cor("#FFF1B8"), cor("#F1EAFF")
    vermelho, amarelo = cor("#E8384F"), pal.amarelo
    base = [(prisma(16, 1.36, 0.2, chanfro=0.03, fase=0.5), turquesa)]
    base[0][0].pintar(lambda c, n: cor("#22A99D") if c[1] < 0.1 and abs(n[1]) < 0.5 else None)
    PIV = np.array([0.0, 0.2, 0.0])
    topo = []
    piso = prisma(16, 1.24, 0.08, chanfro=0.015, fase=0.5, centro=(0, 0.2, 0))
    piso.pintar(lambda c, n: creme if n[1] > 0.7 else (vermelho if int((math.atan2(c[2], c[0]) + PI) / (2 * PI) * 16) % 2 else amarelo))
    topo.append((piso, creme))
    col = prisma(10, 0.2, 1.56, raio_topo=0.18, fase=0.5, centro=(0, 0.28, 0))
    topo.append((col, roxo))
    for y in (0.34, 1.0, 1.72):
        topo.append((prisma(10, 0.235, 0.07, chanfro=0.015, fase=0.5, centro=(0, y, 0)), amarelo))
    crinas = [cor("#FF6F61"), cor("#2EC4B6"), cor("#9BE15D"), cor("#4FC3F7")]
    for i in range(4):
        a = PI / 4 + i * PI / 2
        pos = np.array([math.cos(a) * 0.86, 0.0, math.sin(a) * 0.86])
        poste = tubo_torcido([pos + (0, y, 0) for y in np.linspace(0.28, 1.84, 6)], 0.034, 6, 0.75,
                             lambda k: amarelo if k % 2 == 0 else cor("#FFFFFF"), tampas=False)
        topo.append((poste, amarelo))
        frente = np.array([-pos[2], 0.0, pos[0]])         # para onde o topo gira (Temas.cs: -vel em Y)
        y = 0.92 if i % 2 == 0 else 0.8
        topo += ponei(pos + np.array([0, y, 0]), frente, crinas[i], vermelho)
    # teto: forro, telhado em gomos vermelho/amarelo, sanefa e bandeirinha
    forro = torno([(0.2, 1.84), (1.3, 1.84)], gomos=12, fase=0.5, tampa_baixo=False, tampa_cima=False)    # virado para baixo
    topo.append((forro, creme))
    perfil = [(1.3, 1.83), (1.42, 1.88), (1.45, 1.96), (1.34, 2.02), (1.02, 2.18), (0.64, 2.36), (0.33, 2.5), (0.12, 2.58),
              (0.0, 2.62)]
    teto = torno(perfil, gomos=12, fase=0.0, tampa_baixo=False)
    teto.pintar(lambda c, n: vermelho if int(((math.atan2(c[2], c[0]) + 2 * PI) % (2 * PI)) / (2 * PI) * 12) % 2 else amarelo)
    topo.append((teto, vermelho))
    V, T, C = [], [], []
    N = 12
    for i in range(N):
        a0, a1 = 2 * PI * i / N, 2 * PI * (i + 1) / N
        am = (a0 + a1) / 2
        rr = 1.43
        k = len(V)
        V += [(math.cos(a0) * rr, 1.86, math.sin(a0) * rr), (math.cos(a1) * rr, 1.86, math.sin(a1) * rr),
              (math.cos(a0) * rr, 1.72, math.sin(a0) * rr), (math.cos(a1) * rr, 1.72, math.sin(a1) * rr),
              (math.cos(am) * rr, 1.62, math.sin(am) * rr)]
        T += [(k, k + 2, k + 4), (k, k + 4, k + 1), (k + 1, k + 4, k + 3)]
        C += [amarelo if i % 2 else vermelho] * 3
    topo.append((duas_faces(V, T, C, lambda c: np.array([c[0], 0.0, c[2]]), escurecer=0.85), amarelo))
    topo += bandeirinha((0, 2.6, 0), 0.5, 0.4, 0.26, cor("#FF6F61"))
    luzes = []
    for k in range(24):
        a = 2 * PI * (k + 0.5) / 24
        d = np.array([math.cos(a), 0.0, math.sin(a)])
        luzes.append((lampada(d * 1.452 + (0, 1.92, 0), d, 0.035, 5), cor("#FFE36A")))
    for k in range(16):
        a = 2 * PI * (k + 0.5) / 16
        d = np.array([math.cos(a), 0.0, math.sin(a)])
        luzes.append((lampada(d * 1.242 + (0, 0.24, 0), d, 0.025, 5), cor("#FFE36A")))
    return montar("carrossel", [
        ("base", None, (0, 0, 0), base, ""),
        ("topo", "base", PIV, topo, ""),
        ("luzes", "topo", PIV, luzes, "luz"),
    ])


EIXO_RODA = 4.6     # altura do eixo da roda-gigante (Temas.cs)
RAIO_RODA = 3.8     # onde as cabines penduram (Temas.cs)
Z_ARO = 0.5         # a roda tem dois aros (z = ±0,5); a cabine passa entre eles
Z_CAVALETE = 0.85   # os cavaletes (pernas em A) ficam em z = ±0,85


def anel_tubo(R, r, segs, gomos, z, cor_fn, fase=0.0):
    """Aro fechado (tubo em volta de um círculo no plano XY, em z). cor_fn(ângulo) -> cor."""
    V, F, C = [], [], []
    for j in range(segs):
        a = 2 * PI * (j + fase) / segs
        u = np.array([math.cos(a), math.sin(a), 0.0])
        for k in range(gomos):
            b = 2 * PI * k / gomos
            V.append(u * (R + r * math.cos(b)) + np.array([0, 0, z + r * math.sin(b)]))
    for j in range(segs):
        j1 = (j + 1) % segs
        am = 2 * PI * (j + 0.5 + fase) / segs
        cc = cor_fn(am)
        for k in range(gomos):
            k1 = (k + 1) % gomos
            a_, b_, c_, d_ = j * gomos + k, j * gomos + k1, j1 * gomos + k, j1 * gomos + k1
            F += [(a_, c_, b_), (b_, c_, d_)]
            C += [cc, cc]
    f = Forma(V, F, C)
    return orientar_fora(f, lambda c: np.array([math.cos(math.atan2(c[1], c[0])) * R, math.sin(math.atan2(c[1], c[0])) * R, z]))


CORES_RODA = [cor("#2EC4B6"), cor("#FFD43B"), cor("#E8384F"), cor("#FF9F2E")]


def _cor_setor(a):
    """A cor do setor de 45° centrado no raio mais perto (o raio 0 aponta para cima)."""
    i = int(round(((a - PI / 2) % (2 * PI)) / (PI / 4))) % 8
    return CORES_RODA[i % 4]


def roda():
    """Roda-gigante (origem no eixo, gira em Z; Temas.cs): dois aros coloridos
    (z = ±0,5), aros de dentro, raios, as 8 barras onde penduram as cabines
    (raio 3,8, nos ângulos de 45 em 45 graus, uma para cima), o cubo amarelo de
    estrela vermelha e as lâmpadas (peça "luz")."""
    branco = cor("#FFFFFF")
    itens = []
    for z in (-Z_ARO, Z_ARO):
        itens.append((anel_tubo(RAIO_RODA, 0.11, 24, 5, z, _cor_setor, fase=0.5), branco))
        itens.append((anel_tubo(3.3, 0.075, 16, 5, z, _cor_setor, fase=0.5), branco))
    for i in range(8):
        a = PI / 2 + i * PI / 4
        d = np.array([math.cos(a), math.sin(a), 0.0])
        d2 = np.array([math.cos(a + PI / 8), math.sin(a + PI / 8), 0.0])
        for z in (-Z_ARO, Z_ARO):
            zz = np.array([0.0, 0.0, z])
            itens.append((tubo([d * 0.4 + zz * 0.55, d * (RAIO_RODA - 0.05) + zz], [0.07, 0.06], gomos=5, tampas=False),
                          CORES_RODA[i % 4]))
            itens.append((tubo([d2 * 3.3 + zz, d2 * RAIO_RODA + zz], 0.05, gomos=4, tampas=False), branco))
        itens.append((tubo([d * RAIO_RODA - (0, 0, Z_ARO + 0.1), d * RAIO_RODA + (0, 0, Z_ARO + 0.1)], 0.06, gomos=6),
                      branco))
    cubo = prisma(12, 0.45, 0.62, chanfro=0.05, fase=0.5).mover((0, -0.31, 0)).girar(90, 0, 0)
    itens.append((cubo, cor("#FFD43B")))
    for z in (0.31, -0.31):
        n = np.array([0.0, 0.0, 1.0 if z > 0 else -1.0])
        itens.append((placa(estrela2d(5, 0.3, 0.13), np.array([0, 0, z]), n, 0.012), cor("#E8384F")))
    luzes = []
    for z in (-Z_ARO, Z_ARO):
        for k in range(16):
            a = PI / 2 + PI / 8 * (k + 0.5)
            c = np.array([math.cos(a) * RAIO_RODA, math.sin(a) * RAIO_RODA, z + np.sign(z) * 0.112])
            luzes.append((lampada(c, (0, 0, np.sign(z)), 0.05, 5), cor("#FFF6D0")))
    return simples("roda", itens, luz=luzes)


def roda_base():
    """Os cavaletes da roda-gigante (eixo a 4,6 m): pernas em A brancas, barras
    turquesa, o eixo amarelo, o tablado e uma escadinha na frente."""
    branco, turquesa, amarelo = cor("#FFFFFF"), cor("#2EC4B6"), cor("#FFD43B")
    pe_x = 2.35
    itens = []
    for z in (-Z_CAVALETE, Z_CAVALETE):
        for s in (-1, 1):
            itens.append((tubo([(0, EIXO_RODA, z), (s * pe_x, 0.1, z)], [0.12, 0.14], gomos=8), branco))
        y = 1.7
        xb = pe_x * (1 - (y - 0.1) / (EIXO_RODA - 0.1))
        itens.append((tubo([(-xb, y, z), (xb, y, z)], 0.08, gomos=6), turquesa))
        y = 3.1
        xb = pe_x * (1 - (y - 0.1) / (EIXO_RODA - 0.1))
        itens.append((tubo([(-xb, y, z), (xb, y, z)], 0.07, gomos=6), turquesa))
        cubo = prisma(10, 0.26, 0.24, chanfro=0.04, fase=0.5).mover((0, -0.12, 0)).girar(90, 0, 0).mover((0, EIXO_RODA, z))
        itens.append((cubo, turquesa))
    itens.append((tubo([(0, EIXO_RODA, -Z_CAVALETE - 0.2), (0, EIXO_RODA, Z_CAVALETE + 0.2)], 0.11, gomos=8), amarelo))
    for s in (-1, 1):
        itens.append((caixa(0.62, 0.28, 2.2, chanfro=0.04, centro=(s * pe_x, 0.14, 0)), turquesa))
    tablado = caixa(4.6, 0.1, 2.3, chanfro=0.025, centro=(0, 0.02, 0))
    tablado.pintar(lambda c, n: turquesa if abs(n[1]) < 0.5 else None)
    itens.append((tablado, branco))
    for k in range(2):
        itens.append((caixa(0.9, 0.06, 0.3, chanfro=0.015, centro=(0, 0.03 - k * 0.04, 1.3 + k * 0.28)), amarelo))
    return simples("roda_base", itens)


def cabine():
    """Cabine da roda-gigante (TINTA: branca e cinza-clarinho). A origem é o
    ponto onde pendura (em cima, na barra da roda): tudo fica para baixo
    (até -0,68) e dentro de |z| < 0,28 (passa entre os aros, z = ±0,5)."""
    branco, cinza, cinza2 = cor("#F4F4F4"), cor("#D9D9E3"), cor("#E6E6EE")
    itens = [(icosfera(0.075, 0), cinza), (tubo([(0, -0.02, 0), (0, -0.16, 0)], 0.026, gomos=5, tampas=False), cinza)]
    teto = prisma(8, 0.42, 0.13, raio_topo=0.08, fase=0.5, centro=(0, -0.24, 0))
    teto.escalar((1.0, 1.0, 0.68))
    teto.pintar(lambda c, n: cinza2 if int((math.atan2(c[2], c[0]) + PI) / (2 * PI) * 8) % 2 else None)
    itens.append((teto, branco))
    for s in (-1, 1):
        itens.append((tubo([(s * 0.3, -0.24, 0), (s * 0.33, -0.47, 0)], 0.022, gomos=4, tampas=False), cinza))
    fora = [(0.3, -0.68), (0.38, -0.56), (0.405, -0.44)]
    tina = torno(fora + [(0.36, -0.44), (0.31, -0.58)], gomos=8, fase=0.5, tampa_baixo=True, tampa_cima=True)
    tina.escalar((1.0, 1.0, 0.68))
    tina.pintar(lambda c, n: cinza if (c[1] > -0.455 or n[1] > 0.5) else None)
    itens.append((tina, branco))
    itens.append((caixa(0.44, 0.05, 0.12, centro=(0, -0.55, -0.08)), cinza))
    return simples("cabine", itens)


# =================================================================== ESPAÇO


def linha_da_esfera(y, aneis):
    """Em que fileira (0 = a de cima) da esfera UV (raio 1, `aneis` anéis) cai a altura y."""
    b = math.acos(max(-1.0, min(1.0, y)))
    return min(int(b / PI * aneis), aneis - 1)


# o eixo do anel do planeta (Temas.cs gira o planeta em volta dele, no espaço do modelo)
EIXO_ANEL = _norm((0.336, 0.894, 0.296))


def planeta_anel():
    """Planeta com anel (origem no centro): esfera de raio 1 em faixas lilás
    (os anéis da malha em volta do eixo do anel: faixas nítidas) e o anel
    azul-gelo de três faixas, perpendicular a EIXO_ANEL (o eixo do
    "girar_proprio" do Temas.cs). Na marca do Espaço (Mundo.cs, escala 0,3 e
    tinta lavanda) fica tudo roxo-azulado."""
    R = eixo_para((0, 1, 0), EIXO_ANEL)
    lilas, claro, escuro = cor("#9D8CFF"), cor("#C9BEFF"), cor("#7A66EE")
    esf = esfera(1.0, 18, 12)
    fileiras = [escuro, escuro, None, claro, None, None, escuro, None, claro, None, escuro, escuro]
    esf.pintar(lambda c, n: fileiras[linha_da_esfera(c[1] / max(np.linalg.norm(c), 1e-9), 12)])
    esf.V = esf.V @ R.T
    itens = [(esf, lilas)]
    segs = 36
    for (r0, r1, c_) in ((1.3, 1.42, cor("#E6FAFF")), (1.42, 1.62, cor("#9FE6FF")), (1.62, 1.76, cor("#5EC4F2"))):
        V, F = [], []
        for j in range(segs):
            a = 2 * PI * j / segs
            ca, sa = math.cos(a), math.sin(a)
            V += [(ca * r0, 0.03, sa * r0), (ca * r1, 0.03, sa * r1), (ca * r0, -0.03, sa * r0), (ca * r1, -0.03, sa * r1)]
        for j in range(segs):
            a, b = 4 * j, 4 * ((j + 1) % segs)
            F += [(a, b, a + 1), (a + 1, b, b + 1), (a + 2, a + 3, b + 2), (a + 3, b + 3, b + 2)]
            if r1 > 1.7:
                F += [(a + 1, b + 1, a + 3), (a + 3, b + 1, b + 3)]
            if r0 < 1.31:
                F += [(a, a + 2, b), (a + 2, b + 2, b)]
        f = Forma(V, F)
        f = orientar_fora(f, lambda c, rm=(r0 + r1) / 2: np.array([c[0], 0, c[2]]) / max(math.hypot(c[0], c[2]), 1e-9) * rm)
        f.V = f.V @ R.T
        itens.append((f, c_))
    return simples("planeta_anel", itens)


def foguete():
    """Foguete (~3,1 m): corpo branco de nariz coral, faixas turquesa, três
    aletas coral, a janela redonda de moldura roxa, a estrela amarela e o
    bocal lilás."""
    branco, coral, turq = cor("#FBF8FF"), cor("#FF6F61"), cor("#2EC4B6")
    perfil = [(0.27, 0.3), (0.36, 0.38), (0.47, 0.5), (0.56, 0.68), (0.6, 0.8), (0.615, 1.0), (0.62, 1.3), (0.605, 1.75),
              (0.58, 2.0), (0.52, 2.3), (0.495, 2.42), (0.38, 2.65), (0.22, 2.92), (0.09, 3.08), (0.0, 3.14)]
    corpo = torno(perfil, gomos=12, fase=0.5)
    corpo.pintar(lambda c, n: coral if c[1] > 2.42 else (turq if (0.8 < c[1] < 1.0 or 2.3 < c[1] < 2.42) else None))
    itens = [(corpo, branco)]
    itens.append((torno([(0.27, 0.1), (0.2, 0.2), (0.17, 0.32)], gomos=10, fase=0.5, tampa_cima=False), cor("#A59FC6")))
    itens.append((torno([(0.12, 0.1), (0.27, 0.1)], gomos=10, fase=0.5, tampa_baixo=False, tampa_cima=False), cor("#5B4BA8")))
    aleta = [(0.4, 1.25), (0.62, 0.9), (0.98, 0.25), (1.0, -0.02), (0.82, 0.0), (0.5, 0.25)]
    for az in (180.0, 60.0, -60.0):
        f = extrudar(aleta, -0.05, 0.05, chanfro=0.02)
        f.girar(0, -90, 0)             # (x2d, y2d) -> (z, y): a aleta sai para +Z
        f.girar(0, az, 0)
        itens.append((f, coral))
    sup = SupMalha(corpo, (0, 1.6, 0))
    jy = 1.82
    itens.append((adesivo(sup, circulo2d(0.27, 14), (0, jy - 1.6, 1.0), 0.04, 0.03), cor("#5B4BA8")))
    itens.append((adesivo(sup, circulo2d(0.2, 14), (0, jy - 1.6, 1.0), 0.052, 0.03), cor("#7FD8FF")))
    luz = [(adesivo(sup, circulo2d(1.0, 8, centro=(-0.07, 0.07), escala=(0.06, 0.05)), (0, jy - 1.6, 1.0), 0.056, 0.03), BRILHO),
           (adesivo(sup, circulo2d(0.025, 6, centro=(0.08, -0.06)), (0, jy - 1.6, 1.0), 0.056, 0.03), BRILHO)]
    itens.append((adesivo(sup, estrela2d(5, 0.17, 0.075), (0, 1.32 - 1.6, 1.0), 0.008, 0.02), cor("#FFD43B")))
    return simples("foguete", itens, luz=luz)


def ovni():
    """Disco voador (origem no centro, ~1,9 m de largura): casco prateado em
    faixas, luzes coloridas em volta (peça "luz"), a cúpula de vidro (peça
    "vidro") com um ETzinho verde lá dentro e a antena."""
    perfil = [(0.0, -0.27), (0.24, -0.26), (0.44, -0.2), (0.62, -0.13), (0.8, -0.05), (0.94, 0.02), (0.9, 0.08),
              (0.7, 0.13), (0.48, 0.17), (0.4, 0.17)]
    casco = torno(perfil, gomos=16, fase=0.5, tampa_baixo=False)

    def faixa(c, n):
        if c[1] < -0.16:
            return cor("#7E8CA6")
        if c[1] < 0.0:
            return cor("#B8C4D6")
        if c[1] > 0.12:
            return cor("#E3E9F2")
        return None
    casco.pintar(faixa)
    itens = [(casco, cor("#CAD3E2"))]
    itens.append((torno([(0.0, 0.17), (0.4, 0.17)], gomos=16, fase=0.5, tampa_baixo=False, tampa_cima=False).inverter(),
                  cor("#8E9BB5")))
    # ETzinho
    et = cor("#8DDB5A")
    cab = Elip((0, 0.36, 0.02), (0.15, 0.13, 0.13))
    itens.append((cab.malha(9, 6), et))
    itens.append((tubo([(0, 0.17, 0), (0, 0.27, 0.0)], 0.09, gomos=7), et))
    for s in (-1, 1):
        a = np.array([s * 0.06, 0.46, 0.0])
        b = a + (s * 0.06, 0.1, 0.0)
        itens.append((tubo([a, b], 0.012, gomos=4), et))
        itens.append((icosfera(0.026, 0, centro=b), cor("#FFD43B")))
    rosto = []
    for s in (-1, 1):
        rosto += olho(cab, (s * 0.42, 0.1, 1.0), (0.036, 0.046), s)
    rosto.append(sorriso(cab, (0, -0.5, 1.0), 0.06, 0.012, abertura=130))
    luzes = []
    cores = [cor("#FFD43B"), cor("#FF6F91"), cor("#9BE15D"), cor("#4FC3F7")]
    for k in range(12):
        a = 2 * PI * (k + 0.5) / 12
        d = np.array([math.cos(a), 0.0, math.sin(a)])
        joia = fecho_convexo(np.array([(0.065, 0, 0), (-0.065, 0, 0), (0, 0.065, 0), (0, -0.065, 0), (0, 0, 0.065),
                                       (0, 0, -0.065)]))
        luzes.append((joia.mover(d * 0.92 + (0, 0.02, 0)), cores[k % 4]))
    luzes.append((icosfera(0.06, 0, centro=(0, 0.66, 0)), cor("#FF6F61")))
    itens.append((tubo([(0, 0.5, 0), (0, 0.62, 0)], 0.02, gomos=5), cor("#B8C4D6")))
    cupula = esfera(0.42, 14, 10)
    cupula.F = cupula.F[cupula.V[cupula.F].mean(axis=1)[:, 1] > 0.0]
    cupula.mover((0, 0.15, 0))
    vidro = Peca("cupula", "vidro")
    vidro.add(cupula, cor("#C8F6FF", 70), suave=60)
    m = montar("ovni", [("ovni", None, (0, 0, 0), itens, ""), ("rosto", "ovni", (0, 0, 0), rosto, "luz"),
                        ("luzes", "ovni", (0, 0, 0), luzes, "luz")])
    m.parte("cupula", "ovni", (0, 0, 0), vidro)
    return m


def planeta():
    """Planetinha (TINTA; origem no centro, raio 0,5 como nas edições Godot —
    o Temas.cs põe com escala 1,6 a 4,2): esfera facetada branca, faixas e
    crateras cinza-clarinho (com a tinta viram tons mais escuros da cor)."""
    branco, cinza, cinza2 = cor("#F6F6F8"), cor("#DEDEE8"), cor("#CDCDDA")
    e = Elip((0, 0, 0), (0.5, 0.5, 0.5))
    esf = e.malha(16, 10)
    fileiras = [None, None, None, cinza, None, None, cinza, None, None, None]
    esf.pintar(lambda c, n: fileiras[linha_da_esfera(c[1] / max(np.linalg.norm(c), 1e-9), 10)])
    itens = [(esf, branco)]
    for (d, r) in (((0.6, 0.55, 0.5), 0.1), ((-0.7, 0.3, 0.6), 0.075), ((0.2, -0.5, 0.85), 0.085), ((-0.4, 0.75, -0.5), 0.09),
                   ((0.8, -0.1, -0.6), 0.07), ((-0.6, -0.65, -0.4), 0.08)):
        itens.append((adesivo(e, circulo2d(r, 9), d, 0.006, 0.01, paredes=False), cinza))
        itens.append((adesivo(e, circulo2d(r * 0.65, 8, centro=(r * 0.12, -r * 0.1)), d, 0.009, 0.01, paredes=False), cinza2))
    return simples("planeta", itens)


def cristal_():
    """Aglomerado de cristais (~1 m): prismas de seis lados de ponta, azul-
    piscina e amarelo, com a face virada para o sol mais clara, numa pedra."""
    ciano, amarelo = cor("#5CE1F0"), cor("#FFC23A")
    ciano_cl, amarelo_cl = cor("#B5F6FF"), cor("#FFE7A3")
    lista = [((0.0, 0.0), 0.0, 0.0, 0.15, 0.95, ciano), ((0.16, 0.05), 25, 15, 0.1, 0.5, amarelo),
             ((-0.16, 0.07), 28, 170, 0.11, 0.56, amarelo), ((0.04, -0.15), 24, -90, 0.11, 0.6, ciano),
             ((-0.06, 0.17), 32, 105, 0.08, 0.36, ciano), ((0.18, -0.14), 35, -35, 0.075, 0.32, amarelo)]
    sol = _norm((0.7, 0.35, 0.6))
    itens = []
    for (x, z), incl, az, r, h, c_ in lista:
        f = cristal(r, h, 6, 0.28)
        f.mover((0, -0.06, 0)).girar(incl, 0, 0).girar(0, az, 0).mover((x * 0.8, 0.0, z * 0.8))
        f.V[:, 1] = np.maximum(f.V[:, 1], 0.0)
        claro = ciano_cl if c_ == ciano else amarelo_cl
        f.pintar(lambda c, n, claro=claro: claro if float(np.dot(n, sol)) > 0.72 else None)
        itens.append((f, c_))
    base = rocha(0.38, (1.0, 0.45, 0.92), 7, sub=1, ruido=0.12, chao=-0.02)
    itens.append((base, cor("#8F9AB0")))
    return simples("cristal", itens)


def pedra_lua():
    """Pedra da lua (~0,95 m de largura, ~0,5 m de altura): rocha lilás
    facetada com crateras (borda clara, fundo escuro)."""
    pedra_c, borda, fundo = cor("#ABA4CB"), cor("#C3BDE0"), cor("#8C84B3")
    e = Elip((0, 0.12, 0), (0.47, 0.34, 0.4))
    f = rocha(1.0, (0.47, 0.34, 0.4), 11, sub=1, ruido=0.05)
    f.mover((0, 0.12, 0))
    f.V[:, 1] = np.maximum(f.V[:, 1], -0.02)
    itens = [(f, pedra_c)]
    for (d, r) in (((0.25, 0.9, 0.2), 0.12), ((-0.5, 0.7, -0.2), 0.09), ((0.0, 0.5, 0.9), 0.08), ((0.75, 0.45, -0.3), 0.08),
                   ((-0.6, 0.4, 0.55), 0.065)):
        itens.append((adesivo(e, arco2d(r, r * 0.38, 0, 360, 10)[:22], d, 0.012, 0.02, leque=False, paredes=False), borda))
        itens.append((adesivo(e, circulo2d(r * 0.82, 9), d, 0.006, 0.02, paredes=False), fundo))
    return simples("pedra_lua", itens)


def estrela_3d():
    """Estrela fofa (origem no centro, ~1,1 m de ponta a ponta): cinco pontas
    estufadas no meio (facetas de joia), com carinha (olhos e sorriso "luz",
    bochechas)."""
    amarelo = cor("#FFD43B")
    n = 10
    R, r = 0.55, 0.27
    contorno = [(math.cos(PI / 2 + PI * i / 5) * (R if i % 2 == 0 else r),
                 math.sin(PI / 2 + PI * i / 5) * (R if i % 2 == 0 else r)) for i in range(n)]
    V = [(0.0, 0.0, 0.2)]
    for (x, y) in contorno:
        V.append((x * 0.5, y * 0.5, 0.15))
    for (x, y) in contorno:
        V.append((x, y, 0.055))
    for (x, y) in contorno:
        V.append((x, y, -0.055))
    for (x, y) in contorno:
        V.append((x * 0.5, y * 0.5, -0.15))
    V.append((0.0, 0.0, -0.2))
    F = []
    for i in range(n):
        j = (i + 1) % n
        F.append((0, 1 + i, 1 + j))
        for L in range(3):
            a0, a1 = 1 + L * n, 1 + (L + 1) * n
            F += [(a0 + i, a1 + i, a0 + j), (a0 + j, a1 + i, a1 + j)]
        F.append((1 + 4 * n, 1 + 3 * n + j, 1 + 3 * n + i))
    f = Forma(V, F)
    f = orientar_fora(f, lambda c: np.zeros(3))
    sup = SupMalha(f, (0, 0, 0))
    itens = [(f, amarelo)]
    for s in (-1, 1):
        itens.append((adesivo(sup, circulo2d(1.0, 9, escala=(0.045, 0.03)), (s * 0.55, -0.4, 1.0), 0.004, 0.01), BOCHECHA))
    rosto = []
    for s in (-1, 1):
        rosto += olho(sup, (s * 0.38, 0.12, 1.0), (0.045, 0.058), s)
    rosto.append(sorriso(sup, (0, -0.32, 1.0), 0.09, 0.016, abertura=130))
    return montar("estrela_3d", [("estrela_3d", None, (0, 0, 0), itens, ""), ("rosto", "estrela_3d", (0, 0, 0), rosto, "luz")])


# =================================================================== FUNDO DO MAR


def coral():
    """Coral de galhos (~1,4 m): galhos facetados que se dividem, pontas
    redondas mais claras, numa pedra lilás."""
    pal = Paleta(coral=cor("#FF7F66"), ponta=cor("#FFB59E"), pedra=cor("#B9B2DA"))
    rnd = np.random.RandomState(23)
    itens = []
    comps = {3: 0.36, 2: 0.42, 1: 0.34, 0: 0.26}
    abre = {2: (0.75, 0.95), 1: (0.45, 0.6), 0: (0.35, 0.55)}

    def galho(a, dr, nivel, r, az0):
        b = a + dr * comps[nivel] * rnd.uniform(0.85, 1.1)
        itens.append((tubo([a, b], [r, r * 0.86], gomos=6, tampas=False), pal.coral))
        if nivel == 0:
            itens.append((icosfera(r * 1.15, 0, centro=b), pal.ponta))
            return
        itens.append((icosfera(r * 0.9, 0, centro=b), pal.coral))
        n = 3 if nivel == 3 else 2
        x = _norm(np.cross(dr, (0.3, 0.1, 1.0)))
        y = np.cross(dr, x)
        for i in range(n):
            ang = az0 + 2 * PI * i / n + rnd.uniform(-0.3, 0.3)
            incl = rnd.uniform(*abre[nivel - 1])
            nova = _norm(dr * math.cos(incl) + (x * math.cos(ang) + y * math.sin(ang)) * math.sin(incl))
            nova = _norm(nova + np.array([0, 0.35, 0]))
            galho(b, nova, nivel - 1, r * 0.8, ang + 0.9)
    galho(np.array([0.0, 0.05, 0.0]), np.array([0.0, 1.0, 0.0]), 3, 0.12, 0.4)
    itens.append((rocha(0.34, (1.0, 0.45, 0.9), 3, sub=1, ruido=0.12, chao=-0.02), pal.pedra))
    return simples("coral", itens)


def coral_roxo():
    """Coral de tubos (esponja, ~1,25 m; amarelo como nas edições Godot):
    tubos ocos de boca clara e o dentro escuro, numa pedra."""
    pal = Paleta(tubo=cor("#FFC93C"), borda=cor("#FFF0A8"), dentro=cor("#C98A1A"), pedra=cor("#C9C2B0"))
    tubos = [(0.0, 0.0, 1.2, 0.14, 0.0), (0.22, 0.07, 0.95, 0.125, 0.22), (-0.2, 0.1, 0.85, 0.12, 0.24),
             (0.07, -0.22, 1.0, 0.13, 0.2), (-0.14, -0.16, 0.72, 0.11, 0.28), (0.29, -0.16, 0.6, 0.1, 0.35),
             (-0.03, 0.25, 0.55, 0.1, 0.3)]
    itens = []
    for (x, z, h, r, incl) in tubos:
        perfil = [(r * 0.8, -0.05), (r * 0.95, h * 0.5), (r, h - 0.03), (r * 1.08, h), (r * 0.78, h), (r * 0.72, h - 0.12)]
        f = torno(perfil, gomos=8, fase=0.5, tampa_baixo=False, tampa_cima=True)
        f.pintar(lambda c, n, h=h, r=r: pal.dentro if c[1] < h - 0.005 and np.hypot(c[0], c[2]) < r * 0.79 else
                 (pal.borda if c[1] > h - 0.035 else None))
        sai = _norm((x, 0.0, z)) if (x or z) else np.array([0.0, 0.0, 0.0])
        eixo = _norm(np.array([0, 1.0, 0]) + sai * math.tan(incl))
        R = eixo_para((0, 1, 0), eixo)
        f.V = f.V @ R.T + np.array([x, 0, z])
        itens.append((f, pal.tubo))
    itens.append((rocha(0.42, (1.0, 0.36, 0.95), 5, sub=1, ruido=0.1, chao=-0.02), pal.pedra))
    return simples("coral_roxo", itens)


def alga():
    """Alga (~2,1 m, origem na base: o Temas.cs balança em volta dela): três
    fitas onduladas de duas faces, verdes, de nervura clara, num tufo de pé."""
    pal = Paleta(verde=cor("#5ED16A"), limao=cor("#9BE15D"), mar=cor("#3FB97A"), baixo=cor("#2E9E6A"), base=cor("#2E9E6A"))
    itens = [(prisma(6, 0.13, 0.12, chanfro=0.03, raio_topo=0.08, fase=0.5), pal.base)]
    for (dx, dz, giro, H, A, f, fase, w, c_) in ((0.0, 0.0, 0.0, 2.1, 0.16, 2.6, 0.3, 0.13, pal.verde),
                                                  (0.08, 0.05, 55.0, 1.7, 0.14, 3.0, 2.0, 0.115, pal.limao),
                                                  (-0.07, -0.02, -60.0, 1.4, 0.13, 3.3, 4.0, 0.11, pal.mar)):
        ys = np.linspace(0.04, H, 10)
        xs = A * np.sin(f * ys + fase) * (0.25 + 0.75 * ys / H) - A * math.sin(fase) * 0.25
        zs = 0.05 * np.sin(1.7 * ys + fase)
        pts = np.stack([xs, ys, zs], 1)
        larg = w * np.sin(PI * np.clip(ys / H, 0, 1) ** 0.75) ** 0.6 + 0.02
        larg[-1] = 0.0
        fo = folha(pts, larg, c_, mais_escura(c_, 0.82), cima=(0, 0, 1), vinco=0.25)
        fo.girar(0, giro, 0).mover((dx, 0, dz))
        itens.append((fo, c_))
    return simples("alga", itens)


def concha():
    """Concha de vieira (~1,3 m de largura) em pé, em leque: costelas em
    gomos pêssego e creme, a borda ondulada e as "orelhas" embaixo."""
    pal = Paleta(pessego=cor("#FFB89A"), creme=cor("#FFEBDD"), escuro=cor("#F59A7C"))
    R = 0.66
    ab = math.radians(70)
    n_cost = 9
    na = n_cost * 2
    raios = [0.08, 0.28, 0.48, R]
    V, F, C = [], [], []

    def ponto(i, j, frente):
        t = -ab + 2 * ab * i / na
        cume = (i % 2 == 0)
        r = raios[j] * (1.0 + (0.035 if (cume and j == len(raios) - 1) else 0.0))
        bojo = math.sqrt(max(0.0, 1 - (raios[j] / R) ** 2 * 0.85))
        z = (0.05 + 0.16 * bojo + (0.03 * min(raios[j] / 0.3, 1.0) if cume else 0.0)) if frente else -(0.04 + 0.07 * bojo)
        return (math.sin(t) * r, math.cos(t) * r, z)
    for frente in (True, False):
        base = len(V)
        V.append((0.0, 0.0, 0.03 if frente else -0.03))
        for j in range(len(raios)):
            for i in range(na + 1):
                V.append(ponto(i, j, frente))
        lin = na + 1
        for i in range(na):
            F.append((base, base + 1 + i, base + 1 + i + 1))
            C.append(pal.pessego if (i // 2) % 2 == 0 else pal.creme)
        for j in range(len(raios) - 1):
            for i in range(na):
                a = base + 1 + j * lin + i
                b = base + 1 + (j + 1) * lin + i
                F += [(a, b, a + 1), (a + 1, b, b + 1)]
                cc = (pal.pessego if (i // 2) % 2 == 0 else pal.creme) if frente else pal.escuro
                C += [cc, cc]
    # beirada: costura a frente com o verso na borda e nos lados
    meio = len(V) // 2
    lin = na + 1
    ult_f = 1 + (len(raios) - 1) * lin
    ult_t = meio + 1 + (len(raios) - 1) * lin
    for i in range(na):
        F += [(ult_f + i, ult_t + i, ult_f + i + 1), (ult_f + i + 1, ult_t + i, ult_t + i + 1)]
        C += [pal.escuro, pal.escuro]
    for (lado_i) in (0, na):
        for j in range(len(raios) - 1):
            a, b = 1 + j * lin + lado_i, 1 + (j + 1) * lin + lado_i
            a2, b2 = meio + 1 + j * lin + lado_i, meio + 1 + (j + 1) * lin + lado_i
            F += [(a, b, a2), (a2, b, b2)]
            C += [pal.escuro, pal.escuro]
    f = Forma(np.array(V), F, C)
    f = orientar_fora(f, lambda c: np.array([c[0] * 0.3, c[1] * 0.6, 0.0]))
    f.girar(-10, 0, 0).mover((0, 0.085, 0))
    itens = [(f, pal.pessego)]
    orelha = extrudar([(-0.2, -0.05), (0.2, -0.05), (0.13, 0.1), (-0.13, 0.1)], -0.06, 0.08, chanfro=0.02)
    itens.append((orelha.mover((0, 0.05, 0.0)), pal.escuro))
    return simples("concha", itens)


def estrela_mar():
    """Estrela-do-mar deitada (~0,8 m): cinco braços facetados com a crista
    no meio e bolinhas claras. Tem uma "sola" de 6 cm (o Temas.cs afunda os
    objetos espalhados 5 cm no chão)."""
    laranja, bolinha = cor("#FF9F2E"), cor("#FFD9A0")
    R, rv = 0.38, 0.15
    V = [(0.0, 0.17, 0.0)]
    for i in range(5):
        a = PI / 2 + 2 * PI * i / 5
        av = a + PI / 5
        V.append((math.cos(a) * R * 0.5, 0.125, math.sin(a) * R * 0.5))       # crista no meio do braço
        V.append((math.cos(a) * R, 0.065, math.sin(a) * R))                    # ponta
        V.append((math.cos(av) * rv, 0.08, math.sin(av) * rv))                 # vale
    for i in range(5):
        a = PI / 2 + 2 * PI * i / 5
        av = a + PI / 5
        V.append((math.cos(a) * R * 1.02, 0.0, math.sin(a) * R * 1.02))
        V.append((math.cos(av) * rv * 1.05, 0.0, math.sin(av) * rv * 1.05))
    F = []
    for i in range(5):
        c_, p_, v_ = 1 + 3 * i, 2 + 3 * i, 3 + 3 * i
        vp = 3 + 3 * ((i - 1) % 5)
        F += [(0, c_, v_), (0, vp, c_), (c_, p_, v_), (c_, vp, p_)]
        pb, vb = 16 + 2 * i, 17 + 2 * i
        vpb = 17 + 2 * ((i - 1) % 5)
        F += [(p_, pb, v_), (v_, pb, vb), (vp, vpb, p_), (p_, vpb, pb)]
    f = Forma(V, F)
    f = orientar_fora(f, lambda c: np.array([0.0, -0.2, 0.0]))
    itens = [(f, laranja)]
    sup = SupMalha(f, (0, -0.05, 0))
    pts = [(0.0, 0.0)]
    for i in range(5):
        a = PI / 2 + 2 * PI * i / 5
        for t in (0.42, 0.66, 0.86):
            pts.append((math.cos(a) * R * t, math.sin(a) * R * t))
    for k, (x, z) in enumerate(pts):
        r = 0.028 if k == 0 else 0.022 - 0.004 * ((k - 1) % 3)
        d = np.array([x, 0.25, z])
        itens.append((adesivo(sup, circulo2d(r, 6), d, 0.004, 0.01, paredes=False, cima=(0, 0, 1)), bolinha))
    return simples("estrela_mar", itens)


def peixe():
    """Peixinho (TINTA; origem no centro, ~0,5 m, nada para +Z): corpo
    branco com a faixa cinza-clarinho, nadadeiras e olhos grandes "luz".
    Partes: corpo > rabo (pivô na junta do rabo, o Temas.cs abana em Y), rosto."""
    branco, cinza = cor("#F6F6F8"), cor("#DADAE6")
    e = Elip((0, 0, 0.03), (0.12, 0.16, 0.2))
    corpo = e.malha(11, 8, polo="z")
    corpo.pintar(lambda c, n: cinza if -0.07 < c[2] < 0.0 else None)
    itens = [(corpo, branco)]
    dorsal = extrudar([(-0.12, 0.0), (0.06, 0.0), (-0.02, 0.1), (-0.14, 0.08)], -0.012, 0.012)
    dorsal.girar(0, -90, 0).mover((0, 0.13, 0.0))
    itens.append((dorsal, cinza))
    for s in (-1, 1):
        lat = extrudar(circulo2d(1.0, 6, escala=(0.07, 0.035)), -0.008, 0.008).mover((0.06, 0, 0))
        lat.girar(0, 0, -25).girar(0, s * 35 + (180 if s < 0 else 0), 0).mover((s * 0.1, -0.06, 0.04))
        itens.append((lat, cinza))
    PIV_RABO = np.array([0.0, 0.0, -0.14])
    rabo = [(tubo([(0, 0, -0.09), (0, 0, -0.2)], [0.06, 0.035], gomos=6), branco)]
    cauda = extrudar([(0.0, 0.02), (-0.13, 0.12), (-0.1, 0.0), (-0.13, -0.12), (0.0, -0.02)], -0.012, 0.012)
    cauda.girar(0, -90, 0).mover((0, 0, -0.18))
    rabo.append((cauda, cinza))
    rosto = []
    for s in (-1, 1):
        rosto += olho(e, (s * 0.75, 0.22, 0.62), (0.042, 0.05), s, simples_=True)
    rosto.append(sorriso(e, (0, -0.22, 1.0), 0.05, 0.011, abertura=120))
    return montar("peixe", [("corpo", None, (0, 0, 0), itens, ""), ("rosto", "corpo", (0, 0, 0), rosto, "luz"),
                            ("rabo", "corpo", PIV_RABO, rabo, "")])


def agua_viva():
    """Água-viva (TINTA; origem no centro do sino, ~0,8 m com os tentáculos):
    sino facetado de beirada aberta e fundo clarinho, pintinhas, tentáculos
    de fita ondulada e os "braços" de babado no meio; carinha (olhos,
    sorriso e bochechas "luz": não pegam a tinta)."""
    branco, pinta, tent = cor("#F6F6FA"), cor("#DFE1EE"), cor("#ECEDF5")
    gomos = 12
    perfil = [(0.325, -0.05), (0.31, 0.0), (0.28, 0.1), (0.22, 0.19), (0.13, 0.25), (0.0, 0.27)]   # de baixo para cima
    sino = torno(perfil, gomos=gomos, fase=0.5, tampa_baixo=False)
    fundo = torno([(0.0, 0.0), (0.2, -0.03), (0.325, -0.05)], gomos=gomos, fase=0.5, tampa_baixo=False,
                  tampa_cima=False)                                                  # virado para baixo
    itens = [(sino, branco), (fundo, pinta)]
    e = Elip((0, 0, 0), (0.31, 0.27, 0.31))
    for d in ((0.3, 0.9, -0.3), (-0.45, 0.8, -0.1), (0.1, 0.85, -0.6), (-0.25, 0.75, -0.7), (0.62, 0.6, -0.35),
              (0.05, 1.0, 0.1)):
        itens.append((adesivo(e, circulo2d(0.035, 6), d, 0.004, 0.01, paredes=False), pinta))
    for i in range(5):
        a = 2 * PI * i / 5 + 0.3
        r0 = 0.2
        ys = np.linspace(-0.03, -0.46 - 0.05 * (i % 2), 6)
        pts = np.array([(r0 * math.cos(a) + 0.04 * math.sin(j * 1.5 + i) * -math.sin(a), y,
                         r0 * math.sin(a) + 0.04 * math.sin(j * 1.5 + i) * math.cos(a)) for j, y in enumerate(ys)])
        larg = np.linspace(0.03, 0.008, len(pts))
        itens.append((folha(pts, larg, tent, pinta, cima=(math.cos(a), 0, math.sin(a)), vinco=0.15), tent))
    for k in range(2):
        a = PI * k + 0.7
        pts = np.array([(0.05 * math.cos(a) + 0.03 * math.sin(y * 9 + k), y, 0.05 * math.sin(a)) for y in np.linspace(-0.02, -0.36, 6)])
        larg = np.linspace(0.065, 0.02, len(pts))
        itens.append((folha(pts, larg, pinta, pinta, cima=(math.cos(a), 0, math.sin(a)), serra=0.45, vinco=0.1), pinta))
    rosto = []
    for s in (-1, 1):
        rosto += olho(e, (s * 0.36, 0.45, 1.0), (0.04, 0.048), s, simples_=True)
        rosto.append((adesivo(e, circulo2d(1.0, 7, escala=(0.032, 0.02)), (s * 0.62, 0.22, 0.85), 0.004, 0.01,
                              paredes=False), BOCHECHA))
    rosto.append(sorriso(e, (0, 0.2, 1.0), 0.06, 0.012, abertura=130))
    return montar("agua_viva", [("agua_viva", None, (0, 0, 0), itens, ""), ("rosto", "agua_viva", (0, 0, 0), rosto, "luz")])


def bau():
    """Baú do tesouro (~1 m com a tampa entreaberta): tábuas em duas cores,
    cintas e fechadura douradas, moedas brilhando lá dentro (peça "luz") e
    algumas moedas e joias caídas na frente."""
    madeira, tabua, ouro, ouro_cl = cor("#B8743F"), cor("#A3632F"), cor("#FFC93B"), cor("#FFE27A")
    itens = []
    for k, (y0, y1) in enumerate(((0.0, 0.18), (0.18, 0.35), (0.35, 0.52))):
        itens.append((caixa(0.86, y1 - y0, 0.56, chanfro=0.012, centro=(0, (y0 + y1) / 2, 0)), madeira if k % 2 == 0 else tabua))
    for x in (-0.27, 0.27):
        itens.append((caixa(0.09, 0.53, 0.58, chanfro=0.01, centro=(x, 0.262, 0)), ouro))
    itens.append((caixa(0.88, 0.05, 0.58, chanfro=0.01, centro=(0, 0.5, 0)), ouro))
    # tampa: meio cilindro deitado (eixo X), aberta 35 graus na dobradiça de trás
    meia = [(math.cos(PI * i / 8) * 0.28, math.sin(PI * i / 8) * 0.28) for i in range(9)]
    tampa = extrudar(meia, -0.43, 0.43)
    tampa.girar(0, 90, 0)            # o contorno (z, y): perfil do meio cilindro em YZ, comprido em X
    tampa.pintar(lambda c, n: tabua if (abs(n[0]) < 0.9 and int((math.atan2(c[1], c[2]) + 0.01) / (PI / 4)) % 2 == 1) else None)
    cintas = []
    for x in (-0.27, 0.27):
        c = extrudar([(math.cos(PI * i / 8) * 0.295, math.sin(PI * i / 8) * 0.295) for i in range(9)], -0.045, 0.045)
        c.girar(0, 90, 0).mover((x, 0, 0))
        cintas.append(c)
    dob = np.array([0.0, 0.52, -0.28])
    for f, c_ in [(tampa, madeira)] + [(c, ouro) for c in cintas]:
        f.mover((0, 0.52, 0)).girar(-35, 0, 0, centro=dob)
        itens.append((f, c_))
    # fechadura
    itens.append((caixa(0.14, 0.17, 0.04, chanfro=0.01, centro=(0, 0.43, 0.29)), ouro_cl))
    itens.append((placa([(-0.018, -0.05), (0.018, -0.05), (0.01, 0.0), (0.024, 0.02), (0.0, 0.04), (-0.024, 0.02), (-0.01, 0.0)],
                        (0, 0.43, 0.31), (0, 0, 1), 0.006), cor("#3A2433")))
    luz = [(torno([(0.38, 0.5), (0.3, 0.56), (0.12, 0.6), (0.0, 0.61)], gomos=8, fase=0.5, tampa_baixo=False).escalar(
        (1.0, 1.0, 0.62)), ouro)]
    for (x, z, g) in ((-0.12, 0.05, 20), (0.15, -0.04, -25), (0.02, 0.1, 10)):
        luz.append((prisma(8, 0.05, 0.012, centro=(0, 0, 0)).girar(70, g, 0).mover((x, 0.6, z)), ouro_cl))
    luz.append((icosfera(0.035, 0, centro=(-0.05, 0.62, -0.08)), cor("#FF4B5C")))
    luz.append((icosfera(0.03, 0, centro=(0.17, 0.6, 0.06)), cor("#4FC3F7")))
    for (x, z, g) in ((0.32, 0.42, 0), (0.42, 0.36, 0), (-0.38, 0.4, 0)):
        itens.append((prisma(8, 0.045, 0.012, centro=(x, 0.0, z)), ouro))
    itens.append((icosfera(0.03, 0, centro=(0.25, 0.025, 0.45)), cor("#2EC4B6")))
    return simples("bau", itens, luz=luz)


TARTA = Paleta(casco=cor("#3FB97A"), placa=cor("#6FD58A"), borda=cor("#2A8A5A"), pele=cor("#9BE15D"),
               pele2=cor("#7CC94A"), barriga=cor("#FFF1B8"), iris=cor("#2F6B2A"))


def nadadeira(s, ombro, comp, giro, pal):
    """Nadadeira de remo, saindo do ombro para o lado s (-1 esquerda, +1 direita)."""
    pts = [(0.0, 0.06), (0.1, 0.1), (0.22, 0.13), (0.34, 0.12), (0.44, 0.06), (0.47, 0.0), (0.42, -0.04), (0.28, -0.06),
           (0.14, -0.07), (0.0, -0.05)]
    pts = [(x * comp / 0.47, z * comp / 0.47) for (x, z) in pts]
    f = extrudar(pts, -0.022, 0.022, chanfro=0.012)
    f.girar(90, 0, 0)                  # deitada: (x, z_2d) -> (x, y, z)
    f.deformar(lambda V: V + np.stack([V[:, 0] * 0, -0.25 * V[:, 0] ** 2, 0.18 * V[:, 0] ** 2], 1))   # curva para trás
    f.pintar(lambda c, n: pal.pele2 if abs(n[1]) < 0.8 else None)
    f.girar(0, giro, -8)
    if s < 0:
        f.espelhar_x()
    return [(f.mover(ombro), pal.pele)]


def tartaruga():
    """Tartaruga-marinha (~1,3 m; origem embaixo da barriga): casco verde com
    placas hexagonais claras e a borda escura, barriga creme, cabeça de olhos
    grandes. Partes: corpo (> rosto) e as quatro nadadeiras (pivôs nos ombros
    e quadris): nadadeira_ef/df = esquerda/direita da FRENTE, nadadeira_ed/dd
    = esquerda/direita de TRÁS (e/d = lado -X/+X; f = frente, d = de trás)."""
    pal = TARTA
    e = Elip((0, 0.22, -0.04), (0.38, 0.22, 0.45))
    casco = e.malha(14, 8)
    fileiras = [None, None, None, pal.borda, pal.barriga, pal.barriga, pal.barriga, pal.barriga]
    casco.pintar(lambda c, n: fileiras[linha_da_esfera(e.local(c)[1] / 0.22 / 1.0001, 8)])
    itens = [(casco, pal.casco)]
    for (d, r) in (((0, 1, 0), 0.11), ((0.0, 0.7, 0.7), 0.09), ((0.0, 0.7, -0.7), 0.09), ((0.62, 0.7, 0.35), 0.085),
                   ((-0.62, 0.7, 0.35), 0.085), ((0.62, 0.7, -0.35), 0.085), ((-0.62, 0.7, -0.35), 0.085)):
        itens.append((adesivo(e, circulo2d(r, 6, ), d, 0.012, 0.012, giro=30, cima=(0, 0, 1)), pal.placa))
    cab = Elip((0, 0.3, 0.56), (0.135, 0.125, 0.15))
    itens.append((cab.malha(10, 7, polo="z"), pal.pele))
    itens.append((tubo([(0, 0.2, 0.28), (0, 0.27, 0.5)], [0.1, 0.09], gomos=8), pal.pele))
    itens.append((tubo([(0, 0.17, -0.45), (0, 0.15, -0.58)], [0.05, 0.0], gomos=6), pal.pele))
    for s in (-1, 1):
        itens.append(oval(cab, (s * 0.7, -0.3, 0.65), 0.035, 0.024, BOCHECHA, lados=8))
    rosto = []
    for s in (-1, 1):
        rosto += olho(cab, (s * 0.55, 0.25, 0.8), (0.04, 0.05), s, cor_iris=pal.iris)
    rosto.append(sorriso(cab, (0, -0.35, 1.0), 0.08, 0.014, abertura=125))
    partes = [("corpo", None, (0, 0, 0), itens, ""), ("rosto", "corpo", (0, 0, 0), rosto, "luz")]
    for (nome, s, ombro, comp, giro) in (("nadadeira_ed", -1, (-0.22, 0.15, -0.34), 0.24, -35),
                                         ("nadadeira_ef", -1, (-0.27, 0.17, 0.22), 0.46, 22),
                                         ("nadadeira_dd", 1, (0.22, 0.15, -0.34), 0.24, -35),
                                         ("nadadeira_df", 1, (0.27, 0.17, 0.22), 0.46, 22)):
        o = np.array([abs(ombro[0]), ombro[1], ombro[2]])
        itens_n = nadadeira(s, np.array([0.0, 0, 0]), comp, giro, pal)
        itens_n = [(f.mover((s * o[0], o[1], o[2])), c_) for (f, c_) in itens_n]
        partes.append((nome, "corpo", (s * o[0], o[1], o[2]), itens_n, ""))
    return montar("tartaruga", partes)


NAVIO = Paleta(madeira=cor("#B8743F"), tabua=cor("#93562A"), conves=cor("#E2B279"), conves2=cor("#D29F68"),
               ouro=cor("#FFC93B"), vidro=cor("#2B3A6B"), mastro=cor("#7A4A2A"), pano=cor("#FFF4DD"), listra=cor("#E53935"),
               preto=cor("#262230"), osso=cor("#FFFFFF"), alga=cor("#4CC46A"), craca=cor("#D8D2E6"))
# seções do casco (z, meia largura, quilha, convés)
SECOES_NAVIO = [(-2.55, 0.86, -0.02, 1.72), (-2.2, 0.97, -0.12, 1.64), (-1.4, 1.05, -0.16, 1.57), (-0.4, 1.07, -0.16, 1.55),
                (0.6, 1.05, -0.16, 1.56), (1.5, 0.93, -0.12, 1.6), (2.2, 0.74, 0.02, 1.68), (2.7, 0.48, 0.28, 1.79),
                (3.05, 0.08, 0.7, 1.92)]
ANG_CASCO = [0, 25, 50, 70, 82, 90]


def _secao_navio(z, w, quilha, conves):
    """Meia seção (x >= 0) do casco, da quilha até o lado de dentro da amurada."""
    pts = []
    for a in ANG_CASCO:
        r = math.radians(a)
        pts.append((w * math.sin(r), quilha + (conves - quilha) * (1 - math.cos(r)), z))
    pts.append((w * 0.97, conves + 0.28, z))
    pts.append((w * 0.97 - 0.07, conves + 0.28, z))
    pts.append((w * 0.97 - 0.07, conves, z))
    return pts


def navio():
    """Navio pirata naufragado (~6,6 m de comprimento com o gurupés, mastro
    a 6 m): casco em tábuas de duas cores com faixa dourada e escotilhas,
    tombadilho na popa com janelas, três mastros com velas listradas rasgadas
    embaixo, cesto da gávea, bandeira de caveirinha sorridente, âncora,
    cracas e algas. Proa para +Z, base em y = 0."""
    pal = NAVIO
    cores_fil = [pal.tabua, pal.madeira, pal.tabua, pal.madeira, pal.ouro, pal.madeira, pal.tabua, pal.madeira]
    V, F, C = [], [], []
    secs = [_secao_navio(*q) for q in SECOES_NAVIO]
    n_meia = len(secs[0])
    for lado in (1, -1):
        base = len(V)
        for sc in secs:
            for (x, y, z) in sc:
                V.append((x * lado, y, z))
        for i in range(len(secs) - 1):
            for k in range(n_meia - 1):
                a = base + i * n_meia + k
                b = base + (i + 1) * n_meia + k
                F += [(a, b, a + 1), (a + 1, b, b + 1)]
                C += [cores_fil[k]] * 2
    casco = Forma(V, F, C)
    # orientação por fileira: o casco e a amurada por fora olham para o lado (±X), o topo da amurada
    # para cima e a amurada por dentro para o meio do navio
    A_, B_, C_ = casco.V[casco.F[:, 0]], casco.V[casco.F[:, 1]], casco.V[casco.F[:, 2]]
    nf = np.cross(B_ - A_, C_ - A_)
    cen = (A_ + B_ + C_) / 3
    # fileiras: 0-4 casco por fora, 5 = amurada por fora, 6 = topo da amurada, 7 = amurada por dentro
    fil = np.tile(np.repeat(np.arange(n_meia - 1), 2), (len(secs) - 1) * 2)
    dentro = fil == n_meia - 2
    topo = fil == n_meia - 3
    ref_fora = np.stack([cen[:, 0], np.zeros(len(cen)), np.zeros(len(cen))], 1)
    ref = np.where(dentro[:, None], -ref_fora, np.where(topo[:, None], np.array([[0, 1.0, 0]]), ref_fora))
    trocar = np.einsum("ij,ij->i", nf, ref) < 0
    casco.F[trocar] = casco.F[trocar][:, [0, 2, 1]]
    itens = [(casco, pal.madeira)]
    # convés (tábuas no comprimento)
    V, F, C = [], [], []
    for (z, w, q, cv) in SECOES_NAVIO:
        xi = w * 0.97 - 0.07
        for t in (-1, -1 / 3, 1 / 3, 1):
            V.append((xi * t, cv, z))
    for i in range(len(SECOES_NAVIO) - 1):
        for k in range(3):
            a, b = 4 * i + k, 4 * (i + 1) + k
            F += [(a, b, a + 1), (a + 1, b, b + 1)]
            C += [pal.conves if k % 2 == 0 else pal.conves2] * 2
    conves = orientar_fora(Forma(V, F, C), lambda c: np.array([c[0], c[1] - 1.0, c[2]]))
    itens.append((conves, pal.conves))
    # popa (espelho) e proa (tampinha): o contorno da seção fechado
    for (sc, sinal) in ((secs[0], -1), (secs[-1], 1)):
        contorno = [(-x, y) for (x, y, z) in sc[::-1]] + [(x, y) for (x, y, z) in sc[1:]]
        tampa = Forma([(x, y, sc[0][2]) for (x, y) in contorno], triangular(contorno))
        tampa = orientar_fora(tampa, lambda c, s_=sinal: np.array([c[0], c[1], c[2] - s_]))
        itens.append((tampa, pal.tabua))
    # tombadilho (castelo da popa) com janelas, porta e o corrimão dourado
    castelo = caixa(1.5, 0.6, 1.12, chanfro=0.03, centro=(0, 1.72 + 0.3, -2.0))
    castelo.pintar(lambda c, n: pal.conves if n[1] > 0.9 else None)
    itens.append((castelo, pal.madeira))
    for (sx, sz, cx, cz) in ((1.56, 0.07, 0, -2.56), (1.56, 0.07, 0, -1.44), (0.07, 1.18, -0.75, -2.0), (0.07, 1.18, 0.75, -2.0)):
        itens.append((caixa(sx, 0.1, sz, chanfro=0.012, centro=(cx, 2.36, cz)), pal.ouro))
    for x in (-0.42, 0.0, 0.42):
        itens.append((placa(janela_arco(0.2, 0.28), (x, 1.86, -2.565), (0, 0, -1), 0.01), pal.ouro))
        itens.append((placa(janela_arco(0.14, 0.22), (x, 1.89, -2.575), (0, 0, -1), 0.01), pal.vidro))
    itens.append((placa(janela_arco(0.34, 0.46), (0, 1.73, -1.435), (0, 0, 1), 0.01), pal.tabua))
    itens.append((caixa(0.08, 0.7, 0.42, chanfro=0.01, centro=(0, 0.45, -2.62)), pal.tabua))
    # mastros, vergas, velas e o cesto
    mastros = [(0.3, 1.55, 6.05, 0.1), (1.85, 1.62, 4.95, 0.085), (-1.95, 2.3, 4.75, 0.08)]
    velas = {0.3: (5.42, 3.3, 0.95, 1.05), 1.85: (4.32, 2.92, 0.76, 0.86), -1.95: (4.13, 3.18, 0.74, 0.8)}
    for (z, y0, y1, r) in mastros:
        itens.append((tubo([(0, y0 - 0.05, z), (0, y1, z)], [r, r * 0.7], gomos=6), pal.mastro))
        yc, yb, mc, mb = velas[z]
        for (yv, meia) in ((yc + 0.04, mc), (yb - 0.02, mb)):
            itens.append((tubo([(-meia, yv, z + 0.1), (meia, yv, z + 0.1)], 0.045, gomos=5), pal.mastro))
        # vela estufada para a frente, em listras, rasgada embaixo
        nx, ny = 5, 4
        Vv, Fv, Cv = [], [], []      # (Fv: triângulos de um lado só; duas_faces faz o avesso)
        for j in range(ny + 1):
            v = j / ny
            for i in range(nx + 1):
                u = i / nx
                meia = mc + (mb - mc) * v
                x = (u * 2 - 1) * meia * 0.96
                y = yc - (yc - yb) * v
                if j == ny:
                    y += (0.1 if i % 2 == 1 else -0.02) * (1 if 0 < i < nx else 0)
                zz = z + 0.14 + 0.38 * math.sin(PI * u) * math.sin(PI * min(v, 1.0) * 0.85 + 0.25)
                Vv.append((x, y, zz))
        for j in range(ny):
            for i in range(nx):
                a = j * (nx + 1) + i
                b = a + nx + 1
                Fv += [(a, b, a + 1), (a + 1, b, b + 1)]
                Cv += [pal.listra if j % 2 == 1 else pal.pano] * 2
        itens.append((duas_faces(Vv, Fv, Cv, lambda c: np.array([0.0, 0.0, 1.0]), escurecer=0.93), pal.pano))
    cesto = torno([(0.3, 5.5), (0.32, 5.7), (0.27, 5.7), (0.25, 5.56)], gomos=8, fase=0.5, tampa_baixo=True, tampa_cima=True)
    itens.append((cesto.mover((0, 0, 0.3)), pal.madeira))
    itens.append((tubo([(0, 1.95, 2.95), (0, 2.5, 4.05)], [0.075, 0.04], gomos=6), pal.mastro))
    # bandeira de caveirinha (para trás do mastro principal)
    by, bz = 6.08, 0.3
    k_ = 1.35
    band = extrudar([(0.0, 0.0), (-0.66 * k_, 0.03), (-0.72 * k_, -0.24 * k_), (-0.64 * k_, -0.46 * k_), (0.0, -0.46 * k_)],
                    -0.012, 0.012)
    band.girar(0, -90, 0).mover((0, by, bz - 0.08))          # o x do contorno (negativo) vira -Z: para trás
    itens.append((band, pal.preto))
    for lado in (1, -1):
        n = np.array([lado * 1.0, 0, 0])
        c0 = np.array([lado * 0.014, by - 0.3, bz - 0.56])
        cima_ = (0, 1, 0)
        itens.append((placa(circulo2d(1.0, 10, escala=(0.12, 0.11)), c0 + (0, 0.04, 0), n, 0.004, cima=cima_), pal.osso))
        itens.append((placa(retangulo2d(0.13, 0.08, 0.025), c0 + (0, -0.055, 0), n, 0.004, cima=cima_), pal.osso))
        for g in (35, -35):
            osso = retangulo2d(0.36, 0.045, 0.02)
            itens.append((placa(osso, c0 + (0, -0.09, 0), n, 0.003, giro=g, cima=cima_), pal.osso))
        for dx in (-0.045, 0.045):
            itens.append((placa(circulo2d(0.032, 7), c0 + (0, 0.05, dx * lado), n, 0.007, cima=cima_), pal.preto))
        itens.append((placa(arco2d(0.04, 0.013, 200, 340, 6), c0 + (0, -0.02, 0), n, 0.007, cima=cima_), pal.preto))
    # escotilhas dos lados
    sup = SupMalha(casco, (0, 0.9, 0))
    for lado in (1, -1):
        for z in (-0.95, 0.05, 1.0):
            d = np.array([lado * 1.0, 0.0, 0.0])
            sup.c = np.array([0.0, 1.12, z])
            itens.append((adesivo(sup, circulo2d(0.15, 10), d, 0.012, 0.03, cima=(0, 1, 0), paredes=False), pal.ouro))
            itens.append((adesivo(sup, circulo2d(0.105, 10), d, 0.02, 0.03, cima=(0, 1, 0), paredes=False), pal.vidro))
    # âncora pendurada na proa (lado direito)
    anc = [(-0.03, 0.0), (0.03, 0.0), (0.03, -0.42), (0.16, -0.32), (0.2, -0.36), (0.02, -0.52), (-0.02, -0.52),
           (-0.2, -0.36), (-0.16, -0.32), (-0.03, -0.42)]
    ancora = extrudar(anc, -0.025, 0.025)
    ancora.girar(0, 90, 0).mover((0.7, 1.45, 2.25))
    itens.append((ancora, cor("#55506A")))
    itens.append((tubo([(0.66, 1.45, 2.25), (0.62, 1.75, 2.2)], 0.02, gomos=4), cor("#55506A")))
    # cracas e algas (naufragou!)
    rnd = np.random.RandomState(4)
    for k in range(7):
        z = rnd.uniform(-2.0, 2.2)
        lado = 1 if k % 2 else -1
        sup.c = np.array([0.0, 0.55, z])
        p, n = sup.superficie((lado, -0.2, 0))
        itens.append((icosfera(0.06, 0, centro=p, escala=(1, 0.7, 1)), pal.craca))
    for (x, z, h, giro) in ((1.05, -1.6, 1.3, 20), (-1.1, 0.4, 1.1, -30), (0.95, 1.4, 0.9, 50)):
        ys = np.linspace(0.0, h, 6)
        pts = np.stack([0.08 * np.sin(ys * 5), ys, np.zeros_like(ys)], 1)
        larg = 0.09 * np.sin(PI * np.clip(ys / h, 0.05, 1) ** 0.7) + 0.02
        larg[-1] = 0.0
        al = folha(pts, larg, pal.alga, mais_escura(pal.alga, 0.8), cima=(0, 0, 1), vinco=0.2)
        al.girar(0, giro, 0).mover((x, 0, z))
        itens.append((al, pal.alga))
    return simples("navio", itens)


# =================================================================== lista (nome -> (função, fase, orçamento de triângulos))

MODELOS = {
    "dino": (dino, "dinos", 3500),
    "trex": (trex, "dinos", 3500),
    "triceratops": (triceratops, "dinos", 3500),
    "estegossauro": (estegossauro, "dinos", 3500),
    "ptero": (ptero, "dinos", 3500),
    "ovo_chocando": (ovo_chocando, "dinos", 3500),
    "ovo_dino": (ovo_dino, "dinos", 400),
    "palmeira": (palmeira, "dinos", 1200),
    "samambaia": (samambaia, "dinos", 400),
    "vulcao": (vulcao, "dinos", 3500),
    "pedra": (pedra_, "dinos", 400),
    "flor_gigante": (flor_gigante, "dinos", 400),
    "arvore_verde": (arvore_verde, "parque", 1200),
    "arvore_laranja": (arvore_laranja, "parque", 1200),
    "arvore_escura": (arvore_escura, "parque", 1200),
    "pirulito": (pirulito, "parque", 400),
    "cupcake": (cupcake, "parque", 400),
    "donut": (donut, "parque", 400),
    "presente": (presente, "parque", 400),
    "bengala": (bengala, "parque", 400),
    "cogumelo": (cogumelo, "parque", 400),
    "nuvem": (nuvem, "parque", 1200),
    "balao": (balao, "parque", 400, "#FF4B5C"),
    "arco_iris": (arco_iris, "parque", 1200),
    "torre": (torre, "parque", 1200),
    "tenda": (tenda, "parque", 1200),
    "carrossel": (carrossel, "parque", 3500),
    "roda_base": (roda_base, "parque", 1200),
    "roda": (roda, "parque", 2000),
    "cabine": (cabine, "parque", 190, "#4FD8FF"),
    "planeta_anel": (planeta_anel, "espaco", 1200),
    "foguete": (foguete, "espaco", 1200),
    "ovni": (ovni, "espaco", 1200),
    "planeta": (planeta, "espaco", 400, "#FF9F2E"),
    "cristal": (cristal_, "espaco", 400),
    "pedra_lua": (pedra_lua, "espaco", 400),
    "estrela_3d": (estrela_3d, "espaco", 400),
    "coral": (coral, "mar", 1200),
    "coral_roxo": (coral_roxo, "mar", 1200),
    "alga": (alga, "mar", 400),
    "concha": (concha, "mar", 400),
    "estrela_mar": (estrela_mar, "mar", 400),
    "peixe": (peixe, "mar", 400, "#4FD8FF"),
    "agua_viva": (agua_viva, "mar", 600, "#FFB36B"),
    "bau": (bau, "mar", 1200),
    "tartaruga": (tartaruga, "mar", 1200),
    "navio": (navio, "mar", 3500),
}
