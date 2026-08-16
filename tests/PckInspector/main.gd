extends SceneTree

const SCHEMA_VERSION := 1


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var arguments := _parse_arguments(OS.get_cmdline_user_args())
	if not arguments.has("pack") or not arguments.has("output"):
		_fail("Usage: --pack <absolute-path> --output <absolute-path>", arguments.get("output", ""))
		return

	var pack_path: String = arguments["pack"]
	var output_path: String = arguments["output"]
	var before := _complete_inventory()
	if not ProjectSettings.load_resource_pack(pack_path, true):
		_fail("Could not mount resource pack: %s" % pack_path, output_path)
		return

	var after := _complete_inventory()
	var before_set := {}
	for path in before:
		before_set[path] = true
	var added: Array[String] = []
	for path in after:
		if not before_set.has(path):
			added.append(path)

	_write_result(output_path, {
		"schema_version": SCHEMA_VERSION,
		"status": "PASS",
		"pack_path": pack_path,
		"paths": after,
		"added_paths": added,
	})
	quit(0)


func _complete_inventory() -> Array[String]:
	var paths := _inventory("res://")
	var seen := {}
	for path in paths:
		seen[path] = true
	# The inspector's .gdignore hides its own metadata from res:// before mount,
	# but a pack containing .godot/imported makes the merged directory visible.
	# Read the physical directory so those pre-existing files are not attributed
	# to the mounted pack while keeping genuinely packaged .godot paths visible.
	for path in _host_godot_inventory():
		if not seen.has(path):
			paths.append(path)
			seen[path] = true
	paths.sort()
	return paths


func _host_godot_inventory() -> Array[String]:
	const VIRTUAL_ROOT := "res://.godot"
	var absolute_root := ProjectSettings.globalize_path(VIRTUAL_ROOT).trim_suffix("/")
	var paths: Array[String] = []
	for absolute_path in _inventory(absolute_root):
		paths.append(VIRTUAL_ROOT + absolute_path.trim_prefix(absolute_root))
	# Godot deliberately omits .gdignore itself from directory listings, even
	# for an absolute path, so baseline the marker through a direct file check.
	if FileAccess.file_exists(absolute_root + "/.gdignore"):
		paths.append(VIRTUAL_ROOT + "/.gdignore")
	paths.sort()
	return paths


func _parse_arguments(values: PackedStringArray) -> Dictionary:
	var result := {}
	var index := 0
	while index < values.size():
		var key := values[index]
		if (key == "--pack" or key == "--output") and index + 1 < values.size():
			result[key.trim_prefix("--")] = values[index + 1]
			index += 2
		else:
			index += 1
	return result


func _inventory(root: String) -> Array[String]:
	var paths: Array[String] = []
	var pending: Array[String] = [root]
	while not pending.is_empty():
		var current: String = pending.pop_back()
		var directory := DirAccess.open(current)
		if directory == null:
			continue
		directory.list_dir_begin()
		var entry := directory.get_next()
		while not entry.is_empty():
			if entry != "." and entry != "..":
				var path: String = current.trim_suffix("/") + "/" + entry
				if directory.current_is_dir():
					pending.append(path)
				else:
					paths.append(path)
			entry = directory.get_next()
		directory.list_dir_end()
	paths.sort()
	return paths


func _fail(message: String, output_path: String) -> void:
	push_error(message)
	if not output_path.is_empty():
		_write_result(output_path, {
			"schema_version": SCHEMA_VERSION,
			"status": "FAIL",
			"failure": message,
			"paths": [],
			"added_paths": [],
		})
	quit(2)


func _write_result(output_path: String, result: Dictionary) -> void:
	var temporary_path := output_path + ".tmp"
	var file := FileAccess.open(temporary_path, FileAccess.WRITE)
	if file == null:
		push_error("Could not write inspector result: %s" % temporary_path)
		return
	file.store_string(JSON.stringify(result, "  "))
	file.close()
	if FileAccess.file_exists(output_path):
		DirAccess.remove_absolute(output_path)
	var rename_error := DirAccess.rename_absolute(temporary_path, output_path)
	if rename_error != OK:
		push_error("Could not atomically publish inspector result: %s" % error_string(rename_error))
