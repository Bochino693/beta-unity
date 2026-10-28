extends SceneTree

## DESPEJA O CENÁRIO DA EDIÇÃO GODOT (temas.gd) peça por peça, para conferir o
## plano da Unity (Scripts/Cenario/PlanoCenario.cs) contra ele:
##   STRIKE_SEM_LOTE=1 godot3 --no-window --path <beta>/strike-kids-gles2 \
##     -s <beta-unity>/tools/godot/despejar_cenario.gd > godot.txt
##   (e tools/godot/conferir_cenario.sh compara com o plano da Unity)
## Uma linha por peça com malha: tema, nível (A = só no alto, L = só no
## leve, - = nos dois), a malha, a posição e a base (no grupo da fase).

func _init() -> void:
	var m = load("res://scripts/mundo.gd").new()
	# (CENARIO=completo e DETALHES=0 conferem os outros jeitos)
	m.leve = OS.get_environment("CENARIO") != "completo"
	m.detalhes = OS.get_environment("DETALHES") != "0"
	var t = load("res://scripts/temas.gd").new()
	t.cenario_leve = false
	root.add_child(t)
	t.construir(m)
	var origem := {}
	for fonte in t._malhas_vento:
		origem[t._malhas_vento[fonte]] = fonte
	for tema in Temas.ORDEM:
		var g: Spatial = t.grupos[tema]
		_despejar(tema, g, Transform(), "-", origem)
		var tipos := []
		for item in t._anim[tema]:
			tipos.append(item["tipo"])
		print("ANIM|%s|%s" % [tema, PoolStringArray(tipos).join(",")])
		var sons := []
		for v in t._vozes[tema]:
			sons.append(v["som"])
		print("VOZ|%s|%s" % [tema, PoolStringArray(sons).join(",")])
	quit()


func _despejar(tema: String, no: Node, pai: Transform, nivel: String, origem: Dictionary) -> void:
	for c in no.get_children():
		if not (c is Spatial):
			continue
		var xf: Transform = pai * (c as Spatial).transform
		var n := nivel
		if c.name == "alto":
			n = "A"
		elif c.name == "leve":
			n = "L"
		if c is MeshInstance and (c as MeshInstance).mesh != null:
			var mesh: Mesh = (c as MeshInstance).mesh
			var nome := mesh.resource_path.get_file().get_basename()
			var vento := ""
			if nome == "" and origem.has(mesh):
				nome = (origem[mesh] as Mesh).resource_path.get_file().get_basename()
				vento = "v"
			if nome == "":
				nome = "?" + str(c.name)
			var b := xf.basis
			print("P|%s|%s|%s%s|%.4f %.4f %.4f|%.4f %.4f %.4f %.4f %.4f %.4f %.4f %.4f %.4f" % [tema, n, nome, vento,
				xf.origin.x, xf.origin.y, xf.origin.z, b.x.x, b.y.x, b.z.x, b.x.y, b.y.y, b.z.y, b.x.z, b.y.z, b.z.z])
		elif c is CPUParticles:
			print("PART|%s|%.3f %.3f %.3f" % [tema, xf.origin.x, xf.origin.y, xf.origin.z])
		_despejar(tema, c, xf, n, origem)
