using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;
namespace STB.Studio.Services;

internal sealed class NativeMeshData
{
    public int Id { get; init; }
    public bool Skinned { get; init; }
    public string Texture { get; init; } = "";
    public Point3D[] Positions { get; init; } = Array.Empty<Point3D>();
    public Point[] Uvs { get; init; } = Array.Empty<Point>();
    public int[] Indices { get; init; } = Array.Empty<int>();
}

internal sealed class NativeMeshService
{
    private readonly string _exe = Path.Combine(AppContext.BaseDirectory,"Tools","stb-mesh.exe");
    private async Task RunAsync(params string[] args)
    {
        if(!File.Exists(_exe))throw new FileNotFoundException("Le moteur YFT interactif est absent",_exe);
        using var p=new Process();
        p.StartInfo=new ProcessStartInfo(_exe){UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardOutput=true,RedirectStandardError=true,StandardErrorEncoding=Encoding.UTF8};
        foreach(string a in args)p.StartInfo.ArgumentList.Add(a);
        if(!p.Start())throw new IOException("Echec du demarrage moteur YFT");
        Task<string> stderr=p.StandardError.ReadToEndAsync();Task<string> stdout=p.StandardOutput.ReadToEndAsync();
        using var timeout=new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(3));
        try{await p.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException)
        {if(!p.HasExited)p.Kill(entireProcessTree:true);throw new TimeoutException("Limite de 3 minutes depassee");}
        string text=(await stderr+"\n"+await stdout).Trim();
        if(p.ExitCode!=0)throw new InvalidDataException(text.Length>1500?text[^1500..]:text);
    }
    public async Task<IReadOnlyList<NativeMeshData>> ReadAsync(string yft)
    {
        RageNativeService.ValidateResource(yft,".yft");
        string temp=Path.Combine(Path.GetTempPath(),"STBStudioMesh",Guid.NewGuid().ToString("N")+".stbmesh");
        Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
        try{
            await RunAsync("scene",yft,temp);
            using var f=File.OpenRead(temp);using var r=new BinaryReader(f,Encoding.UTF8);
            if(Encoding.ASCII.GetString(r.ReadBytes(8))!="STBMESH2")throw new InvalidDataException("Scene 3D invalide");
            int count=checked((int)r.ReadUInt32());if(count<1||count>10000)throw new InvalidDataException("Nombre de geometries incorrect");
            var results=new List<NativeMeshData>();int total=0;
            for(int g=0;g<count;g++){
                int id=checked((int)r.ReadUInt32());bool skinned=r.ReadByte()!=0;
                string tex=Encoding.UTF8.GetString(r.ReadBytes(r.ReadByte()));
                int nv=checked((int)r.ReadUInt32()),ni=checked((int)r.ReadUInt32());total+=nv;
                if(nv<0||ni<0||total>750000||ni>3000000)throw new InvalidDataException("Maillage trop grand");
                var pos=new Point3D[nv];var uv=new Point[nv];
                for(int v=0;v<nv;v++){
                    float x=r.ReadSingle(),y=r.ReadSingle(),z=r.ReadSingle();
                    float u=r.ReadSingle(),t=r.ReadSingle();
                    if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(z))throw new InvalidDataException("Sommet non fini");
                    pos[v]=new Point3D(x,z,-y);uv[v]=new Point(u,1-t);
                }
                var ix=new int[ni];for(int i=0;i<ni;i++){
                    ix[i]=r.ReadUInt16();if(ix[i]>=nv)throw new InvalidDataException("Triangle invalide");}
                results.Add(new NativeMeshData{Id=id,Skinned=skinned,Texture=tex,Positions=pos,Uvs=uv,Indices=ix});
            }
            if(f.Position!=f.Length)throw new InvalidDataException("Fin de scene incorrecte");return results;
        }finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public async Task WriteAsync(string source,string target,IReadOnlyDictionary<(int Group,int Vertex),Vector3D> changes)
    {
        if(changes.Count==0){File.Copy(source,target,false);return;}
        if(changes.Count>10000)throw new InvalidDataException("Trop de sommets edites");
        string file=Path.Combine(Path.GetTempPath(),"STBStudioMesh",Guid.NewGuid().ToString("N")+".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try{
            await File.WriteAllLinesAsync(file,changes.OrderBy(x=>x.Key.Group).ThenBy(x=>x.Key.Vertex)
                .Select(x=>FormattableString.Invariant($"g:{x.Key.Group} {x.Key.Vertex} {x.Value.X:R} {-x.Value.Z:R} {x.Value.Y:R}")));
            await RunAsync("vertices",source,target,file);RageNativeService.ValidateResource(target,".yft");
        }finally{if(File.Exists(file))File.Delete(file);}
    }
}
