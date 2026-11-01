"""Apply the STB Studio 0.1.14 3D-editor upgrade to decompiled v0.1.13 source.

All textual replacements are asserted. A mismatched upstream assembly fails the
GitHub Actions build explicitly instead of silently shipping a broken editor.
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


def once(data, old, new, name):
    occurrences = data.count(old)
    if occurrences != 1:
        raise RuntimeError(f"Unexpected v0.1.13 code for {name}: found {occurrences}")
    return data.replace(old, new)


def save(path, contents):
    path.write_text(contents, encoding="utf-8")


# Original WPF assembly references runtime resources that do not come through
# ILSpy's project export, so restore the actual bundled 0.1.13 dependencies.
p, project = load("STB.Studio.csproj")
project = once(project, "<PlatformTarget>x64</PlatformTarget>",
    "<PlatformTarget>x64</PlatformTarget>\n    <StartupObject>STB.Studio.Program</StartupObject>\n    <Version>0.1.14</Version>\n    <Nullable>enable</Nullable>\n    <EnableWindowsTargeting>true</EnableWindowsTargeting>", "version")
project, n = re.subn(r"<HintPath>[^<]*Velopack\.dll</HintPath>",
                     "<HintPath>Dependencies/Velopack.dll</HintPath>", project)
if n != 1:
    raise RuntimeError(f"Velopack reference mismatch: {n}")
project = once(project, "</Project>", """  <ItemGroup>
    <PackageReference Include="SharpCompress" Version="0.50.7" />
    <Content Include="Tools/*" Link="Tools/%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="Always" />
    <Content Include="stb-updates.json" Link="stb-updates.json" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="Always" />
  </ItemGroup>
</Project>""", "runtime files / archive dependency")
save(p, project)

p, info = load("Properties/AssemblyInfo.cs")
if "0.1.13" not in info:
    raise RuntimeError("Expected v0.1.13 AssemblyInfo")
save(p, info.replace("0.1.13", "0.1.14"))

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

p, code = load("STB/Studio/Studio3DEditorWindow.xaml.cs")

code = once(code, "public string LogoText { get; set; } = \"\";",
    "public string LogoText { get; set; } = \"\";\n\n\t\tpublic BitmapSource? LogoArtwork { get; set; }",
    "bitmap artwork on editable parts")

# The existing asynchronous native YFT importer is reused. After archive
# extraction, feed it the selected file path without reopening a dialog.
code = once(code,
    'OpenFileDialog val = new OpenFileDialog\n\t\t\t\t\t{\n\t\t\t\t\t\tTitle = "Ouvrir un véhicule GTA V / FiveM (YFT)",',
    'string? fromArchive = studio3DEditorWindow._archiveImportYft;\n'
    '\t\t\t\t\tstudio3DEditorWindow._archiveImportYft = null;\n'
    '\t\t\t\t\tOpenFileDialog val = new OpenFileDialog\n\t\t\t\t\t{\n'
    '\t\t\t\t\t\tTitle = "Ouvrir un véhicule GTA V / FiveM (YFT)",',
    "native archive integration")
code = once(code,
    'if (((CommonDialog)val).ShowDialog((Window)(object)studio3DEditorWindow) == true)\n\t\t\t\t\t{\n\t\t\t\t\t\t_003Cyft_003E5__2 = ((FileDialog)val).FileName;',
    'if (fromArchive != null || ((CommonDialog)val).ShowDialog((Window)(object)studio3DEditorWindow) == true)\n'
    '\t\t\t\t\t{\n\t\t\t\t\t\t_003Cyft_003E5__2 = fromArchive ?? ((FileDialog)val).FileName;',
    "native import selected path")
code = once(code,
    'studio3DEditorWindow.ModelNameLabel.Text = Path.GetFileName(_003Cyft_003E5__2) + "  +  " + Path.GetFileName(_003C_003E8__1.ytd);',
    'studio3DEditorWindow.ModelNameLabel.Text = Path.GetFileName(_003Cyft_003E5__2) + "  +  " + Path.GetFileName(_003C_003E8__1.ytd);\n'
    '\t\t\t\t\t\tstudio3DEditorWindow.HideWelcomeOverlay();', "native model ready")

# Keep original gradient grid, lights and camera. Only remove the mocked-up
# blue checkerboard car, which users understandably mistook for an actual model.
pattern = r'(\tprivate void LoadDemoVehicle\(\)\n\t\{).*?(\n\t\}\n\n\tprivate void AddPart\()'
replacement = '''\tprivate void LoadDemoVehicle()
\t{
\t\tSetNativeMode(active: false);
\t\t_parts.Clear();
\t\t_modelGroup.Children.Clear();
\t\tModelNameLabel.Text = "Aucun véhicule ouvert";
\t\tBindParts();
\t\tSelectedPartLabel.Text = "En attente d'un modèle";
\t\tTexturePreview.Source = null;
\t\tTextureDimensionsLabel.Text = "Importe une voiture pour afficher ses textures.";
\t\tResetView();
\t\tEditorStatus.Text = "Atelier prêt. Ouvre un YFT / YTD, un OBJ ou une archive RAR / ZIP / 7Z.";
\t}

\tprivate void AddPart('''
code, n = re.subn(pattern, replacement, code, flags=re.DOTALL)
if n != 1:
    raise RuntimeError(f"Demo car removal failed: {n}")

code = once(code,
    '\t\tLoadDemoVehicle();\n\t\tUpdateCamera();',
    '\t\tLoadDemoVehicle();\n\t\tUpdateCamera();\n\t\tSetupArchiveAndBrandUi();',
    "initialize archive and logos UI")

code = once(code,
    'ModelNameLabel.Text = Path.GetFileNameWithoutExtension(((FileDialog)val).FileName);\n\t\t\tResetView();',
    'ModelNameLabel.Text = Path.GetFileNameWithoutExtension(((FileDialog)val).FileName);\n'
    '\t\t\tHideWelcomeOverlay();\n\t\t\tResetView();',
    "OBJ hide welcome")

code = once(code,
    'LogoTextBox.Text = ((ContentControl)val).Content.ToString().ToUpperInvariant();',
    'LogoTextBox.Text = ((ContentControl)val).Content.ToString().ToUpperInvariant();',
    "brand combo existing behavior")

code = once(code,
    '\tprivate void LogoTextBox_TextChanged(object sender, TextChangedEventArgs e)\n\t{\n\t\tSaveLogoChanges();',
    '\tprivate void LogoTextBox_TextChanged(object sender, TextChangedEventArgs e)\n'
    '\t{\n\t\tif (!_loadingPart && PartList.SelectedItem is EditablePart part) part.LogoArtwork = null;\n'
    '\t\tSaveLogoChanges();', "switching to text-based brand")

code = once(code,
    'BitmapSource val = ((editablePart.LogoText.Length == 0) ? editablePart.BaseTexture : ComposeTexture(editablePart));',
    'BitmapSource val = ((editablePart.LogoText.Length == 0 && editablePart.LogoArtwork == null) ? editablePart.BaseTexture : ComposeTexture(editablePart));',
    "compose both image and text logos")
code = once(code,
    'string logoText = part.LogoText;\n\t\t\tif (logoText.Length > 0 && part.LogoOpacity > 0.0)',
    'string logoText = part.LogoText;\n'
    '\t\t\tif (part.LogoArtwork != null && part.LogoOpacity > 0.0)\n'
    '\t\t\t{\n\t\t\t\tDrawLogoArtwork(part, val2, pixelWidth, pixelHeight, num, num2);\n'
    '\t\t\t}\n\t\t\telse if (logoText.Length > 0 && part.LogoOpacity > 0.0)',
    "real brand image draw")
save(p, code)

(src / "STB/Studio").mkdir(parents=True, exist_ok=True)
shutil.copy2(repo / "rebuild014/overlays/Studio3DEditorWindow.ArchiveAndBrands.cs",
             src / "STB/Studio/Studio3DEditorWindow.ArchiveAndBrands.cs")
(src / "Dependencies").mkdir(exist_ok=True)
(src / "Tools").mkdir(exist_ok=True)
shutil.copy2(base / "lib/app/Velopack.dll", src / "Dependencies/Velopack.dll")
shutil.copytree(base / "lib/app/Tools", src / "Tools", dirs_exist_ok=True)
shutil.copy2(base / "lib/app/stb-updates.json", src / "stb-updates.json")
shutil.copy2(repo / "rebuild014/assets/brands-atlas.png", src / "Tools/brands-atlas.png")
print("STB Studio 0.1.14: archives, image logos and clean empty editor applied")
