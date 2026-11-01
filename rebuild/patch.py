"""Reconstruct STB Studio Windows UI from public 0.1.10 assembly, then add 0.1.13 tools.

Each replacement is checked; workflow must fail if upstream source changes.
"""
from pathlib import Path
import re
import shutil
import sys

repo = Path(__file__).resolve().parents[1]
src = Path(sys.argv[1]).resolve()
base = Path(sys.argv[2]).resolve()


def load(rel):
    p = src / rel
    return p, p.read_text(encoding="utf-8-sig").replace("\r\n", "\n")


def once(text, old, new, description):
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"Patch mismatch {description}: {count} occurrences")
    return text.replace(old, new)


def save(path, data):
    path.write_text(data, encoding="utf-8")


p, project = load("STB.Studio.csproj")
project = once(project, "<PlatformTarget>x64</PlatformTarget>",
    "<PlatformTarget>x64</PlatformTarget>\n    <StartupObject>STB.Studio.Program</StartupObject>\n    <Version>0.1.13</Version>\n    <Nullable>enable</Nullable>\n    <EnableWindowsTargeting>true</EnableWindowsTargeting>", "project version")
project, n = re.subn(r"<HintPath>[^<]*Velopack\.dll</HintPath>", "<HintPath>Dependencies/Velopack.dll</HintPath>", project)
if n != 1:
    raise RuntimeError(f"Velopack reference mismatch: {n}")
project = once(project, "</Project>", """  <ItemGroup>
    <Content Include="Tools/*" Link="Tools/%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="Always" />
    <Content Include="stb-updates.json" Link="stb-updates.json" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="Always" />
  </ItemGroup>
</Project>""", "content includes")
save(p, project)

p, info = load("Properties/AssemblyInfo.cs")
save(p, info.replace("0.1.10", "0.1.13"))

save(src / "STB/Studio/App.xaml.cs", """using System.Windows;
namespace STB.Studio;
public partial class App : Application { }
""")
save(src / "STB/Studio/Program.cs", """using System;
using Velopack;
namespace STB.Studio;
internal static class Program {
    [STAThread] public static void Main(string[] args) {
        VelopackApp.Build().SetAutoApplyOnStartup(autoApply:false).Run();
        var app = new App(); app.InitializeComponent(); app.Run();
    }
}
""")

p, c = load("STB/Studio/Studio3DEditorWindow.xaml.cs")
replacements = [
    ("ModelViewport.Visibility = (active ? Visibility.Collapsed : Visibility.Visible);",
     "ModelViewport.Visibility = (active && !_meshLive ? Visibility.Collapsed : Visibility.Visible);", "live viewport"),
    ("NativeVehicleImage.Visibility = ((!active) ? Visibility.Collapsed : Visibility.Visible);",
     "NativeVehicleImage.Visibility = (active && !_meshLive ? Visibility.Visible : Visibility.Collapsed);", "native image"),
    ("NativeTurntableBar.Visibility = ((!active) ? Visibility.Collapsed : Visibility.Visible);",
     "NativeTurntableBar.Visibility = (active && !_meshLive ? Visibility.Visible : Visibility.Collapsed);", "turntable"),
    ("AxisHint.Visibility = (active ? Visibility.Collapsed : Visibility.Visible);",
     "AxisHint.Visibility = (active && !_meshLive ? Visibility.Collapsed : Visibility.Visible);", "axis hint"),
    ("ResetCameraButton.Visibility = (active ? Visibility.Collapsed : Visibility.Visible);",
     "ResetCameraButton.Visibility = (active && !_meshLive ? Visibility.Collapsed : Visibility.Visible);", "camera button"),
    ("ModelNameLabel.Text = Path.GetFileName(yft) + \"  +  \" + Path.GetFileName(ytd);\n\t\t\tSetNativeMode(active: true);",
     "ModelNameLabel.Text = Path.GetFileName(yft) + \"  +  \" + Path.GetFileName(ytd);\n\t\t\t_meshLive = false;\n\t\t\tSetNativeMode(active: true);", "reset live mode"),
    ("NativeAngleSlider.Value = 0.0;\n\t\t\tShowNativeFrame();",
     "NativeAngleSlider.Value = 0.0;\n\t\t\tShowNativeFrame();\n\t\t\ttry { await LoadNativeMeshPreviewAsync(yft, textures); }\n\t\t\tcatch (Exception exLive) { EditorStatus.Text = \"Aperçu interactif non disponible : \" + exLive.Message; }", "live import"),
    ("private void ResetView_Click(object sender, RoutedEventArgs e)\n\t{\n\t\tif (_nativeMode)",
     "private void ResetView_Click(object sender, RoutedEventArgs e)\n\t{\n\t\tif (_nativeMode && !_meshLive)", "reset camera"),
    ("private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)\n\t{",
     "private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)\n\t{\n\t\tif (_nativeMode && _meshLive) { NativeMeshMouseDown(e); return; }", "mesh mouse down"),
    ("private void Viewport_MouseMove(object sender, MouseEventArgs e)\n\t{",
     "private void Viewport_MouseMove(object sender, MouseEventArgs e)\n\t{\n\t\tif (_nativeMode && _meshLive) { NativeMeshMouseMove(e); return; }", "mesh mouse move"),
    ("private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)\n\t{",
     "private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)\n\t{\n\t\tif (_nativeMode && _meshLive) { _radius = Math.Clamp(_radius * (e.Delta>0?.86:1.16),.5,100000); UpdateCamera(); e.Handled=true; return; }", "mesh mouse zoom"),
    ("\t\tdouble[] array = CurrentYftValues();\n\t\tawait _yftMeshEditor.TransformAsync(input, output, yftMeshPart.Id",
     "\t\tif (_meshEdits.Count>0 && _nativeYft!=null && string.Equals(Path.GetFullPath(input),Path.GetFullPath(_nativeYft),StringComparison.OrdinalIgnoreCase))\n\t\t{\n\t\t\tstring staged=Path.Combine(Path.GetTempPath(),\"STBStudioMesh\",Guid.NewGuid().ToString(\"N\")+\".yft\");\n\t\t\tawait _nativeMeshService.WriteAsync(input,staged,_meshEdits); input=staged;\n\t\t}\n\t\tdouble[] array = CurrentYftValues();\n\t\tawait _yftMeshEditor.TransformAsync(input, output, yftMeshPart.Id", "stage vertex edits"),
]
for old, new, label in replacements:
    c = once(c, old, new, label)
save(p, c)

(src / "STB/Studio/Services").mkdir(parents=True,exist_ok=True)
shutil.copy2(repo / "rebuild/overlays/NativeMeshService.cs", src / "STB/Studio/Services/NativeMeshService.cs")
shutil.copy2(repo / "rebuild/overlays/Studio3DEditorWindow.Mesh.cs", src / "STB/Studio/Studio3DEditorWindow.Mesh.cs")

(src / "Dependencies").mkdir(exist_ok=True)
(src / "Tools").mkdir(exist_ok=True)
shutil.copy2(base / "lib/app/Velopack.dll", src / "Dependencies/Velopack.dll")
shutil.copytree(base / "lib/app/Tools", src / "Tools", dirs_exist_ok=True)
shutil.copy2(base / "lib/app/stb-updates.json", src / "stb-updates.json")
print("STB Studio 0.1.13 source overlay applied successfully")
