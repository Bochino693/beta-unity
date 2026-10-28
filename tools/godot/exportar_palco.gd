extends SceneTree

## TRAZ O PALCO DA EDIÇÃO GODOT (strike-kids-gles2, a que roda na TV Box)
## PARA A UNITY: Bob e Mia (os 3 níveis de detalhe e as 4 roupinhas), os
## pinos, a bola, a pista, as canaletas, os para-choques, o gabinete, a
## cortina da bola, o poço do alçapão, a área dos jogadores, os pódios, os
## símbolos de cada fase, a máquina de pinos e o sol, a lua e a baleia do
## céu — os mesmos triângulos, os mesmos materiais e as cores de cada fase.
## Roda dentro do projeto Godot, gravando direto na pasta Resources daqui:
##   godot3 --no-window --path <beta>/strike-kids-gles2 \
##     -s <beta-unity>/tools/godot/exportar_palco.gd -- --saida=<beta-unity>/Assets/StrikeKids/Resources
## O Godot 3.6.3 "headless" (sem tela) não guarda os parâmetros dos
## materiais (a placa de mentira dele joga fora): rode numa CÓPIA do projeto
## em que cada ShaderMaterial.new() dos scripts vira o material que guarda
## (tools/godot/copia_que_guarda.sh monta a cópia: links para as pastas e os
## scripts copiados com a troca).
##
## Grava (formato SKP1 da Unity, Scripts/Malhas.cs; o X trocado de sinal
## e o V da textura virado, como no exportar_cenario.gd):
##   Malhas/<bicho>[_<roupa>][_hd|_longe].bytes   uma parte por junta (pai e pivô)
##   Malhas/pino[_perto|_longe].bytes, Malhas/bola_esfera.bytes
##   Malhas/palco.bytes                            as malhas do palco (m0, m1...)
##   Palco/palco.json      as peças (pai, lugar, malha, material, papel), os
##                         materiais (shader e parâmetros), o que muda em
##                         cada fase e a luz de palco de cada fase
##   Palco/personagens.json  rosto, mão, tornozelo e estampa de cada bicho
##   Texturas/palco/*.png  as texturas que os materiais usam (no tamanho do
##                         jogo: o limite do .import do Godot)
## Os parâmetros dos materiais ficam como no Godot (pontos e direções no
## espaço de lá: os shaders da Unity, Shaders/Palco/G_*, fazem a conta);
## só o lugar das peças vem já na Unity (x ao contrário).

const FASES := ["parque", "espaco", "mar", "dinos"]
const BICHOS := ["bob", "mia"]
const ROUPAS := ["", "astronauta", "dino", "parque", "pirata"]

var saida := ""
var n_malhas := 0
var n_tri := 0
# o palco
var _nos := []            # dicionários das peças
var _no_de := {}          # instance_id do nó -> índice
var _mats := []           # materiais (Material)
var _mat_de := {}         # instance_id -> índice
var _malhas := []         # [arrays, nome]
var _malha_de := {}       # instance_id da malha -> índice
var _texturas := {}       # caminho -> nome
var _papel_no := {}       # instance_id -> papel
var _papel_mat := {}      # instance_id -> papel
var _mundo


func _init() -> void:
	for a in OS.get_cmdline_args():
		if a.begins_with("--saida="):
			saida = a.substr(8)
	if saida == "":
		printerr("faltou --saida=<pasta Resources da Unity>")
		quit(1)
		return
	var d := Directory.new()
	for p in ["Malhas", "Palco", "Texturas/palco"]:
		d.make_dir_recursive(saida + "/" + p)
	_personagens()
	_pinos_e_bola()
	_palco()
	print("EXPORTADO: %d malhas, %d triângulos, %d texturas" % [n_malhas, n_tri, _texturas.size()])
	quit()


# ================================================================ bichinhos
func _personagens() -> void:
	var meta := {}
	for b in BICHOS:
		for r in ROUPAS:
			for nv in ["", "_hd", "_longe"]:
				var c := ""
				if r == "":
					c = "res://modelos/%s.scn" % b if nv == "" else "res://modelos/niveis/%s%s.scn" % [b, nv]
				else:
					c = "res://modelos/roupas/%s_%s.scn" % [b, r] if nv == "" else "res://modelos/niveis/roupas/%s_%s%s.scn" % [b, r, nv]
				if not ResourceLoader.exists(c):
					printerr("falta ", c)
					continue
				var nome: String = b + ("_" + r if r != "" else "") + nv
				var inst: Spatial = load(c).instance()
				_gravar_bicho(inst, saida + "/Malhas/%s.bytes" % nome)
				if nv == "":
					var m := {}
					for k in ["rosto", "mao_d", "tornozelo_y", "estampa", "roupa"]:
						if inst.has_meta(k):
							m[k] = _json(inst.get_meta(k))
					meta[nome] = m
				inst.free()
	# o rosto dos pinos (a malha do pino guarda o lugar dele)
	var pino: Mesh = load("res://modelos/pino.res")
	meta["pino"] = {"rosto": _json(pino.get_meta("rosto", {}))}
	_texto(saida + "/Palco/personagens.json", JSON.print(meta, "\t"))
	for t in ["rosto_bob", "rosto_mia", "rosto_pino", "bola_bob", "bola_mia", "bola_alvo", "estrela", "sombra"]:
		_textura(load("res://texturas/%s.png" % t))


## As juntas do bicho (corpo > cabeça...) e a malha de cada uma: uma parte
## por junta, com o pai e o pivô (a malha da junta fica no nó "malha" dela).
func _gravar_bicho(raiz: Spatial, caminho: String) -> void:
	var partes := []
	_juntas(raiz, -1, partes)
	var f := File.new()
	if f.open(caminho, File.WRITE) != OK:
		printerr("não gravei ", caminho)
		return
	f.store_buffer("SKP1".to_ascii())
	f.store_16(partes.size())
	for p in partes:
		_parte(f, p[0], p[1], p[2], "toon", p[3])
	f.close()
	n_malhas += 1


func _juntas(no: Node, pai: int, partes: Array) -> void:
	for c in no.get_children():
		if not (c is Spatial) or c.name == "malha":
			continue
		var s := c as Spatial
		if s.transform.basis != Basis():
			printerr("%s: junta girada (a Unity espera sem giro)" % s.name)
		var mi := s.get_node_or_null("malha") as MeshInstance
		if mi != null and mi.transform != Transform():
			printerr("%s: malha fora do lugar da junta" % s.name)
		partes.append([s.name, pai, s.translation, mi.mesh if mi else null])
		_juntas(s, partes.size() - 1, partes)


# ================================================================ pinos e bola
func _pinos_e_bola() -> void:
	for n in ["pino", "pino_perto", "pino_longe"]:
		var c: String = "res://modelos/%s.res" % n
		if ResourceLoader.exists(c):
			_gravar_unica(load(c), n, saida + "/Malhas/%s.bytes" % n)
	var esfera := SphereMesh.new()
	var raio: float = load("res://scripts/bola.gd").RAIO
	esfera.radius = raio
	esfera.height = raio * 2
	esfera.radial_segments = 32
	esfera.rings = 16
	_gravar_unica(esfera, "bola", saida + "/Malhas/bola_esfera.bytes")


func _gravar_unica(m: Mesh, nome: String, caminho: String) -> void:
	var f := File.new()
	if f.open(caminho, File.WRITE) != OK:
		printerr("não gravei ", caminho)
		return
	f.store_buffer("SKP1".to_ascii())
	f.store_16(1)
	_parte(f, nome, -1, Vector3.ZERO, "toon", m)
	f.close()
	n_malhas += 1


# ================================================================ o palco
func _palco() -> void:
	# (carregados na hora: citar as classes no script faz o Godot carregar
	# os autoloads em ciclo)
	_mundo = load("res://scripts/mundo.gd").new()
	root.add_child(_mundo)
	_mundo.construir()
	var pinos = load("res://scripts/pinos.gd").new()
	root.add_child(pinos)
	pinos.montar(true)
	var maquina = load("res://scripts/maquina.gd").new()
	root.add_child(maquina)
	maquina.montar(pinos)
	_papeis(_mundo, "")
	_papeis(maquina, "maquina.")
	# o que fica de fora: o cenário (Temas, já na Unity), o céu e o chão
	# (feitos lá), o sol (só a direção da luz) e a física
	var fora := {}
	for n in [_mundo.temas, _mundo.ceu, _mundo.sol]:
		if n:
			fora[n] = true
	for c in _mundo.get_children():
		if c is MeshInstance and (c as MeshInstance).material_override == _mundo.mat_terreno:
			fora[c] = true
		if c is WorldEnvironment:
			fora[c] = true
	_coletar(_mundo, -1, fora)
	var raiz_maquina := _nos.size()
	_nos.append({"id": 0, "n": "maquina", "p": -1, "t": _transformacao(Transform()), "m": -1, "mat": -1, "v": true, "papel": "maquina"})
	_coletar(maquina, raiz_maquina, fora)
	# o sol, a lua e a baleia: as três malhas (o astro troca de malha na fase)
	var astros := {}
	for k in _mundo._malhas_astro:
		astros[k] = _malha(_mundo._malhas_astro[k])
	# o que muda em cada fase: parâmetros dos materiais, peças que aparecem
	# ou somem, a malha do astro e a luz de palco (Materiais.definir_luz)
	var por_fase := {}
	var vistas := {}
	var luz := {}
	for fase in FASES:
		_mundo.aplicar_tema(fase)
		var p := {}
		for i in _mats.size():
			p[str(i)] = _parametros(_mats[i])
		por_fase[fase] = p
		var v := {}
		for i in _nos.size():
			var no = instance_from_id(int(_nos[i]["id"])) if int(_nos[i]["id"]) != 0 else null
			v[str(i)] = [(no as Spatial).visible, _malha_do_no(no)] if no is Spatial else [true, -1]
		vistas[fase] = v
		var e: Dictionary = load("res://scripts/materiais.gd")._e()
		luz[fase] = {"luz_dir": _json(e["luz_dir"]), "luz_cor": _json(e["luz_cor"]), "ambiente": _json(e["ambiente"])}
	# guarda só o que muda de uma fase para outra
	var temas := {}
	for fase in FASES:
		temas[fase] = {"mats": {}, "nos": {}}
	for i in _mats.size():
		var k := str(i)
		var base: Dictionary = por_fase[FASES[0]][k]
		for nome in base:
			var muda := false
			for fase in FASES:
				if str(por_fase[fase][k].get(nome)) != str(base[nome]):
					muda = true
			if muda:
				for fase in FASES:
					if not temas[fase]["mats"].has(k):
						temas[fase]["mats"][k] = {}
					temas[fase]["mats"][k][nome] = por_fase[fase][k].get(nome)
	for i in _nos.size():
		var k := str(i)
		var muda := false
		for fase in FASES:
			if str(vistas[fase][k]) != str(vistas[FASES[0]][k]):
				muda = true
		if muda:
			for fase in FASES:
				temas[fase]["nos"][k] = vistas[fase][k]
	# os materiais (com os parâmetros da 1ª fase) e as peças
	var mats := []
	for i in _mats.size():
		mats.append(_material(_mats[i], por_fase[FASES[0]][str(i)]))
	for no in _nos:
		no.erase("id")
	var palco := {"nos": _nos, "materiais": mats, "temas": temas, "luz": luz, "astros": astros, "fases": FASES}
	_texto(saida + "/Palco/palco.json", JSON.print(palco))
	# as malhas
	var f := File.new()
	if f.open(saida + "/Malhas/palco.bytes", File.WRITE) != OK:
		printerr("não gravei palco.bytes")
		return
	f.store_buffer("SKP1".to_ascii())
	f.store_16(_malhas.size())
	for i in _malhas.size():
		_parte_arrays(f, "m%d" % i, -1, Vector3.ZERO, "palco", _malhas[i])
	f.close()
	n_malhas += 1
	print("palco: %d peças, %d materiais, %d malhas" % [_nos.size(), _mats.size(), _malhas.size()])


## Os nós e materiais guardados nas variáveis do script (balao, _tiras,
## mat_pista, mats_canaleta[0], _marcas.parque...): o "papel" de cada um,
## para a Unity saber o que mexer.
func _papeis(obj: Object, prefixo: String) -> void:
	for p in obj.get_property_list():
		if not (int(p["usage"]) & PROPERTY_USAGE_SCRIPT_VARIABLE):
			continue
		var nome: String = p["name"]
		var v = obj.get(nome)
		_papel(prefixo + nome, v)


func _papel(nome: String, v) -> void:
	if v is Spatial:
		if not _papel_no.has(v.get_instance_id()):
			_papel_no[v.get_instance_id()] = nome
	elif v is Material:
		if not _papel_mat.has(v.get_instance_id()):
			_papel_mat[v.get_instance_id()] = nome
	elif v is Array:
		for i in v.size():
			_papel("%s[%d]" % [nome, i], v[i])
	elif v is Dictionary:
		for k in v:
			_papel("%s.%s" % [nome, str(k)], v[k])


func _coletar(no: Node, pai: int, fora: Dictionary) -> void:
	for c in no.get_children():
		if fora.has(c) or c is CollisionShape or c is StaticBody or c is CPUParticles or c is ImmediateGeometry:
			continue
		if not (c is Spatial):
			continue
		var s := c as Spatial
		var reg := {"id": s.get_instance_id(), "n": str(s.name), "p": pai, "t": _transformacao(s.transform),
			"m": -1, "mat": -1, "v": s.visible}
		if _papel_no.has(s.get_instance_id()):
			reg["papel"] = _papel_no[s.get_instance_id()]
		if s.is_set_as_toplevel():
			reg["solto"] = true
		if s is MeshInstance and (s as MeshInstance).mesh:
			var mi := s as MeshInstance
			reg["m"] = _malha(mi.mesh)
			var mat: Material = mi.material_override
			if mat == null and mi.mesh.get_surface_count() > 0:
				mat = mi.mesh.surface_get_material(0)
			reg["mat"] = _mat(mat)
			if mi.mesh.get_surface_count() > 1:
				printerr("%s: %d superfícies (só a 1ª vai)" % [s.name, mi.mesh.get_surface_count()])
		elif s is MultiMeshInstance and (s as MultiMeshInstance).multimesh:
			var mm := s as MultiMeshInstance
			reg["m"] = _malha_multi(mm.multimesh)
			reg["mat"] = _mat(mm.material_override)
		_nos.append(reg)
		_no_de[s.get_instance_id()] = _nos.size() - 1
		_coletar(s, _nos.size() - 1, fora)


func _malha_do_no(no: Spatial) -> int:
	if no is MeshInstance and (no as MeshInstance).mesh:
		return _malha((no as MeshInstance).mesh)
	return -1


## Lugar, giro e escala do nó na Unity (x ao contrário): o giro M·R·M vira o
## quaternião (x, -y, -z, w); a escala fica igual.
func _transformacao(t: Transform) -> Array:
	var esc: Vector3 = t.basis.get_scale()
	var q: Quat = t.basis.orthonormalized().get_rotation_quat()
	return [-t.origin.x, t.origin.y, t.origin.z, q.x, -q.y, -q.z, q.w, esc.x, esc.y, esc.z]


func _malha(m: Mesh) -> int:
	if m == null:
		return -1
	var id := m.get_instance_id()
	if _malha_de.has(id):
		return _malha_de[id]
	if m.get_surface_count() == 0:
		return -1
	_malhas.append(m.surface_get_arrays(0))
	_malha_de[id] = _malhas.size() - 1
	return _malhas.size() - 1


## A MultiMesh (as lâmpadas) vira uma malha só: cada instância no lugar
## dela, a cor da instância na cor do vértice e o número dela
## (INSTANCE_CUSTOM.r) no U da textura.
func _malha_multi(mm: MultiMesh) -> int:
	var id := mm.get_instance_id()
	if _malha_de.has(id):
		return _malha_de[id]
	var a: Array = mm.mesh.surface_get_arrays(0)
	var vs: PoolVector3Array = a[Mesh.ARRAY_VERTEX]
	var ns = a[Mesh.ARRAY_NORMAL]
	var cs = a[Mesh.ARRAY_COLOR]
	var ix = a[Mesh.ARRAY_INDEX]
	var V := PoolVector3Array()
	var N := PoolVector3Array()
	var C := PoolColorArray()
	var U := PoolVector2Array()
	var I := PoolIntArray()
	var usa_cor := mm.color_format != MultiMesh.COLOR_NONE
	var usa_dado := mm.custom_data_format != MultiMesh.CUSTOM_DATA_NONE
	for k in mm.instance_count:
		var t := mm.get_instance_transform(k)
		var cor: Color = mm.get_instance_color(k) if usa_cor else Color.white
		var dado: Color = mm.get_instance_custom_data(k) if usa_dado else Color(0, 0, 0, 0)
		var base := V.size()
		for i in vs.size():
			V.append(t.xform(vs[i]))
			N.append(t.basis.xform(ns[i]).normalized() if ns != null and ns.size() == vs.size() else Vector3.UP)
			var c: Color = cs[i] if cs != null and cs.size() == vs.size() else Color.white
			C.append(Color(c.r * cor.r, c.g * cor.g, c.b * cor.b, c.a * cor.a))
			# (o V da textura vira 1 - v na gravação: guardado ao contrário aqui)
			U.append(Vector2(dado.r, 1.0))
		if ix != null and ix.size() > 0:
			for i in ix:
				I.append(base + i)
		else:
			for i in vs.size():
				I.append(base + i)
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = V
	arr[Mesh.ARRAY_NORMAL] = N
	arr[Mesh.ARRAY_COLOR] = C
	arr[Mesh.ARRAY_TEX_UV] = U
	arr[Mesh.ARRAY_INDEX] = I
	_malhas.append(arr)
	_malha_de[id] = _malhas.size() - 1
	return _malhas.size() - 1


func _mat(m: Material) -> int:
	if m == null:
		return -1
	var id := m.get_instance_id()
	if _mat_de.has(id):
		return _mat_de[id]
	_mats.append(m)
	_mat_de[id] = _mats.size() - 1
	if m.next_pass:
		_mat(m.next_pass)
	return _mats.size() - 1


## Os parâmetros do material agora (ShaderMaterial: os uniforms; os de luz
## de palco ficam de fora — na Unity são globais).
func _parametros(m: Material) -> Dictionary:
	var p := {}
	if m is ShaderMaterial and (m as ShaderMaterial).shader:
		var sm := m as ShaderMaterial
		# o Godot sem tela não guarda os parâmetros na placa: a cópia do
		# projeto usada aqui troca ShaderMaterial.new() por um que guarda
		# (_p; ver "Rodar" no começo); senão, a lista do shader
		var nomes := []
		if "_p" in sm:
			nomes = sm._p.keys()
		else:
			for u in VisualServer.shader_get_param_list(sm.shader.get_rid()):
				nomes.append(u["name"])
		for nome in nomes:
			if nome in ["luz_dir", "luz_cor", "ambiente"]:
				continue
			var v = sm.get_shader_param(nome)
			if v == null:
				continue
			var j = _json(v)
			if j != null:
				p[nome] = j
	return p


func _material(m: Material, p: Dictionary) -> Dictionary:
	var r := {}
	if _papel_mat.has(m.get_instance_id()):
		r["papel"] = _papel_mat[m.get_instance_id()]
	r["prio"] = m.render_priority
	if m.next_pass:
		r["np"] = _mat_de[m.next_pass.get_instance_id()]
	if m is ShaderMaterial:
		r["s"] = (m as ShaderMaterial).shader.resource_path.get_file().get_basename()
		r["p"] = p
	elif m is SpatialMaterial:
		var s := m as SpatialMaterial
		r["s"] = "spatial"
		r["cor"] = _json(s.albedo_color)
		if s.albedo_texture:
			r["tex"] = _textura(s.albedo_texture)
		r["sem_luz"] = s.flags_unshaded
		r["transparente"] = s.flags_transparent
		r["corte"] = s.params_alpha_scissor_threshold if s.params_use_alpha_scissor else -1.0
		r["billboard"] = s.params_billboard_mode
		r["soma"] = s.params_blend_mode == SpatialMaterial.BLEND_MODE_ADD
		r["cor_vertice"] = s.vertex_color_use_as_albedo
		r["cull"] = s.params_cull_mode
		if s.emission_enabled:
			r["emissao"] = _json(s.emission * s.emission_energy)
	return r


## Um valor do Godot em JSON (cor e vetores viram listas; textura vira
## {"tex": nome}, gravada em Texturas/palco).
func _json(v):
	match typeof(v):
		TYPE_BOOL, TYPE_INT, TYPE_REAL, TYPE_STRING:
			return v
		TYPE_VECTOR2:
			return [v.x, v.y]
		TYPE_VECTOR3:
			return [v.x, v.y, v.z]
		TYPE_COLOR:
			return [v.r, v.g, v.b, v.a]
		TYPE_ARRAY, TYPE_REAL_ARRAY, TYPE_INT_ARRAY:
			var l := []
			for x in v:
				l.append(_json(x))
			return l
		TYPE_DICTIONARY:
			var d := {}
			for k in v:
				d[str(k)] = _json(v[k])
			return d
		TYPE_OBJECT:
			if v is Texture:
				return {"tex": _textura(v)}
	return null


## Grava a textura (uma vez) no tamanho do jogo: o limite do .import.
func _textura(t: Texture) -> String:
	if t == null:
		return ""
	var c := t.resource_path
	if _texturas.has(c):
		return _texturas[c]
	var nome := c.get_file().get_basename()
	if nome in _texturas.values():
		nome = c.get_base_dir().get_file() + "_" + nome
	_texturas[c] = nome
	# (as do cenário já estão em Texturas/cenario: a Unity procura lá também)
	if File.new().file_exists(saida + "/Texturas/cenario/%s.png" % nome):
		return nome
	var img := Image.new()
	if img.load(ProjectSettings.globalize_path(c)) != OK:
		printerr("não li ", c)
		return nome
	var limite := 0
	var imp := ConfigFile.new()
	if imp.load(c + ".import") == OK:
		limite = int(imp.get_value("params", "size_limit", 0))
	if limite > 0 and max(img.get_width(), img.get_height()) > limite:
		var k: float = float(limite) / max(img.get_width(), img.get_height())
		img.resize(int(round(img.get_width() * k)), int(round(img.get_height() * k)), Image.INTERPOLATE_LANCZOS)
	img.save_png(saida + "/Texturas/palco/%s.png" % nome)
	return nome


func _texto(caminho: String, s: String) -> void:
	var f := File.new()
	if f.open(caminho, File.WRITE) == OK:
		f.store_string(s)
		f.close()
	else:
		printerr("não gravei ", caminho)


# ================================================================ SKP1
func _parte(f: File, nome: String, pai: int, pos: Vector3, material: String, m: Mesh) -> void:
	_parte_arrays(f, nome, pai, pos, material, m.surface_get_arrays(0) if m != null and m.get_surface_count() > 0 else null)


## Uma parte do pacote: nome, pai, pivô (x ao contrário), material e a malha
## (flags: 1 = UV, 2 = UV2).
func _parte_arrays(f: File, nome: String, pai: int, pos: Vector3, material: String, arr) -> void:
	_str(f, nome)
	f.store_16(pai & 0xFFFF)
	f.store_float(-pos.x)
	f.store_float(pos.y)
	f.store_float(pos.z)
	_str(f, material)
	if arr == null:
		f.store_8(0)
		f.store_32(0)
		f.store_float(1.0)
		f.store_32(0)
		return
	var vs: PoolVector3Array = arr[Mesh.ARRAY_VERTEX]
	var ns = arr[Mesh.ARRAY_NORMAL]
	var cs = arr[Mesh.ARRAY_COLOR]
	var us = arr[Mesh.ARRAY_TEX_UV]
	var u2 = arr[Mesh.ARRAY_TEX_UV2]
	var ix = arr[Mesh.ARRAY_INDEX]
	var nv := vs.size()
	var tem_uv: bool = us != null and (us as PoolVector2Array).size() == nv
	var tem_uv2: bool = u2 != null and (u2 as PoolVector2Array).size() == nv
	f.store_8((1 if tem_uv else 0) | (2 if tem_uv2 else 0))
	if nv > 65535:
		printerr("%s: %d vértices (máx 65535)" % [nome, nv])
	var escala := 0.0
	for v in vs:
		escala = max(escala, max(abs(v.x), max(abs(v.y), abs(v.z))))
	if escala <= 0.0:
		escala = 1.0
	f.store_32(nv)
	f.store_float(escala)
	for v in vs:
		f.store_16(_q16(-v.x / escala))
		f.store_16(_q16(v.y / escala))
		f.store_16(_q16(v.z / escala))
	for i in nv:
		var n := Vector3.UP
		if ns != null and (ns as PoolVector3Array).size() == nv:
			n = (ns[i] as Vector3).normalized()
		f.store_8(_q8(-n.x))
		f.store_8(_q8(n.y))
		f.store_8(_q8(n.z))
	for i in nv:
		var c := Color.white
		if cs != null and (cs as PoolColorArray).size() == nv:
			c = cs[i]
		f.store_8(int(clamp(round(c.r * 255.0), 0, 255)))
		f.store_8(int(clamp(round(c.g * 255.0), 0, 255)))
		f.store_8(int(clamp(round(c.b * 255.0), 0, 255)))
		f.store_8(int(clamp(round(c.a * 255.0), 0, 255)))
	if tem_uv:
		for u in us:
			f.store_float(u.x)
			f.store_float(1.0 - u.y)
	if tem_uv2:
		for u in u2:
			f.store_float(u.x)
			f.store_float(1.0 - u.y)
	var indices := PoolIntArray()
	if ix != null and (ix as PoolIntArray).size() > 0:
		indices = ix
	else:
		for i in nv:
			indices.append(i)
	f.store_32(indices.size())
	for i in indices:
		f.store_16(i)
	n_tri += indices.size() / 3


func _str(f: File, s: String) -> void:
	var b := s.to_utf8()
	f.store_8(b.size())
	f.store_buffer(b)


func _q16(x: float) -> int:
	var v := int(round(clamp(x, -1.0, 1.0) * 32767.0))
	return v & 0xFFFF


func _q8(x: float) -> int:
	var v := int(round(clamp(x, -1.0, 1.0) * 127.0))
	return v & 0xFF
