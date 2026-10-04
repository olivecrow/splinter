[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateCount(1,128)][int[]]$ProcessIds,
    [Parameter(Mandatory)][string]$OutputPath,
    [ValidateRange(1,12)][int]$Samples=3,
    [ValidateRange(0,10000)][int]$IntervalMilliseconds=1000
)
$ErrorActionPreference='Stop'
if(-not $IsWindows-or -not [Environment]::Is64BitProcess){throw 'A 64-bit Windows PowerShell process is required.'}
if(@($ProcessIds|Where-Object {$_-le 0}).Count-or @($ProcessIds|Sort-Object -Unique).Count-ne $ProcessIds.Count){throw 'Use distinct positive process IDs.'}
$output=[IO.Path]::GetFullPath($OutputPath)
if(Test-Path -LiteralPath $output){throw 'Preserve existing measurement evidence.'}
if(-not [IO.Directory]::Exists([IO.Path]::GetDirectoryName($output))){throw 'The output parent must already exist.'}

# Queries address metadata and residency; never reads target payload or trims RAM.
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Splinter.MemoryMeasurement {
 public sealed class Result {
  public long StartedUtcTicks { get; set; }
  public ulong PrivateBytes { get; set; }
  public ulong WorkingSetBytes { get; set; }
  public ulong PeakWorkingSetBytes { get; set; }
  public ulong QueriedVirtualPages { get; set; }
  public ulong ResidentVirtualPageBytes { get; set; }
  public ulong NonShareableResidentBytes { get; set; }
  public ulong ShareableResidentVirtualPageBytes { get; set; }
  public double PssEstimateBytes { get; set; }
  public ulong CappedShareCountPages { get; set; }
  public ulong UnknownShareCountPages { get; set; }
  public long ElapsedMilliseconds { get; set; }
 }
 public static class Native {
  [StructLayout(LayoutKind.Sequential)] struct Region {
   public UIntPtr BaseAddress, AllocationBase; public uint AllocationProtect; public ushort PartitionId;
   public UIntPtr RegionSize; public uint State, Protect, Type;
  }
  [StructLayout(LayoutKind.Sequential)] struct SystemInfo {
   public ushort Architecture, Reserved; public uint PageSize; public UIntPtr Minimum, Maximum, ProcessorMask;
   public uint Processors, ProcessorType, AllocationGranularity; public ushort ProcessorLevel, ProcessorRevision;
  }
  [StructLayout(LayoutKind.Sequential)] struct Counters {
   public uint Size, PageFaultCount; public UIntPtr PeakWorkingSet, WorkingSet, PeakPagedPool, PagedPool,
    PeakNonPagedPool, NonPagedPool, Pagefile, PeakPagefile, Private;
  }
  [DllImport("kernel32.dll", SetLastError=true)] static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetExitCodeProcess(SafeProcessHandle process, out uint exit);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool IsWow64Process(SafeProcessHandle process, out bool wow64);
  [DllImport("kernel32.dll")] static extern void GetSystemInfo(out SystemInfo info);
  [DllImport("kernel32.dll", SetLastError=true)] static extern UIntPtr VirtualQueryEx(SafeProcessHandle process, UIntPtr address, out Region info, UIntPtr size);
  [DllImport("psapi.dll", SetLastError=true)] static extern bool QueryWorkingSetEx(SafeProcessHandle process, IntPtr info, uint bytes);
  [DllImport("psapi.dll", SetLastError=true)] static extern bool GetProcessMemoryInfo(SafeProcessHandle process, ref Counters info, uint size);
  static void Check(bool success) { if(!success) throw new Win32Exception(Marshal.GetLastWin32Error()); }
  static long Start(SafeProcessHandle process) {
   long created, exited, kernel, user; Check(GetProcessTimes(process,out created,out exited,out kernel,out user));
   uint exitCode; Check(GetExitCodeProcess(process,out exitCode));
   if(exitCode!=259) throw new InvalidOperationException("The target process exited during measurement.");
   return DateTime.FromFileTimeUtc(created).Ticks;
  }
  public static Result Measure(int pid, long expectedTicks) {
   var watch=Stopwatch.StartNew();
   using(var process=OpenProcess(0x0400,false,pid)) {
    if(process.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
    long started=Start(process);
    if(started!=expectedTicks) throw new InvalidOperationException("Process identity changed; do not measure a reused PID.");
    SystemInfo system; GetSystemInfo(out system);
    bool wow64; Check(IsWow64Process(process,out wow64));
    ulong address=system.Minimum.ToUInt64(), maximum=system.Maximum.ToUInt64();
    if(wow64) maximum=Math.Min(maximum,0xffffffffUL);
    const int capacity=4096, stride=16;
    var result=new Result { StartedUtcTicks=started };
    IntPtr buffer=Marshal.AllocHGlobal(capacity*stride);
    try {
     while(address<maximum) {
      Region region;
      if(VirtualQueryEx(process,new UIntPtr(address),out region,new UIntPtr((uint)Marshal.SizeOf<Region>()))==UIntPtr.Zero)
       throw new Win32Exception(Marshal.GetLastWin32Error());
      ulong end=checked(region.BaseAddress.ToUInt64()+region.RegionSize.ToUInt64());
      if(end<=address) throw new InvalidOperationException("Address enumeration did not advance.");
      if(region.State==0x1000) {
       for(ulong at=address;at<end;) {
        int count=(int)Math.Min((ulong)capacity,(end-at)/system.PageSize);
        if(count<=0) throw new InvalidOperationException("Unexpected unaligned committed region.");
        if(result.QueriedVirtualPages+(ulong)count>10000000UL || watch.Elapsed.TotalSeconds>30)
         throw new InvalidOperationException("The bounded measurement budget was exceeded; attribution is unknown.");
        for(int i=0;i<count;i++) {
         Marshal.WriteInt64(buffer,i*stride,unchecked((long)(at+(ulong)i*system.PageSize)));
         Marshal.WriteInt64(buffer,i*stride+8,0);
        }
        Check(QueryWorkingSetEx(process,buffer,(uint)(count*stride)));
        for(int i=0;i<count;i++) {
         ulong flags=unchecked((ulong)Marshal.ReadInt64(buffer,i*stride+8));
         if((flags&1)==0) continue;
         result.ResidentVirtualPageBytes+=system.PageSize;
         bool shareable=(flags&(1UL<<15))!=0;
         int shares=(int)((flags>>1)&7);
         if(shareable) {
          result.ShareableResidentVirtualPageBytes+=system.PageSize;
          if(shares==7) result.CappedShareCountPages++;
          if(shares==0) result.UnknownShareCountPages++;
         } else result.NonShareableResidentBytes+=system.PageSize;
         result.PssEstimateBytes+=(double)system.PageSize/(shareable ? Math.Max(1,shares) : 1);
        }
        result.QueriedVirtualPages+=(ulong)count; at+=(ulong)count*system.PageSize;
       }
      }
      address=end;
     }
     var counters=new Counters { Size=(uint)Marshal.SizeOf<Counters>() };
     Check(GetProcessMemoryInfo(process,ref counters,counters.Size));
     result.PrivateBytes=counters.Private.ToUInt64(); result.WorkingSetBytes=counters.WorkingSet.ToUInt64();
     result.PeakWorkingSetBytes=counters.PeakWorkingSet.ToUInt64();
     if(Start(process)!=expectedTicks) throw new InvalidOperationException("The target identity changed during measurement.");
     result.ElapsedMilliseconds=watch.ElapsedMilliseconds;
     return result;
    } finally { Marshal.FreeHGlobal(buffer); }
   }
  }
 }
}
'@
$owners=@{}
foreach($processId in $ProcessIds) {
 try {
  $targetProcess=Get-Process -Id $processId -ErrorAction Stop
  $owners[$processId]=@{pid=$processId;name=$targetProcess.ProcessName;startedUtcTicks=$targetProcess.StartTime.ToUniversalTime().Ticks.ToString()}
 } catch { $owners[$processId]=@{pid=$processId;error=$_.Exception.Message} }
}
$observations=@()
for($sample=0;$sample-lt $Samples;$sample++) {
 $entries=@()
 foreach($processId in $ProcessIds) {
  $owner=$owners[$processId]
  try {
   if($owner.error){throw $owner.error}
   $measurement=[Splinter.MemoryMeasurement.Native]::Measure($processId,[long]$owner.startedUtcTicks)
   $entries+=@{identity=$owner;status='complete';metrics=$measurement}
  } catch { $entries+=@{identity=$owner;status='unavailable';metrics=$null;error=$_.Exception.Message} }
 }
 $observations+=@{atUtc=[DateTime]::UtcNow.ToString('o');sample=$sample;processes=$entries}
 if($sample+1-lt $Samples-and $IntervalMilliseconds-gt 0){Start-Sleep -Milliseconds $IntervalMilliseconds}
}
$value=@{version=1;observations=$observations;totalPhysicalPeak='unknown';limitations=@(
 'Live page metadata samples are not an atomic physical-memory snapshot.',
 'Private Bytes are committed private address-space cost, not resident RAM.',
 'PSS is an estimate from virtual-page share counts; counts saturate at 7 and zero counts use 1.',
 'Shareable is not a claim that this page is shared specifically between the selected Editors.',
 'Virtual-address aliases, AWE and concurrent page changes limit physical attribution.',
 'Failed/expired/budget-exceeded queries are unavailable, never zero.'
)}
$bytes=[Text.UTF8Encoding]::new($false).GetBytes(($value|ConvertTo-Json -Depth 25))
$file=[IO.FileStream]::new($output,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
try{$file.Write($bytes);$file.Flush($true)}finally{$file.Dispose()}
@{output=$output;samples=$Samples;processCount=$ProcessIds.Count;unavailable=@($observations.processes|Where-Object {$_.status-ne 'complete'}).Count}|ConvertTo-Json -Compress
