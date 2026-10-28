# -*- coding: utf-8 -*-
"""
GERA O CENÁRIO DAS FASES (Assets/StrikeKids/Resources/Malhas/cenario/*.bytes)
e as prévias de cada fase.

    python3 tools/modelador/gerar_cenario.py                 # tudo
    python3 tools/modelador/gerar_cenario.py dino trex       # só alguns
    python3 tools/modelador/gerar_cenario.py dinos           # uma fase inteira (parque,
                                                      # espaco, mar, dinos)
    python3 tools/modelador/gerar_cenario.py --sem-previa    # só grava
    python3 tools/modelador/gerar_cenario.py --previa=PASTA  # prévias nessa pasta
                                                      # (padrão: $PREVIA_CENARIO ou
                                                      # <tmp>/strike_kids_cenario)

Para cada objeto: grava o .bytes, mostra vértices/triângulos (e avisa se
passou do orçamento) e faz a prévia (3 vistas; os que mexem, mais uma vista
com as juntas giradas). No fim, uma folha por fase com todos os objetos e a
conta dos conjuntos (a roda-gigante inteira: roda + 8 cabines).
Os objetos e os orçamentos ficam em cenario.MODELOS (os que o jogo tinge
aparecem na prévia com uma tinta de exemplo, como o Temas.cs faz).
"""
import math
import os
import sys
import tempfile
import time

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)

import malha  # noqa: E402
from malha import gravar, rot  # noqa: E402
import cenario  # noqa: E402

SAIDA = os.path.normpath(os.path.join(AQUI, "..", "..", "Assets", "StrikeKids", "Resources", "Malhas", "cenario"))
# as prévias vão para fora do projeto (pasta temporária), ou para PREVIA_CENARIO / --previa=PASTA
PREVIA = os.environ.get("PREVIA_CENARIO") or os.path.join(tempfile.gettempdir(), "strike_kids_cenario")
FUNDO = (0.93, 0.95, 0.98)
# conjuntos que dividem um orçamento (o jogo monta juntos)
CONJUNTOS = {"roda-gigante (roda + 8 cabines)": ({"roda": 1, "cabine": 8}, 3500)}


def limites(modelo, poses=None):
    """Caixa (mínimo, máximo) do modelo montado (com as juntas giradas, se poses)."""
    from previa import itens_do_modelo
    from malha import aplicar
    P = []
    for (peca, M) in itens_do_modelo(modelo, poses=poses):
        if peca.pos:
            P.append(aplicar(M, np.array(peca.pos)))
    P = np.concatenate(P)
    return P.min(axis=0), P.max(axis=0)


def tingir(itens, tinta):
    """Como o jogo faz com a TINTA: multiplica a cor das peças com luz (as "luz" ficam)."""
    from malha import Peca
    if not tinta:
        return itens
    t = np.array([int(tinta[1:3], 16), int(tinta[3:5], 16), int(tinta[5:7], 16)]) / 255.0
    out = []
    for (peca, M) in itens:
        if peca.material == "luz":
            out.append((peca, M))
            continue
        q = Peca(peca.nome, peca.material)
        q.pos, q.nrm, q.uv = peca.pos, peca.nrm, peca.uv
        q.cor = [(int(c[0] * t[0]), int(c[1] * t[1]), int(c[2] * t[2]), c[3]) for c in peca.cor]
        out.append((q, M))
    return out


def vistas(modelo, angulos=(30, 100, 215), tam=380, elev=0.28, poses=None, rotulo="", tinta=None):
    """Prévias de um modelo (graus em volta: 0 = de frente, olhando de +Z)."""
    from previa import Camera, itens_do_modelo, renderizar
    from PIL import ImageDraw
    itens = tingir(itens_do_modelo(modelo, poses=poses), tinta)
    lo, hi = limites(modelo, poses)
    alvo = (lo + hi) / 2.0
    raio = float(np.linalg.norm(hi - lo)) / 2.0
    fov = 34.0
    dist = raio / math.tan(math.radians(fov) / 2.0) * 1.08
    ims = []
    for a in angulos:
        r = math.radians(a)
        d = np.array([math.sin(r) * math.cos(elev), math.sin(elev), math.cos(r) * math.cos(elev)])
        im = renderizar(itens, Camera(alvo + d * dist, alvo, fov), tam, tam, fundo=FUNDO)
        if rotulo:
            ImageDraw.Draw(im).text((6, 4), rotulo, fill=(60, 60, 80))
        ims.append(im)
    return ims


def poses_animacao(modelo):
    """Juntas giradas (como no Temas.cs, perto do máximo) para conferir frestas."""
    nomes = {p[0] for p in modelo.partes}
    P = {}

    def R(ax=0.0, ay=0.0, az=0.0):
        M = np.eye(4)
        M[:3, :3] = rot(ax, ay, az)
        return M
    tabela = {"pescoco": R(6, 12, 0), "cabeca": R(8, 18, 5), "mandibula": R(24, 0, 0), "rabo": R(4, 14, 0),
              "braco_e": R(20, 0, 0), "braco_d": R(-20, 0, 0), "asa_e": R(0, 0, 32), "asa_d": R(0, 0, -32),
              "nadadeira_ed": R(0, 25, 18), "nadadeira_ef": R(0, -25, 18), "nadadeira_dd": R(0, 25, -18),
              "nadadeira_df": R(0, -25, -18), "topo": R(0, 30, 0)}
    for k, M in tabela.items():
        if k in nomes:
            P[k] = M
    return P


# resolução das formas curvas: os bichos grandes e as atrações aparecem de
# perto (mais); o que se espalha às dezenas pelo terreno, um pouco menos
RESOLUCAO_PADRAO = 1.4
RESOLUCAO = {"dino": 1.6, "trex": 1.6, "triceratops": 1.6, "estegossauro": 1.6, "ptero": 1.6, "ovo_chocando": 1.6,
             "carrossel": 1.5, "navio": 1.5, "tartaruga": 1.6, "ovni": 1.6, "foguete": 1.6, "navinha": 1.6,
             "planeta_anel": 1.6, "balao": 1.6, "roda": 1.5, "cabine": 1.5}


def main(args):
    """Grava os .bytes pedidos (todos, se nenhum) e faz as prévias."""
    # o cenário do jogo agora vem da edição Godot 7.6 (tools/godot/exportar_cenario.gd):
    # gravar estes modelos antigos por cima estragaria as fases
    if "--forcar" not in args:
        print("O cenário agora vem da edição Godot (tools/godot/exportar_cenario.gd).\n"
              "Estes são os modelos ANTIGOS desta edição: para gravá-los mesmo assim, use --forcar.")
        return 1
    sem_previa = "--sem-previa" in args
    global PREVIA
    for a in args:
        if a.startswith("--previa="):
            PREVIA = a.split("=", 1)[1]
    alvos = [a for a in args if not a.startswith("--")]
    os.makedirs(SAIDA, exist_ok=True)
    if not sem_previa:
        os.makedirs(PREVIA, exist_ok=True)
    todos = cenario.MODELOS
    temas = sorted({e[1] for e in todos.values()})
    fases_pedidas = [a for a in alvos if a in temas and a not in todos]
    nomes = []
    for a in (alvos or list(todos.keys())):
        if a in fases_pedidas:
            nomes += [n for n, e in todos.items() if e[1] == a]
        elif a in todos:
            nomes.append(a)
        else:
            print("não existe:", a)
    folhas = {}
    total = 0
    tri_de = {}
    for n in nomes:
        fn, tema, orcamento = todos[n][:3]
        tinta = todos[n][3] if len(todos[n]) > 3 else None
        t0 = time.time()
        malha.RESOLUCAO = RESOLUCAO.get(n, RESOLUCAO_PADRAO)
        m = fn()
        v, tri = gravar(m, os.path.join(SAIDA, n + ".bytes"))
        total += tri
        tri_de[n] = tri
        partes = [p[0] for p in m.partes]
        aviso = "  <<< PASSOU DO ORÇAMENTO (%d)" % orcamento if tri > orcamento else ""
        print("%-14s %-7s %6d vértices %6d triângulos (orç. %4d)  %d partes  %.1fs%s" %
              (n, tema, v, tri, orcamento, len(partes), time.time() - t0, aviso))
        if sem_previa:
            continue
        from previa import folha
        rot_ = "%s  %d tri" % (n, tri)
        ims = vistas(m, rotulo=rot_, tinta=tinta)
        poses = poses_animacao(m)
        if poses:
            ims += vistas(m, angulos=(320,), poses=poses, rotulo="juntas giradas", tinta=tinta)
        folha(ims, len(ims)).save(os.path.join(PREVIA, n + ".png"))
        folhas.setdefault(tema, []).append(ims[0])
    if not sem_previa:
        from previa import folha
        for tema, ims in folhas.items():
            if not alvos or tema in fases_pedidas:
                folha(ims, 5).save(os.path.join(PREVIA, "_" + tema + ".png"))
    for nome_c, (pecas, orcamento) in CONJUNTOS.items():
        if all(k in tri_de for k in pecas):
            soma = sum(tri_de[k] * q for k, q in pecas.items())
            print("%s: %d triângulos (orç. %d)%s" % (nome_c, soma, orcamento,
                                                      "  <<< PASSOU DO ORÇAMENTO" if soma > orcamento else ""))
    print("total: %d triângulos em %d objetos" % (total, len(nomes)))
    if not sem_previa:
        print("prévias em", PREVIA)


if __name__ == "__main__":
    main(sys.argv[1:])
