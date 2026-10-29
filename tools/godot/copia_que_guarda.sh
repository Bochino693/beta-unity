#!/bin/bash
# Monta a CÓPIA do projeto Godot para o exportar_palco.gd: o Godot sem tela
# (headless) não guarda os parâmetros dos materiais nem as cópias das
# MultiMesh (as fileiras de lâmpadas viravam um bolo na origem); nesta cópia
# cada ShaderMaterial.new() e MultiMesh.new() dos scripts vira um que guarda
# o que recebe.
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
cat > "$C/_cap_multimesh.gd" <<'FIM'
extends MultiMesh
# (exportar_palco: o Godot sem tela não guarda as cópias; este guarda)
var _t := {}
var _c := {}
var _d := {}
func set_instance_transform(i, t):
	_t[i] = t
	.set_instance_transform(i, t)
func get_instance_transform(i):
	return _t.get(i, Transform())
func set_instance_color(i, c):
	_c[i] = c
	.set_instance_color(i, c)
func get_instance_color(i):
	return _c.get(i, Color.white)
func set_instance_custom_data(i, d):
	_d[i] = d
	.set_instance_custom_data(i, d)
func get_instance_custom_data(i):
	return _d.get(i, Color(0, 0, 0, 0))
FIM
for f in "$C"/scripts/*.gd "$C"/autoload/*.gd; do
	sed -i 's/ShaderMaterial\.new()/(load("res:\/\/_cap_material.gd").new() as ShaderMaterial)/g' "$f"
	sed -i 's/\bMultiMesh\.new()/(load("res:\/\/_cap_multimesh.gd").new() as MultiMesh)/g' "$f"
done
echo "cópia pronta em $C"
