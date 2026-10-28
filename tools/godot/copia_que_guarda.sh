#!/bin/bash
# Monta a CÓPIA do projeto Godot para o exportar_palco.gd: o Godot sem tela
# (headless) não guarda os parâmetros dos materiais; nesta cópia cada
# ShaderMaterial.new() dos scripts vira um material que guarda o que recebe.
#   tools/godot/copia_que_guarda.sh <beta>/strike-kids-gles2 <pasta da cópia>
# Depois:
#   godot3 --no-window --path <pasta da cópia> -s <beta-unity>/tools/godot/exportar_palco.gd \
#     -- --saida=<beta-unity>/Assets/StrikeKids/Resources
set -e
G="$1"
C="$2"
[ -f "$G/project.godot" ] || { echo "uso: $0 <projeto Godot> <pasta da cópia>"; exit 1; }
rm -rf "$C"
mkdir -p "$C"
for f in .import texturas modelos ui sons fontes shaders cenas dados; do ln -s "$G/$f" "$C/$f"; done
cp "$G/project.godot" "$C/"
cp -r "$G/scripts" "$G/autoload" "$C/"
cat > "$C/_cap_material.gd" <<'FIM'
extends ShaderMaterial
# (exportar_palco: o Godot sem tela não guarda os parâmetros; este guarda)
var _p := {}
func set_shader_param(n, v):
	_p[n] = v
	.set_shader_param(n, v)
func get_shader_param(n):
	return _p.get(n)
FIM
for f in "$C"/scripts/*.gd "$C"/autoload/*.gd; do
	sed -i 's/ShaderMaterial\.new()/(load("res:\/\/_cap_material.gd").new() as ShaderMaterial)/g' "$f"
done
echo "cópia pronta em $C"
