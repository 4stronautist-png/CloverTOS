#!/usr/bin/env python3
import argparse
import re
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


def ipf_decrypt(data):
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
		if index % 2 == 0:
			value ^= magic_byte()
			update_keys(value)
		result.append(value)

	return bytes(result)


def extract_ipf_file(ipf_path, target_name):
	with ipf_path.open("rb") as file:
		file.seek(-24, 2)
		file_count, table_offset, _, _, signature, _, new_version = struct.unpack("<HIHI4sII", file.read(24))
		if signature != b"PK\x05\x06":
			raise ValueError(f"{ipf_path} is not a supported IPF archive")

		file.seek(table_offset)
		for _ in range(file_count):
			path_len, _, compressed_size, _, data_offset, pack_len = struct.unpack("<HIIIIH", file.read(20))
			file.read(pack_len)
			path = file.read(path_len).decode("utf-8", errors="replace")
			if path != target_name:
				continue

			file.seek(data_offset)
			data = file.read(compressed_size)
			if new_version > 11000 or new_version == 0:
				data = ipf_decrypt(data)
			return zlib.decompress(data, -15)

	raise ValueError(f"{target_name} was not found in {ipf_path}")


def decode_ies_string(data):
	return bytes(value ^ 1 for value in data).decode("utf-8")


def load_server_items(items_path):
	pattern = re.compile(r'itemId: (\d+), className: "([^"]+)", name: "([^"]*)"')
	items = {}

	for line in items_path.read_text(encoding="utf-8", errors="replace").splitlines():
		match = pattern.search(line)
		if not match:
			continue

		item_id = int(match.group(1))
		items[item_id] = (match.group(2), match.group(3))

	return items


def load_existing_itemmonsters(path):
	pattern = re.compile(r'\{ itemId: (\d+), monsterId: (\d+), className: "([^"]*)", name: "([^"]*)" \},')
	entries = []

	if not path.exists():
		return entries

	for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
		match = pattern.search(line)
		if not match:
			continue

		entries.append((
			int(match.group(1)),
			int(match.group(2)),
			match.group(3),
			match.group(4),
		))

	return entries


def extract_client_item_order(item_ies, server_items):
	# The Item IES header stores the row offset at 0x84. In each row,
	# ClassID is immediately followed by a length-prefixed XOR(1) ClassName.
	row_offset = struct.unpack_from("<I", item_ies, 0x84)[0]
	rows = []
	seen = set()

	for position in range(row_offset, len(item_ies) - 8):
		item_id = int.from_bytes(item_ies[position:position + 4], "little")
		if item_id not in server_items or item_id in seen:
			continue

		class_name_len = int.from_bytes(item_ies[position + 4:position + 6], "little")
		if class_name_len < 1 or class_name_len > 96:
			continue

		end = position + 6 + class_name_len
		if end > len(item_ies):
			continue

		try:
			class_name = decode_ies_string(item_ies[position + 6:end])
		except UnicodeDecodeError:
			continue

		if class_name != server_items[item_id][0]:
			continue

		seen.add(item_id)
		rows.append((item_id, class_name, server_items[item_id][1]))

	return rows


def write_itemmonsters(path, entries, source_count):
	lines = [
		"// Melia",
		"// Database file",
		"//---------------------------------------------------------------------------",
		"// Synced with the local client's data/ies.ipf Item.ies where possible.",
		"// The client resolves known item-drop sprites as monsterId = 800000 + Item.ies row index.",
		f"// Client-synced entries: {source_count}",
		"",
		"[",
	]

	for item_id, monster_id, class_name, name in entries:
		escaped_class_name = class_name.replace("\\", "\\\\").replace('"', '\\"')
		escaped_name = name.replace("\\", "\\\\").replace('"', '\\"')
		lines.append(f'{{ itemId: {item_id}, monsterId: {monster_id}, className: "{escaped_class_name}", name: "{escaped_name}" }},')

	lines.append("]")
	lines.append("")
	path.write_text("\n".join(lines), encoding="utf-8")


def main():
	parser = argparse.ArgumentParser(description="Regenerate itemmonsters.txt from the local ToS client Item.ies order.")
	parser.add_argument("--client-root", default="/mnt/c/CloverTOS-Local", help="Path to the local CloverTOS client root.")
	parser.add_argument("--items", default="system/db/items.txt", help="Path to the server items.txt file.")
	parser.add_argument("--output", default="system/db/itemmonsters.txt", help="Path to write itemmonsters.txt.")
	parser.add_argument("--fallback", default="system/db/itemmonsters.txt", help="Existing itemmonsters.txt to keep entries for client-unknown items.")
	args = parser.parse_args()

	client_root = Path(args.client_root)
	items_path = Path(args.items)
	output_path = Path(args.output)
	fallback_path = Path(args.fallback)

	server_items = load_server_items(items_path)
	item_ies = extract_ipf_file(client_root / "data" / "ies.ipf", "item.ies")
	rows = extract_client_item_order(item_ies, server_items)

	if not rows:
		raise RuntimeError("No client item rows matched the server item database.")

	client_entries = {}
	for index, (item_id, class_name, name) in enumerate(rows):
		client_entries[item_id] = (item_id, 800000 + index, class_name, name)

	entries_by_item_id = {}
	for item_id, monster_id, class_name, name in load_existing_itemmonsters(fallback_path):
		entries_by_item_id[item_id] = (item_id, monster_id, class_name, name)

	entries_by_item_id.update(client_entries)
	entries = [entries_by_item_id[item_id] for item_id in sorted(entries_by_item_id)]

	write_itemmonsters(output_path, entries, len(client_entries))
	print(f"Wrote {len(entries)} entries to {output_path} ({len(client_entries)} synced from client)")

	for item_id in (900011, 645024, 900013, 511103):
		entry = entries_by_item_id.get(item_id)
		if entry:
			source = "client" if item_id in client_entries else "fallback"
			print(f"{item_id}: monsterId={entry[1]}, className={entry[2]}, name={entry[3]} ({source})")


if __name__ == "__main__":
	main()
