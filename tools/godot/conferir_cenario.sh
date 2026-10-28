#!/bin/bash
# CONFERE O CENÁRIO DA UNITY CONTRA O DA EDIÇÃO GODOT, PEÇA POR PEÇA, sem a
# Unity: o plano da Unity (Assets/StrikeKids/Scripts/Cenario/PlanoCenario.cs,
# a cópia do temas.gd) roda no .NET com as malhas exportadas
# (Resources/Malhas/cenario) e o Godot 3.6 despeja o dele
# (tools/godot/despejar_cenario.gd); as duas listas são comparadas: as
# mesmas peças, nos mesmos lugares (diferença em metros), as mesmas
# animações, vozes e partículas.
#
#   GODOT=/caminho/do/godot3 BETA=/caminho/do/beta bash tools/godot/conferir_cenario.sh
#
# Precisa do SDK do .NET (dotnet) e do Godot 3.6.3 (o "headless" serve).
set -e
AQUI=$(cd "$(dirname "$0")" && pwd)
RAIZ=$(cd "$AQUI/../.." && pwd)
GODOT=${GODOT:-godot3}
BETA=${BETA:-$RAIZ/../beta}
TMP=$(mktemp -d)
cat > "$TMP/conferir.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$AQUI/conferir/Program.cs" />
    <Compile Include="$RAIZ/Assets/StrikeKids/Scripts/Cenario/Geometria.cs" />
    <Compile Include="$RAIZ/Assets/StrikeKids/Scripts/Cenario/PlanoCenario.cs" />
  </ItemGroup>
</Project>
XML
dotnet build "$TMP/conferir.csproj" -c Release -o "$TMP/bin" -v q -nologo > "$TMP/build.log" || { cat "$TMP/build.log"; exit 1; }
dotnet "$TMP/bin/conferir.dll" "$RAIZ/Assets/StrikeKids/Resources/Malhas/cenario" > "$TMP/unity.txt"
(cd "$BETA/strike-kids-gles2" && STRIKE_SEM_LOTE=1 "$GODOT" --no-window --path . -s "$AQUI/despejar_cenario.gd" > "$TMP/godot.txt" 2> "$TMP/godot.err")
python3 -I "$AQUI/conferir/comparar.py" "$TMP/godot.txt" "$TMP/unity.txt"
for k in ANIM VOZ PART; do
  if diff <(grep "^$k|" "$TMP/godot.txt") <(grep "^$k|" "$TMP/unity.txt") > /dev/null; then echo "$k: iguais"; else echo "$k: DIFERENTES"; fi
done
rm -rf "$TMP"
