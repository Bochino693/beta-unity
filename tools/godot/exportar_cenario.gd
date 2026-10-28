extends SceneTree

## TRAZ OS MODELOS E AS TEXTURAS DA EDIÇÃO GODOT (strike-kids-gles2, a que
## roda na TV Box) PARA A UNITY: o mesmo desenho, os mesmos triângulos.
## Roda dentro do projeto Godot, gravando direto na pasta Resources daqui:
##   godot3 --no-window --path <beta>/strike-kids-gles2 \
##     -s <beta-unity>/tools/godot/exportar_cenario.gd -- --saida=<beta-unity>/Assets/StrikeKids/Resources
## (o Godot 3.6.3 "headless" também serve; não abre janela)
##
## Grava, no formato SKP1 da Unity (Scripts/Malhas.cs):
##   Malhas/cenario/<nome>[_meio|_longe].bytes   os 3 níveis de cada peça
##   Malhas/terreno.bytes                         o chão com os patamares
## e copia as texturas do cenário (atlas de cada fase, fundos pintados, o
## detalhe do chão) para Texturas/.
##
## A Unity é de MÃO ESQUERDA e o jogo dela espelha o X (docs/ARQUITETURA.md:
## x_unity = -x_godot): cada ponto e normal tem o X trocado de sinal. A
## ordem dos triângulos fica a mesma — espelhar já troca a mão, e a frente
## continua a frente (conferido: os triângulos ficam como os do modelador,
## Malhas.Quadrado). O V da textura vira (a Unity conta de baixo para cima).

var saida := ""
var n_malhas := 0
var n_tri := 0


func _init() -> void:
	for a in OS.get_cmdline_args():
		if a.begins_with("--saida="):
			saida = a.substr(8)
	if saida == "":
		printerr("faltou --saida=<pasta Resources da Unity>")
		quit(1)
		return
	var d := Directory.new()
	d.make_dir_recursive(saida + "/Malhas/cenario")
	d.make_dir_recursive(saida + "/Texturas/cenario")
	# o cenário: todas as peças, nos 3 níveis
	d.open("res://modelos/cenario")
	d.list_dir_begin(true)
	var f := d.get_next()
	var nomes := []
	while f != "":
		if f.ends_with(".res"):
			nomes.append(f.get_basename())
		f = d.get_next()
	nomes.sort()
	for n in nomes:
		_gravar(load("res://modelos/cenario/%s.res" % n), n, saida + "/Malhas/cenario/%s.bytes" % n)
	_gravar(load("res://modelos/terreno.res"), "terreno", saida + "/Malhas/terreno.bytes")
	# as texturas do cenário
	for t in ["parque", "espaco", "mar", "dinos"]:
		_copiar("res://texturas/cenario/atlas_%s.png" % t, saida + "/Texturas/cenario/atlas_%s.png" % t)
		_copiar("res://texturas/fundo_%s.png" % t, saida + "/Texturas/cenario/fundo_%s.png" % t)
	for i in 4:
		_copiar("res://texturas/chao_%d.png" % i, saida + "/Texturas/cenario/chao_%d.png" % i)
	print("EXPORTADO: %d malhas, %d triângulos" % [n_malhas, n_tri])
	quit()


func _copiar(de: String, para: String) -> void:
	var dir := Directory.new()
	var erro := dir.copy(ProjectSettings.globalize_path(de), para)
	if erro != OK:
		printerr("não copiei %s (%d)" % [de, erro])


## Uma malha do Godot (todas as superfícies) num pacote SKP1: uma parte por
## superfície (nome, nome_1...), sem pai, no lugar.
func _gravar(m: Mesh, nome: String, caminho: String) -> void:
	if m == null:
		printerr("sem malha: ", nome)
		return
	var f := File.new()
	if f.open(caminho, File.WRITE) != OK:
		printerr("não gravei ", caminho)
		return
	f.store_buffer("SKP1".to_ascii())
	f.store_16(m.get_surface_count())
	for s in m.get_surface_count():
		var arr: Array = m.surface_get_arrays(s)
		var vs: PoolVector3Array = arr[Mesh.ARRAY_VERTEX]
		var ns = arr[Mesh.ARRAY_NORMAL]
		var cs = arr[Mesh.ARRAY_COLOR]
		var us = arr[Mesh.ARRAY_TEX_UV]
		var ix = arr[Mesh.ARRAY_INDEX]
		var tem_uv: bool = us != null and (us as PoolVector2Array).size() == vs.size()
		_str(f, nome if s == 0 else "%s_%d" % [nome, s])
		f.store_16(0xFFFF)          # pai = -1
		f.store_float(0.0)
		f.store_float(0.0)
		f.store_float(0.0)
		_str(f, "atlas" if tem_uv else "cor")
		f.store_8(1 if tem_uv else 0)
		var nv := vs.size()
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
		var indices := PoolIntArray()
		if ix != null and (ix as PoolIntArray).size() > 0:
			indices = ix
		else:
			for i in nv:
				indices.append(i)
		f.store_32(indices.size())
		var t := 0
		while t + 2 < indices.size():
			f.store_16(indices[t])
			f.store_16(indices[t + 1])
			f.store_16(indices[t + 2])
			t += 3
		n_tri += indices.size() / 3
	f.close()
	n_malhas += 1


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
