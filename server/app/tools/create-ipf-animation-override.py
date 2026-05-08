#!/usr/bin/env python3
import argparse
import importlib.util
import struct
import zlib
from pathlib import Path


IPF_PASSWORD = bytes([
	0x6F, 0x66, 0x4F, 0x31, 0x61, 0x30, 0x75, 0x65,
	0x58, 0x41, 0x3F, 0x20, 0x5B, 0xFF, 0x73, 0x20,
	0x68, 0x20, 0x25, 0x3F,
])


def crc32_byte(crc, value):
	crc ^= value
	for _ in range(8):
		crc = (crc >> 1) ^ (0xEDB88320 if (crc & 1) else 0)
	return crc & 0xFFFFFFFF


def ipf_encrypt(data):
	key0 = 0x12345678
	key1 = 0x23456789
	key2 = 0x34567890

	def update_keys(value):
		nonlocal key0, key1, key2
		key0 = crc32_byte(key0, value)
		key1 = (key1 + (key0 & 0xFF)) & 0xFFFFFFFF
		key1 = (key1 * 0x08088405 + 1) & 0xFFFFFFFF
		key2 = crc32_byte(key2, (key1 >> 24) & 0xFF)

	def magic_byte():
		value = (key2 & 0xFFFF) | 2
		return ((value * (value ^ 1)) >> 8) & 0xFF

	for value in IPF_PASSWORD:
		update_keys(value)

	result = bytearray()
	for index, value in enumerate(data):
		plain = value
		if index % 2 == 0:
			value ^= magic_byte()
			update_keys(plain)
		result.append(value)

	return bytes(result)


def load_ipf_reader():
	script = Path(__file__).with_name("sync-itemmonsters-from-client.py")
	spec = importlib.util.spec_from_file_location("ipf_reader", script)
	module = importlib.util.module_from_spec(spec)
	spec.loader.exec_module(module)
	return module


def raw_deflate(data):
	compressor = zlib.compressobj(level=9, wbits=-15)
	return compressor.compress(data) + compressor.flush()


def encode_ies_string(value):
	return bytes(char ^ 1 for char in value.encode("utf-8"))


def decode_ies_string(data):
	return bytes(value ^ 1 for value in data).decode("utf-8")


def read_costume_anim_rows(content):
	row_count_offset = 0x92
	row_start_offset = 0x1AC
	row_count = int.from_bytes(content[row_count_offset:row_count_offset + 2], "little")
	rows = []
	position = row_start_offset

	for _ in range(row_count):
		if position + 6 > len(content):
			raise ValueError("costumeAnimOverride.ies ended before all rows could be read")

		class_id = int.from_bytes(content[position:position + 4], "little")
		position += 4

		name_len = int.from_bytes(content[position:position + 2], "little")
		position += 2
		name = decode_ies_string(content[position:position + name_len])
		position += name_len

		position += 4

		copy_len = int.from_bytes(content[position:position + 2], "little")
		position += 2 + copy_len

		if position < len(content) and content[position] == 0:
			position += 1

		rows.append((class_id, name))

	return rows


def ipf_contains(ipf_path, target_name):
	with ipf_path.open("rb") as file:
		file.seek(-24, 2)
		file_count, table_offset, _, _, signature, _, new_version = struct.unpack("<HIHI4sII", file.read(24))
		if signature != b"PK\x05\x06":
			return None

		file.seek(table_offset)
		for _ in range(file_count):
			path_len, _, _, _, _, pack_len = struct.unpack("<HIIIIH", file.read(20))
			file.read(pack_len)
			path = file.read(path_len).decode("utf-8", errors="replace")
			if path == target_name:
				return new_version

	return None


def find_best_costume_anim_override(reader, client_root, output_path, explicit_patch=None):
	target_name = "costumeAnimOverride.ies"
	patch_root = client_root / "patch"
	candidates = []

	if explicit_patch:
		patch_path = patch_root / explicit_patch
		content = reader.extract_ipf_file(patch_path, target_name)
		rows = read_costume_anim_rows(content)
		return patch_path, content, rows

	for patch_path in patch_root.glob("*.ipf"):
		if patch_path.resolve() == output_path.resolve():
			continue

		try:
			version = ipf_contains(patch_path, target_name)
		except OSError:
			continue

		if version is None:
			continue

		try:
			content = reader.extract_ipf_file(patch_path, target_name)
			rows = read_costume_anim_rows(content)
		except (OSError, ValueError, UnicodeDecodeError, zlib.error):
			continue

		candidates.append((len(rows), version, patch_path, content, rows))

	if not candidates:
		raise RuntimeError(f"No official {target_name} was found in {patch_root}")

	candidates.sort(key=lambda candidate: (candidate[0], candidate[1], candidate[2].name))
	_, _, patch_path, content, rows = candidates[-1]
	return patch_path, content, rows


def make_costume_anim_override(base_content, aliases):
	# costumeAnimOverride.ies lists the costume suffixes for which the client
	# attempts costume-specific weapon animations. Rows start at 0x1ac in this
	# client format. Each row is:
	# ClassID:int, ClassName:len+xor-string, ClassID as float, ClassName copy.
	row_count_offset = 0x92
	file_size_offset = 0x8C
	row_start_offset = 0x1AC

	content = bytearray(base_content[:row_start_offset])
	existing_rows = base_content[row_start_offset:]
	content.extend(existing_rows)

	existing_rows = read_costume_anim_rows(base_content)
	existing_names = {name.lower() for _, name in existing_rows}
	row_count = len(existing_rows)
	next_class_id = max((class_id for class_id, _ in existing_rows), default=0) + 1
	appended_count = 0

	for alias in aliases:
		if alias.lower() in existing_names:
			continue

		encoded = encode_ies_string(alias)
		content.extend(next_class_id.to_bytes(4, "little"))
		content.extend(len(encoded).to_bytes(2, "little"))
		content.extend(encoded)
		content.extend(struct.pack("<f", float(next_class_id)))
		content.extend(len(encoded).to_bytes(2, "little"))
		content.extend(encoded)
		content.append(0)
		existing_names.add(alias.lower())
		next_class_id += 1
		appended_count += 1

	content[row_count_offset:row_count_offset + 2] = (row_count + appended_count).to_bytes(2, "little")
	content[file_size_offset:file_size_offset + 4] = len(content).to_bytes(4, "little")

	return bytes(content)


def create_ipf(output_path, entries, old_version=1, new_version=999998):
	data_blocks = []
	table_entries = []
	offset = 0

	for pack_name, path, content in entries:
		compressed = raw_deflate(content)
		encrypted = ipf_encrypt(compressed)
		data_blocks.append(encrypted)
		table_entries.append({
			"pack": pack_name.encode("utf-8"),
			"path": path.encode("utf-8"),
			"checksum": zlib.crc32(compressed) & 0xFFFFFFFF,
			"compressed_size": len(encrypted),
			"uncompressed_size": len(content),
			"offset": offset,
		})
		offset += len(encrypted)

	table_offset = offset
	table = bytearray()
	for entry in table_entries:
		table += struct.pack(
			"<HIIIIH",
			len(entry["path"]),
			entry["checksum"],
			entry["compressed_size"],
			entry["uncompressed_size"],
			entry["offset"],
			len(entry["pack"]),
		)
		table += entry["pack"]
		table += entry["path"]

	footer = struct.pack(
		"<HIHI4sII",
		len(entries),
		table_offset,
		0,
		0,
		b"PK\x05\x06",
		old_version,
		new_version,
	)

	output_path.parent.mkdir(parents=True, exist_ok=True)
	output_path.write_bytes(b"".join(data_blocks) + bytes(table) + footer)


def main():
	parser = argparse.ArgumentParser(description="Create a local IPF patch that gives kimono costumes the Highlander THS animation.")
	parser.add_argument("--client-root", default="/mnt/c/CloverTOS-Local")
	parser.add_argument("--source-animation-patch", default="128009_001001.ipf")
	parser.add_argument("--costume-override-patch", default=None, help="Patch to use as the base costumeAnimOverride.ies. Defaults to the newest/largest official one.")
	parser.add_argument("--output", default="/mnt/c/CloverTOS-Local/patch/999998_001001.ipf")
	parser.add_argument("--include-base-ths", action="store_true", help="Also override the base THS animations used by every costume.")
	args = parser.parse_args()

	reader = load_ipf_reader()
	client_root = Path(args.client_root)
	source_patch = client_root / "patch" / args.source_animation_patch
	output_path = Path(args.output)
	overrides = []

	# Kimono costumes:
	# 633311 costume_Com_311         Kimono Costume (Male)
	# 633312 costume_Com_312         Kimono Costume (Female)
	# 633329 costume_Com_311_NoTrade Kimono Costume (Male)(Untradable)
	# 633330 costume_Com_312_NoTrade Kimono Costume (Female)(Untradable)
	#
	# The client can resolve costume-specific animations through a few different
	# names depending on the code path: the model suffix, the XAC internal name,
	# or the item ClassName that has UseAnim=YES. Keep all known kimono aliases
	# here without touching the base THS files used by every other costume.
	suffixes_by_gender = {
		"m": (
			"jpn_kimono",
			"jpn_kimono01",
			"warrior_m_jpn_kimono",
			"costume_Com_311",
			"costume_Com_311_NoTrade",
		),
		"f": (
			"jpn_kimono",
			"jpn_kimono01",
			"warrior_f_jpn_kimono",
			"costume_Com_312",
			"costume_Com_312_NoTrade",
		),
	}
	all_suffixes = tuple(dict.fromkeys(suffix for suffixes in suffixes_by_gender.values() for suffix in suffixes))

	costume_override_patch, costume_anim_override, costume_rows = find_best_costume_anim_override(
		reader,
		client_root,
		output_path,
		args.costume_override_patch,
	)
	overrides.append((
		"ies_client.ipf",
		"costumeAnimOverride.ies",
		make_costume_anim_override(costume_anim_override, all_suffixes),
	))
	print(f"Using costumeAnimOverride.ies from {costume_override_patch.name} with {len(costume_rows)} official rows.")

	for gender, suffixes in suffixes_by_gender.items():
		prefix = f"pc/warrior_{gender}/warrior_{gender}_ths"
		for anim in ("astd", "arun", "aturn"):
			for extension in ("xml", "xsm", "xsmtime"):
				source = f"{prefix}_{anim}_Highlander01_3.{extension}"
				try:
					content = reader.extract_ipf_file(source_patch, source)
				except ValueError:
					continue

				for suffix in suffixes:
					target = f"{prefix}_{anim}_{suffix}.{extension}"
					overrides.append(("animation.ipf", target, content))

				if args.include_base_ths:
					target = f"{prefix}_{anim}.{extension}"
					overrides.append(("animation.ipf", target, content))

	create_ipf(output_path, overrides)
	print(f"Wrote {args.output} with {len(overrides)} kimono animation aliases.")


if __name__ == "__main__":
	main()
