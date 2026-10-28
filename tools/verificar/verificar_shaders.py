# -*- coding: utf-8 -*-
"""
CONFERE OS SHADERS SEM A UNITY: tira cada bloco CGPROGRAM dos .shader e
compila o vértice e o pixel como HLSL no glslangValidator (com uma imitação
do UnityCG.cginc). Pega erros de sintaxe, de tipo e nomes errados. Também
avisa o que a OpenGL ES 2.0 (Mali-450) não aceita bem.

    python3 tools/verificar/verificar_shaders.py
"""
import os
import re
import subprocess
import sys
import tempfile

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.normpath(os.path.join(AQUI, "..", ".."))
PASTA = os.path.join(RAIZ, "Assets", "StrikeKids", "Resources", "Shaders")
STUB = os.path.join(AQUI, "unity_stub")

# o que não vai na OpenGL ES 2.0 (shader model 2): avisos
PROIBIDOS = [
    (r"\btex2Dlod\b", "tex2Dlod no pixel não existe em GLES2"),
    (r"\btex2Dgrad\b|\bddx\b|\bddy\b|\bfwidth\b", "derivadas (precisam de extensão na Mali-400)"),
    (r"\buint\b|<<|>>|\^|\b(?:asuint|asint|asfloat)\b", "operações de inteiros/bits não existem em GLES2"),
    (r"#pragma\s+target\s+3", "target 3.0+ (GLES2 é 2.0)"),
    (r"\bSV_VertexID\b|\bSV_InstanceID\b", "IDs de vértice/instância não existem em GLES2"),
]


def blocos(texto):
    return re.findall(r"CGPROGRAM(.*?)ENDCG", texto, re.S)


def main():
    exe = "glslangValidator"
    erros = 0
    avisos = 0
    nomes = []
    for pasta, _, arqs in os.walk(PASTA):
        for n in arqs:
            nomes.append(os.path.relpath(os.path.join(pasta, n), PASTA).replace(os.sep, "/"))
    for nome in sorted(nomes):
        if not nome.endswith(".shader"):
            continue
        caminho = os.path.join(PASTA, nome)
        texto = open(caminho, encoding="utf-8").read()
        for k, b in enumerate(blocos(texto)):
            vs = re.search(r"#pragma\s+vertex\s+(\w+)", b)
            fs = re.search(r"#pragma\s+fragment\s+(\w+)", b)
            corpo = re.sub(r"^\s*#pragma.*$", "", b, flags=re.M)
            for (pad, msg) in PROIBIDOS:
                for m in re.finditer(pad, b):
                    linha = b[:m.start()].count("\n") + 1
                    print("AVISO %s bloco %d linha %d: %s" % (nome, k, linha, msg))
                    avisos += 1
            with tempfile.NamedTemporaryFile("w", suffix=".hlsl", delete=False, dir=tempfile.gettempdir()) as f:
                f.write(corpo)
                tmp = f.name
            try:
                for (estagio, ent) in (("vert", vs), ("frag", fs)):
                    if ent is None:
                        print("ERRO %s bloco %d: sem #pragma %s" % (nome, k, estagio))
                        erros += 1
                        continue
                    cmd = [exe, "-D", "-S", estagio, "-e", ent.group(1), "-I" + os.path.dirname(caminho), "-I" + PASTA,
                           "-I" + STUB, "--hlsl-iomap", "-V", "-o", os.devnull, tmp]
                    r = subprocess.run(cmd, capture_output=True, text=True)
                    saida = (r.stdout + r.stderr).strip()
                    if r.returncode != 0:
                        erros += 1
                        print("ERRO %s bloco %d (%s %s):" % (nome, k, estagio, ent.group(1)))
                        for l in saida.splitlines():
                            if "ERROR" in l or "error" in l:
                                print("   ", l.replace(tmp, nome))
            finally:
                os.unlink(tmp)
    print("shaders: %d erro(s), %d aviso(s)" % (erros, avisos))
    return 1 if erros else 0


if __name__ == "__main__":
    sys.exit(main())
