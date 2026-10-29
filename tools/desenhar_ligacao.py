"""Desenha a MONTAGEM DO ARDUINO da maquina, em fundo branco e com textura
(placas de circuito com trilhas, pinos dourados, dissipador de aluminio,
bornes com parafuso, fios com brilho, fonte de metal perfurado, motor,
trava, botoes de fliperama), nas DUAS versoes do firmware (o jogo e o mesmo
nas duas):
  rele    -> docs/ligacao_arduino.svg: arduino/strike_kids, a porta pelo
             RELE (trava solenoide / eletroima) e/ou SERVO, sem ponte H,
             como a primeira versao;
  ponte_h -> docs/ligacao_arduino_ponte_h.svg: arduino/strike_kids_ponte_h,
             a PONTE H com o motor da porta e os fins de curso (e,
             opcionais, o servo e o rele).
Nas duas: os 5 sensores, a fita de LED, o moedeiro, os botoes START e
CONFIGURACAO e a TV Box - com os mesmos pinos do firmware. O mesmo desenho
da edicao Godot (strike-kids-gles2/tools/desenhar_ligacao.py), so a fonte
vem da pasta da Unity.

    python3 -I tools/desenhar_ligacao.py                 (as duas, em docs/)
    python3 -I tools/desenhar_ligacao.py rele docs/ligacao_arduino.svg

O PNG e o PDF saem do SVG por qualquer navegador (Chrome: abrir o SVG e
imprimir em PDF, A3 deitado) - a fonte dos titulos (a Fredoka das telas do
jogo) vai dentro do SVG.
"""
import base64
import os
import sys

W, H = 3800, 3000
SANS = "DejaVu Sans, Verdana, Arial, sans-serif"
TITULO = "Fredoka, DejaVu Sans, Arial, sans-serif"
TXT = "#1b2a41"
CINZA = "#5d6d7e"

# cores dos fios
COR = {
    "S1": "#8e44ad", "S2": "#1f6fb2", "S3": "#1e8a4c", "S4": "#c27c0e", "S5": "#c2185b",
    "D6": "#0097a7", "IN1": "#e64a19", "IN2": "#3949ab", "ENA": "#78909c",
    "A4": "#6d4c41", "A3": "#00897b", "A2": "#546e7a", "A1": "#2e7d32", "A0": "#f57c00",
    "12V": "#d32f2f", "GND": "#262626", "USB": "#8a9399",
    "D8": "#e64a19", "D9": "#3949ab",
}
# qual desenho: "rele" (arduino/strike_kids) ou "ponte_h" (arduino/strike_kids_ponte_h)
VERSAO = "ponte_h"
RELE_AZUL = "#1a5276"

_saida = []
_defs = {}


def add(s):
    _saida.append(s)


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


# ======================================================================== cores e texturas
def _rgb(c):
    c = c.lstrip("#")
    return tuple(int(c[i:i + 2], 16) for i in (0, 2, 4))


def mistura(c1, c2, t):
    a, b = _rgb(c1), _rgb(c2)
    return "#%02x%02x%02x" % tuple(int(round(x + (y - x) * t)) for x, y in zip(a, b))


def clarear(c, t=0.3):
    return mistura(c, "#ffffff", t)


def escurecer(c, t=0.3):
    return mistura(c, "#000000", t)


def _paradas(paradas):
    s = ""
    for p in paradas:
        a = ' stop-opacity="%g"' % p[2] if len(p) > 2 else ""
        s += '<stop offset="%g" stop-color="%s"%s/>' % (p[0], p[1], a)
    return s


def grad(paradas, x1=0, y1=0, x2=0, y2=1):
    """Degrade linear (de cima para baixo, se nada for dito)."""
    chave = ("l", tuple(paradas), x1, y1, x2, y2)
    if chave not in _defs:
        gid = "g%d" % len(_defs)
        _defs[chave] = (gid, '<linearGradient id="%s" x1="%g" y1="%g" x2="%g" y2="%g">%s</linearGradient>'
                        % (gid, x1, y1, x2, y2, _paradas(paradas)))
    return "url(#%s)" % _defs[chave][0]


def rgrad(paradas, fx=0.5, fy=0.5, r=0.5):
    """Degrade redondo (fx, fy = de onde vem a luz)."""
    chave = ("r", tuple(paradas), fx, fy, r)
    if chave not in _defs:
        gid = "g%d" % len(_defs)
        _defs[chave] = (gid, '<radialGradient id="%s" cx="0.5" cy="0.5" r="%g" fx="%g" fy="%g">%s</radialGradient>'
                        % (gid, r, fx, fy, _paradas(paradas)))
    return "url(#%s)" % _defs[chave][0]


def vertical(c, t=0.18):
    return grad(((0, clarear(c, t)), (1, escurecer(c, t))))


def metal(deitado=False):
    p = ((0, "#f8fafb"), (0.4, "#cfd5da"), (0.5, "#aab3ba"), (0.6, "#cfd5da"), (1, "#eef1f3"))
    return grad(p, 0, 0, 1, 0) if deitado else grad(p)


def preto():
    return grad(((0, "#58636e"), (0.4, "#2a323a"), (1, "#111518")))


def ouro():
    return rgrad(((0, "#fff8cf"), (0.5, "#e6b923"), (1, "#8c6a00")), 0.35, 0.32)


def cupula(c):
    return rgrad(((0, clarear(c, 0.8)), (0.35, clarear(c, 0.25)), (0.8, c), (1, escurecer(c, 0.35))), 0.38, 0.3, 0.55)


def luz(c):
    return rgrad(((0, "#ffffff"), (0.35, clarear(c, 0.2)), (1, c)), 0.42, 0.38)


FIXOS = """
<filter id="f_sombra" x="-10%" y="-10%" width="120%" height="130%">
  <feDropShadow dx="0" dy="9" stdDeviation="9" flood-color="#0b1f45" flood-opacity="0.20"/>
</filter>
<filter id="f_sombra_leve" x="-20%" y="-20%" width="140%" height="170%">
  <feDropShadow dx="0" dy="3" stdDeviation="3" flood-color="#000000" flood-opacity="0.28"/>
</filter>
<filter id="f_brilho" x="-100%" y="-100%" width="300%" height="300%">
  <feGaussianBlur stdDeviation="7"/>
</filter>
<pattern id="p_trilhas" width="96" height="96" patternUnits="userSpaceOnUse">
  <path d="M0 22 H34 L50 38 H96 M8 70 H52 L64 58 H96 M22 0 V12 M74 96 V80 M86 0 V24 L76 34"
        stroke="#ffffff" stroke-width="2.4" fill="none"/>
  <circle cx="34" cy="22" r="4" fill="none" stroke="#ffffff" stroke-width="2"/>
  <circle cx="52" cy="70" r="4" fill="none" stroke="#ffffff" stroke-width="2"/>
  <circle cx="76" cy="34" r="3" fill="#ffffff"/>
</pattern>
<pattern id="p_furos" width="20" height="20" patternUnits="userSpaceOnUse">
  <circle cx="10" cy="10" r="5.2" fill="#3b4348"/>
  <path d="M4.8 10 a5.2 5.2 0 0 0 10.4 0" fill="none" stroke="#ffffff" stroke-opacity="0.55" stroke-width="1.3"/>
</pattern>
<pattern id="p_escovado" width="300" height="5" patternUnits="userSpaceOnUse">
  <rect width="300" height="2" fill="#ffffff" opacity="0.35"/>
</pattern>
<pattern id="p_rosca" width="9" height="80" patternUnits="userSpaceOnUse">
  <rect width="3.5" height="80" fill="#000000" opacity="0.35"/>
  <rect x="3.5" width="1.5" height="80" fill="#ffffff" opacity="0.18"/>
</pattern>
<pattern id="p_aletas" width="26" height="140" patternUnits="userSpaceOnUse">
  <rect width="26" height="140" fill="#20262c"/>
  <rect x="5" width="14" height="140" fill="#7d8892"/>
  <rect x="5" width="5" height="140" fill="#aab4bd"/>
</pattern>
"""


# ======================================================================== primitivas
def _attrs(op=None, filtro=None, dash=None):
    s = ""
    if op is not None:
        s += ' opacity="%g"' % op
    if filtro:
        s += ' filter="url(#%s)"' % filtro
    if dash:
        s += ' stroke-dasharray="%s"' % dash
    return s


def rect(x, y, w, h, fill, stroke="none", sw=0, rx=0, dash=None, op=None, filtro=None):
    add('<rect x="%g" y="%g" width="%g" height="%g" rx="%g" fill="%s" stroke="%s" stroke-width="%g"%s/>'
        % (x, y, w, h, rx, fill, stroke, sw, _attrs(op, filtro, dash)))


def circ(x, y, r, fill, stroke="none", sw=0, op=None, filtro=None):
    add('<circle cx="%g" cy="%g" r="%g" fill="%s" stroke="%s" stroke-width="%g"%s/>'
        % (x, y, r, fill, stroke, sw, _attrs(op, filtro)))


def elipse(x, y, rx, ry, fill, op=None, rot=0):
    add('<ellipse cx="%g" cy="%g" rx="%g" ry="%g" fill="%s" transform="rotate(%g %g %g)"%s/>'
        % (x, y, rx, ry, fill, rot, x, y, _attrs(op)))


def caminho(d, fill, stroke="none", sw=0, op=None, filtro=None):
    add('<path d="%s" fill="%s" stroke="%s" stroke-width="%g" stroke-linejoin="round"%s/>'
        % (d, fill, stroke, sw, _attrs(op, filtro)))


def linha(x1, y1, x2, y2, cor, sw=4, dash=None, op=None):
    add('<line x1="%g" y1="%g" x2="%g" y2="%g" stroke="%s" stroke-width="%g" stroke-linecap="round"%s/>'
        % (x1, y1, x2, y2, cor, sw, _attrs(op, None, dash)))


def texto(x, y, s, tam=24, cor=TXT, peso="normal", ancora="start", fonte=SANS):
    """Uma linha; **negrito** no meio do texto vira tspan."""
    partes = s.split("**")
    corpo = ""
    for i, p in enumerate(partes):
        if p == "":
            continue
        if i % 2:
            corpo += '<tspan font-weight="bold">%s</tspan>' % esc(p)
        else:
            corpo += esc(p)
    add('<text x="%g" y="%g" font-family="%s" font-size="%g" font-weight="%s" fill="%s" text-anchor="%s">%s</text>'
        % (x, y, fonte, tam, peso, cor, ancora, corpo))


def bloco(x, y, linhas, tam=25, entre=1.42, cor=TXT):
    """Varias linhas (lista de strings; "" = meia linha em branco)."""
    for l in linhas:
        if l == "":
            y += tam * entre * 0.5
            continue
        texto(x, y, l, tam, cor)
        y += tam * entre
    return y


def topo_arredondado(x, y, w, h, r):
    return "M %g %g Q %g %g %g %g H %g Q %g %g %g %g V %g H %g Z" % (
        x, y + r, x, y, x + r, y, x + w - r, x + w, y, x + w, y + r, y + h, x)


# ======================================================================== pecas
def fio(pontos, cor, sw=8, dash=None, pulos=()):
    """Fio (cabo com brilho) em angulo reto pelos pontos; pulos = (x, y) onde
    um trecho HORIZONTAL passa por cima de outro fio (meio-circulo)."""
    r = 15
    d = "M %g %g" % pontos[0]
    for (x1, y1), (x2, y2) in zip(pontos, pontos[1:]):
        if y1 == y2:
            sentido = 1 if x2 > x1 else -1
            for px_, _ in sorted([p for p in pulos if p[1] == y1 and min(x1, x2) < p[0] < max(x1, x2)],
                                 key=lambda p: p[0] * sentido):
                d += " L %g %g A %d %d 0 0 %d %g %g" % (px_ - r * sentido, y1, r, r, 1 if sentido > 0 else 0,
                                                         px_ + r * sentido, y1)
        d += " L %g %g" % (x2, y2)
    tr = ' stroke-dasharray="%s"' % dash if dash else ""
    base = '<path d="%s" fill="none" stroke="%s" stroke-width="%g" stroke-linejoin="round" stroke-linecap="round"%s%s/>'
    add(base % (d, "white", sw + 10, "", ""))
    add(base % (d, escurecer(cor, 0.4), sw + 3, tr, ""))
    add(base % (d, cor, sw, tr, ""))
    add(base % (d, "white", max(1.5, sw * 0.28), tr, ' opacity="0.42"'))
    for p in (pontos[0], pontos[-1]):
        circ(p[0], p[1], sw * 0.95, escurecer(cor, 0.3))
        circ(p[0], p[1], sw * 0.6, luz(cor))


def etiqueta(x, y, s, fundo="#212121", cor="white", tam=22, ancora="middle"):
    """Etiqueta (GND, 5V, 12V...): ligar ali."""
    w = len(s) * tam * 0.74 + 22
    x0 = x - w / 2 if ancora == "middle" else (x if ancora == "start" else x - w)
    rect(x0, y - tam * 0.95, w, tam * 1.5, vertical(fundo, 0.2), escurecer(fundo, 0.35), 1.5, 8, filtro="f_sombra_leve")
    texto(x0 + w / 2, y + tam * 0.3, s, tam, cor, "bold", "middle")


def pino_placa(x, y, cor_anel=None):
    circ(x, y, 18, ouro(), "#6e5600", 2)
    circ(x, y, 7, "#3a2a10")
    circ(x - 5, y - 6, 3.5, "white", op=0.55)
    if cor_anel:
        circ(x, y, 23, "none", cor_anel, 5)


def parafuso(x, y, r=11):
    circ(x, y, r, rgrad(((0, "#ffffff"), (0.55, "#b9c2c9"), (1, "#69737b")), 0.35, 0.3), "#4f585f", 1.5)
    linha(x - r * 0.6, y + r * 0.6, x + r * 0.6, y - r * 0.6, "#4f585f", 3)


def furo_fixacao(x, y):
    circ(x, y, 15, metal(), "#7b858c", 2)
    circ(x, y, 8, "white", "#8a949b", 1.5)


def placa(x, y, w, h, cor, rx=18):
    """Placa de circuito: verniz com degrade, trilhas e borda clara."""
    rect(x, y, w, h, grad(((0, clarear(cor, 0.14)), (1, escurecer(cor, 0.2)))), escurecer(cor, 0.45), 4, rx,
         filtro="f_sombra")
    rect(x, y, w, h, "url(#p_trilhas)", rx=rx, op=0.13)
    rect(x + 5, y + 5, w - 10, h - 10, "none", clarear(cor, 0.4), 2, max(rx - 4, 2), op=0.4)


def smd(x, y, w=16, h=8, cor="#c8a46a"):
    rect(x, y, w, h, cor, escurecer(cor, 0.4), 1, 1.5)
    rect(x, y, 3, h, "#d9dde0")
    rect(x + w - 3, y, 3, h, "#d9dde0")


def caixa(x, y, w, h, titulo, cor, tam=28, tracejada=False):
    rect(x, y, w, h, "white", cor, 5, 20, dash="18 10" if tracejada else None, filtro="f_sombra")
    caminho(topo_arredondado(x, y, w, 56, 20), vertical(cor, 0.16))
    rect(x + 3, y + 3, w - 6, 22, "white", rx=16, op=0.12)
    texto(x + 24, y + 39, titulo, tam, "white", "bold", fonte=TITULO)


def secao(x, y, s, tam=32):
    texto(x, y, s, tam, TXT, "bold")


# ======================================================================== desenho
def cabecalho():
    texto(60, 108, "STRIKE KIDS · MONTAGEM DO ARDUINO", 88,
          grad(((0, "#1f4e9c"), (1, "#0b1f45")), 0, 0, 0, 1), "bold", fonte=TITULO)
    if VERSAO == "rele":
        selo, cor, mega, pasta = "VERSÃO DO RELÉ (sem ponte H)", RELE_AZUL, "servo no D11", "arduino/strike_kids"
    else:
        selo, cor, mega, pasta = "VERSÃO COM PONTE H", "#c0392b", "servo no D11 e ENA no D9", \
            "arduino/strike_kids_ponte_h"
    rect(1790, 40, 870, 92, vertical(cor, 0.18), escurecer(cor, 0.35), 2, 22, filtro="f_sombra_leve")
    rect(1796, 46, 858, 30, "white", rx=16, op=0.14)
    texto(2225, 102, selo, 42, "white", "bold", "middle", TITULO)
    texto(60, 162, "Arduino **Nano** visto de cima · no **Uno** e no **Pro Mini**: mesmos nomes de pinos · "
          "no **Mega**: %s · firmware **%s** (versão 3)" % (mega, pasta), 28, "#33415c")
    cores = [COR["S1"], COR["S2"], COR["S3"], COR["S4"], COR["S5"]]
    for i, c in enumerate(cores):
        rect(i * W / 5, 190, W / 5 + 1, 12, vertical(c, 0.2))
    # legenda
    x0, y0 = 2700, 34
    rect(x0, y0, 1060, 128, "white", "#ccd3da", 2, 14, filtro="f_sombra_leve")
    fio([(x0 + 30, y0 + 40), (x0 + 130, y0 + 40)], "#455a64", 8)
    texto(x0 + 150, y0 + 49, "fio: ligar (obrigatório)", 24)
    fio([(x0 + 30, y0 + 92), (x0 + 130, y0 + 92)], "#455a64", 8, dash="16 12")
    texto(x0 + 150, y0 + 101, "tracejado: opcional", 24)
    etiqueta(x0 + 640, y0 + 46, "GND")
    texto(x0 + 700, y0 + 54, "= no GND comum", 24)
    fio([(x0 + 615, y0 + 70), (x0 + 615, y0 + 114)], "#9aa5b1", 8)
    fio([(x0 + 560, y0 + 92), (x0 + 670, y0 + 92)], "#455a64", 8, pulos=[(x0 + 615, y0 + 92)])
    texto(x0 + 700, y0 + 101, "fios só se cruzam (não ligam)", 24)


SENS_X = [40, 412, 784, 1156, 1528]   # cartoes dos sensores (340 de largura)
SENS_NOMES = ["S1 · ESQUERDA", "S2 · MEIO-ESQ.", "S3 · CENTRO", "S4 · MEIO-DIR.", "S5 · DIREITA"]
SENS_PINO = ["D2", "D3", "D4", "D5", "D7"]
SENS_BORNE_Y = 497   # parafusos do borne de cada sensor

# Arduino Nano
NX, NY, NW, NH = 560, 1020, 1240, 300
TOPO = ["TX1", "RX0", "RST", "GND", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9", "D10", "D11", "D12"]
BAIXO = ["VIN", "GND", "RST", "5V", "A7", "A6", "A5", "A4", "A3", "A2", "A1", "A0", "AREF", "3V3", "D13"]
Y_TOPO, Y_BAIXO = NY + 38, NY + NH - 38


def px(nome, linha_de_cima=True):
    lista = TOPO if linha_de_cima else BAIXO
    return NX + 60 + lista.index(nome) * 80


def sensores():
    secao(40, 262, "OS 5 SENSORES DA PISTA — da ESQUERDA para a DIREITA, olhando da criança para os pinos", 30)
    for i, x in enumerate(SENS_X):
        c = COR["S%d" % (i + 1)]
        rect(x, 284, 340, 236, "white", c, 5, 18, filtro="f_sombra")
        caminho(topo_arredondado(x, 284, 340, 52, 18), vertical(c, 0.16))
        rect(x + 3, 287, 334, 20, "white", rx=14, op=0.12)
        texto(x + 170, 320, SENS_NOMES[i], 27, "white", "bold", "middle")
        # o sensor (corpo rosqueado M18, emissor e receptor)
        sx0, sy0 = x + 60, 342
        rect(sx0, sy0, 220, 58, grad(((0, "#6b7782"), (0.45, "#2f373e"), (1, "#181d21"))), "#0f1316", 2, 12)
        rect(sx0 + 6, sy0 + 3, 208, 52, "url(#p_rosca)", rx=10, op=0.7)
        for lx, miolo in ((sx0 + 70, "#e53935"), (sx0 + 150, "#1e3a5f")):
            if miolo == "#e53935":
                circ(lx, sy0 + 29, 25, "#ff5252", op=0.35, filtro="f_brilho")
            circ(lx, sy0 + 29, 18, "#0b0e10")
            circ(lx, sy0 + 29, 14, rgrad(((0, clarear(miolo, 0.7)), (0.5, miolo), (1, escurecer(miolo, 0.6))),
                                         0.4, 0.35))
            elipse(lx - 5, sy0 + 23, 5, 3, "white", 0.75, -30)
        texto(sx0 + 70, sy0 + 74, "emissor", 14, CINZA, ancora="middle")
        texto(sx0 + 150, sy0 + 74, "receptor", 14, CINZA, ancora="middle")
        texto(x + 170, 452, "sinal → %s" % SENS_PINO[i], 28, c, "bold", "middle")
        # borne de 3 parafusos: +, sinal, -
        rect(x + 22, SENS_BORNE_Y - 16, 296, 32, grad(((0, "#3ccf7c"), (1, "#167a42"))), "#0f5c32", 2, 6)
        for xx, rot, cr in ((x + 50, "+", "#8d4004"), (x + 170, "sinal", TXT), (x + 290, "−", "#263238")):
            parafuso(xx, SENS_BORNE_Y, 10)
            texto(xx, SENS_BORNE_Y - 22, rot, 19 if rot == "sinal" else 26, cr, "bold", "middle")
        fio([(x + 50, SENS_BORNE_Y), (x + 50, 554)], "#8d5a2b", 6)
        fio([(x + 290, SENS_BORNE_Y), (x + 290, 554)], "#1565c0", 6)
        etiqueta(x + 50, 568, "+5/12V", "#8d4004", tam=20)
        etiqueta(x + 290, 568, "GND", tam=20)


def nano():
    placa(NX, NY, NW, NH, "#1d5fa8", 22)
    # serigrafia das fileiras de pinos
    rect(NX + 32, Y_TOPO - 27, 1176, 54, "none", "white", 2, 8, op=0.45)
    rect(NX + 32, Y_BAIXO - 27, 1176, 54, "none", "white", 2, 8, op=0.45)
    # cristal de 16 MHz e botao de reset
    rect(NX + 360, NY + 128, 74, 40, metal(), "#7b858c", 2, 18)
    texto(NX + 397, NY + 153, "16.000", 13, "#455a64", "bold", "middle")
    rect(NX + 210, NY + 118, 64, 64, metal(), "#7b858c", 2, 6)
    circ(NX + 242, NY + 150, 20, preto(), "#0b0e10", 2)
    texto(NX + 242, NY + 206, "RESET", 14, "white", "bold", "middle")
    # o processador (TQFP de 32 pernas)
    cx, cy, lado = NX + 540, NY + 150, 96
    for k in range(8):
        d = -lado / 2 + 8 + k * 11.5
        rect(cx + d - 2.5, cy - lado / 2 - 9, 5, 9, "#c3cbd1")
        rect(cx + d - 2.5, cy + lado / 2, 5, 9, "#c3cbd1")
        rect(cx - lado / 2 - 9, cy + d - 2.5, 9, 5, "#c3cbd1")
        rect(cx + lado / 2, cy + d - 2.5, 9, 5, "#c3cbd1")
    rect(cx - lado / 2, cy - lado / 2, lado, lado, preto(), "#0b0e10", 2, 4)
    circ(cx - lado / 2 + 13, cy - lado / 2 + 13, 5, "#444c53")
    texto(cx, cy - 2, "ATMEGA", 15, "#aab4bd", "bold", "middle")
    texto(cx, cy + 18, "328P", 15, "#aab4bd", "bold", "middle")
    # pecinhas SMD
    for sx, sy in ((NX + 630, NY + 112), (NX + 630, NY + 176), (NX + 300, NY + 100), (NX + 300, NY + 192),
                   (NX + 1060, NY + 112), (NX + 1060, NY + 182), (NX + 120, NY + 140)):
        smd(sx, sy)
    for sx, sy in ((NX + 660, NY + 140), (NX + 140, NY + 176)):
        smd(sx, sy, 16, 9, "#30363b")
    texto(NX + 880, NY + 146, "ARDUINO NANO", 32, "white", "bold", "middle", TITULO)
    texto(NX + 880, NY + 176, "ATmega328P · 16 MHz · 5 V", 19, "#cfe3f7", ancora="middle")
    # LEDs da placa
    circ(NX + 1180, NY + 150, 16, "#ffb300", op=0.5, filtro="f_brilho")
    rect(NX + 1170, NY + 142, 20, 16, luz("#ffb300"), "#b37400", 1, 3)
    texto(NX + 1180, NY + 192, "L = D13", 18, "white", "bold", "middle")
    for fx, fy in ((NX + 24, NY + 24), (NX + NW - 24, NY + 24), (NX + 24, NY + NH - 24), (NX + NW - 24, NY + NH - 24)):
        furo_fixacao(fx, fy)
    # USB (mini)
    rect(NX + NW - 10, NY + 95, 95, 110, metal(), "#6c767d", 3, 8, filtro="f_sombra_leve")
    rect(NX + NW - 4, NY + 103, 83, 6, "white", op=0.6)
    rect(NX + NW + 4, NY + 124, 66, 52, grad(((0, "#1a1f23"), (1, "#3d464d"))), "#0b0e10", 2, 6)
    rect(NX + NW + 16, NY + 140, 42, 20, "#c3cbd1", rx=3)
    usados_topo = {"GND": None, "D2": COR["S1"], "D3": COR["S2"], "D4": COR["S3"], "D5": COR["S4"],
                   "D6": COR["D6"], "D7": COR["S5"], "D8": "#9e9e9e", "D9": "#9e9e9e",
                   "D10": COR["IN1"], "D11": COR["ENA"], "D12": COR["IN2"]}
    usados_baixo = {"GND": None, "5V": None, "A4": COR["A4"], "A3": COR["A3"], "A2": COR["A2"],
                    "A1": COR["A1"], "A0": COR["A0"]}
    if VERSAO == "rele":   # D10-D12, A3 e A4 livres (sao da ponte H)
        for nome in ("D10", "D11", "D12"):
            del usados_topo[nome]
        del usados_baixo["A3"], usados_baixo["A4"]
        usados_topo["D8"], usados_topo["D9"] = COR["D8"], COR["D9"]
    for nome in TOPO:
        x = px(nome)
        pino_placa(x, Y_TOPO, usados_topo.get(nome))
        texto(x, Y_TOPO + 50, nome, 22, "white" if nome in usados_topo else "#aed6f1",
              "bold" if nome in usados_topo else "normal", "middle")
    for nome in BAIXO:
        x = px(nome, False)
        pino_placa(x, Y_BAIXO, usados_baixo.get(nome))
        texto(x, Y_BAIXO - 33, nome, 22, "white" if nome in usados_baixo else "#aed6f1",
              "bold" if nome in usados_baixo else "normal", "middle")


def fios_sensores_e_fita():
    # niveis das curvas: ninguem atravessa ninguem (so o D6 pula o S5)
    nivel = {"S1": 680, "S2": 650, "S3": 620, "S4": 640, "S5": 700}
    for i, x in enumerate(SENS_X):
        s = "S%d" % (i + 1)
        sx = x + 170
        dx = px(SENS_PINO[i])
        fio([(sx, SENS_BORNE_Y), (sx, nivel[s]), (dx, nivel[s]), (dx, Y_TOPO)], COR[s])
    # D6 -> fita de LED (passa por cima do fio do S5)
    x6 = px("D6")
    fio([(x6, Y_TOPO), (x6, 740), (1940, 740), (1940, 276), (2018, 276)], COR["D6"], pulos=[(px("D7"), 740)])
    texto(1955, 250, "DIN", 24, COR["D6"], "bold")


def fita():
    texto(2010, 232, "FITA DE LED WS2812 (opcional) — DIN no **D6** (resistor de 330 Ω no caminho)", 26, TXT)
    rect(2010, 248, 1750, 58, grad(((0, "#454e55"), (0.5, "#22282d"), (1, "#121518"))), "#0b0e10", 2, 14,
         filtro="f_sombra_leve")
    cores = ["#ff5252", "#ffd740", "#40c4ff", "#69f0ae", "#ff80ab", "#b388ff"]
    for i in range(30):
        cx = 2045 + i * 57.5
        c = cores[i % len(cores)]
        if i < 29:
            rect(cx + 23, 258, 10, 8, "#d19a4a", rx=1)
            rect(cx + 23, 288, 10, 8, "#d19a4a", rx=1)
        circ(cx, 277, 21, c, op=0.6, filtro="f_brilho")
        rect(cx - 17, 260, 34, 34, grad(((0, "#ffffff"), (1, "#dfe3e6"))), "#aab2b8", 1, 5)
        circ(cx, 277, 11, luz(c))
    texto(3760, 340, "+5V e GND da fita: FONTE 5 V própria (60 LEDs ≈ 3,6 A) + capacitor de 1000 µF · GND junto",
          22, CINZA, ancora="end")


# ponte H (L298N): pinos na borda de baixo
PH_X, PH_Y, PH_W, PH_H = 2060, 420, 660, 330
PH_PINOS = ["ENA", "IN1", "IN2", "IN3", "IN4", "ENB"]
PH_FORCA = ["+12V", "GND", "+5V"]
FONTE_X, FONTE_Y = 2500, 1080


def ph_x(nome):
    if nome in PH_PINOS:
        return PH_X + 70 + PH_PINOS.index(nome) * 62
    return PH_X + 490 + PH_FORCA.index(nome) * 70


def borne(x, y, w, h, parafusos):
    rect(x, y, w, h, grad(((0, "#40d585"), (0.55, "#1f9a55"), (1, "#136b3a"))), "#0d5230", 2, 5)
    rect(x + 3, y + 3, w - 6, 6, "white", rx=3, op=0.25)
    for px_, py in parafusos:
        parafuso(px_, py, 11)


def fonte_12v(fx=FONTE_X, fy=FONTE_Y, bornes=None, uso="2 A ou mais (motor + moedeiro)"):
    """A fonte de 12 V (metal perfurado): bornes = os x do +V e do −V (de
    fabrica, embaixo do +12V e do GND da ponte H)."""
    w, h = 270, 230
    if bornes is None:
        bornes = ((ph_x("+12V"), "+V"), (ph_x("GND"), "−V"))
    rect(fx, fy, w, h, metal(True), "#7f8b93", 3, 12, filtro="f_sombra")
    rect(fx, fy, w, h, "url(#p_escovado)", rx=12, op=0.5)
    # borne de cima: +V, -V, L, N
    rect(fx + 12, fy + 8, w - 24, 48, preto(), "#0b0e10", 2, 6)
    for xx, rot in tuple(bornes) + ((fx + 205, "L"), (fx + 246, "N")):
        parafuso(xx, fy + 31, 13)
        texto(xx, fy + 78, rot, 17, "#263238", "bold", "middle")
    rect(fx + 14, fy + 88, w - 28, 50, "url(#p_furos)", rx=4)
    # plaqueta
    rect(fx + 14, fy + 146, w - 28, 76, grad(((0, "#24476f"), (1, "#0f2238"))), "#091726", 2, 6)
    texto(fx + w / 2, fy + 178, "FONTE 12 V", 27, "white", "bold", "middle", TITULO)
    texto(fx + w / 2, fy + 198, uso, 13, "#cfe3f7", ancora="middle")
    texto(fx + w / 2, fy + 215, "tomada 127/220 V ~ no L e N", 13, "#cfe3f7", ancora="middle")


def ponte_h():
    texto(2060, 398, "PORTA DAS BOLAS — PONTE H (L298N) + MOTOR", 30, TXT, "bold")
    placa(PH_X, PH_Y, PH_W, PH_H, "#c0392b", 18)
    furo_fixacao(PH_X + 24, PH_Y + 22)
    furo_fixacao(PH_X + PH_W - 24, PH_Y + 22)
    # dissipador de aluminio e o L298N preso nele
    rect(PH_X + 196, PH_Y + 18, 258, 128, "#1d2328", "#0d1013", 2, 6, filtro="f_sombra_leve")
    rect(PH_X + 200, PH_Y + 22, 250, 120, "url(#p_aletas)", rx=4)
    rect(PH_X + 200, PH_Y + 22, 250, 120, grad(((0, "#ffffff", 0.35), (0.3, "#ffffff", 0), (1, "#000000", 0.35))), rx=4)
    for k in range(15):
        rect(PH_X + 262 + k * 8.6, PH_Y + 132, 4, 14, "#c3cbd1")
    rect(PH_X + 250, PH_Y + 92, 150, 44, preto(), "#0b0e10", 2, 4)
    texto(PH_X + 325, PH_Y + 121, "L298N", 17, "#d5dbe0", "bold", "middle")
    # capacitor (visto de cima) e um regulador
    circ(PH_X + 486, PH_Y + 74, 30, grad(((0, "#3557a8"), (1, "#162a5c"))), "#0f1d40", 2)
    circ(PH_X + 486, PH_Y + 74, 22, rgrad(((0, "#ffffff"), (0.6, "#c3cbd1"), (1, "#7b858c")), 0.4, 0.35))
    linha(PH_X + 476, PH_Y + 74, PH_X + 496, PH_Y + 74, "#6c767d", 2)
    linha(PH_X + 486, PH_Y + 64, PH_X + 486, PH_Y + 84, "#6c767d", 2)
    rect(PH_X + 130, PH_Y + 22, 44, 30, preto(), "#0b0e10", 1.5, 3)
    for k in range(3):
        rect(PH_X + 138 + k * 12, PH_Y + 52, 4, 10, "#c3cbd1")
    texto(PH_X + 325, PH_Y + 172, "PONTE H · L298N", 26, "white", "bold", "middle")
    texto(PH_X + 325, PH_Y + 196, "(outras pontes: quadro embaixo)", 17, "#fadbd8", ancora="middle")
    # saidas do motor (direita) e as que nao usa (esquerda)
    for i, nome in enumerate(["OUT1", "OUT2"]):
        y = PH_Y + 60 + i * 70
        borne(PH_X + PH_W - 50, y - 24, 50, 48, [(PH_X + PH_W - 25, y)])
        texto(PH_X + PH_W - 62, y + 8, nome, 20, "white", "bold", "end")
    for i, nome in enumerate(["OUT3", "OUT4"]):
        y = PH_Y + 60 + i * 70
        borne(PH_X, y - 24, 50, 48, [(PH_X + 25, y)])
        texto(PH_X + 60, y + 8, nome, 18, "#f5b7b1", ancora="start")
    texto(PH_X + 8, PH_Y + 162, "não usa", 17, "#f5b7b1", ancora="start")
    # header (pinos quadrados dourados) e jumpers
    rect(PH_X + 40, PH_Y + PH_H - 70, 372, 50, preto(), "#0b0e10", 2, 6)
    for nome in PH_PINOS:
        x = ph_x(nome)
        rect(x - 9, PH_Y + PH_H - 54, 18, 18, ouro(), "#6e5600", 1.5, 2)
        texto(x, PH_Y + PH_H - 84, nome, 19, "white", "bold" if nome in ("ENA", "IN1", "IN2") else "normal", "middle")
    for nome in ("ENA", "ENB"):
        rect(ph_x(nome) - 14, PH_Y + PH_H - 128, 28, 30, preto(), "#000000", 1.5, 4)
        rect(ph_x(nome) - 10, PH_Y + PH_H - 125, 20, 6, "white", rx=3, op=0.3)
    texto(ph_x("ENA"), PH_Y + PH_H - 136, "jumper", 16, "#fadbd8", ancora="middle")
    # borne de forca
    yh = PH_Y + PH_H - 45
    borne(PH_X + 460, PH_Y + PH_H - 78, 220, 60, [(ph_x(n), PH_Y + PH_H - 48) for n in PH_FORCA])
    for nome in PH_FORCA:
        texto(ph_x(nome), PH_Y + PH_H - 88, nome, 19, "white", "bold", "middle")
    # fios do Arduino (D10 passa por cima do D11, que vai ao ENA por dentro)
    fio([(px("D10"), Y_TOPO), (px("D10"), 840), (ph_x("IN1"), 840), (ph_x("IN1"), yh)], COR["IN1"],
        pulos=[(px("D11"), 840)])
    fio([(px("D12"), Y_TOPO), (px("D12"), 890), (ph_x("IN2"), 890), (ph_x("IN2"), yh)], COR["IN2"])
    fio([(px("D11"), Y_TOPO), (px("D11"), 790), (ph_x("ENA"), 790), (ph_x("ENA"), yh)], COR["ENA"], dash="16 12")
    texto(px("D12") + 40, 829, "D10 → IN1", 22, COR["IN1"], "bold")
    texto(px("D12") + 40, 879, "D12 → IN2", 22, COR["IN2"], "bold")
    texto(px("D12") + 40, 779, "D11 → ENA (opcional)", 21, "#546e7a", "bold")
    # D8 e D9: opcionais (quadro do servo e do rele)
    for nome, rot in (("D8", "RELÉ"), ("D9", "SERVO")):
        x = px(nome)
        fio([(x, Y_TOPO), (x, 975)], "#9e9e9e", 6, dash="10 8")
        etiqueta(x, 960, rot, "#8d8d8d", tam=16)
    # forca: 12 V
    fonte_12v()
    fy = FONTE_Y + 31
    fio([(ph_x("+12V"), PH_Y + PH_H - 48), (ph_x("+12V"), fy)], COR["12V"])
    fio([(ph_x("GND"), PH_Y + PH_H - 48), (ph_x("GND"), fy)], COR["GND"])
    fio([(ph_x("GND"), FONTE_Y - 70), (ph_x("GND") + 66, FONTE_Y - 70)], COR["GND"], 6)
    etiqueta(ph_x("GND") + 118, FONTE_Y - 70, "GND", tam=19)
    texto(ph_x("+5V") - 20, yh + 82, "+5V: solto", 18, CINZA, "bold")
    texto(FONTE_X, FONTE_Y + 262, "o − da fonte 12 V também", 20, CINZA)
    texto(FONTE_X, FONTE_Y + 288, "vai no GND do Arduino", 20, CINZA)


def micro_switch_pequeno(x, y, alavanca):
    """O fim de curso no desenho da porta (alavanca: 1 = para a direita)."""
    rect(x - 24, y - 10, 48, 66, preto(), "#0b0e10", 2, 6, filtro="f_sombra_leve")
    circ(x, y + 6, 4, "#5c666e")
    circ(x, y + 40, 4, "#5c666e")
    linha(x, y - 10, x + 16 * alavanca, y - 44, "#b0bac2", 5)
    circ(x + 16 * alavanca, y - 44, 6, metal(), "#7b858c", 1.5)


def porta():
    x0, y0, w, h = 2800, 420, 960, 600
    rect(x0, y0, w, h, "white", "#90a4ae", 3, 18, filtro="f_sombra")
    texto(x0 + 24, y0 + 44, "MOTOR DA PORTA (motor DC com redução)", 26, TXT, "bold")
    mx, my = x0 + 130, y0 + 210
    # caixa de reducao (amarela) e eixo
    rect(mx + 40, my - 62, 82, 124, grad(((0, "#ffe57f"), (0.5, "#f4c20d"), (1, "#b38600"))), "#8a6700", 3, 10,
         filtro="f_sombra_leve")
    for py in (my - 40, my + 40):
        parafuso(mx + 81, py, 8)
    rect(mx + 122, my - 11, 62, 22, metal(), "#6c767d", 2, 4)
    # motor (lata de metal) com o rotulo
    rect(mx - 96, my - 40, 20, 80, grad(((0, "#c9d0d5"), (0.5, "#7d878f"), (1, "#bfc6cb"))), "#5f6b73", 2, 6)
    rect(mx - 82, my - 52, 126, 104, metal(), "#5f6b73", 3, 14, filtro="f_sombra_leve")
    rect(mx - 58, my - 52, 64, 104, grad(((0, "#2f6fd0"), (0.5, "#1a4fa0"), (1, "#123a78"))))
    texto(mx - 26, my + 10, "M", 38, "white", "bold", "middle")
    texto(mx - 26, my + 34, "12 V", 14, "#cfe3f7", "bold", "middle")
    # fios do motor saem do OUT1/OUT2
    fio([(PH_X + PH_W - 25, PH_Y + 60), (mx - 30, PH_Y + 60), (mx - 30, my - 52)], "#c62828", 8)
    fio([(PH_X + PH_W - 25, PH_Y + 130), (2770, PH_Y + 130), (2770, my + 80), (mx + 30, my + 80),
         (mx + 30, my + 52)], "#263238", 8)
    texto(mx + 60, my - 82, "2 fios do motor", 20, CINZA)
    # porta: trilho, folha de acrilico e os dois fins de curso
    tx, ty = x0 + 330, y0 + 230
    rect(tx, ty + 70, 580, 18, metal(), "#7b858c", 2, 6)
    rect(tx + 60, ty - 60, 200, 130, grad(((0, "#fff3c4"), (0.6, "#ffd54f"), (1, "#f0a800"))), "#e08e00", 4, 10,
         filtro="f_sombra_leve")
    caminho("M %g %g l 60 0 l -70 130 l -60 0 z" % (tx + 120, ty - 60), "white", op=0.35)
    texto(tx + 160, ty + 6, "PORTA", 24, "#8d4004", "bold", "middle")
    texto(tx + 160, ty + 34, "das bolas", 20, "#8d4004", ancora="middle")
    linha(tx + 290, ty + 5, tx + 390, ty + 5, "#8d4004", 4)
    caminho("M %g %g l -18 -10 l 0 20 z" % (tx + 280, ty + 5), "#8d4004")
    caminho("M %g %g l 18 -10 l 0 20 z" % (tx + 400, ty + 5), "#8d4004")
    linha(mx + 184, my, tx + 60, my, "#78909c", 6, "4 8")
    for fx, nome, pino, cor, lado in ((tx + 20, "FECHADA", "A4", COR["A4"], 1), (tx + 540, "ABERTA", "A3", COR["A3"], -1)):
        micro_switch_pequeno(fx, ty, lado)
        texto(fx, ty + 126, "fim de curso", 19, CINZA, "bold", "middle")
        texto(fx, ty + 152, "%s → %s" % (nome, pino), 22, cor, "bold", "middle")
    texto(x0 + 24, y0 + 470, "FIM DE CURSO (opcional): no menu, **Porta: fim de curso = SIM**;", 21)
    texto(x0 + 24, y0 + 500, "a ligação dos micro-switches está no quadro de baixo, à esquerda.", 21)
    texto(x0 + 24, y0 + 540, "**ou ATUADOR LINEAR 12 V** nos mesmos 2 fios: ele para sozinho no", 21)
    texto(x0 + 24, y0 + 570, "fim do curso (sem micro-switch; menu: **Porta: tempo do motor**).", 21)


def tv_box():
    x0, y0 = 1990, 1050
    fio([(NX + NW + 70, NY + 150), (x0 - 34, NY + 150)], COR["USB"], 16)
    rect(x0 - 40, NY + 134, 44, 32, metal(), "#6c767d", 2, 4)
    texto((NX + NW + 70 + x0) / 2, NY + 124, "USB", 22, TXT, "bold", "middle")
    rect(x0, y0, 470, 230, grad(((0, "#46525d"), (0.06, "#262f38"), (1, "#0b0f13"))), "#05080a", 3, 18,
         filtro="f_sombra")
    rect(x0 + 20, y0 + 20, 430, 110, grad(((0, "#3a5068"), (1, "#1c2a38"))), "#0b1118", 2, 10)
    caminho("M %g %g l 120 0 l -60 110 l -120 0 z" % (x0 + 260, y0 + 20), "white", op=0.06)
    texto(x0 + 235, y0 + 72, "TV BOX (Android)", 30, "#ffd43b", "bold", "middle", TITULO)
    texto(x0 + 235, y0 + 108, "o jogo Strike Kids", 22, "white", ancora="middle")
    texto(x0 + 235, y0 + 172, "USB: dados + alimenta o Arduino", 20, "white", "bold", "middle")
    texto(x0 + 235, y0 + 206, "1ª vez: marque “Usar por padrão”", 19, "#d6dbdf", ancora="middle")
    circ(x0 + 446, y0 + 150, 9, "#40c4ff", op=0.7, filtro="f_brilho")
    circ(x0 + 446, y0 + 150, 5, luz("#40c4ff"))


def gnd_5v_placa():
    for nome, rot, fundo in (("GND", "GND COMUM", "#212121"), ("5V", "5V", "#c62828")):
        x = px(nome, False)
        fio([(x, Y_BAIXO), (x, 1378)], fundo, 6)
        etiqueta(x, 1392, rot, fundo, tam=19)


def micro_switch(cx, yh):
    """Micro-switch grande (quadro dos fins de curso): terminais em cima."""
    for k, t in enumerate(("C", "NF", "NA")):
        tx = cx - 70 + k * 70
        rect(tx - 12, yh - 34, 24, 38, metal(True), "#6c767d", 1.5, 3)
        circ(tx, yh - 22, 4, "white")
    rect(cx - 110, yh, 220, 110, preto(), "#0b0e10", 3, 10, filtro="f_sombra_leve")
    rect(cx - 104, yh + 4, 208, 10, "white", rx=5, op=0.12)
    for hx in (cx - 80, cx + 80):
        circ(hx, yh + 78, 8, "#0b0e10", "#59636b", 2)
    linha(cx - 90, yh + 116, cx + 80, yh + 150, "#9aa5ae", 8)
    linha(cx - 90, yh + 114, cx + 80, yh + 148, "#e3e8eb", 3)
    circ(cx + 82, yh + 150, 13, rgrad(((0, "#ffffff"), (0.6, "#c3cbd1"), (1, "#6c767d")), 0.35, 0.3), "#59636b", 2)
    for k, t in enumerate(("C", "NF", "NA")):
        texto(cx - 70 + k * 70, yh + 40, t, 22, "white", "bold", "middle")
    texto(cx, yh + 86, "micro-switch", 18, "#cfd8dc", "bold", "middle")


def fins_de_curso():
    x0, y0, w, h = 40, 1560, 860, 410
    caixa(x0, y0, w, h, "FIM DE CURSO (opcional)", COR["A3"], 26, tracejada=True)
    bloco(x0 + 24, y0 + 100, [
        "Micro-switch na porta,",
        "um em cada ponta:",
        "",
        "**C** → GND",
        "**NA** → A4 / A3",
        "**NF** → solto",
        "",
        "(ligado no NF:",
        "FC_NF 1 no .ino)",
    ], 21, 1.45)
    yh = y0 + 150
    for cx, nome, pino in ((x0 + 430, "PORTA FECHADA", "A4"), (x0 + 710, "PORTA ABERTA", "A3")):
        micro_switch(cx, yh)
        texto(cx, yh + 200, nome, 23, COR[pino], "bold", "middle")
        texto(cx, yh + 230, "NA → %s · C → GND" % pino, 19, TXT, ancora="middle")
        fio([(cx - 70, yh - 22), (cx - 70, yh - 62)], "#262626", 5)
        etiqueta(cx - 70, yh - 72, "GND", tam=16)
    # fios dos fins de curso (A4 por cima, A3 por baixo: nao se cruzam)
    fio([(px("A4", False), Y_BAIXO), (px("A4", False), 1450), (x0 + 500, 1450), (x0 + 500, yh - 22)], COR["A4"])
    fio([(px("A3", False), Y_BAIXO), (px("A3", False), 1490), (x0 + 780, 1490), (x0 + 780, yh - 22)], COR["A3"])
    texto(x0 + 524, 1438, "A4 → NA (porta FECHADA)", 21, COR["A4"], "bold")
    texto(x0 + 804, 1528, "A3 → NA (porta ABERTA)", 21, COR["A3"], "bold")


def botao_fliperama(bx, by, cor):
    circ(bx, by + 8, 70, "#000000", op=0.18, filtro="f_brilho")
    circ(bx, by, 68, rgrad(((0, "#5b6670"), (0.7, "#2a3138"), (1, "#101417")), 0.4, 0.3), "#0b0e10", 2)
    circ(bx, by, 52, escurecer(cor, 0.35))
    circ(bx, by - 3, 48, cupula(cor))
    elipse(bx - 16, by - 22, 20, 11, "white", 0.6, -25)


def _botao(x, y0, w, titulo, cor, pino, nivel, oque):
    caixa(x, y0, w, 390, titulo, cor, 26)
    bx, by = x + 160, y0 + 190
    # os 2 fios do botao: um no pino (vem de cima, pela direita), outro no GND
    entra = x + w - 90
    fio([(px(pino, False), Y_BAIXO), (px(pino, False), nivel), (entra, nivel), (entra, by), (bx + 64, by)], COR[pino])
    fio([(bx - 64, by), (bx - 112, by)], "#262626", 6)
    botao_fliperama(bx, by, cor)
    etiqueta(bx - 112, by + 40, "GND", tam=17)
    texto(entra + 12, by - 16, pino, 24, COR[pino], "bold")
    texto(x + w / 2, y0 + 300, "um fio no %s, outro no GND" % pino, 20, TXT, "bold", "middle")
    texto(x + w / 2, y0 + 334, oque, 19, CINZA, ancora="middle")


def botoes():
    # moedeiro (A2), START (A1), CONFIGURACAO (A0)
    y0 = 1580
    mx, mw = 920, 500
    caixa(mx, y0, mw, 390, "MOEDEIRO / FICHA", COR["A2"], 26)
    # frente cromada, fenda, botao de devolver e a ficha
    rect(mx + 30, y0 + 84, 160, 210, metal(True), "#6c767d", 3, 10, filtro="f_sombra_leve")
    rect(mx + 30, y0 + 84, 160, 210, "url(#p_escovado)", rx=10, op=0.6)
    rect(mx + 86, y0 + 104, 48, 10, "#1a1f23", "#0b0e10", 1, 4)
    circ(mx + 110, y0 + 262, 14, rgrad(((0, "#ff8a80"), (1, "#b71c1c")), 0.4, 0.35), "#7f0000", 1.5)
    circ(mx + 110, y0 + 186, 36, rgrad(((0, "#fff6c2"), (0.55, "#f2c12e"), (1, "#a77800")), 0.35, 0.3),
         "#8a6400", 2, filtro="f_sombra_leve")
    circ(mx + 110, y0 + 186, 26, "none", "#a77800", 2)
    texto(mx + 110, y0 + 195, "R$", 22, "#7a5600", "bold", "middle")
    texto(mx + 110, y0 + 322, "(CH-926 e parecidos)", 17, CINZA, ancora="middle")
    # COIN em cima: o fio do A2 desce e entra nele
    xa2 = px("A2", False)
    yc = y0 + 120
    fio([(xa2, Y_BAIXO), (xa2, yc), (mx + 190, yc)], COR["A2"])
    texto(mx + 206, yc - 14, "COIN → A2", 21, COR["A2"], "bold")
    for k, (n, c, et, fundo) in enumerate((("DC12V", "#d32f2f", "+12V", "#d32f2f"),
                                           ("GND", "#262626", "GND", "#212121"),
                                           ("COUNTER", "#bdbdbd", "solto", "#9e9e9e"))):
        yy = yc + 58 + k * 50
        fio([(mx + 190, yy), (mx + 260, yy)], c, 7)
        texto(mx + 274, yy + 8, n, 20, TXT, "bold")
        etiqueta(mx + 445, yy, et, fundo, tam=17)
    texto(mx + mw / 2, y0 + 362, "1 pulso = 1 ficha · moedeiro NA ou NF", 18, TXT, ancora="middle")
    _botao(1440, y0, 440, "BOTÃO START", COR["A1"], "A1", 1530, "começa a partida (o da Zero Delay também)")
    _botao(1900, y0, 440, "CONFIGURAÇÃO", COR["A0"], "A0", 1470, "entra e sai do menu do operador")


def opcionais():
    x0, y0, w, h = 2800, 1080, 960, 420
    rect(x0, y0, w, h, "white", "#9e9e9e", 4, 18, dash="18 10", filtro="f_sombra")
    texto(x0 + 24, y0 + 44, "OPCIONAL — TRAVA POR SERVO (D9) OU RELÉ (D8)", 25, "#616161", "bold")
    texto(x0 + 24, y0 + 76, "junto com a ponte H (o mesmo comando) · sem ponte H: arduino/strike_kids", 20, CINZA)
    # servo
    sx, sy = x0 + 30, y0 + 110
    rect(sx, sy, 410, 280, "#f7f9fa", "#cfd6db", 2, 12)
    rect(sx + 14, sy + 70, 202, 30, grad(((0, "#3a6aa8"), (1, "#173a6b"))), "#0f2a4d", 2, 6)
    rect(sx + 30, sy + 40, 170, 110, grad(((0, "#3a6aa8"), (0.5, "#1f4e8a"), (1, "#0f2a4d"))), "#0b1f3a", 2, 10,
         filtro="f_sombra_leve")
    rect(sx + 44, sy + 112, 100, 26, "#e9eef2", rx=3)
    texto(sx + 94, sy + 131, "MG996R", 15, "#1f4e8a", "bold", "middle")
    circ(sx + 115, sy + 82, 30, rgrad(((0, "#ffffff"), (0.7, "#e3e8eb"), (1, "#9aa5ae")), 0.4, 0.35), "#7b858c", 2)
    rect(sx + 115, sy + 75, 92, 14, grad(((0, "#ffffff"), (1, "#d5dbe0"))), "#7b858c", 1.5, 7)
    circ(sx + 115, sy + 82, 7, metal(), "#6c767d", 1.5)
    texto(sx + 115, sy + 186, "SERVO", 24, TXT, "bold", "middle")
    for i, (n, c, et, fundo) in enumerate((("sinal", "#ef6c00", "D9", "#8d8d8d"),
                                            ("+", "#c62828", "5-6 V", "#c62828"),
                                            ("−", "#4e342e", "GND", "#212121"))):
        yy = sy + 60 + i * 50
        fio([(sx + 200, yy), (sx + 250, yy)], c, 6)
        texto(sx + 260, yy + 8, n, 20, TXT, "bold")
        etiqueta(sx + 362, yy, et, fundo, tam=16)
    texto(sx + 20, sy + 226, "fonte 5-6 V própria", 19, CINZA)
    texto(sx + 20, sy + 254, "0° = fechada · 90° = aberta", 19, CINZA)
    # rele
    rx, ry = x0 + 470, y0 + 110
    rect(rx, ry, 460, 280, "#f7f9fa", "#cfd6db", 2, 12)
    placa(rx + 20, ry + 30, 200, 150, "#1a5276", 10)
    rect(rx + 52, ry + 50, 136, 70, grad(((0, "#5aa9e6"), (0.5, "#2e86c1"), (1, "#1b5d8c"))), "#123f61", 2, 6,
         filtro="f_sombra_leve")
    texto(rx + 120, ry + 82, "relé", 22, "white", "bold", "middle")
    texto(rx + 120, ry + 104, "SRD-05VDC", 12, "#d6eaf8", ancora="middle")
    circ(rx + 196, ry + 48, 6, "#ff5252", op=0.6, filtro="f_brilho")
    circ(rx + 196, ry + 48, 4, luz("#ff5252"))
    for i, (n, et, fundo) in enumerate((("IN", "D8", "#8d8d8d"), ("VCC", "5V", "#c62828"), ("GND", "GND", "#212121"))):
        xx = rx + 50 + i * 70
        texto(xx, ry + 166, n, 17, "white", "bold", "middle")
        fio([(xx, ry + 180), (xx, ry + 214)], "#616161", 5)
        etiqueta(xx, ry + 230, et, fundo, tam=15)
    texto(rx + 240, ry + 60, "COM / NA:", 19, TXT, "bold")
    texto(rx + 240, ry + 88, "trava solenoide", 19, TXT)
    texto(rx + 240, ry + 116, "+ fonte 12 V", 19, TXT)
    texto(rx + 240, ry + 144, "(diodo 1N4007", 17, CINZA)
    texto(rx + 240, ry + 168, "na trava)", 17, CINZA)


# ---------------------------------------------------------------- versao do RELE
# modulo rele (1 canal): IN, VCC e GND embaixo; NO, COM e NC do lado direito
RX, RY, RW, RH = 2060, 430, 560, 360
RL_PINOS = {"IN": RX + 80, "VCC": RX + 170, "GND": RX + 260}
RL_BORNE = {"NO": RY + 70, "COM": RY + 140, "NC": RY + 210}


def rele():
    texto(2060, 398, "PORTA DAS BOLAS — MÓDULO RELÉ (D8) + TRAVA", 30, TXT, "bold")
    placa(RX, RY, RW, RH, RELE_AZUL, 18)
    furo_fixacao(RX + 24, RY + 22)
    furo_fixacao(RX + 24, RY + RH - 24)
    # o rele (cubo azul) com o LED e um transistor
    rect(RX + 150, RY + 36, 240, 150, grad(((0, "#5aa9e6"), (0.5, "#2e86c1"), (1, "#1b5d8c"))), "#123f61", 2, 8,
         filtro="f_sombra_leve")
    rect(RX + 156, RY + 42, 228, 26, "white", rx=6, op=0.16)
    texto(RX + 270, RY + 104, "relé", 34, "white", "bold", "middle")
    texto(RX + 270, RY + 140, "SRD-05VDC", 17, "#d6eaf8", ancora="middle")
    circ(RX + 70, RY + 60, 10, "#ff5252", op=0.6, filtro="f_brilho")
    circ(RX + 70, RY + 60, 7, luz("#ff5252"))
    texto(RX + 70, RY + 96, "LED", 16, "#d6eaf8", "bold", "middle")
    rect(RX + 54, RY + 120, 34, 24, preto(), "#0b0e10", 1.5, 3)
    for k in range(3):
        rect(RX + 60 + k * 10, RY + 144, 4, 8, "#c3cbd1")
    smd(RX + 104, RY + 128)
    texto(RX + 240, RY + 224, "MÓDULO RELÉ 5 V · 1 canal", 22, "white", "bold", "middle")
    # borne do contato (direita): NO, COM e NC com parafuso
    borne(RX + RW - 60, RY + 40, 60, 200, [(RX + RW - 30, y) for y in RL_BORNE.values()])
    for nome, y in RL_BORNE.items():
        texto(RX + RW - 72, y + 8, nome, 20, "white", "bold", "end")
    texto(RX + RW - 8, RY + 268, "NC: solto", 16, "#d6eaf8", "bold", "end")
    # pinos (embaixo, quadrados dourados)
    rect(RX + 40, RY + RH - 70, 260, 50, preto(), "#0b0e10", 2, 6)
    for nome, x in RL_PINOS.items():
        rect(x - 9, RY + RH - 54, 18, 18, ouro(), "#6e5600", 1.5, 2)
        texto(x, RY + RH - 84, nome, 19, "white", "bold", "middle")
    yp = RY + RH - 45
    # D8 -> IN
    fio([(px("D8"), Y_TOPO), (px("D8"), 840), (RL_PINOS["IN"], 840), (RL_PINOS["IN"], yp)], COR["D8"])
    texto(px("D12") + 40, 829, "D8 → IN do relé", 22, COR["D8"], "bold")
    # VCC no 5V e GND no GND
    for nome, rot, cor in (("VCC", "5V", "#c62828"), ("GND", "GND", "#212121")):
        x = RL_PINOS[nome]
        fio([(x, yp), (x, yp + 66)], cor, 6)
        etiqueta(x, yp + 84, rot, cor, tam=19)
    texto(RL_PINOS["GND"] + 50, yp + 92, "do Arduino", 19, CINZA, "bold")
    texto(px("D12") + 40, 964, "D9 → servo (opcional)", 21, COR["D9"], "bold")


# a fonte de 12 V da versao do rele (a mesma peca da ponte H, noutro lugar)
RF_X, RF_Y = 3460, 690


def trava():
    tx, ty, tw, th = 2800, 450, 420, 170
    # a trava solenoide: corpo de metal escovado com a bobina e a lingueta
    rect(tx, ty, tw, th, metal(True), "#7f8b93", 3, 14, filtro="f_sombra")
    rect(tx, ty, tw, th, "url(#p_escovado)", rx=14, op=0.5)
    rect(tx + 14, ty + 14, tw - 28, 26, "white", rx=10, op=0.25)
    for xx in (tx + 22, tx + tw - 22):
        for yy in (ty + 22, ty + th - 22):
            parafuso(xx, yy, 8)
    rect(tx + 44, ty + 46, tw - 88, 82, grad(((0, "#c0392b"), (1, "#7b241c"))), "#641e16", 2, 8)
    rect(tx + 44, ty + 46, tw - 88, 82, "url(#p_rosca)", rx=8, op=0.5)
    texto(tx + tw / 2, ty + 86, "TRAVA SOLENOIDE 12 V", 21, "white", "bold", "middle")
    texto(tx + tw / 2, ty + 114, "(fechadura elétrica)", 17, "#fadbd8", ancora="middle")
    rect(tx + tw / 2 - 40, ty + th, 80, 34, metal(), "#7f8c8d", 2, 6, filtro="f_sombra_leve")   # a lingueta
    texto(tx + tw / 2 + 56, ty + th + 26, "lingueta", 17, CINZA)
    texto(tx - 34, ty + 38, "+", 30, COR["12V"], "bold")
    # a fonte de 12 V (borne de cima: +V e −V; tomada no L e N)
    fonte_12v(RF_X, RF_Y, ((RF_X + 40, "+V"), (RF_X + 110, "−V")), "2 A ou mais (trava + moedeiro)")
    mais, menos = (RF_X + 40, RF_Y + 31), (RF_X + 110, RF_Y + 31)
    # NO -> + da trava · COM <- + da fonte · - da trava -> - da fonte
    xb = RX + RW - 30
    fio([(xb, RL_BORNE["NO"]), (tx, RL_BORNE["NO"])], COR["12V"])
    fio([(xb, RL_BORNE["COM"]), (2700, RL_BORNE["COM"]), (2700, 700), (mais[0], 700), mais], COR["12V"])
    fio([(tx + tw, ty + 120), (menos[0], ty + 120), menos], COR["GND"])
    texto(2712, 688, "COM ← + da fonte 12 V", 20, COR["12V"], "bold")
    texto(tx + tw + 20, ty + 108, "− da trava → − da fonte", 19, TXT, "bold")
    bloco(tx + tw + 20, ty + 160, ["diodo 1N4007 nos fios", "da trava (faixa no +)"], 18, 1.35, CINZA)
    bloco(2790, 836, [
        "**NO** → + da trava: o relé LIGA com a porta",
        "ABERTA (RELE_LIGADO_ABERTA 1).",
        "**Eletroímã** que segura a porta fechada:",
        "use o **NC** no lugar do NO.",
        "Trava solenoide **esquenta**: no .ino",
        "**RELE_TEMPO_MAX_S 5** (liga 5 s e solta).",
    ], 20, 1.38)
    texto(RF_X, RF_Y + 262, "o − da fonte 12 V também", 20, CINZA)
    texto(RF_X, RF_Y + 288, "vai no GND do Arduino", 20, CINZA)


def servo_rele():
    x0, y0, w, h = 2800, 1080, 960, 420
    caixa(x0, y0, w, h, "SERVO NO D9 (opcional — junto com o relé ou no lugar)", COR["D9"], 24, tracejada=True)
    # D9 -> sinal do servo (depois da caixa: por cima do fundo dela)
    fio([(px("D9"), Y_TOPO), (px("D9"), 975), (2770, 975), (2770, 1190), (2900, 1190)], COR["D9"], dash="16 12")
    sx, sy = 2900, 1150
    # o servo (MG996R), visto de cima
    rect(sx - 16, sy + 30, 222, 30, grad(((0, "#3a6aa8"), (1, "#173a6b"))), "#0f2a4d", 2, 6)
    rect(sx, sy, 190, 120, grad(((0, "#3a6aa8"), (0.5, "#1f4e8a"), (1, "#0f2a4d"))), "#0b1f3a", 2, 10,
         filtro="f_sombra_leve")
    rect(sx + 14, sy + 82, 100, 26, "#e9eef2", rx=3)
    texto(sx + 64, sy + 101, "MG996R", 15, "#1f4e8a", "bold", "middle")
    circ(sx + 130, sy + 50, 32, rgrad(((0, "#ffffff"), (0.7, "#e3e8eb"), (1, "#9aa5ae")), 0.4, 0.35), "#7b858c", 2)
    rect(sx + 130, sy + 43, 80, 14, grad(((0, "#ffffff"), (1, "#d5dbe0"))), "#7b858c", 1.5, 7)
    circ(sx + 130, sy + 50, 7, metal(), "#6c767d", 1.5)
    texto(sx + 95, sy + 152, "SERVO", 22, TXT, "bold", "middle")
    texto(sx + 8, sy + 24, "sinal", 16, "#d6eaf8", "bold")
    # + e - para a fonte do servo
    fx, fy = 3330, 1160
    fio([(sx + 190, sy + 70), (fx, sy + 70)], "#c62828", 7)
    fio([(sx + 190, sy + 104), (fx, sy + 104)], "#4e342e", 7)
    rect(fx, fy, 300, 140, grad(((0, "#2ecc71"), (0.5, "#1e8449"), (1, "#145a32"))), "#0e3d22", 3, 12,
         filtro="f_sombra_leve")
    rect(fx + 6, fy + 6, 288, 30, "white", rx=8, op=0.15)
    texto(fx + 160, fy + 64, "FONTE 5–6 V", 26, "white", "bold", "middle", TITULO)
    texto(fx + 160, fy + 96, "do servo", 18, "#d5f5e3", ancora="middle")
    texto(fx + 14, fy + 68, "+", 24, "white", "bold")
    texto(fx + 14, fy + 104, "−", 26, "white", "bold")
    bloco(x0 + 30, y0 + 330, [
        "**sinal → D9** (o fio laranja/amarelo do servo) · + e − na fonte",
        "5-6 V própria; o − dela também no GND do Arduino.",
        "SERVO_FECHADA 0° · SERVO_ABERTA 90° · sem servo: USAR_SERVO 0",
    ], 19, 1.45)


def versoes():
    x0, y0, w, h = 40, 1560, 860, 410
    caixa(x0, y0, w, h, "QUAL ARDUINO GRAVAR?", RELE_AZUL, 26)
    bloco(x0 + 24, y0 + 100, [
        "**ESTE DESENHO: RELÉ / SERVO** (sem ponte H)",
        "grave **arduino/strike_kids/strike_kids.ino**",
        "(como a primeira versão).",
        "",
        "**Porta com MOTOR ou ATUADOR (ponte H)?**",
        "grave **arduino/strike_kids_ponte_h** e siga",
        "o desenho **docs/ligacao_arduino_ponte_h.pdf**.",
        "",
        "**O JOGO É O MESMO** (o mesmo APK) para os dois:",
        "o TESTE DA PORTA mostra qual está gravado.",
    ], 22, 1.45)


def travas():
    x0, y0, w, h = 2360, 1580, 1400, 390
    caixa(x0, y0, w, h, "TRAVAS QUE FUNCIONAM (o mesmo firmware, os mesmos pinos)", RELE_AZUL, 26)
    bloco(x0 + 26, y0 + 100, [
        "**Trava solenoide 12 V**: COM ← +12 V · NO → trava · no .ino **RELE_TEMPO_MAX_S 5**",
        "**Eletroímã 12 V** que segura a porta fechada: COM ← +12 V · **NC** → eletroímã",
        "**Servo** (MG996R e parecidos): sinal no D9 · fonte 5-6 V própria",
        "**Módulo relé 5 V** (1 canal): IN = D8 · VCC = 5V · GND = GND",
        "Módulo que liga com HIGH (jumper H/L no H): **RELE_LIGA_EM_LOW 0**",
        "**Motor DC ou atuador linear** (abre E fecha pelo motor): só com a **PONTE H**",
        "→ arduino/strike_kids_ponte_h e o desenho ligacao_arduino_ponte_h.pdf",
    ], 23, 1.5)


def tabela_pinos():
    x0, y0, w = 40, 760, 480
    caixa(x0, y0, w, 640, "TABELA DE PINOS", "#455a64", 26)
    linhas = [
        ("D2…D5, D7", "sensores S1…S5", COR["S3"]),
        ("D6", "fita de LED (DIN)", COR["D6"]),
        ("D8", "relé · IN (trava)", COR["D8"]),
        ("D9", "servo (opcional)", COR["D9"]),
        ("D10…D12", "livres (sem ponte H)", "#cfd8dc"),
        ("D13", "LED da placa (pisca)", "#f5b041"),
        ("A0", "CONFIGURAÇÃO", COR["A0"]),
        ("A1", "START", COR["A1"]),
        ("A2", "moedeiro (COIN)", COR["A2"]),
        ("A3, A4", "livres", "#cfd8dc"),
        ("5V", "sensores 5 V, relé (VCC)", "#c62828"),
        ("GND", "GND de TUDO", "#212121"),
    ] if VERSAO == "rele" else [
        ("D2…D5, D7", "sensores S1…S5", COR["S3"]),
        ("D6", "fita de LED (DIN)", COR["D6"]),
        ("D8", "relé (opcional)", "#9e9e9e"),
        ("D9", "servo (opcional)", "#9e9e9e"),
        ("D10", "ponte H · IN1", COR["IN1"]),
        ("D11", "ponte H · ENA (opc.)", COR["ENA"]),
        ("D12", "ponte H · IN2", COR["IN2"]),
        ("D13", "LED da placa (pisca)", "#f5b041"),
        ("A0", "CONFIGURAÇÃO", COR["A0"]),
        ("A1", "START", COR["A1"]),
        ("A2", "moedeiro (COIN)", COR["A2"]),
        ("A3", "fim de curso ABERTA", COR["A3"]),
        ("A4", "fim de curso FECHADA", COR["A4"]),
        ("5V", "sensores 5 V, relé", "#c62828"),
        ("GND", "GND de TUDO", "#212121"),
    ]
    for i, (p, o, c) in enumerate(linhas):
        y = y0 + 98 + i * 36
        if i % 2:
            rect(x0 + 5, y - 26, w - 10, 36, "#f2f4f6")
        circ(x0 + 30, y - 8, 10, luz(c), escurecer(c, 0.3), 1.5)
        texto(x0 + 50, y, p, 22, TXT, "bold")
        texto(x0 + 200, y, o, 22, TXT)
    texto(x0, y0 + 680, "No **UNO**: os mesmos nomes de pinos", 21, CINZA)
    texto(x0, y0 + 708, "(DIGITAL, ANALOG IN e POWER).", 21, CINZA)


def outras_pontes():
    x0, y0, w, h = 2360, 1580, 1400, 390
    caixa(x0, y0, w, h, "OUTRAS PONTES H (o mesmo firmware, os mesmos pinos)", "#7b241c", 26)
    bloco(x0 + 26, y0 + 100, [
        "**L298N**: IN1 = D10 · IN2 = D12 · ENA = jumper (ou D11) · borne +5V solto",
        "**L9110S / HG7881**: A-IA = D10 · A-IB = D12 (não tem ENA)",
        "**BTS7960** (motor forte): RPWM = D10 · LPWM = D12 · R_EN + L_EN = D11 (ou 5V)",
        "**TB6612FNG**: AIN1 = D10 · AIN2 = D12 · PWMA = D11 · STBY = 5V · VM = 12 V",
        "**DRV8871**: IN1 = D10 · IN2 = D12 (não tem ENA)",
        "**Mega 2560**: o ENA vai no **D9** (o servo vai no D11)",
        "Em todas: o GND da ponte no GND do Arduino e o motor nas saídas.",
    ], 23, 1.5)


def paineis():
    y0, h = 2020, 940
    ws = [900, 940, 940, 860]
    x = 40
    dados = [
        ("A TRAVA E A BOLA (sem botões)", "#1e8449", [
            "**MODO FICHAS** (menu: Modo livre = NÃO):",
            "sem ficha a trava fica **FECHADA** e a tela",
            "pede INSIRA FICHA.",
            "**Ficha no moedeiro:** a trava **ABRE** e a",
            "tela pede ROLE A BOLA! A 1ª bola é o START:",
            "**1 ficha = 1 jogador** — o lado da bola",
            "escolhe: esquerda BOB, direita MIA (no meio",
            "ou sem bola: sorteio) · **2 fichas = BOB x MIA**.",
            "**Fim da partida:** a trava FECHA (com",
            "ficha sobrando, continua aberta).",
            "",
            "**MODO LIVRE** (Modo livre = SIM): **sem",
            "trava** — a porta fica sempre aberta e a",
            "bola é o START (outra bola logo depois =",
            "2 jogadores).",
            "",
            "Jogo sumiu (cabo solto) com a trava",
            "aberta: ela FECHA sozinha em 15 s.",
        ]),
        ("PONTE H NO MENU DO OPERADOR", "#c0392b", [
            "**Porta: tempo do motor** (0,5 a 10 s; 1,5 s de",
            "fábrica): SEM fim de curso o motor anda esse",
            "tempo. Meça o curso e ponha um pouco a mais.",
            "",
            "**Porta: fim de curso = SIM:** anda até o",
            "micro-switch (A3 aberta, A4 fechada). Não chegou",
            "em 6 s (ou 2x o tempo): para, o teste da porta",
            "avisa “A PORTA NÃO CHEGOU” e tenta de novo (3x).",
            "O jogo manda ao Arduino, que guarda na EEPROM.",
            "",
            "**No começo do .ino:** USAR_SERVO / USAR_RELE 0",
            "= sem servo / relé · FC_NF 1 = micro-switch no NF.",
            "**Só relé/servo, sem motor?** arduino/strike_kids.",
            "**Abriu ao contrário?** Inverta os fios OUT1/OUT2",
            "ou PONTE_INVERTIDA 1.",
            "**PONTE_VELOCIDADE_ABRE / _FECHA** (0 a 255): só",
            "com o ENA no D11 (sem o jumper). Fechar mais",
            "devagar (ex.: 180) protege as mãos das crianças.",
        ]),
        ("PRIMEIRA VEZ (passo a passo)", "#1f6fb2", [
            "**1. Grave o strike_kids_ponte_h.ino** pela IDE",
            "do Arduino (Nano clone: Processador “ATmega328P",
            "(Old Bootloader)”; biblioteca Adafruit NeoPixel,",
            "ou USAR_LEDS 0 sem a fita).",
            "**2. Monitor Serial a 9600:** OLA:STRIKE_KIDS:3",
            "e a porta FECHA (PORTA:FECHADA quando chega).",
            "Mande PORTA:ABRE e PORTA:FECHA para testar.",
            "**3. USB na TV Box** e abra o jogo: na janela do",
            "USB marque “Usar por padrão” e OK.",
            "**4. CONFIGURAÇÃO → menu do operador:**",
            "TESTE DOS SENSORES (ao vivo) → CALIBRAR",
            "SENSORES (pista vazia) → **Porta: tempo do**",
            "**motor** ou **fim de curso** → TESTE DA PORTA",
            "(C ou START abre/fecha; “o Arduino confirmou”",
            "= a porta chegou).",
            "**5. Modo livre** SIM ou NÃO e **Fichas por",
            "jogador**. CONFIGURAÇÃO de novo → jogo. Pronto!",
        ]),
        ("CUIDADOS", "#b03a2e", [
            "**Todos os GND juntos:** Arduino, ponte H,",
            "fontes, moedeiro, sensores, botões.",
            "**O motor NUNCA no Arduino:** só pela",
            "ponte H, com a fonte de 12 V.",
            "**L298N:** o borne +5V fica solto (o",
            "Arduino já tem o 5 V do USB).",
            "**Sensor com saída NPN — nunca PNP**",
            "(o PNP manda 12 V no pino e queima).",
            "Fios do sensor: **marrom +**, **azul −**,",
            "**preto = sinal**.",
            "**Moedeiro:** só o COIN no A2; se ele",
            "manda 12 V no COIN, use optoacoplador.",
            "**Antes de ligar:** meça cada fio de",
            "sinal: nunca pode ter 12 V.",
            "**Teste a porta sem crianças** perto; a",
            "porta não pode prender a mão.",
            "**Esquerda e direita trocadas?** Menu:",
            "“Sensores invertidos”.",
        ]),
    ]
    if VERSAO == "rele":
        dados[1:] = [
            ("RELÉ E SERVO (no começo do .ino)", RELE_AZUL, [
                "**RELÉ (D8):** liga com a trava ABERTA",
                "(RELE_LIGADO_ABERTA 1). Ao contrário:",
                "ponha 0, ou use o NC do relé.",
                "**Módulo que liga com HIGH:**",
                "RELE_LIGA_EM_LOW 0.",
                "**Trava solenoide esquenta** se fica",
                "ligada: RELE_TEMPO_MAX_S 5 (liga 5 s e",
                "solta; para o jogo ela segue aberta).",
                "",
                "**SERVO (D9):** SERVO_FECHADA 0 e",
                "SERVO_ABERTA 90 (graus, de 0 a 180).",
                "**Sem servo ou sem relé:** USAR_SERVO 0",
                "ou USAR_RELE 0.",
                "",
                "**Menu do operador:** “Porta invertida”",
                "troca aberta e fechada. “Tempo do motor” e",
                "“fim de curso” são só da ponte H.",
            ]),
            ("PRIMEIRA VEZ (passo a passo)", "#1f6fb2", [
                "**1. Grave o strike_kids.ino** (pasta",
                "arduino/strike_kids) pela IDE do Arduino",
                "(Nano clone: Processador “ATmega328P (Old",
                "Bootloader)”; biblioteca Adafruit NeoPixel,",
                "ou USAR_LEDS 0 sem a fita).",
                "**2. Monitor Serial a 9600:** OLA:STRIKE_KIDS:3.",
                "Mande PORTA:ABRE (o relé liga, o servo vai",
                "a 90°) e PORTA:FECHA para testar.",
                "**3. USB na TV Box** e abra o jogo: na janela",
                "do USB marque “Usar por padrão” e OK.",
                "**4. CONFIGURAÇÃO → menu do operador:**",
                "TESTE DOS SENSORES (ao vivo) → CALIBRAR",
                "SENSORES (pista vazia) → TESTE DA PORTA",
                "(C ou START abre/fecha; aparece “Arduino",
                "do RELÉ/SERVO” = esta versão gravada).",
                "**5. Modo livre** SIM ou NÃO e **Fichas por",
                "jogador**. CONFIGURAÇÃO de novo → jogo!",
            ]),
            ("CUIDADOS", "#b03a2e", [
                "**Todos os GND juntos:** Arduino, relé,",
                "fontes, moedeiro, sensores, botões.",
                "**A trava NUNCA no Arduino:** só pelo",
                "contato do relé, com a fonte de 12 V.",
                "**Diodo 1N4007 na trava** (faixa no +):",
                "protege o relé do pico da bobina.",
                "**Servo:** fonte 5-6 V própria (nunca o",
                "5 V do Arduino).",
                "**Sensor com saída NPN — nunca PNP**",
                "(o PNP manda 12 V no pino e queima).",
                "Fios do sensor: **marrom +**, **azul −**,",
                "**preto = sinal**.",
                "**Moedeiro:** só o COIN no A2; se ele",
                "manda 12 V no COIN, use optoacoplador.",
                "**Antes de ligar:** meça cada fio de",
                "sinal: nunca pode ter 12 V.",
                "**Teste a trava sem crianças** perto.",
                "**Esquerda e direita trocadas?** Menu:",
                "“Sensores invertidos”.",
            ]),
        ]
    for (titulo, cor, linhas), w in zip(dados, ws):
        caixa(x, y0, w, h, titulo, cor, 28)
        bloco(x + 28, y0 + 108, linhas, 26, 1.48)
        x += w + 20


def fonte_titulos():
    caminho_ = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "StrikeKids", "Resources",
                            "Fontes", "fredoka_700.ttf")
    with open(caminho_, "rb") as f:
        b64 = base64.b64encode(f.read()).decode()
    return '<style>@font-face{font-family:"Fredoka";src:url(data:font/ttf;base64,%s) format("truetype");}</style>' % b64


def desenhar(versao, destino):
    global VERSAO
    VERSAO = versao
    del _saida[:]
    _defs.clear()
    rect(0, 0, W, H, "white")
    cabecalho()
    sensores()
    tabela_pinos()
    fios_sensores_e_fita()
    fita()
    if versao == "rele":
        rele()
        trava()
        servo_rele()
    else:
        ponte_h()
        porta()
        opcionais()
    tv_box()
    if versao == "rele":
        versoes()
    else:
        fins_de_curso()
    botoes()
    if versao == "rele":
        travas()
    else:
        outras_pontes()
    gnd_5v_placa()
    nano()
    paineis()
    corpo = _saida[:]
    _saida.clear()
    add('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d">' % (W, H, W, H))
    add("<defs>")
    add(fonte_titulos())
    add(FIXOS)
    for gid, d in _defs.values():
        add(d)
    add("</defs>")
    _saida.extend(corpo)
    add("</svg>")
    with open(destino, "w", encoding="utf-8") as f:
        f.write("\n".join(_saida) + "\n")
    print("ok:", destino)


def main():
    if len(sys.argv) > 2:
        assert sys.argv[1] in ("rele", "ponte_h"), "versão: rele ou ponte_h"
        desenhar(sys.argv[1], sys.argv[2])
        return
    desenhar("rele", "docs/ligacao_arduino.svg")
    desenhar("ponte_h", "docs/ligacao_arduino_ponte_h.svg")


if __name__ == "__main__":
    main()
