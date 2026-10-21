#!/bin/bash
# CONFERE O C# DO PROJETO SEM A UNITY INSTALADA: compila os scripts contra as
# bibliotecas de referência da Unity (as mesmas classes e assinaturas), do
# jeito que a Unity separa:
#   Assembly-CSharp         = Assets/**/*.cs fora das pastas Editor
#   Assembly-CSharp-Editor  = Assets/**/Editor/**/*.cs
# Compila duas vezes o do jogo: como no Android (UNITY_ANDROID) e como no PC
# (UNITY_STANDALONE_WIN) — os dois lados dos #if.
#
#   bash tools/verificar/compilar.sh
#
# Precisa do SDK do .NET (DOTNET=/caminho/do/dotnet se não estiver no PATH) e,
# na primeira vez, de internet (api.nuget.org) para baixar as referências:
#   UnityEngine.Modules 2021.3.33   (UnityEngine.*Module.dll)
#   Unity3D.UnityEngine.UI 2020.3.21 (uGUI 1.0, o mesmo da Unity 2022.3)
#   Unity3D.SDK 2021.1.14.1         (UnityEditor.dll)
#   Microsoft.NETFramework.ReferenceAssemblies.net471 (mscorlib & cia.)
# Elas ficam em UNITY_REFS (padrão ~/.cache/strikekids_unity_refs). Os .dll
# baixados só são lidos pelo compilador (metadados); nada deles é executado.
set -e
AQUI=$(cd "$(dirname "$0")" && pwd)
RAIZ=$(cd "$AQUI/../.." && pwd)
REFS=${UNITY_REFS:-$HOME/.cache/strikekids_unity_refs}
DOTNET=${DOTNET:-dotnet}
SAIDA=${SAIDA:-$(mktemp -d)}
mkdir -p "$REFS" "$SAIDA"

baixar() {   # id versão
  local d="$REFS/$1.$2"
  if [ ! -f "$d/.ok" ]; then
    rm -rf "$d"
    mkdir -p "$d"
    curl -sSfL -m 300 -o "$d/pacote.nupkg" "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg"
    (cd "$d" && unzip -q -o pacote.nupkg && touch .ok)
  fi
  echo "$d"
}

MOD=$(baixar unityengine.modules 2021.3.33)/lib/net45
UI=$(baixar unity3d.unityengine.ui 2020.3.21)/lib
SDK=$(baixar unity3d.sdk 2021.1.14.1)/lib
NET=$(baixar microsoft.netframework.referenceassemblies.net471 1.0.3)/build/.NETFramework/v4.7.1

SDKDIR=$("$DOTNET" --list-sdks | tail -1 | sed -E 's/^([^ ]+) \[(.*)\]$/\2\/\1/')
CSC="$SDKDIR/Roslyn/bincore/csc.dll"
[ -f "$CSC" ] || { echo "não achei o csc.dll do SDK do .NET ($CSC)" >&2; exit 1; }

BASE=(-nologo -noconfig -nostdlib+ -target:library -langversion:9.0 -unsafe- -optimize- -warn:4
  -nowarn:1701,1702,0649,0414,0169
  -r:"$NET/mscorlib.dll" -r:"$NET/System.dll" -r:"$NET/System.Core.dll" -r:"$NET/System.Xml.dll"
  -r:"$NET/Facades/netstandard.dll")
for f in "$MOD"/UnityEngine.*Module.dll; do BASE+=(-r:"$f"); done
BASE+=(-r:"$UI/UnityEngine.UI.dll")
DEF_COMUM="UNITY_2022_3_OR_NEWER;UNITY_2021_3_OR_NEWER;UNITY_5_3_OR_NEWER;ENABLE_IL2CPP;CSHARP_7_3_OR_NEWER"

mapfile -t JOGO < <(cd "$RAIZ" && find Assets -name '*.cs' -not -path '*/Editor/*' | sort)
mapfile -t EDITOR < <(cd "$RAIZ" && find Assets -name '*.cs' -path '*/Editor/*' | sort)
[ ${#JOGO[@]} -gt 0 ] || { echo "nenhum .cs em Assets" >&2; exit 1; }
cd "$RAIZ"
falhou=0
echo "== Assembly-CSharp (Android) — ${#JOGO[@]} arquivos"
"$DOTNET" "$CSC" "${BASE[@]}" -define:"$DEF_COMUM;UNITY_ANDROID" -out:"$SAIDA/Assembly-CSharp.dll" "${JOGO[@]}" || falhou=1
echo "== Assembly-CSharp (PC)"
"$DOTNET" "$CSC" "${BASE[@]}" -define:"$DEF_COMUM;UNITY_STANDALONE;UNITY_STANDALONE_WIN" -out:"$SAIDA/Assembly-CSharp-PC.dll" "${JOGO[@]}" || falhou=1
if [ ${#EDITOR[@]} -gt 0 ] && [ $falhou = 0 ]; then
  echo "== Assembly-CSharp-Editor — ${#EDITOR[@]} arquivos"
  "$DOTNET" "$CSC" "${BASE[@]}" -define:"$DEF_COMUM;UNITY_EDITOR;UNITY_ANDROID" -r:"$SDK/UnityEditor.dll" \
    -r:"$SAIDA/Assembly-CSharp.dll" -out:"$SAIDA/Assembly-CSharp-Editor.dll" "${EDITOR[@]}" || falhou=1
fi
[ $falhou = 0 ] && echo "ok: o C# compila" || { echo "ERROS acima" >&2; exit 1; }
