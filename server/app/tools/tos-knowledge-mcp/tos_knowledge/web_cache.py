from __future__ import annotations

import hashlib
import json
import time
from pathlib import Path
from urllib.parse import urlparse
from urllib.request import Request, urlopen

from tos_knowledge.paths import data_root, source_registry_path


DEFAULT_ALLOWED_DOMAINS = {
    "home.treeofsavior.net",
    "treeofsavior.com",
    "www.tosbase.com",
    "tosbase.com",
    "tos.guru",
    "www.tos.guru",
    "treeofsavior.fandom.com",
    "www.reddit.com",
    "old.reddit.com",
    "github.com",
    "raw.githubusercontent.com",
    "gist.githubusercontent.com",
}


def allowed_domains(app_root: Path) -> set[str]:
    domains = set(DEFAULT_ALLOWED_DOMAINS)
    path = source_registry_path(app_root)
    if path.exists():
        for source in json.loads(path.read_text(encoding="utf-8", errors="replace")):
            url = source.get("url")
            if url:
                host = urlparse(url).netloc.lower()
                if host:
                    domains.add(host)
    return domains


def cache_url(app_root: Path, url: str) -> dict[str, str | int]:
    parsed = urlparse(url)
    host = parsed.netloc.lower()
    if parsed.scheme not in {"http", "https"}:
        raise ValueError("Only http/https URLs are supported")
    if host not in allowed_domains(app_root):
        raise ValueError(f"Domain is not allowlisted for TOS knowledge cache: {host}")

    cache_dir = data_root(app_root) / "web_cache"
    cache_dir.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256(url.encode("utf-8")).hexdigest()[:24]
    body_path = cache_dir / f"{digest}.html"
    meta_path = cache_dir / f"{digest}.json"

    request = Request(url, headers={"User-Agent": "CloverTOS-TosKnowledgeMCP/0.1"})
    with urlopen(request, timeout=30) as response:
        body = response.read()
        content_type = response.headers.get("Content-Type", "")

    body_path.write_bytes(body)
    meta = {
        "url": url,
        "host": host,
        "captured_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "content_type": content_type,
        "bytes": len(body),
        "body_path": str(body_path),
    }
    meta_path.write_text(json.dumps(meta, indent=2, sort_keys=True), encoding="utf-8")
    return meta
