#!/usr/bin/env python3
import subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[1]/'native/osx-arm64'
count=0
for path in sorted(root.iterdir()):
 if not path.is_file():continue
 output=subprocess.check_output(['otool','-L',str(path)],text=True).splitlines()[1:]
 for index,line in enumerate(output):
  if not line.startswith('\t'):continue
  name=line.strip().split(' (')[0]
  if path.suffix=='.dylib' and name=='@rpath/'+path.name:continue
  if name.startswith(('/usr/lib/','/System/Library/')):continue
  if not name.startswith('@loader_path/') or not (path.parent/name.removeprefix('@loader_path/')).exists():raise SystemExit(f'Unresolved: {path.name}: {name}')
 subprocess.run(['codesign','--verify',str(path)],check=True,capture_output=True)
 count+=1
print(f'PASS {count} native binaries: signatures and relative dependencies')
