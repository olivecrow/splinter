param(
    [Parameter(Mandatory)][string[]]$Roots,
    [Parameter(Mandatory)][string]$Output,
    [string]$BaselineInventory,
    [string[]]$ExcludeRoots = @()
)
$ErrorActionPreference='Stop'
$Output=[IO.Path]::GetFullPath($Output)
$rowsPath=$Output+'.files.jsonl'
if([IO.File]::Exists($Output) -or [IO.File]::Exists($rowsPath)){throw 'Preserve existing allocation evidence.'}
if(-not ('ScalAllocation' -as [type])) {
Add-Type -TypeDefinition @'
using System; using System.IO; using System.Runtime.InteropServices; using Microsoft.Win32.SafeHandles;
public static class ScalAllocation {
 [StructLayout(LayoutKind.Sequential)] struct Standard { public long Allocation,Length; public uint Links; public byte DeletePending,Directory; }
 [StructLayout(LayoutKind.Sequential)] struct Info { public uint Attributes,CL,CH,AL,AH,WL,WH,Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow; }
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFileW(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandleEx(SafeFileHandle h,int kind,out Standard info,uint size);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle h,out Info info);
 public static object Read(string path) {
  string full=Path.GetFullPath(path); string native=full.StartsWith(@"\\?\",StringComparison.Ordinal)?full:@"\\?\"+full;
  using(var file=CreateFileW(native,0x80,7,IntPtr.Zero,3,0x00200000,IntPtr.Zero)) {
   if(file.IsInvalid || !GetFileInformationByHandleEx(file,1,out var standard,(uint)Marshal.SizeOf<Standard>()) || !GetFileInformationByHandle(file,out var info))
    throw new IOException("Allocation identity unavailable: "+Marshal.GetLastWin32Error());
   if((info.Attributes & (0x400u|0x10u))!=0)throw new IOException("Do not follow reparse or directory allocation paths.");
   return new {path=full,identity=$"{info.Volume:x8}:{info.IndexHigh:x8}{info.IndexLow:x8}",allocated=standard.Allocation,logical=standard.Length,links=standard.Links};
  }
 }
}
'@
}
$before=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
if($BaselineInventory) {
 foreach($line in [IO.File]::ReadLines([IO.Path]::GetFullPath($BaselineInventory))) {
  $row=$line|ConvertFrom-Json
  if($row.identity){[void]$before.Add([string]$row.identity)}
 }
}
$excluded=[Collections.Generic.List[string]]::new()
foreach($path in $ExcludeRoots){$excluded.Add([IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($path)))}
function Excluded([string]$Path){
 foreach($root in $excluded){if($Path.Equals($root,[StringComparison]::OrdinalIgnoreCase) -or $Path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){return $true}}
 return $false
}
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$errors=[Collections.Generic.List[object]]::new()
$paths=0L;$unknown=0L;$skipped=0L;$allocated=0L;$logical=0L;$newAllocated=0L;$newLogical=0L;$prior=0L
$watch=[Diagnostics.Stopwatch]::StartNew()
$outFile=[IO.FileStream]::new($rowsPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
$writer=[IO.StreamWriter]::new($outFile,[Text.UTF8Encoding]::new($false),65536,$true)
try {
 foreach($rootPath in $Roots) {
  $root=[IO.Path]::GetFullPath($rootPath)
  if(-not [IO.Directory]::Exists($root)){continue}
  $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($root)
  while($pending.Count -gt 0) {
   $directory=$pending.Pop()
   if(Excluded $directory){$skipped++;continue}
   if(([IO.File]::GetAttributes($directory) -band [IO.FileAttributes]::ReparsePoint) -ne 0){$skipped++;continue}
   foreach($path in [IO.Directory]::EnumerateFileSystemEntries($directory)) {
    if(Excluded $path){$skipped++;continue}
    $attributes=[IO.File]::GetAttributes($path)
    if(($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){$skipped++;continue}
    if(($attributes -band [IO.FileAttributes]::Directory) -ne 0){$pending.Push($path);continue}
    try {
     $entry=[ScalAllocation]::Read($path);$paths++
     $writer.WriteLine(($entry|ConvertTo-Json -Compress))
     if($seen.Add($entry.identity)){
      $allocated+=$entry.allocated;$logical+=$entry.logical
      if($before.Contains($entry.identity)){$prior++}else{$newAllocated+=$entry.allocated;$newLogical+=$entry.logical}
     }
    } catch {
     $unknown++;if($errors.Count -lt 16){$errors.Add(@{path=$path;error=$_.Exception.Message})}
     $writer.WriteLine((@{path=$path;error=$_.Exception.GetType().FullName}|ConvertTo-Json -Compress))
    }
   }
  }
 }
 $writer.Flush();$outFile.Flush($true)
} finally {$writer.Dispose();$outFile.Dispose()}
$watch.Stop()
$summary=@{
 version=1;observedAt=[DateTimeOffset]::UtcNow.ToString('O');roots=$Roots;excludeRoots=$ExcludeRoots;baselineInventory=$BaselineInventory
 observedPaths=$paths;uniqueFiles=$seen.Count;uniqueAllocatedBytes=$allocated;uniqueLogicalBytes=$logical
 baselineMatchedIds=$prior;newUniqueAllocatedBytes=$newAllocated;newUniqueLogicalBytes=$newLogical
 unknownPaths=$unknown;skippedRootsOrReparsePoints=$skipped;firstErrors=$errors.ToArray();inventory=$rowsPath;measurementSeconds=$watch.Elapsed.TotalSeconds
 scope='Non-atomic native NTFS file allocation by unique volume/file identity; hard-link aliases counted once. Existing moved seed data can be excluded with its baseline identity inventory. Directory/MFT/system-cache allocation is outside this file-allocation scope. HashSets belong to this separate measurement host, outside product timings and memory samples.'
}
[IO.File]::WriteAllText($Output,($summary|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
$summary|ConvertTo-Json -Depth 4
