using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using _3DS.Core.Models;
using _3DS.Core.Services;
using CHD.Core.Interop;
using CHD.Core.Services;
using PSP.Core.Services;
using RomForge.Core.Services.Switch;
using RomForge.Portable;
using WiiGC.Core.Services;
using WiiU.Core.Services;

static class EngineChecks
{
 [DllImport("/usr/lib/libSystem.B.dylib",EntryPoint="pipe")]static extern int Pipe([Out]int[] fds);
 [DllImport("/usr/lib/libSystem.B.dylib",EntryPoint="write")]static extern nint Write(int fd,byte[] bytes,nuint count);
 [DllImport("/usr/lib/libSystem.B.dylib",EntryPoint="close")]static extern int Close(int fd);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Available(int fd,ref uint size);
 public static void Run()
 {
  string directory=Path.Combine(Common.PortablePaths.DataDirectory,"fixtures");Directory.CreateDirectory(directory);
  int count=0;
  void Test(string name,Action action){action();Console.WriteLine("PASS "+name);count++;}
  string FileOf(string name)=>Path.Combine(directory,name);
  byte[] source=Enumerable.Range(0,512*1024).Select(i=>(byte)(i*17+i/97)).ToArray();
  Test("Darwin arm64 ioctl bridge uses correct variadic ABI on synthetic pipe",()=>
  {
   int[] fds=new int[2];Check(Pipe(fds)==0);
   try
   {
    Check(Write(fds[1],new byte[]{1,2,3},3)==3);var lib=NativeLibrary.Load(Path.Combine(Common.NativeTools.DirectoryPath,"libromforge.dylib"));
    var available=Marshal.GetDelegateForFunctionPointer<Available>(NativeLibrary.GetExport(lib,"romforge_bytes_available"));uint size=0;Check(available(fds[0],ref size)==0&&size==3);
   }finally{Close(fds[0]);Close(fds[1]);}
  });
  Test("IPS patch uses upstream UniversalPatcher",()=>
  {
   byte[] patch="PATCH"u8.ToArray().Concat(new byte[]{0,0,2,0,3,9,8,7}).Concat("EOF"u8.ToArray()).ToArray();
   byte[] actual=Patch.Core.UniversalPatcher.ApplyPatchAsync(source,patch).GetAwaiter().GetResult();
   var expected=source.ToArray();expected[2]=9;expected[3]=8;expected[4]=7;Check(actual.SequenceEqual(expected));
  });
  Test("native xdelta3 encode/decode roundtrip",()=>
  {
   byte[] modified=source.ToArray();modified[200]=99;modified[2048]=33;
   IntPtr library=NativeLibrary.Load(Path.Combine(Common.NativeTools.DirectoryPath,"libxdelta3.dylib"));
   var encode=Marshal.GetDelegateForFunctionPointer<Encode>(NativeLibrary.GetExport(library,"xd3_encode_memory"));
   byte[] patch=new byte[source.Length*2+4096];nuint written=0;
   Check(encode(modified,(nuint)modified.Length,source,(nuint)source.Length,patch,ref written,(nuint)patch.Length,0)==0);
   byte[] actual=Patch.Core.Formats.Xdelta3.ApplyPatch(source,patch.AsSpan(0,(int)written).ToArray());Check(actual.SequenceEqual(modified));
  });
  Test("native BLAKE3 standard empty digest",()=>
  {
   Check(Blake3.Hasher.Hash(Array.Empty<byte>()).ToString().ToLowerInvariant()=="af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262");
  });
  Test("7z compatibility reader extracts ZIP entries and matches bytes",()=>
  {
   string path=FileOf("input.zip");using(var zip=ZipFile.Open(path,ZipArchiveMode.Create)){using var output=zip.CreateEntry("folder/data.bin").Open();output.Write(source);}
   using var reader=new SevenZip.SevenZipExtractor(path);using var bytes=new MemoryStream();reader.ExtractFile(0,bytes);Check(bytes.ToArray().SequenceEqual(source));
   reader.ExtractArchive(FileOf("unzipped"));Check(System.IO.File.ReadAllBytes(FileOf("unzipped/folder/data.bin")).SequenceEqual(source));
  });
  Test("encrypted solid 7z reads multiple entries with managed decoder",()=>
  {
   string path=Path.Combine(AppContext.BaseDirectory,"Fixtures","encrypted-solid.7z");
   using var reader=new SevenZip.SevenZipExtractor(path,"fixture-password");
   foreach(var entry in reader.ArchiveFileData.Where(x=>!x.IsDirectory))
   {
    using var output=new MemoryStream();reader.ExtractFile((int)entry.Index,output);
    byte[] expected=entry.FileName.EndsWith("alpha.txt")?System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("RomForge synthetic archive fixture\n",1000))):Enumerable.Range(0,25600).Select(x=>(byte)x).ToArray();
    Check(output.ToArray().SequenceEqual(expected));
   }
  });
  Test("PSP ISO → CSO → ISO roundtrip uses native libdeflate",()=>
  {
   using var input=new MemoryStream(source);using var compressed=new MemoryStream();CsoService.CompressAsync(input,compressed).GetAwaiter().GetResult();compressed.Position=0;
   using var output=new MemoryStream();CsoService.DecompressAsync(compressed,output).GetAwaiter().GetResult();Check(output.ToArray().SequenceEqual(source));
  });
  Test("PSP ISO → ZSO → ISO LZ4 roundtrip",()=>
  {
   using var input=new MemoryStream(source);using var compressed=new MemoryStream();CsoService.CompressAsync(input,compressed,"ZISO"u8.ToArray(),1,true).GetAwaiter().GetResult();compressed.Position=0;
   using var output=new MemoryStream();CsoService.DecompressAsync(compressed,output).GetAwaiter().GetResult();Check(output.ToArray().SequenceEqual(source));
  });
  Test("3DS ExeFS pack/unpack validates stored SHA-256",()=>
  {
   var files=new[]{new ExeFsFile{Name=".code",Data=source[..8192],ExpectedHash=Array.Empty<byte>()},new ExeFsFile{Name="icon",Data=source[..1024],ExpectedHash=Array.Empty<byte>()}};
   byte[] packed=ExeFsPacker.Pack(files);using var stream=new MemoryStream(new byte[0x200].Concat(packed).ToArray());
   var result=ExeFsUnpacker.UnpackAsync(stream,new NcchHeader{ExefsOffset=1,ExefsSize=(uint)(packed.Length/512)}).GetAwaiter().GetResult();
   Check(result.Files.Count==2&&result.Files.All(x=>x.HashValid)&&result.Files[0].Data.SequenceEqual(files[0].Data));
  });
  Test("3DS RomFS folder pack/unpack preserves nested UTF-8 names and data",()=>
  {
   string folder=FileOf("romfs-input");Directory.CreateDirectory(Path.Combine(folder,"한국어"));System.IO.File.WriteAllBytes(Path.Combine(folder,"한국어/data.bin"),source);
   using var packed=new MemoryStream();RomFsPacker.PackFromFolderAsync(folder,packed).GetAwaiter().GetResult();
   using var ncch=new MemoryStream(new byte[512].Concat(packed.ToArray()).ToArray());
   var result=RomFsUnpacker.UnpackAsync(ncch,new NcchHeader{RomfsOffset=1,RomfsSize=(uint)((packed.Length+511)/512)}).GetAwaiter().GetResult();
   string output=FileOf("romfs-output");RomFsUnpacker.SaveToDirectoryAsync(ncch,result,output).GetAwaiter().GetResult();
   Check(System.IO.File.ReadAllBytes(Path.Combine(output,"한국어/data.bin")).SequenceEqual(source));
  });
  Test("3DS NCSD → ZCCI → NCSD roundtrip",()=>
  {
   byte[] bytes=new byte[0x4000+source.Length];source.CopyTo(bytes,0x4000);"NCSD"u8.CopyTo(bytes.AsSpan(0x100));
   "NCCH"u8.CopyTo(bytes.AsSpan(0x4100));bytes[0x418f]=4;BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x104), (uint)(bytes.Length/512));
   BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x120),0x20);BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x124),(uint)(source.Length/512));
   string path=FileOf("card.cci");System.IO.File.WriteAllBytes(path,bytes);
   string compressed=Z3dsCompressor.CompressAsync(path,3).GetAwaiter().GetResult();
   string output=FileOf("zcci-output");Directory.CreateDirectory(output);Z3dsDecompressor.DecompressAsync(compressed,outputDir:output).GetAwaiter().GetResult();
   Check(System.IO.File.ReadAllBytes(Directory.GetFiles(output).Single()).SequenceEqual(bytes));
  });
  Test("GameCube ISO → RVZ → ISO roundtrip",()=>
  {
   byte[] bytes=source.ToArray();"RFTE01"u8.CopyTo(bytes);BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0x1c),0xc2339f3d);
   string iso=FileOf("gamecube.iso"),rvz=FileOf("gamecube.rvz"),restored=FileOf("gamecube-restored.iso");System.IO.File.WriteAllBytes(iso,bytes);
   IsoToRvzConverter.Convert(iso,rvz,3);RvzToIsoConverter.Convert(rvz,restored);Check(System.IO.File.ReadAllBytes(restored).SequenceEqual(bytes));
  });
  Test("CHD native creation, libchdr read and extraction roundtrip",()=>
  {
   string iso=FileOf("raw.iso"),chd=FileOf("raw.chd"),output=FileOf("raw-restored.iso");System.IO.File.WriteAllBytes(iso,source);
   using var service=new ChdmanService();Check(service.CreateDvdAsync(iso,chd,"zlib").GetAwaiter().GetResult());
   using var reader=new LibChdrWrapper();Check(reader.Open(chd)==CHD.Core.Interop.Enums.ChdrError.CHDERR_NONE);
   Check(reader.ReadHunk(0).AsSpan().SequenceEqual(source.AsSpan(0,reader.ReadHunk(0).Length)));
   Check(service.ExtractRawAsync(chd,output).GetAwaiter().GetResult());Check(System.IO.File.ReadAllBytes(output).SequenceEqual(source));
  });
  Test("Switch PFS0 builder writes table and payload with correct alignment",()=>
  {
   using var output=new MemoryStream();
   var entries=new[]{("payload.bin",new Func<Stream,Action<long>,Task>(async(s,progress)=>{await s.WriteAsync(source);progress(source.Length);}), (long)source.Length,"payload")};
   Pfs0Builder.WriteAsync("Synthetic","0000000000000000",entries,output).GetAwaiter().GetResult();
   byte[] bytes=output.ToArray();Check(bytes.AsSpan(0,4).SequenceEqual("PFS0"u8));
   int strings=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8));int start=16+24+strings;Check(bytes.AsSpan(start,source.Length).SequenceEqual(source));
  });
  Test("Wii U WUA archive write/read preserves payload",()=>
  {
   string path=FileOf("synthetic.wua");using(var output=System.IO.File.Create(path))using(var writer=new WuaWriter(output))
   {Check(writer.MakeDir("content",true));Check(writer.StartNewFile("content/data.bin"));writer.AppendData(source);writer.FinalizeArchive();}
   using var reader=WuaReader.OpenFromFile(path);uint node=reader.LookUp("content/data.bin");using var outputData=new MemoryStream();reader.ExtractFileTo(node,outputData);Check(outputData.ToArray().SequenceEqual(source));
  });
  Test("PS/Vita PARAM.SFO metadata interoperability",()=>
  {
   var builder=new PBP.Core.Services.SfoBuilder();builder.AddEntry("TITLE","Synthetic title");builder.AddEntry("DISC_ID","SLUS00000");var sfo=builder.Build();
   using var output=new MemoryStream();PBP.Core.Services.StreamExtensions.WriteSFO(output,sfo);
   var data=Vita.Core.Services.VitaSfoParser.Parse(output.ToArray());Check(Vita.Core.Services.VitaSfoParser.GetString(data,"TITLE")=="Synthetic title");
  });
  Test("PBP pack/unpack uses recovered official base resources",()=>
  {
   byte[] raw=new byte[2352*32];for(int i=0;i<raw.Length;i++)raw[i]=(byte)(i%17);
   using var input=new MemoryStream(raw);var disc=new PBP.Core.Services.DiscWriteInfo(input,raw.Length,"SLUS00000","Synthetic",PBP.Core.Services.TocBuilder.BuildSingleTrackToc((uint)raw.Length));
   string output=FileOf("synthetic.pbp");PBP.Core.Services.PbpPackager.WritePbpAsync(new[]{disc},"SLUS00000","Synthetic",output,5,null).GetAwaiter().GetResult();
   new PBP.Core.Services.PbpUnpacker().UnpackAsync(output,FileOf("pbp-unpacked"),false).GetAwaiter().GetResult();
   Check(System.IO.File.ReadAllBytes(Directory.GetFiles(FileOf("pbp-unpacked"),"*.bin").Single()).SequenceEqual(raw));
  });
  Test("Portable raster image PNG roundtrip and resize",()=>
  {
   using var raster=Raster.FromBgra(new byte[]{0,0,255,255,0,255,0,255},2,1);using var stream=new MemoryStream();raster.SavePng(stream);stream.Position=0;using var decoded=Raster.Load(stream);decoded.Resize(4,2);Check(decoded.Width==4&&decoded.Height==2);
  });
  Console.WriteLine($"PASS {count} engine checks");
 }
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Encode(byte[] input,nuint inputSize,byte[] source,nuint sourceSize,[Out]byte[] output,ref nuint outputSize,nuint outputCapacity,int flags);
 static void Check(bool value){if(!value)throw new Exception("Engine assertion failed");}
}
