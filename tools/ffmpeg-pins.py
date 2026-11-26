#!/usr/bin/env python3
"""Creates or checks the pinned ffmpeg downloads (src/VideoThumbnailer.Core/Ffmpeg/ffmpeg-pins.json).

The app installs exactly the files listed there and compares their SHA-256 with the hashes stored in this
repository, not with a checksum file from the download server. This script looks up the newest release builds,
downloads every file, hashes it itself and compares the result with the publisher's checksum. On macOS it also
reads the Apple Team ID of the code signature of the macOS binaries.

  python3 tools/ffmpeg-pins.py generate OUT.json   # newest builds -> OUT.json
  python3 tools/ffmpeg-pins.py verify PINS.json    # download and check every pinned file

Only uses the Python standard library. GITHUB_TOKEN (optional) avoids the GitHub API rate limit.
"""
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
import urllib.request
import zipfile

UA = "Video-Thumbnailer-pins/1"
GYAN_REPO = "GyanD/codexffmpeg"
RIEDL = "https://ffmpeg.martin-riedl.de"
# app runtime id -> (martin-riedl os, arch)
RIEDL_TARGETS = {
    "linux-x64": ("linux", "amd64"),
    "linux-arm64": ("linux", "arm64"),
    "osx-x64": ("macos", "amd64"),
    "osx-arm64": ("macos", "arm64"),
}


def request(url, accept=None):
    headers = {"User-Agent": UA}
    if accept:
        headers["Accept"] = accept
    if url.startswith("https://api.github.com/") and os.environ.get("GITHUB_TOKEN"):
        headers["Authorization"] = "Bearer " + os.environ["GITHUB_TOKEN"]
    return urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=120)


def get_text(url):
    with request(url) as r:
        return r.read().decode("utf-8")


def resolve(url):
    with request(url) as r:
        return r.geturl()


def download(url, path):
    h = hashlib.sha256()
    with request(url) as r, open(path, "wb") as f:
        while chunk := r.read(1 << 20):
            h.update(chunk)
            f.write(chunk)
    return h.hexdigest()


def team_id(zip_path, workdir):
    """Apple Team ID of every Mach-O binary in the ZIP (macOS runners only), or None elsewhere."""
    if sys.platform != "darwin":
        return None
    out = os.path.join(workdir, "x-" + os.path.basename(zip_path))
    with zipfile.ZipFile(zip_path) as z:
        z.extractall(out)
    ids = set()
    for root, _, files in os.walk(out):
        for name in files:
            p = os.path.join(root, name)
            os.chmod(p, 0o755)
            subprocess.run(["codesign", "--verify", "--strict", p], check=True)
            info = subprocess.run(["codesign", "-dv", p], capture_output=True, text=True).stderr
            m = re.search(r"^TeamIdentifier=(\S+)$", info, re.M)
            if not m or m.group(1) == "not":
                sys.exit(f"{p}: no Team ID in code signature")
            ids.add(m.group(1))
    if len(ids) != 1:
        sys.exit(f"{zip_path}: unexpected Team IDs {ids}")
    return ids.pop()


def gyan(workdir):
    rel = json.load(request(f"https://api.github.com/repos/{GYAN_REPO}/releases/latest", "application/vnd.github+json"))
    version = rel["tag_name"]
    name = f"ffmpeg-{version}-full_build-shared.zip"
    asset = next((a for a in rel["assets"] if a["name"] == name), None)
    if asset is None:
        sys.exit(f"{GYAN_REPO} {version}: {name} not found")
    url = asset["browser_download_url"]
    published = (asset.get("digest") or "").removeprefix("sha256:")
    sha = download(url, os.path.join(workdir, name))
    if published and published != sha:
        sys.exit(f"{url}: SHA-256 {sha} does not match GitHub's digest {published}")
    return {
        "source": f"gyan.dev ({GYAN_REPO})",
        "homepage": "https://www.gyan.dev/ffmpeg/builds/",
        "version": version,
        "assets": [{"fileName": name, "url": url, "sha256": sha, "kind": "Zip",
                    "binaries": ["ffmpeg", "ffprobe"], "libraries": [".dll"]}],
    }


def riedl(rid, workdir):
    os_name, arch = RIEDL_TARGETS[rid]
    assets, versions, teams = [], set(), set()
    for tool in ("ffmpeg", "ffprobe"):
        url = resolve(f"{RIEDL}/redirect/latest/{os_name}/{arch}/release/{tool}.zip")
        m = re.search(r"/download/[^/]+/[^/]+/\d+_(?P<v>[^/]+)/[^/]+\.zip$", url)
        if not m:
            sys.exit(f"unexpected download URL {url}")
        versions.add(m.group("v"))
        published = get_text(url + ".sha256").split()[0].lower()
        path = os.path.join(workdir, f"{rid}-{tool}.zip")
        sha = download(url, path)
        if sha != published:
            sys.exit(f"{url}: SHA-256 {sha} does not match {published}")
        if os_name == "macos":
            teams.add(team_id(path, workdir))
        assets.append({"fileName": f"{tool}.zip", "url": url, "sha256": sha, "kind": "Zip", "binaries": [tool]})
    if len(versions) != 1:
        sys.exit(f"{rid}: ffmpeg and ffprobe have different versions {versions}")
    pin = {
        "source": f"ffmpeg.martin-riedl.de ({os_name} {arch})",
        "homepage": RIEDL,
        "version": versions.pop(),
        "assets": assets,
    }
    if os_name == "macos":
        if len(teams) != 1 or None in teams:
            if sys.platform == "darwin":
                sys.exit(f"{rid}: unexpected Team IDs {teams}")
            sys.exit("generate must run on macOS to read the Team ID of the macOS builds")
        pin["macTeamId"] = teams.pop()
    return pin


def generate(out):
    with tempfile.TemporaryDirectory() as work:
        platforms = {"win-x64": gyan(work)}
        for rid in RIEDL_TARGETS:
            platforms[rid] = riedl(rid, work)
    pins = {
        "comment": "Generated by tools/ffmpeg-pins.py. The app only installs these files and checks them against "
                   "these SHA-256 hashes. win-arm64 has no pinned build (falls back to BtbN 'latest').",
        "generated": datetime.date.today().isoformat(),
        "platforms": platforms,
    }
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(pins, f, indent=2)
        f.write("\n")
    print(json.dumps(pins, indent=2))


def verify(path):
    with open(path, encoding="utf-8") as f:
        pins = json.load(f)
    failed = False
    with tempfile.TemporaryDirectory() as work:
        for rid, pin in pins["platforms"].items():
            for a in pin["assets"]:
                file = os.path.join(work, f"{rid}-{a['fileName']}")
                sha = download(a["url"], file)
                ok = sha == a["sha256"]
                if ok and pin.get("macTeamId") and sys.platform == "darwin":
                    ok = team_id(file, work) == pin["macTeamId"]
                print(f"{'OK  ' if ok else 'FAIL'} {rid} {pin['version']} {a['url']}")
                failed |= not ok
                os.remove(file)
    if failed:
        sys.exit(1)


if __name__ == "__main__":
    if len(sys.argv) != 3 or sys.argv[1] not in ("generate", "verify"):
        sys.exit(__doc__)
    (generate if sys.argv[1] == "generate" else verify)(sys.argv[2])
