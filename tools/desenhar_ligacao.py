"""Desenha a MONTAGEM DO ARDUINO da maquina (docs/ligacao_arduino.svg), em
fundo branco: os 5 sensores, a fita de LED, a PONTE H com o motor da porta
das bolas, os fins de curso, o moedeiro, os botoes START e CONFIGURACAO, a
TV Box e (opcionais) o servo e o rele - com os mesmos pinos do firmware
arduino/strike_kids/strike_kids.ino. O mesmo desenho da edicao Godot
(strike-kids-gles2/tools/desenhar_ligacao.py), so a fonte vem da pasta da
Unity.

    python3 -I tools/desenhar_ligacao.py docs/ligacao_arduino.svg

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
OURO = "#d4ac0d"

# cores dos fios
COR = {
    "S1": "#8e44ad", "S2": "#1f6fb2", "S3": "#1e8a4c", "S4": "#c27c0e", "S5": "#c2185b",
    "D6": "#0097a7", "IN1": "#e64a19", "IN2": "#3949ab", "ENA": "#78909c",
    "A4": "#6d4c41", "A3": "#00897b", "A2": "#546e7a", "A1": "#2e7d32", "A0": "#f57c00",
    "12V": "#d32f2f", "GND": "#212121", "USB": "#8a9399",
}

_saida = []


def add(s):
    _saida.append(s)


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def rect(x, y, w, h, fill, stroke="none", sw=0, rx=0, dash=None, op=None):
    d = ' stroke-dasharray="%s"' % dash if dash else ""
    o = ' opacity="%s"' % op if op else ""
    add('<rect x="%g" y="%g" width="%g" height="%g" rx="%g" fill="%s" stroke="%s" stroke-width="%g"%s%s/>'
        % (x, y, w, h, rx, fill, stroke, sw, d, o))


def circ(x, y, r, fill, stroke="none", sw=0):
    add('<circle cx="%g" cy="%g" r="%g" fill="%s" stroke="%s" stroke-width="%g"/>' % (x, y, r, fill, stroke, sw))


def linha(x1, y1, x2, y2, cor, sw=4, dash=None):
    d = ' stroke-dasharray="%s"' % dash if dash else ""
    add('<line x1="%g" y1="%g" x2="%g" y2="%g" stroke="%s" stroke-width="%g" stroke-linecap="round"%s/>'
        % (x1, y1, x2, y2, cor, sw, d))


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


def fio(pontos, cor, sw=8, dash=None, pulos=()):
    """Fio em angulo reto pelos pontos; pulos = (x, y) onde um trecho
    HORIZONTAL passa por cima de outro fio (meio-circulo)."""
    r = 15
    d = "M %g %g" % pontos[0]
    for (x1, y1), (x2, y2) in zip(pontos, pontos[1:]):
        if y1 == y2:
            sentido = 1 if x2 > x1 else -1
            for px, py in sorted([p for p in pulos if p[1] == y1 and min(x1, x2) < p[0] < max(x1, x2)],
                                 key=lambda p: p[0] * sentido):
                d += " L %g %g A %d %d 0 0 %d %g %g" % (px - r * sentido, y1, r, r, 1 if sentido > 0 else 0,
                                                         px + r * sentido, y1)
        d += " L %g %g" % (x2, y2)
    tr = ' stroke-dasharray="%s"' % dash if dash else ""
    add('<path d="%s" fill="none" stroke="white" stroke-width="%g" stroke-linejoin="round"/>' % (d, sw + 6))
    add('<path d="%s" fill="none" stroke="%s" stroke-width="%g" stroke-linejoin="round" stroke-linecap="round"%s/>'
        % (d, cor, sw, tr))
    for p in (pontos[0], pontos[-1]):
        circ(p[0], p[1], sw * 0.75, cor)


def etiqueta(x, y, s, fundo="#212121", cor="white", tam=22, ancora="middle"):
    """Etiqueta (GND, 5V, 12V...): ligar ali."""
    w = len(s) * tam * 0.74 + 22
    x0 = x - w / 2 if ancora == "middle" else (x if ancora == "start" else x - w)
    rect(x0, y - tam * 0.95, w, tam * 1.5, fundo, rx=8)
    texto(x0 + w / 2, y + tam * 0.3, s, tam, cor, "bold", "middle")


def pino_placa(x, y, cor_anel=None):
    circ(x, y, 17, OURO, "#7d6608", 3)
    circ(x, y, 6, "#5d4037")
    if cor_anel:
        circ(x, y, 22, "none", cor_anel, 4)


def caixa(x, y, w, h, titulo, cor, tam=28, tracejada=False):
    rect(x + 6, y + 8, w, h, "#00000014", rx=20)
    rect(x, y, w, h, "white", cor, 5, 20, dash="18 10" if tracejada else None)
    rect(x, y, w, 56, cor, rx=20)
    rect(x, y + 30, w, 26, cor)
    texto(x + 24, y + 39, titulo, tam, "white", "bold", fonte=TITULO)


def secao(x, y, s, tam=32):
    texto(x, y, s, tam, TXT, "bold")


# ======================================================================== desenho
def cabecalho():
    texto(60, 108, "STRIKE KIDS · MONTAGEM DO ARDUINO", 88, "#0b1f45", "bold", fonte=TITULO)
    texto(60, 162, "Arduino **Nano** visto de cima · no **Uno** e no **Pro Mini** os pinos têm os mesmos nomes · "
          "no **Mega**: servo no D11 e ENA no D9 · firmware arduino/strike_kids (versão 3)", 30, "#33415c")
    cores = [COR["S1"], COR["S2"], COR["S3"], COR["S4"], COR["S5"]]
    for i, c in enumerate(cores):
        rect(i * W / 5, 190, W / 5 + 1, 12, c)
    # legenda
    x0, y0 = 2700, 34
    rect(x0, y0, 1060, 128, "white", "#ccd3da", 3, 14)
    fio([(x0 + 30, y0 + 40), (x0 + 130, y0 + 40)], "#455a64", 8)
    texto(x0 + 150, y0 + 49, "fio: ligar (obrigatório)", 24)
    fio([(x0 + 30, y0 + 92), (x0 + 130, y0 + 92)], "#455a64", 8, dash="16 12")
    texto(x0 + 150, y0 + 101, "tracejado: opcional", 24)
    etiqueta(x0 + 640, y0 + 46, "GND")
    texto(x0 + 700, y0 + 54, "= no GND comum", 24)
    fio([(x0 + 560, y0 + 92), (x0 + 670, y0 + 92)], "#455a64", 8, pulos=[(x0 + 615, y0 + 92)])
    linha(x0 + 615, y0 + 70, x0 + 615, y0 + 114, "#9aa5b1", 8)
    texto(x0 + 700, y0 + 101, "fios só se cruzam (não ligam)", 24)


SENS_X = [40, 412, 784, 1156, 1528]   # cartoes dos sensores (340 de largura)
SENS_NOMES = ["S1 · ESQUERDA", "S2 · MEIO-ESQ.", "S3 · CENTRO", "S4 · MEIO-DIR.", "S5 · DIREITA"]
SENS_PINO = ["D2", "D3", "D4", "D5", "D7"]

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
        rect(x + 6, 290, 340, 236, "#00000012", rx=18)
        rect(x, 284, 340, 236, "white", c, 5, 18)
        rect(x, 284, 340, 52, c, rx=18)
        rect(x, 310, 340, 26, c)
        texto(x + 170, 320, SENS_NOMES[i], 27, "white", "bold", "middle")
        rect(x + 110, 352, 120, 52, "#eceff1", "#90a4ae", 3, 12)
        circ(x + 140, 378, 14, "#ef9a9a", "#c62828", 3)
        circ(x + 200, 378, 14, "#90caf9", "#1565c0", 3)
        texto(x + 170, 446, "sinal → %s" % SENS_PINO[i], 28, c, "bold", "middle")
        texto(x + 50, 506, "+", 30, "#8d4004", "bold", "middle")
        texto(x + 170, 504, "sinal", 22, TXT, "bold", "middle")
        texto(x + 290, 504, "−", 30, "#263238", "bold", "middle")
        etiqueta(x + 50, 568, "+5/12V", "#8d4004", tam=20)
        etiqueta(x + 290, 568, "GND", tam=20)
        linha(x + 50, 520, x + 50, 550, "#8d4004", 5)
        linha(x + 290, 520, x + 290, 550, "#212121", 5)


def nano():
    rect(NX + 8, NY + 10, NW, NH, "#00000018", rx=22)
    rect(NX, NY, NW, NH, "#1d5fa8", "#123f73", 4, 22)
    # USB
    rect(NX + NW - 10, NY + 95, 95, 110, "#aab7b8", "#7b7d7d", 3, 8)
    rect(NX + NW, NY + 122, 70, 56, "#7b7d7d", rx=6)
    rect(NX + 430, NY + 100, 220, 100, "#17202a", rx=8)
    texto(NX + 540, NY + 144, "ATmega328P", 22, "#bfc9ca", "bold", "middle")
    texto(NX + 540, NY + 178, "ARDUINO NANO", 22, "#ffd43b", "bold", "middle")
    circ(NX + 1180, NY + 150, 11, "#f5b041", "white", 2)
    texto(NX + 1180, NY + 190, "L = D13", 18, "white", "bold", "middle")
    usados_topo = {"GND": None, "D2": COR["S1"], "D3": COR["S2"], "D4": COR["S3"], "D5": COR["S4"],
                   "D6": COR["D6"], "D7": COR["S5"], "D8": "#9e9e9e", "D9": "#9e9e9e",
                   "D10": COR["IN1"], "D11": COR["ENA"], "D12": COR["IN2"]}
    usados_baixo = {"GND": None, "5V": None, "A4": COR["A4"], "A3": COR["A3"], "A2": COR["A2"],
                    "A1": COR["A1"], "A0": COR["A0"]}
    for nome in TOPO:
        x = px(nome)
        pino_placa(x, Y_TOPO, usados_topo.get(nome))
        texto(x, Y_TOPO + 48, nome, 22, "white" if nome in usados_topo else "#aed6f1",
              "bold" if nome in usados_topo else "normal", "middle")
    for nome in BAIXO:
        x = px(nome, False)
        pino_placa(x, Y_BAIXO, usados_baixo.get(nome))
        texto(x, Y_BAIXO - 32, nome, 22, "white" if nome in usados_baixo else "#aed6f1",
              "bold" if nome in usados_baixo else "normal", "middle")


def fios_sensores_e_fita():
    # niveis das curvas: ninguem atravessa ninguem (so o D6 pula o S5)
    nivel = {"S1": 680, "S2": 650, "S3": 620, "S4": 640, "S5": 700}
    for i, x in enumerate(SENS_X):
        s = "S%d" % (i + 1)
        sx = x + 170
        dx = px(SENS_PINO[i])
        fio([(sx, 520), (sx, nivel[s]), (dx, nivel[s]), (dx, Y_TOPO)], COR[s])
    # D6 -> fita de LED (passa por cima do fio do S5)
    x6 = px("D6")
    fio([(x6, Y_TOPO), (x6, 740), (1940, 740), (1940, 276), (2010, 276)], COR["D6"], pulos=[(px("D7"), 740)])
    texto(1955, 250, "DIN", 24, COR["D6"], "bold")


def fita():
    texto(2010, 232, "FITA DE LED WS2812 (opcional) — DIN no **D6** (resistor de 330 Ω no caminho)", 26, TXT)
    rect(2010, 248, 1750, 58, "#263238", rx=14)
    cores = ["#ff5252", "#ffd740", "#40c4ff", "#69f0ae", "#ff80ab", "#b388ff"]
    for i in range(30):
        cx = 2045 + i * 57.5
        rect(cx - 17, 260, 34, 34, "white", rx=6)
        circ(cx, 277, 11, cores[i % len(cores)])
    texto(3760, 340, "+5V e GND da fita: FONTE 5 V própria (60 LEDs ≈ 3,6 A) + capacitor de 1000 µF · GND junto",
          22, CINZA, ancora="end")


# ponte H (L298N): pinos na borda de baixo
PH_X, PH_Y, PH_W, PH_H = 2060, 420, 660, 330
PH_PINOS = ["ENA", "IN1", "IN2", "IN3", "IN4", "ENB"]
PH_FORCA = ["+12V", "GND", "+5V"]


def ph_x(nome):
    if nome in PH_PINOS:
        return PH_X + 70 + PH_PINOS.index(nome) * 62
    return PH_X + 490 + PH_FORCA.index(nome) * 70


def ponte_h():
    texto(2060, 398, "PORTA DAS BOLAS — PONTE H (L298N) + MOTOR", 30, TXT, "bold")
    rect(PH_X + 8, PH_Y + 10, PH_W, PH_H, "#00000018", rx=18)
    rect(PH_X, PH_Y, PH_W, PH_H, "#c0392b", "#7b241c", 4, 18)
    # dissipador
    rect(PH_X + 200, PH_Y + 22, 250, 120, "#2c3e50", rx=6)
    for i in range(9):
        rect(PH_X + 212 + i * 27, PH_Y + 30, 12, 104, "#5d6d7e", rx=3)
    texto(PH_X + 325, PH_Y + 170, "PONTE H · L298N", 26, "white", "bold", "middle")
    texto(PH_X + 325, PH_Y + 194, "(outras pontes: quadro embaixo)", 17, "#fadbd8", ancora="middle")
    # saidas do motor (direita) e as que nao usa (esquerda)
    for i, nome in enumerate(["OUT1", "OUT2"]):
        y = PH_Y + 60 + i * 70
        rect(PH_X + PH_W - 50, y - 24, 50, 48, "#1e8449", "#145a32", 2, 4)
        circ(PH_X + PH_W - 25, y, 10, "#bdc3c7", "#7f8c8d", 2)
        texto(PH_X + PH_W - 62, y + 8, nome, 20, "white", "bold", "end")
    for i, nome in enumerate(["OUT3", "OUT4"]):
        y = PH_Y + 60 + i * 70
        rect(PH_X, y - 24, 50, 48, "#1e8449", "#145a32", 2, 4)
        texto(PH_X + 60, y + 8, nome, 18, "#f5b7b1", ancora="start")
    texto(PH_X + 8, PH_Y + 162, "não usa", 17, "#f5b7b1", ancora="start")
    # header e borne de forca
    rect(PH_X + 40, PH_Y + PH_H - 70, 372, 50, "#17202a", rx=6)
    for nome in PH_PINOS:
        x = ph_x(nome)
        circ(x, PH_Y + PH_H - 45, 10, OURO, "#7d6608", 2)
        texto(x, PH_Y + PH_H - 84, nome, 19, "white", "bold" if nome in ("ENA", "IN1", "IN2") else "normal", "middle")
    # jumpers do ENA e do ENB (de fabrica)
    rect(ph_x("ENA") - 14, PH_Y + PH_H - 128, 28, 30, "#111", rx=4)
    rect(ph_x("ENB") - 14, PH_Y + PH_H - 128, 28, 30, "#111", rx=4)
    texto(ph_x("ENA"), PH_Y + PH_H - 136, "jumper", 16, "#fadbd8", ancora="middle")
    rect(PH_X + 460, PH_Y + PH_H - 78, 220, 60, "#1e8449", "#145a32", 2, 6)
    for nome in PH_FORCA:
        x = ph_x(nome)
        circ(x, PH_Y + PH_H - 48, 12, "#bdc3c7", "#7f8c8d", 2)
        texto(x, PH_Y + PH_H - 88, nome, 19, "white", "bold", "middle")
    # fios do Arduino (D10 passa por cima do D11, que vai ao ENA por dentro)
    yh = PH_Y + PH_H - 45
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
        linha(x, Y_TOPO, x, 975, "#9e9e9e", 6, "10 8")
        etiqueta(x, 960, rot, "#9e9e9e", tam=16)
    # forca: 12 V
    fx, fy = 2500, 1080
    fio([(ph_x("+12V"), yh), (ph_x("+12V"), fy)], COR["12V"])
    fio([(ph_x("GND"), yh), (ph_x("GND"), fy)], COR["GND"])
    rect(fx + 6, fy + 8, 270, 200, "#00000018", rx=14)
    rect(fx, fy, 270, 200, "#c0392b", "#7b241c", 4, 14)
    texto(fx + 135, fy + 84, "FONTE 12 V", 32, "white", "bold", "middle", TITULO)
    texto(fx + 135, fy + 116, "2 A ou mais", 20, "#fadbd8", ancora="middle")
    texto(fx + 135, fy + 142, "(motor + moedeiro)", 18, "#fadbd8", ancora="middle")
    texto(fx + 135, fy + 182, "tomada 127/220 V ~", 18, "#fadbd8", ancora="middle")
    texto(ph_x("+12V"), fy + 36, "+", 34, "white", "bold", "middle")
    texto(ph_x("GND"), fy + 36, "−", 34, "white", "bold", "middle")
    linha(ph_x("GND"), fy - 70, ph_x("GND") + 60, fy - 70, COR["GND"], 6)
    etiqueta(ph_x("GND") + 118, fy - 70, "GND", tam=19)
    texto(ph_x("+5V") - 20, yh + 82, "+5V: solto", 18, CINZA, "bold")
    texto(fx, fy + 240, "o − da fonte 12 V também", 20, CINZA)
    texto(fx, fy + 266, "vai no GND do Arduino", 20, CINZA)


def porta():
    x0, y0, w, h = 2800, 420, 960, 600
    rect(x0 + 6, y0 + 8, w, h, "#00000012", rx=18)
    rect(x0, y0, w, h, "#fbfcfd", "#90a4ae", 3, 18)
    texto(x0 + 24, y0 + 44, "MOTOR DA PORTA (motor DC com redução)", 26, TXT, "bold")
    # motor
    mx, my = x0 + 130, y0 + 210
    rect(mx - 70, my - 60, 150, 120, "#b0bec5", "#546e7a", 4, 16)
    circ(mx, my, 44, "#eceff1", "#546e7a", 4)
    texto(mx, my + 14, "M", 40, "#37474f", "bold", "middle")
    rect(mx + 80, my - 12, 90, 24, "#78909c", rx=6)
    # fios do motor saem do OUT1/OUT2
    fio([(PH_X + PH_W - 25, PH_Y + 60), (mx - 30, PH_Y + 60), (mx - 30, my - 60)], "#b71c1c", 8)
    fio([(PH_X + PH_W - 25, PH_Y + 130), (2770, PH_Y + 130), (2770, my + 80), (mx + 30, my + 80),
         (mx + 30, my + 60)], "#263238", 8)
    texto(mx + 60, my - 76, "2 fios do motor", 20, CINZA)
    # porta: trilho, folha e os dois fins de curso
    tx, ty = x0 + 330, y0 + 230
    rect(tx, ty + 70, 580, 16, "#90a4ae", rx=6)
    rect(tx + 60, ty - 60, 200, 130, "#ffe082", "#ff8f00", 4, 10)
    texto(tx + 160, ty + 6, "PORTA", 24, "#8d4004", "bold", "middle")
    texto(tx + 160, ty + 34, "das bolas", 20, "#8d4004", ancora="middle")
    linha(tx + 290, ty + 5, tx + 390, ty + 5, "#8d4004", 4)
    add('<path d="M %g %g l -18 -10 l 0 20 z" fill="#8d4004"/>' % (tx + 280, ty + 5))
    add('<path d="M %g %g l 18 -10 l 0 20 z" fill="#8d4004"/>' % (tx + 400, ty + 5))
    rect(tx + 168 - 260, ty + 40, 1, 1, "none")
    linha(mx + 170, my, tx + 60, my, "#78909c", 6, "4 8")
    for fx, nome, pino, cor in ((tx + 20, "FECHADA", "A4", COR["A4"]), (tx + 540, "ABERTA", "A3", COR["A3"])):
        rect(fx - 24, ty - 10, 48, 66, "#455a64", rx=6)
        linha(fx, ty - 10, fx + (16 if nome == "FECHADA" else -16), ty - 44, "#cfd8dc", 5)
        texto(fx, ty + 126, "fim de curso", 19, CINZA, "bold", "middle")
        texto(fx, ty + 152, "%s → %s" % (nome, pino), 22, cor, "bold", "middle")
    texto(x0 + 24, y0 + 470, "FIM DE CURSO (opcional): no menu, **Porta: fim de curso = SIM**;", 21)
    texto(x0 + 24, y0 + 500, "a ligação dos micro-switches está no quadro de baixo, à esquerda.", 21)
    texto(x0 + 24, y0 + 540, "**ou ATUADOR LINEAR 12 V** nos mesmos 2 fios: ele para sozinho no", 21)
    texto(x0 + 24, y0 + 570, "fim do curso (sem micro-switch; menu: **Porta: tempo do motor**).", 21)


def tv_box():
    x0, y0 = 1990, 1050
    fio([(NX + NW + 70, NY + 150), (x0, NY + 150)], COR["USB"], 16)
    texto((NX + NW + 70 + x0) / 2, NY + 128, "USB", 22, TXT, "bold", "middle")
    rect(x0 + 6, y0 + 8, 470, 230, "#00000018", rx=18)
    rect(x0, y0, 470, 230, "#1c2833", "#0b1015", 4, 18)
    rect(x0 + 20, y0 + 20, 430, 110, "#2c3e50", rx=10)
    texto(x0 + 235, y0 + 72, "TV BOX (Android)", 30, "#ffd43b", "bold", "middle", TITULO)
    texto(x0 + 235, y0 + 108, "o jogo Strike Kids", 22, "white", ancora="middle")
    texto(x0 + 235, y0 + 172, "USB: dados + alimenta o Arduino", 20, "white", "bold", "middle")
    texto(x0 + 235, y0 + 206, "1ª vez: marque “Usar por padrão”", 19, "#d6dbdf", ancora="middle")


def gnd_5v_placa():
    for nome, rot, fundo in (("GND", "GND COMUM", "#212121"), ("5V", "5V", "#c62828"), ("VIN", None, None)):
        if rot is None:
            continue
        x = px(nome, False)
        linha(x, Y_BAIXO, x, 1380, fundo, 6)
        etiqueta(x, 1392, rot, fundo, tam=19)


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
        c = COR[pino]
        # micro-switch: terminais em cima (C, NF, NA), alavanca embaixo
        rect(cx - 110, yh, 220, 110, "#37474f", "#263238", 3, 10)
        linha(cx - 90, yh + 116, cx + 80, yh + 150, "#b0bec5", 7)
        circ(cx + 82, yh + 150, 12, "#eceff1", "#90a4ae", 3)
        texto(cx, yh + 84, "micro-switch", 19, "#cfd8dc", "bold", "middle")
        for k, t in enumerate(("C", "NF", "NA")):
            tx = cx - 70 + k * 70
            rect(tx - 12, yh - 34, 24, 34, "#bdc3c7", "#7f8c8d", 2, 3)
            texto(tx, yh + 36, t, 22, "white", "bold", "middle")
        texto(cx, yh + 200, nome, 23, c, "bold", "middle")
        texto(cx, yh + 230, "NA → %s · C → GND" % pino, 19, TXT, ancora="middle")
        # C -> GND
        linha(cx - 70, yh - 34, cx - 70, yh - 60, "#212121", 5)
        etiqueta(cx - 70, yh - 72, "GND", tam=16)
    # fios dos fins de curso (A4 por cima, A3 por baixo: nao se cruzam)
    fio([(px("A4", False), Y_BAIXO), (px("A4", False), 1450), (x0 + 500, 1450), (x0 + 500, yh - 34)],
        COR["A4"])
    fio([(px("A3", False), Y_BAIXO), (px("A3", False), 1490), (x0 + 780, 1490), (x0 + 780, yh - 34)],
        COR["A3"])
    texto(x0 + 524, 1438, "A4 → NA (porta FECHADA)", 21, COR["A4"], "bold")
    texto(x0 + 804, 1528, "A3 → NA (porta ABERTA)", 21, COR["A3"], "bold")


def _botao(x, y0, w, titulo, cor, claro, medio, pino, nivel, oque):
    caixa(x, y0, w, 390, titulo, cor, 26)
    bx, by = x + 160, y0 + 190
    circ(bx, by, 62, claro, cor, 8)
    circ(bx, by, 42, medio)
    # os 2 fios do botao: um no pino (vem de cima, pela direita), outro no GND
    entra = x + w - 90
    fio([(px(pino, False), Y_BAIXO), (px(pino, False), nivel), (entra, nivel), (entra, by), (bx + 70, by)], COR[pino])
    linha(bx - 70, by, bx - 112, by, "#212121", 6)
    etiqueta(bx - 112, by + 40, "GND", tam=17)
    texto(entra + 12, by - 16, pino, 24, COR[pino], "bold")
    texto(x + w / 2, y0 + 300, "um fio no %s, outro no GND" % pino, 20, TXT, "bold", "middle")
    texto(x + w / 2, y0 + 334, oque, 19, CINZA, ancora="middle")


def botoes():
    # moedeiro (A2), START (A1), CONFIGURACAO (A0)
    y0 = 1580
    mx, mw = 920, 500
    caixa(mx, y0, mw, 390, "MOEDEIRO / FICHA", COR["A2"], 26)
    rect(mx + 30, y0 + 84, 160, 210, "#b0bec5", "#546e7a", 3, 10)
    rect(mx + 90, y0 + 106, 40, 8, "#37474f", rx=3)
    circ(mx + 110, y0 + 196, 34, "#ffd54f", "#ff8f00", 4)
    texto(mx + 110, y0 + 206, "$", 30, "#8d4004", "bold", "middle")
    texto(mx + 110, y0 + 322, "(CH-926 e parecidos)", 17, CINZA, ancora="middle")
    # COIN em cima: o fio do A2 desce e entra nele
    xa2 = px("A2", False)
    yc = y0 + 120
    fio([(xa2, Y_BAIXO), (xa2, yc), (mx + 190, yc)], COR["A2"])
    texto(mx + 206, yc - 14, "COIN → A2", 21, COR["A2"], "bold")
    for k, (n, c, et, fundo) in enumerate((("DC12V", "#d32f2f", "+12V", "#d32f2f"),
                                           ("GND", "#212121", "GND", "#212121"),
                                           ("COUNTER", "#bdbdbd", "solto", "#9e9e9e"))):
        yy = yc + 58 + k * 50
        linha(mx + 190, yy, mx + 260, yy, c, 7)
        texto(mx + 272, yy + 8, n, 20, TXT, "bold")
        etiqueta(mx + 445, yy, et, fundo, tam=17)
    texto(mx + mw / 2, y0 + 362, "1 pulso = 1 ficha · moedeiro NA ou NF", 18, TXT, ancora="middle")
    _botao(1440, y0, 440, "BOTÃO START", COR["A1"], "#a5d6a7", "#66bb6a", "A1", 1530,
           "começa a partida (o da Zero Delay também)")
    _botao(1900, y0, 440, "CONFIGURAÇÃO", COR["A0"], "#ffe0b2", "#ffa726", "A0", 1470,
           "entra e sai do menu do operador")


def opcionais():
    x0, y0, w, h = 2800, 1080, 960, 420
    rect(x0, y0, w, h, "white", "#9e9e9e", 4, 18, dash="18 10")
    texto(x0 + 24, y0 + 44, "OPCIONAL — TRAVA POR SERVO (D9) OU RELÉ (D8)", 25, "#616161", "bold")
    texto(x0 + 24, y0 + 76, "no lugar da ponte H (ou junto: todos recebem o mesmo comando)", 20, CINZA)
    # servo
    sx, sy = x0 + 30, y0 + 110
    rect(sx, sy, 400, 280, "#f4f6f7", "#bdc3c7", 3, 12)
    rect(sx + 30, sy + 40, 170, 110, "#2c3e50", rx=10)
    circ(sx + 115, sy + 95, 34, "#ecf0f1", "#95a5a6", 3)
    rect(sx + 115, sy + 88, 90, 14, "white", "#95a5a6", 2, 7)
    texto(sx + 115, sy + 186, "SERVO", 24, TXT, "bold", "middle")
    for i, (n, c, et, fundo) in enumerate((("sinal", "#ef6c00", "D9", "#9e9e9e"),
                                            ("+", "#c62828", "5-6 V", "#c62828"),
                                            ("−", "#4e342e", "GND", "#212121"))):
        yy = sy + 60 + i * 50
        linha(sx + 200, yy, sx + 270, yy, c, 6)
        texto(sx + 280, yy + 8, n, 20, TXT, "bold")
        etiqueta(sx + 352, yy, et, fundo, tam=16)
    texto(sx + 20, sy + 226, "fonte 5-6 V própria", 19, CINZA)
    texto(sx + 20, sy + 254, "0° = fechada · 90° = aberta", 19, CINZA)
    # rele
    rx, ry = x0 + 470, y0 + 110
    rect(rx, ry, 460, 280, "#f4f6f7", "#bdc3c7", 3, 12)
    rect(rx + 20, ry + 30, 200, 150, "#1a5276", rx=10)
    rect(rx + 60, ry + 60, 120, 60, "#2e86c1", rx=6)
    texto(rx + 120, ry + 98, "relé", 22, "white", "bold", "middle")
    for i, (n, et, fundo) in enumerate((("IN", "D8", "#9e9e9e"), ("VCC", "5V", "#c62828"), ("GND", "GND", "#212121"))):
        yy = ry + 210 + i * 0
        xx = rx + 50 + i * 70
        texto(xx, ry + 168, n, 17, "white", "bold", "middle")
        linha(xx, ry + 180, xx, ry + 210, "#616161", 5)
        etiqueta(xx, ry + 230, et, fundo, tam=15)
    texto(rx + 240, ry + 60, "COM / NA:", 19, TXT, "bold")
    texto(rx + 240, ry + 88, "trava solenoide", 19, TXT)
    texto(rx + 240, ry + 116, "+ fonte 12 V", 19, TXT)
    texto(rx + 240, ry + 144, "(diodo 1N4007", 17, CINZA)
    texto(rx + 240, ry + 168, "na trava)", 17, CINZA)


def tabela_pinos():
    x0, y0, w = 40, 760, 480
    rect(x0 + 6, y0 + 8, w, 640, "#00000012", rx=18)
    rect(x0, y0, w, 640, "white", "#455a64", 4, 18)
    rect(x0, y0, w, 56, "#455a64", rx=18)
    rect(x0, y0 + 30, w, 26, "#455a64")
    texto(x0 + 24, y0 + 39, "TABELA DE PINOS", 26, "white", "bold", fonte=TITULO)
    linhas = [
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
            rect(x0 + 4, y - 26, w - 8, 36, "#f2f4f6")
        circ(x0 + 30, y - 8, 9, c)
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
        ("A PORTA ABRE E FECHA COM O JOGO", "#1e8449", [
            "**Ligou a máquina:** a porta FECHA.",
            "",
            "**MODO FICHAS** (menu: Modo livre = NÃO):",
            "ficha no moedeiro → **START** → a porta",
            "ABRE e começa a partida.",
            "**Fim da partida:** a porta FECHA.",
            "Outra ficha + START → ABRE de novo.",
            "Sem ficha, a espera pede INSIRA FICHA.",
            "",
            "**MODO LIVRE** (Modo livre = SIM):",
            "só o **START** abre; no fim, FECHA.",
            "",
            "**Atenção:** com “Começar rolando a bola”",
            "LIGADO (vem assim no modo livre), a porta",
            "fica ABERTA na espera e no fim — a bola",
            "é o START. Quer que feche? Desligue no",
            "menu do operador.",
            "",
            "Jogo sumiu (cabo solto, TV desligada) com",
            "a porta aberta: ela FECHA sozinha em 15 s.",
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
            "**No começo do .ino:** USAR_PONTE_H 0 = sem ponte H",
            "· USAR_SERVO / USAR_RELE 0 = sem servo / relé",
            "· FC_NF 1 = micro-switch no NF.",
            "**Abriu ao contrário?** Inverta os fios OUT1/OUT2",
            "ou PONTE_INVERTIDA 1.",
            "**PONTE_VELOCIDADE_ABRE / _FECHA** (0 a 255): só",
            "com o ENA no D11 (sem o jumper). Fechar mais",
            "devagar (ex.: 180) protege as mãos das crianças.",
        ]),
        ("PRIMEIRA VEZ (passo a passo)", "#1f6fb2", [
            "**1. Grave o strike_kids.ino** pela IDE do Arduino",
            "(Nano clone: Processador “ATmega328P (Old",
            "Bootloader)”; biblioteca Adafruit NeoPixel, ou",
            "USAR_LEDS 0 sem a fita).",
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
            "partida**. CONFIGURAÇÃO de novo → jogo. Pronto!",
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
    for (titulo, cor, linhas), w in zip(dados, ws):
        caixa(x, y0, w, h, titulo, cor, 28)
        bloco(x + 28, y0 + 108, linhas, 26, 1.48)
        x += w + 20


def fonte_titulos():
    caminho = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "StrikeKids", "Resources",
                           "Fontes", "fredoka_700.ttf")
    with open(caminho, "rb") as f:
        b64 = base64.b64encode(f.read()).decode()
    return ('<defs><style>@font-face{font-family:"Fredoka";src:url(data:font/ttf;base64,%s) format("truetype");}'
            '</style></defs>' % b64)


def main():
    destino = sys.argv[1] if len(sys.argv) > 1 else "docs/ligacao_arduino.svg"
    add('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d">' % (W, H, W, H))
    add(fonte_titulos())
    rect(0, 0, W, H, "white")
    cabecalho()
    sensores()
    tabela_pinos()
    fios_sensores_e_fita()
    fita()
    ponte_h()
    porta()
    opcionais()
    tv_box()
    fins_de_curso()
    botoes()
    outras_pontes()
    gnd_5v_placa()
    nano()
    paineis()
    add("</svg>")
    with open(destino, "w", encoding="utf-8") as f:
        f.write("\n".join(_saida) + "\n")
    print("ok:", destino)


if __name__ == "__main__":
    main()
