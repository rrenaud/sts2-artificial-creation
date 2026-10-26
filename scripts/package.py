#!/usr/bin/env python3
"""Package the mod and the pinned BaseLib runtime from the restored NuGet package."""
import json
import shutil
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
assets = json.loads((root / 'obj/project.assets.json').read_text())
package = next(Path(p) / 'alchyr.sts2.baselib/3.4.7' for p in assets['packageFolders']
               if (Path(p) / 'alchyr.sts2.baselib/3.4.7').exists())
out = root / 'dist'
mod = out / 'ArtificialCreation'
base = out / 'BaseLib'
mod.mkdir(parents=True, exist_ok=True)
base.mkdir(parents=True, exist_ok=True)
for src in [root / 'bin/Release/net9.0/ArtificialCreation.dll', root / 'ArtificialCreation.json']:
    shutil.copy2(src, mod / src.name)
for src in [package / 'lib/net9.0/BaseLib.dll', package / 'Content/BaseLib.json', package / 'Content/BaseLib.pck']:
    shutil.copy2(src, base / src.name)
shutil.copy2(root / 'THIRD_PARTY_NOTICES.md', base / 'THIRD_PARTY_NOTICES.md')
shutil.copy2(root / 'README.md', out / 'README.md')
shutil.copy2(root / 'THIRD_PARTY_NOTICES.md', out / 'THIRD_PARTY_NOTICES.md')
archive = out / 'ArtificialCreation-0.1.0.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
    for folder in [mod, base]:
        for path in sorted(folder.iterdir()):
            z.write(path, path.relative_to(out))
    z.write(out / 'README.md', 'README.md')
    z.write(out / 'THIRD_PARTY_NOTICES.md', 'THIRD_PARTY_NOTICES.md')
print(archive)
