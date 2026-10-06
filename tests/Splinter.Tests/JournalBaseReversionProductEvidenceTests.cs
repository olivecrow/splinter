using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Cli;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalBaseReversionProductEvidenceTests
{
    private sealed record Input(string Home, string A, string B, string Output, string[] Files);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {WriteIndented=true};
    [Fact]
    public void RecordedProductResponsesMatchCurrentSchema()
    {
        string? directory=Environment.GetEnvironmentVariable("SPLINTER_BASE_REVERSION_RESPONSES");if(directory is null)return;
        var catalog=new ContractCatalog();int checkedResponses=0;
        foreach(string path in Directory.EnumerateFiles(directory,"*.json"))
        {
            using var document=JsonDocument.Parse(File.ReadAllText(path));
            if(!document.RootElement.TryGetProperty("schemaVersion",out _))continue;
            Assert.True(catalog.Validate("urn:splinter:v1:response",document.RootElement).IsValid,path);
            checkedResponses++;
        }
        Assert.True(checkedResponses>=10);
    }
    [Fact]
    public async Task OwnedWinFspPeerAndIndependentResults()
    {
        string? config=Environment.GetEnvironmentVariable("SPLINTER_BASE_REVERSION_PRODUCT");if(config is null)return;
        var input=JsonSerializer.Deserialize<Input>(File.ReadAllText(config),Json)!;
        Assert.False(Directory.Exists(input.Output));Directory.CreateDirectory(input.Output);
        var store=new WorkspaceStore(input.Home);var a=store.Get(input.A);var b=store.Get(input.B);
        Assert.Equal("jr1006-native-a",a.RequestKey);Assert.Equal("jr1006-native-b",b.RequestKey);
        Assert.Equal("ready",a.Phase);Assert.Equal("ready",b.Phase);Assert.Equal(a.ProjectPath,b.ProjectPath);Assert.Equal(a.BaseRevision,b.BaseRevision);
        var ct=TestContext.Current.CancellationToken;
        async Task<JsonElement> Command(string label,params string[] args)
        {
            // Native lifecycle evidence uses a standalone CLI process, as the
            // documented agent entrypoint does; keep the test host separate.
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
            foreach(string argument in args)start.ArgumentList.Add(argument);
            start.ArgumentList.Add("--home");start.ArgumentList.Add(input.Home);
            long began=Stopwatch.GetTimestamp();
            using var process=Process.Start(start)!;
            var outputTask=process.StandardOutput.ReadToEndAsync(ct);var errorTask=process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);string output=await outputTask;string error=await errorTask;int exit=process.ExitCode;
            File.WriteAllText(Path.Combine(input.Output,label+".json"),output);
            File.WriteAllText(Path.Combine(input.Output,label+".stderr.txt"),error);
            File.WriteAllText(Path.Combine(input.Output,label+".time.json"),JsonSerializer.Serialize(new {startedTicks=began,finishedTicks=Stopwatch.GetTimestamp(),stopwatchFrequency=Stopwatch.Frequency},Json));
            var result=JsonSerializer.Deserialize<JsonElement>(output);
            Assert.True(new ContractCatalog().Validate("urn:splinter:v1:response",result).IsValid,output);
            Assert.Equal(0,exit);Assert.Equal("Succeeded",result.GetProperty("status").GetString());return result;
        }
        await Command("before-a","workspace","status","--id",a.Id);await Command("before-b","workspace","status","--id",b.Id);
        var expected=new Dictionary<string,byte[]>();
        for(int i=0;i<input.Files.Length;i++)
        {
            string path=input.Files[i];byte[] baseline=File.ReadAllBytes(Path.Combine(a.Path,path));expected[path]=baseline;
            int size=i==0?4096:1048576;byte[] original=baseline[..size],first=(byte[])original.Clone(),second=(byte[])original.Clone();
            for(int at=0;at<size;at++){first[at]^=0x55;second[at]^=0xAA;}
            foreach(string root in new[]{a.Path,b.Path})
            {
                using(var file=new FileStream(Path.Combine(root,path),FileMode.Open,FileAccess.ReadWrite,FileShare.Read,1048576))
                {
                    for(int cycle=0;cycle<32;cycle++)foreach(byte[] bytes in new[]{first,second,original}){file.Position=0;file.Write(bytes);file.Flush(true);}
                    if(i==input.Files.Length-1){byte[] mixed=(byte[])original.Clone();first.AsSpan(size/2).CopyTo(mixed.AsSpan(size/2));file.Position=0;file.Write(mixed);file.Flush(true);}
                }
                File.SetLastWriteTimeUtc(Path.Combine(root,path),new DateTime(2026,10,6,0,0,0,DateTimeKind.Utc));
            }
            if(i==input.Files.Length-1)first.AsSpan(size/2).CopyTo(baseline.AsSpan(size/2));
        }
        await Command("written-a","workspace","status","--id",a.Id);await Command("written-b","workspace","status","--id",b.Id);
        var writes=new List<object>();var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var peer=Task.Run(async()=>
        {
            byte[] bytes=new byte[131072];new Random(61006).NextBytes(bytes);
            for(int i=0;i<64;i++)
            {
                long began=Stopwatch.GetTimestamp();string path=Path.Combine(b.Path,$"jr-peer-{i:D2}.bin");
                using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read)){file.Write(bytes);file.Flush(true);}
                Assert.Equal(bytes,File.ReadAllBytes(path));writes.Add(new {i,startedTicks=began,finishedTicks=Stopwatch.GetTimestamp(),sha256=Convert.ToHexStringLower(SHA256.HashData(bytes))});
                if(i==0)ready.TrySetResult();await Task.Delay(100,ct);
            }
        },ct);
        await ready.Task.WaitAsync(ct);long compactStart=Stopwatch.GetTimestamp();
        var compact=await Command("compact-a-on","workspace","compact","--id",a.Id,"--revert-base-ranges","true");long compactEnd=Stopwatch.GetTimestamp();
        await peer;
        await Command("peer-status","workspace","status","--id",b.Id);
        var resumed=await Command("same-b","workspace","ensure","--project",b.ProjectPath,"--key",b.RequestKey,"--source","git","--application","none","--base","development","--storage","git-snapshot");
        Assert.Equal(b.Id,resumed.GetProperty("data").GetProperty("workspace").GetProperty("id").GetString());
        foreach(var (path,bytes)in expected){Assert.Equal(bytes,File.ReadAllBytes(Path.Combine(a.Path,path)));Assert.Equal(bytes,File.ReadAllBytes(Path.Combine(b.Path,path)));}
        var overlap=writes.Select(x=>JsonSerializer.SerializeToElement(x)).Count(x=>x.GetProperty("startedTicks").GetInt64()<compactEnd&&x.GetProperty("finishedTicks").GetInt64()>compactStart);
        Assert.True(overlap>0,"No peer write overlapped actual product compaction.");
        await Command("compact-b-off","workspace","compact","--id",b.Id,"--revert-base-ranges","false");
        await Command("checkpoint-a","workspace","checkpoint","--id",a.Id,"--message","base reversion native A result");
        await Command("release-a","workspace","release","--id",a.Id);
        Assert.Equal(expected[input.Files[0]],File.ReadAllBytes(Path.Combine(b.Path,input.Files[0])));
        await Command("after-a-peer","workspace","status","--id",b.Id);
        await Command("checkpoint-b","workspace","checkpoint","--id",b.Id,"--message","base reversion native B result");
        await Command("release-b","workspace","release","--id",b.Id);
        var endA=store.Get(a.Id);var endB=store.Get(b.Id);
        Assert.Equal("released",endA.Phase);Assert.Equal("released",endB.Phase);Assert.False(Directory.Exists(a.Path));Assert.False(Directory.Exists(b.Path));
        Assert.True(endA.Snapshot!.Result!.IndependentOfSnapshot);Assert.True(endB.Snapshot!.Result!.IndependentOfSnapshot);
        File.WriteAllText(Path.Combine(input.Output,"result.json"),JsonSerializer.Serialize(new {qualification="actual-large-recorded-Git/NTFS/WinFsp/editorless product pair; no Unity import qualification",a=a.Id,b=b.Id,baseRevision=a.BaseRevision,
            compactStart,compactEnd,stopwatchFrequency=Stopwatch.Frequency,overlappingPeerWrites=overlap,peerWrites=writes,independentA=endA.Snapshot.Result,independentB=endB.Snapshot.Result,
            releasedA=endA.ReleasedResources,releasedB=endB.ReleasedResources},Json));
    }
}
