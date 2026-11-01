using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using STB.Studio.Services;
namespace STB.Studio;

public partial class Studio3DEditorWindow
{
    private sealed class LivePiece {public required NativeMeshData Data{get;init;}public required MeshGeometry3D Mesh{get;init;}}
    private sealed class DeltaEntry {public int Group{get;set;}public int Vertex{get;set;}public double X{get;set;}public double Y{get;set;}public double Z{get;set;}}
    private sealed class Session {public string Model{get;set;}="";public string SourceSha256{get;set;}="";public List<DeltaEntry> Edits{get;set;}=new();}
    private readonly NativeMeshService _nativeMeshService=new();
    private readonly Dictionary<(int Group,int Vertex),Vector3D> _meshEdits=new();
    private readonly HashSet<(int Group,int Vertex)> _meshSelected=new();
    private readonly Dictionary<GeometryModel3D,LivePiece> _meshModels=new();
    private readonly List<LivePiece> _meshPieces=new();
    private readonly Stack<Dictionary<(int,int),Vector3D>> _undo=new(),_redo=new();
    private GeometryModel3D? _marker;private bool _meshLive,_meshDragging,_meshControlsReady;
    private ComboBox _editMode=new ComboBox();private TextBlock _selectionInfo=new TextBlock();

    private void SetupMeshControls()
    {
        if(_meshControlsReady)return;
        if(NativeMeshSection.Child is not StackPanel parent)throw new InvalidOperationException("Panneau YFT introuvable");
        var stack=new StackPanel();
        stack.Children.Add(new TextBlock{Text="ÉDITEUR 3D INTERACTIF",Foreground=Brushes.Turquoise,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,7)});
        stack.Children.Add(new TextBlock{Text="Clic gauche : sélectionner • clic droit : déplacer X/Y • Maj + clic droit : profondeur Z • molette : zoom",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightSteelBlue,FontSize=11});
        _editMode=new ComboBox{Margin=new Thickness(0,9,0,7),Height=30};
        _editMode.Items.Add("Sommets (V)");_editMode.Items.Add("Arêtes (E)");_editMode.Items.Add("Faces (F)");_editMode.SelectedIndex=0;stack.Children.Add(_editMode);
        _selectionInfo=new TextBlock{Text="Clique une partie du véhicule",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Orange,Margin=new Thickness(0,5,0,10)};
        stack.Children.Add(_selectionInfo);
        var buttons=new WrapPanel();
        void Button(string label,RoutedEventHandler handler){var b=new Button{Content=label,Margin=new Thickness(0,0,6,6),Padding=new Thickness(8,4,8,4)};b.Click+=handler;buttons.Children.Add(b);}
        Button("↶ Annuler",(s,e)=>MeshUndo());Button("↷ Refaire",(s,e)=>MeshRedo());
        Button("Réinitialiser",(s,e)=>MeshReset());Button("Sauver session",(s,e)=>MeshSave());Button("Charger session",(s,e)=>MeshLoad());
        stack.Children.Add(buttons);
        stack.Children.Add(new TextBlock{Text="LOD0 rigide uniquement. Les collisions, os, UV et ELS ne sont pas édités.",TextWrapping=TextWrapping.Wrap,FontSize=10,Foreground=Brushes.Wheat});
        var box=new Border{Background=new SolidColorBrush(Color.FromRgb(24,43,68)),BorderBrush=new SolidColorBrush(Color.FromRgb(53,94,131)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(11),Padding=new Thickness(10),Margin=new Thickness(0,0,0,13),Child=stack};
        parent.Children.Insert(0,box);
        KeyDown+=MeshKeyDown;_meshControlsReady=true;
    }

    private async Task LoadNativeMeshPreviewAsync(string yft,IReadOnlyList<string> textures)
    {
        _meshLive=false;_meshPieces.Clear();_meshModels.Clear();_meshSelected.Clear();_meshEdits.Clear();_undo.Clear();_redo.Clear();_marker=null;
        var models=await _nativeMeshService.ReadAsync(yft);_modelGroup.Children.Clear();
        foreach(var src in models){
            if(src.Indices.Length==0)continue;
            var mesh=new MeshGeometry3D();foreach(var p in src.Positions)mesh.Positions.Add(p);
            foreach(var p in src.Uvs)mesh.TextureCoordinates.Add(p);foreach(var index in src.Indices)mesh.TriangleIndices.Add(index);
            string? png=textures.FirstOrDefault(f=>string.Equals(Path.GetFileNameWithoutExtension(f),src.Texture,StringComparison.OrdinalIgnoreCase));
            Material material;
            if(png!=null){var brush=new ImageBrush(LoadBitmap(png)){Stretch=Stretch.Fill};brush.Freeze();material=new DiffuseMaterial(brush);}
            else material=new DiffuseMaterial(new SolidColorBrush(Color.FromRgb((byte)(118+src.Id%4*9),(byte)(148+src.Id%4*8),(byte)(191+src.Id%4*5))));
            var gm=new GeometryModel3D(mesh,material){BackMaterial=material};var piece=new LivePiece{Data=src,Mesh=mesh};
            _meshPieces.Add(piece);_meshModels[gm]=piece;_modelGroup.Children.Add(gm);
        }
        if(_meshPieces.Count==0)throw new InvalidDataException("Aucun triangle dans le YFT");
        SetupMeshControls();_modelVisual.Transform=Transform3D.Identity;_meshLive=true;SetNativeMode(true);ResetView();
        _selectionInfo.Text=$"{_meshPieces.Count} géométries affichées. Clique pour sélectionner.";
    }

    private void NativeMeshMouseDown(MouseButtonEventArgs e)
    {
        if(e.ChangedButton==MouseButton.Left&&e.ClickCount>1){ResetView();return;}
        if(e.ChangedButton==MouseButton.Left)SelectMesh(e.GetPosition(ModelViewport));
        if(e.ChangedButton!=MouseButton.Left&&e.ChangedButton!=MouseButton.Right&&e.ChangedButton!=MouseButton.Middle)return;
        _dragButton=e.ChangedButton;_lastMouse=e.GetPosition(ModelViewport);_meshDragging=false;ViewInput.CaptureMouse();
    }
    private void SelectMesh(Point point)
    {
        RayMeshGeometry3DHitTestResult? hit=null;
        VisualTreeHelper.HitTest(ModelViewport,null,result=>{
            if(result is RayMeshGeometry3DHitTestResult h&&h.ModelHit is GeometryModel3D model&&_meshModels.ContainsKey(model)){
                hit=h;return HitTestResultBehavior.Stop;}return HitTestResultBehavior.Continue;},new PointHitTestParameters(point));
        if(hit==null||hit.ModelHit is not GeometryModel3D m||!_meshModels.TryGetValue(m,out var part))return;
        if(part.Data.Skinned){_selectionInfo.Text="Géométrie skinnée protégée (os)";return;}
        int[] indices={hit.VertexIndex1,hit.VertexIndex2,hit.VertexIndex3};
        double[] weights={hit.VertexWeight1,hit.VertexWeight2,hit.VertexWeight3};
        var order=Enumerable.Range(0,3).OrderByDescending(i=>weights[i]).ToArray();_meshSelected.Clear();
        for(int i=0;i<Math.Clamp(_editMode.SelectedIndex+1,1,3);i++){
            int v=indices[order[i]];if(v>=0&&v<part.Data.Positions.Length)_meshSelected.Add((part.Data.Id,v));}
        UpdateMarker();
    }
    private void NativeMeshMouseMove(MouseEventArgs e)
    {
        if(_dragButton==null)return;Point p=e.GetPosition(ModelViewport);Vector delta=p-_lastMouse;_lastMouse=p;
        if(_dragButton==MouseButton.Right&&_meshSelected.Count>0){
            if(!_meshDragging){SaveUndo();_meshDragging=true;}
            double d=Math.Max(.001,_radius*.0007);
            Vector3D move=Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                ?new Vector3D(delta.X*d,0,delta.Y*d):new Vector3D(delta.X*d,-delta.Y*d,0);
            foreach(var key in _meshSelected){_meshEdits.TryGetValue(key,out var old);_meshEdits[key]=old+move;}
            ApplySelected();return;
        }
        if(_dragButton==MouseButton.Left){_yaw-=delta.X*.009;_pitch=Math.Clamp(_pitch+delta.Y*.009,-1.4,1.4);}
        else{
            Vector3D forward=_camera.LookDirection;forward.Normalize();
            Vector3D right=Vector3D.CrossProduct(forward,_camera.UpDirection);right.Normalize();
            Vector3D up=Vector3D.CrossProduct(right,forward);
            _lookAt-=right*(delta.X*_radius*.0025)+up*(delta.Y*_radius*.0025);
        }UpdateCamera();
    }
    private void ApplySelected(){foreach(var (group,index) in _meshSelected){
        var part=_meshPieces.FirstOrDefault(p=>p.Data.Id==group);if(part==null||index>=part.Mesh.Positions.Count)continue;
        _meshEdits.TryGetValue((group,index),out var v);part.Mesh.Positions[index]=part.Data.Positions[index]+v;}
        UpdateMarker();}
    private void ApplyAll(){foreach(var p in _meshPieces)for(int i=0;i<p.Data.Positions.Length;i++){
        _meshEdits.TryGetValue((p.Data.Id,i),out var d);p.Mesh.Positions[i]=p.Data.Positions[i]+d;}
        UpdateMarker();}
    private void UpdateMarker(){
        if(_marker!=null)_modelGroup.Children.Remove(_marker);_marker=null;
        if(_meshSelected.Count==0){_selectionInfo.Text="Aucun élément sélectionné";return;}
        var pts=new List<Point3D>();foreach(var (group,index) in _meshSelected){
            var part=_meshPieces.FirstOrDefault(p=>p.Data.Id==group);if(part!=null&&index<part.Mesh.Positions.Count)pts.Add(part.Mesh.Positions[index]);}
        if(pts.Count==0)return;
        _selectionInfo.Text=$"{pts.Count} sommet(s) sélectionné(s), {_meshEdits.Count} modifiés";
        double x=pts.Average(p=>p.X),y=pts.Average(p=>p.Y),z=pts.Average(p=>p.Z),r=Math.Clamp(_radius*.006,.02,.11);
        var mat=new DiffuseMaterial(Brushes.Orange);
        _marker=new GeometryModel3D(CreateBox(new Point3D(x-r,y-r,z-r),new Point3D(x+r,y+r,z+r)),mat){BackMaterial=mat};
        _modelGroup.Children.Add(_marker);
    }
    private void SaveUndo(){_undo.Push(new Dictionary<(int,int),Vector3D>(_meshEdits));_redo.Clear();}
    private void Restore(Dictionary<(int,int),Vector3D> edits){_meshEdits.Clear();foreach(var v in edits)_meshEdits[v.Key]=v.Value;ApplyAll();}
    private void MeshUndo(){if(_undo.Count==0)return;_redo.Push(new Dictionary<(int,int),Vector3D>(_meshEdits));Restore(_undo.Pop());}
    private void MeshRedo(){if(_redo.Count==0)return;_undo.Push(new Dictionary<(int,int),Vector3D>(_meshEdits));Restore(_redo.Pop());}
    private void MeshReset(){if(_meshSelected.Count==0)return;SaveUndo();foreach(var key in _meshSelected)_meshEdits.Remove(key);ApplySelected();}
    private void MeshKeyDown(object? sender,KeyEventArgs e){if(!_meshLive||e.OriginalSource is TextBox)return;
        if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){if(e.Key==Key.Z){MeshUndo();e.Handled=true;}
            else if(e.Key==Key.Y){MeshRedo();e.Handled=true;}return;}
        int mode=e.Key==Key.V?0:e.Key==Key.E?1:e.Key==Key.F?2:-1;
        if(mode>=0){_editMode.SelectedIndex=mode;e.Handled=true;}}
    private static string Sha(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    private void MeshSave(){if(!_meshLive||_nativeYft==null)return;
        var d=new SaveFileDialog{Title="Enregistrer STB 3D",Filter="Session STB (*.stbedits.json)|*.stbedits.json",
            FileName=Path.GetFileNameWithoutExtension(_nativeYft)+".stbedits.json"};if(d.ShowDialog(this)!=true)return;
        try{var s=new Session{Model=Path.GetFileName(_nativeYft),SourceSha256=Sha(_nativeYft),
            Edits=_meshEdits.Select(v=>new DeltaEntry{Group=v.Key.Group,Vertex=v.Key.Vertex,X=v.Value.X,Y=v.Value.Y,Z=v.Value.Z}).ToList()};
            File.WriteAllText(d.FileName,JsonSerializer.Serialize(s,new JsonSerializerOptions{WriteIndented=true}));
            EditorStatus.Text="Session enregistrée : "+Path.GetFileName(d.FileName);}
        catch(Exception ex){ShowError("Sauvegarde impossible",ex);}}
    private void MeshLoad(){if(!_meshLive||_nativeYft==null)return;
        var d=new OpenFileDialog{Title="Charger STB 3D",Filter="Session STB (*.stbedits.json)|*.stbedits.json"};
        if(d.ShowDialog(this)!=true)return;
        try{if(new FileInfo(d.FileName).Length>2000000)throw new InvalidDataException("Session trop grande");
            var s=JsonSerializer.Deserialize<Session>(File.ReadAllText(d.FileName))??throw new InvalidDataException("Session vide");
            if(!string.Equals(s.Model,Path.GetFileName(_nativeYft),StringComparison.OrdinalIgnoreCase)||s.SourceSha256!=Sha(_nativeYft))
                throw new InvalidDataException("Cette session ne correspond pas au véhicule");
            if(s.Edits.Count>10000)throw new InvalidDataException("Trop de modifications");
            var edits=new Dictionary<(int,int),Vector3D>();foreach(var v in s.Edits){
                var p=_meshPieces.FirstOrDefault(q=>q.Data.Id==v.Group);
                if(p==null||p.Data.Skinned||v.Vertex<0||v.Vertex>=p.Data.Positions.Length||
                   !double.IsFinite(v.X)||!double.IsFinite(v.Y)||!double.IsFinite(v.Z)||
                   Math.Abs(v.X)>10||Math.Abs(v.Y)>10||Math.Abs(v.Z)>10)throw new InvalidDataException("Sommet invalide");
                edits[(v.Group,v.Vertex)]=new Vector3D(v.X,v.Y,v.Z);
            }SaveUndo();Restore(edits);EditorStatus.Text=$"Session rechargée : {edits.Count} sommets.";}
        catch(Exception ex){ShowError("Chargement impossible",ex);}}
}
