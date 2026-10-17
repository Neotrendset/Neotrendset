#!/usr/bin/env python3
"""Rebuild pinned native dependencies on Apple Silicon; requires clang and CMake."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess,tarfile,urllib.request
root=Path(__file__).resolve().parents[2]
port=root/'port';scratch=port/'native-build';scratch.mkdir(exist_ok=True)
native=port/'native/osx-arm64';native.mkdir(parents=True,exist_ok=True)
cellar=scratch/'cellar';cellar.mkdir(exist_ok=True)
def run(*args):subprocess.run([str(x) for x in args],check=True)
def unpack(archive,target):
 with tarfile.open(archive) as package:
  # These archives are version pinned and checked before use.
  for m in package.getmembers():
   if Path(m.name).is_absolute() or '..' in Path(m.name).parts:raise ValueError(m.name)
  package.extractall(target)
for item in json.loads((port/'native-provenance.json').read_text()):
 name=item['name'];bottle=item['bottle'];archive=scratch/(name+'.tar.gz')
 if not archive.exists():
  scope=f'repository:homebrew/core/{name}:pull'
  token=json.load(urllib.request.urlopen('https://ghcr.io/token?service=ghcr.io&scope='+scope))['token']
  request=urllib.request.Request(bottle['url'],headers={'Authorization':'Bearer '+token,'User-Agent':'Homebrew'})
  archive.write_bytes(urllib.request.urlopen(request).read())
 if hashlib.sha256(archive.read_bytes()).hexdigest()!=bottle['sha256']:raise ValueError('Bottle digest mismatch: '+name)
 unpack(archive,cellar)
for name in ['libchdr','xdelta']:
 archive=port/'native-source'/('libchdr.tar.gz' if name=='libchdr' else 'xdelta-source.tar.gz');target=scratch/name;target.mkdir(exist_ok=True);unpack(archive,target)
chdr=next((scratch/'libchdr').iterdir());chdrbuild=scratch/'libchdr-build'
run('cmake','-S',chdr,'-B',chdrbuild,'-DCMAKE_BUILD_TYPE=Release','-DCMAKE_OSX_DEPLOYMENT_TARGET=12.0','-DCMAKE_POLICY_VERSION_MINIMUM=3.5')
run('cmake','--build',chdrbuild,'-j4')
for old in native.iterdir():
 if old.is_file():old.chmod(0o755)
shutil.copy2(next(chdrbuild.glob('libchdr.*.dylib')),native/'libchdr.dylib')
xz=next((cellar/'xz').iterdir());xdelta=next((scratch/'xdelta').iterdir())/'xdelta3'
run('clang','-O2','-fPIC','-dynamiclib','-DXD3_ENCODER=1','-DSECONDARY_LZMA=1','-DSECONDARY_DJW=1','-DXD3_USE_LARGEFILE64=1','-DXD3_STDIO=1','-DSIZEOF_SIZE_T=8','-DSIZEOF_USIZE_T=8','-DSIZEOF_XOFF_T=8','-I'+str(xdelta),'-I'+str(xz/'include'),port/'native-source/xdelta3_wrapper.c',xdelta/'xdelta3.c','-L'+str(xz/'lib'),'-llzma','-o',native/'libxdelta3.dylib')
shutil.copy2(next(cellar.glob('rom-tools/*/bin/chdman')),native/'chdman')
for name in ['sdl3','flac','libogg','utf8proc','zstd','xz']:
 for lib in (next((cellar/name).iterdir())/'lib').glob('*.dylib'):shutil.copy2(lib.resolve(),native/lib.name)
packages=Path(os.environ.get('NUGET_PACKAGES',str(Path.home()/'.nuget/packages')))
shutil.copy2(packages/'libdeflate.native/1.19.0/runtimes/osx.11.0/native/libdeflate.dylib',native/'libdeflate.dylib')
run('clang','-O2','-dynamiclib','-mmacosx-version-min=15.0','-install_name','@rpath/libromforge.dylib',port/'native-source/romforge_native.c','-o',native/'libromforge.dylib')
for binary in native.iterdir():
 if not binary.is_file():continue
 for line in subprocess.check_output(['otool','-L',str(binary)],text=True).splitlines()[1:]:
  if not line.startswith('\t'):continue
  name=line.strip().split(' (')[0]
  if name.startswith(('/usr/lib/','/System/Library/')) or name=='@rpath/'+binary.name:continue
  target=native/Path(name).name
  if not target.exists() and Path(name).name.startswith('libchdr'):target=native/'libchdr.dylib'
  if not target.exists():raise FileNotFoundError(name)
  run('install_name_tool','-change',name,'@loader_path/'+target.name,binary)
 if binary.suffix=='.dylib':run('install_name_tool','-id','@rpath/'+binary.name,binary)
 run('codesign','--force','--sign','-',binary)
print('Native dependencies rebuilt. Run check-native.py and tests before publishing.')
