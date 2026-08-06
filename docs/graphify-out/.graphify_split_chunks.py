import json
from pathlib import Path
from itertools import groupby

files = [l for l in Path('graphify-out/.graphify_uncached.txt').read_text(encoding='utf-8').splitlines() if l]

def dirkey(f):
    return str(Path(f).parent)

files_sorted = sorted(files, key=dirkey)

CHUNK_SIZE = 22
chunks = []
current = []
for f in files_sorted:
    current.append(f)
    if len(current) >= CHUNK_SIZE:
        chunks.append(current)
        current = []
if current:
    chunks.append(current)

root = Path('.').resolve()
out = []
for i, chunk in enumerate(chunks, 1):
    chunk_path = str(root / 'graphify-out' / f'.graphify_chunk_{i:02d}.json')
    out.append({'chunk_num': i, 'total_chunks': len(chunks), 'chunk_path': chunk_path, 'files': chunk})

Path('graphify-out/.graphify_chunks_plan.json').write_text(json.dumps(out, indent=2, ensure_ascii=False), encoding='utf-8')
print(f'{len(chunks)} chunks planned')
for c in out:
    print(f"  chunk {c['chunk_num']}: {len(c['files'])} files")
