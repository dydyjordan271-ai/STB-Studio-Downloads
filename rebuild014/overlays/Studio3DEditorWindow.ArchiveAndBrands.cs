using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace STB.Studio;

public partial class Studio3DEditorWindow
{
    // These entries correspond to the illustration supplied by the STB Studio creator.
    // The source atlas is kept with the app; individual logos become transparent thumbnails.
    private static readonly (string Name, Int32Rect Area)[] LoreBrands =
    [
        ("Albany", new(0, 0, 49, 81)),
        ("Annis", new(55, 1, 61, 81)),
        ("Coil", new(123, 0, 64, 82)),
        ("BF", new(195, 0, 61, 81)),
        ("Bollokan", new(263, 0, 60, 80)),
        ("Bravado", new(325, 0, 72, 82)),
        ("Brute", new(399, 0, 118, 81)),
        ("Benefactor", new(520, 0, 77, 81)),
        ("Cheval", new(57, 88, 72, 69)),
        ("Dewbauchee", new(266, 88, 187, 70)),
        ("Emperor", new(584, 88, 55, 72)),
        ("Fathom", new(60, 159, 73, 72)),
        ("Gallivanter", new(142, 160, 441, 76)),
        ("Grotti", new(584, 159, 54, 75)),
        ("HVY", new(65, 233, 120, 69)),
        ("Vapid", new(191, 230, 58, 76)),
        ("Karin", new(0, 304, 63, 70)),
        ("Maibatsu", new(191, 308, 62, 68)),
        ("Mammoth", new(251, 306, 143, 70)),
        ("Nagasaki", new(525, 306, 114, 71)),
        ("Obey", new(0, 379, 62, 73)),
        ("Pegassi", new(194, 379, 59, 73)),
        ("Pfister", new(257, 380, 65, 72)),
        ("Principe", new(329, 381, 65, 70)),
        ("Progen", new(397, 382, 67, 70)),
        ("Rune", new(467, 377, 56, 73)),
        ("Speedophile", new(0, 454, 190, 67)),
        ("Truffade", new(263, 460, 123, 65)),
        ("Vapid Motor", new(0, 530, 62, 70)),
        ("Zirconium", new(474, 538, 163, 70)),
    ];

    private readonly Dictionary<string, BitmapSource> _loreLogos = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Name, Button Button)> _loreButtons = [];
    private Border? _welcomePanel;
    private string? _archiveImportYft;

    private void SetupArchiveAndBrandUi()
    {
        // Insert the archive action directly into the existing toolbar.
        if (ExportObjButton.Parent is Panel topButtons)
        {
            var archiveButton = new Button
            {
                Content = "📦 RAR / ZIP / 7Z",
                Style = (Style)FindResource("SecondaryButton"),
                ToolTip = "Ouvrir une archive contenant un véhicule FiveM, sans installer WinRAR"
            };
            archiveButton.Click += OpenArchive_Click;
            topButtons.Children.Insert(1, archiveButton);
        }

        // Add actual image thumbnails and a custom PNG option beneath the old text-only combo.
        if (BrandCombo.Parent is Panel inspector)
        {
            int index = inspector.Children.IndexOf(BrandCombo) + 1;
            var panel = new StackPanel { Margin = new Thickness(0, 13, 0, 3) };
            panel.Children.Add(new TextBlock
            {
                Text = "LOGOS DES MARQUES GTA V",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 207, 255))
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Sélectionne une texture à gauche puis clique sur un logo.",
                Margin = new Thickness(0, 5, 0, 8),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(162, 184, 213)),
                FontSize = 11
            });

            var search = new TextBox { Text = "", ToolTip = "Filtrer par marque", Margin = new Thickness(0, 0, 0, 7) };
            panel.Children.Add(search);

            var thumbnails = new WrapPanel { Orientation = Orientation.Horizontal };
            var scroll = new ScrollViewer
            {
                Content = thumbnails,
                Height = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Color.FromRgb(10, 18, 33))
            };
            panel.Children.Add(scroll);

            try
            {
                LoadLoreAtlas();
                foreach (var (name, source) in _loreLogos)
                {
                    var image = new Image { Source = source, Width = 65, Height = 38, Stretch = Stretch.Uniform };
                    var label = new TextBlock
                    {
                        Text = name,
                        FontSize = 9,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 69
                    };
                    var content = new StackPanel();
                    content.Children.Add(image);
                    content.Children.Add(label);
                    var button = new Button
                    {
                        Content = content,
                        Width = 76,
                        Height = 65,
                        Margin = new Thickness(2),
                        Padding = new Thickness(2),
                        Background = new SolidColorBrush(Color.FromRgb(28, 42, 66)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(52, 79, 116)),
                        BorderThickness = new Thickness(1),
                        ToolTip = "Appliquer le logo " + name
                    };
                    button.Click += (_, _) => ApplyLoreLogo(source, name);
                    _loreButtons.Add((name, button));
                    thumbnails.Children.Add(button);
                }
                search.TextChanged += (_, _) =>
                {
                    foreach (var pair in _loreButtons)
                        pair.Button.Visibility = pair.Name.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                            ? Visibility.Visible : Visibility.Collapsed;
                };
            }
            catch (Exception error)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Bibliothèque de logos indisponible : " + error.Message,
                    Foreground = Brushes.Orange,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            var custom = new Button
            {
                Content = "＋  Importer mon logo PNG",
                Style = (Style)FindResource("SecondaryButton"),
                Margin = new Thickness(0, 9, 0, 0),
                ToolTip = "PNG transparent recommandé ; JPG et BMP également acceptés"
            };
            custom.Click += ImportCustomLogo_Click;
            panel.Children.Add(custom);
            inspector.Children.Insert(index, panel);
        }

        // No imitation cube car: the viewport greets the user until a real model is opened.
        if (ViewInput.Parent is Grid viewport)
        {
            var surface = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 470
            };
            surface.Children.Add(new TextBlock
            {
                Text = "◈",
                FontSize = 66,
                Foreground = new SolidColorBrush(Color.FromRgb(89, 192, 255)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            surface.Children.Add(new TextBlock
            {
                Text = "TON ATELIER 3D",
                FontSize = 25,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 3, 0, 10)
            });
            surface.Children.Add(new TextBlock
            {
                Text = "Ouvre une vraie voiture pour commencer.",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(164, 192, 222)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            surface.Children.Add(new TextBlock
            {
                Text = "YFT + YTD  •  OBJ / MTL  •  Archives RAR, ZIP et 7Z",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(126, 154, 195)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            });
            _welcomePanel = new Border
            {
                Child = surface,
                Background = new SolidColorBrush(Color.FromArgb(238, 11, 22, 40)),
                IsHitTestVisible = false,
                CornerRadius = new CornerRadius(17),
                Padding = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush(Color.FromRgb(44, 64, 98)),
                BorderThickness = new Thickness(1)
            };
            viewport.Children.Add(_welcomePanel);
        }
    }

    private void HideWelcomeOverlay()
    {
        if (_welcomePanel != null) _welcomePanel.Visibility = Visibility.Collapsed;
    }

    private void LoadLoreAtlas()
    {
        string imagePath = Path.Combine(AppContext.BaseDirectory, "Tools", "brands-atlas.png");
        var atlas = LoadBitmap(imagePath);
        foreach (var (name, area) in LoreBrands)
        {
            if (area.X + area.Width > atlas.PixelWidth || area.Y + area.Height > atlas.PixelHeight) continue;
            _loreLogos[name] = RemoveConnectedWhiteBackground(new CroppedBitmap(atlas, area));
        }
    }

    // Flood-fill connected near-white background only. White details enclosed by a logo remain.
    private static BitmapSource RemoveConnectedWhiteBackground(BitmapSource image)
    {
        var source = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int width = source.PixelWidth, height = source.PixelHeight, stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);
        var seen = new bool[width * height];
        var queue = new Queue<int>();
        void Visit(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            int point = y * width + x;
            if (seen[point]) return;
            seen[point] = true;
            int offset = point * 4;
            if (pixels[offset] >= 243 && pixels[offset + 1] >= 243 && pixels[offset + 2] >= 243)
                queue.Enqueue(point);
        }
        for (int x = 0; x < width; x++) { Visit(x, 0); Visit(x, height - 1); }
        for (int y = 0; y < height; y++) { Visit(0, y); Visit(width - 1, y); }
        while (queue.Count > 0)
        {
            int point = queue.Dequeue(), x = point % width, y = point / width;
            pixels[point * 4 + 3] = 0;
            Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
        }
        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }

    private void ApplyLoreLogo(BitmapSource artwork, string name)
    {
        if (PartList.SelectedItem is not EditablePart part)
        {
            EditorStatus.Text = "Choisis d'abord une texture du véhicule dans l'explorateur de scène.";
            return;
        }
        part.LogoArtwork = artwork;
        part.LogoText = "";
        _loadingPart = true;
        LogoTextBox.Text = "";
        _loadingPart = false;
        if (_nativeMode) part.IsDirty = true;
        RefreshTexture();
        EditorStatus.Text = $"Logo {name} placé sur « {part.Name} ». Ajuste taille et position UV, puis exporte la texture.";
    }

    private void ImportCustomLogo_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Importer un logo de marque transparent",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
        };
        if (picker.ShowDialog(this) != true) return;
        try { ApplyLoreLogo(LoadBitmap(picker.FileName), Path.GetFileName(picker.FileName)); }
        catch (Exception error) when (error is IOException or ArgumentException or NotSupportedException)
        {
            ShowError("Logo illisible", error);
        }
    }

    private void ClearArtworkForManualBrand()
    {
        if (PartList.SelectedItem is EditablePart part)
        {
            part.LogoArtwork = null;
            if (_nativeMode) part.IsDirty = true;
            RefreshTexture();
        }
    }

    private static void DrawLogoArtwork(EditablePart part, DrawingContext canvas, int width, int height, double scaleX, double scaleY)
    {
        var image = part.LogoArtwork;
        if (image == null || part.LogoOpacity <= 0) return;

        double cx = width * part.LogoX / 100, cy = height * part.LogoY / 100;
        double targetWidth = Math.Clamp(part.LogoSize * 3.0 * Math.Min(scaleX, scaleY), 18, width * .9);
        double targetHeight = targetWidth * image.PixelHeight / image.PixelWidth;
        double boundHeight = height * .72;
        if (targetHeight > boundHeight)
        {
            targetWidth *= boundHeight / targetHeight;
            targetHeight = boundHeight;
        }
        var rect = new Rect(Math.Clamp(cx - targetWidth / 2, 0, Math.Max(0, width - targetWidth)),
                            Math.Clamp(cy - targetHeight / 2, 0, Math.Max(0, height - targetHeight)), targetWidth, targetHeight);
        if (part.MaskOldLogo)
        {
            double maskWidth = part.MaskWidth * scaleX, maskHeight = part.MaskHeight * scaleY;
            double u = Math.Clamp((cx - maskWidth / 2 - 6) / width, 0, 1);
            double v = Math.Clamp((cy - maskHeight / 2 - 6) / height, 0, 1);
            canvas.DrawRectangle(new SolidColorBrush(SampleTextureColor(part.BaseTexture, u, v)), null,
                new Rect(cx - maskWidth / 2, cy - maskHeight / 2, maskWidth, maskHeight));
        }
        canvas.PushOpacity(part.LogoOpacity / 100);
        canvas.DrawImage(image, rect);
        canvas.Pop();
    }

    private async void OpenArchive_Click(object sender, RoutedEventArgs e)
    {
        if (_nativeBusy) { EditorStatus.Text = "Une opération YFT est déjà en cours."; return; }
        var picker = new OpenFileDialog
        {
            Title = "Importer un véhicule depuis une archive WinRAR, ZIP ou 7Z",
            Filter = "Archives prises en charge (*.rar;*.zip;*.7z)|*.rar;*.zip;*.7z"
        };
        if (picker.ShowDialog(this) != true) return;
        string path = picker.FileName;
        EditorStatus.Text = "Extraction sécurisée de " + Path.GetFileName(path) + "…";
        try
        {
            var extracted = await System.Threading.Tasks.Task.Run(() => ExtractVehicleArchive(path));
            var yfts = extracted.Where(x => x.EndsWith(".yft", StringComparison.OrdinalIgnoreCase)
                                             && !x.EndsWith("_hi.yft", StringComparison.OrdinalIgnoreCase)).ToList();
            if (yfts.Count > 0)
            {
                string selected = yfts[0];
                if (yfts.Count > 1)
                {
                    // Multiple vehicles: let the user choose, instead of silently picking the wrong model.
                    var list = new Window
                    {
                        Owner = this, Title = "Choisir le véhicule de l'archive", Width = 510, Height = 365,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Background = new SolidColorBrush(Color.FromRgb(18, 30, 50))
                    };
                    var layout = new DockPanel { Margin = new Thickness(16) };
                    var options = new ListBox { ItemsSource = yfts.Select(x => Path.GetFileName(x)).ToList(), SelectedIndex = 0 };
                    var accept = new Button { Content = "Ouvrir ce véhicule", Height = 39, Margin = new Thickness(0, 12, 0, 0), IsDefault = true };
                    accept.Click += (_, _) => { selected = yfts[options.SelectedIndex]; list.DialogResult = true; list.Close(); };
                    DockPanel.SetDock(accept, Dock.Bottom); layout.Children.Add(accept); layout.Children.Add(options);
                    list.Content = layout;
                    if (list.ShowDialog() != true) return;
                }
                // Use the native importer so meshes, YTD and export keep the same behavior.
                // FiveM archives sometimes keep the YFT and YTD in different
                // folders; stage a matching texture next to the selected YFT.
                string expectedYtd = Path.ChangeExtension(selected, ".ytd");
                if (!File.Exists(expectedYtd))
                {
                    string stem = Path.GetFileNameWithoutExtension(selected);
                    string? matchedYtd = extracted.FirstOrDefault(x =>
                        x.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase) &&
                        Path.GetFileNameWithoutExtension(x).Equals(stem, StringComparison.OrdinalIgnoreCase));
                    matchedYtd ??= extracted.Where(x => x.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase)).Take(2).Count() == 1
                        ? extracted.First(x => x.EndsWith(".ytd", StringComparison.OrdinalIgnoreCase)) : null;
                    if (matchedYtd != null) File.Copy(matchedYtd, expectedYtd);
                }
                _archiveImportYft = selected;
                ImportGta_Click(this, new RoutedEventArgs());
                EditorStatus.Text = "Archive extraite : ouverture du YFT et recherche de son YTD…";
                return;
            }
            string? obj = extracted.FirstOrDefault(x => x.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
            if (obj != null)
            {
                ImportObj(obj);
                SetNativeMode(false);
                ModelNameLabel.Text = Path.GetFileNameWithoutExtension(obj);
                ResetView();
                HideWelcomeOverlay();
                EditorStatus.Text = "OBJ importé depuis l'archive : " + Path.GetFileName(path);
                return;
            }
            EditorStatus.Text = "Archive ouverte, mais aucun fichier YFT ou OBJ compatible n'a été trouvé.";
            MessageBox.Show(this, "Cette archive ne contient pas de modèle YFT ou OBJ compatible. Les fichiers originaux n'ont pas été modifiés.", "STB Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception error)
        {
            ShowError("Impossible de lire l'archive", error);
        }
    }

    private static List<string> ExtractVehicleArchive(string archivePath)
    {
        const int maxFiles = 3000;
        const long maxFile = 256L * 1024 * 1024;
        const long maxTotal = 1024L * 1024 * 1024;
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".yft", ".ytd", ".ydr", ".obj", ".mtl", ".png", ".jpg", ".jpeg", ".bmp", ".dds", ".xml", ".meta" };
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "STBStudio", "ArchiveImports", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        var files = new List<string>();
        long written = 0;
        int archiveItems = 0;
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        using var reader = archive.ExtractAllEntries();
        while (reader.MoveToNextEntry())
        {
            var item = reader.Entry;
            if (item.IsDirectory) continue;
            if (++archiveItems > maxFiles) throw new InvalidDataException("Archive contenant trop de fichiers.");
            string key = (item.Key ?? "").Replace('\\', '/');
            string[] segments = key.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (key.StartsWith('/') || segments.Length == 0 || segments.Any(p => p == "." || p == ".." || p.Contains(':')))
                throw new InvalidDataException("Chemin non sécurisé dans l'archive.");
            if (!allowed.Contains(Path.GetExtension(key))) continue;
            string file = Path.GetFullPath(Path.Combine(folder, Path.Combine(segments)));
            if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Chemin non sécurisé dans l'archive.");
            if (item.Size > maxFile || item.Size + written > maxTotal)
                throw new InvalidDataException("Archive trop volumineuse pour l'import 3D.");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            // No overwriting: reject ambiguous duplicate paths.
            using var dest = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var source = reader.OpenEntryStream();
            var buffer = new byte[128 * 1024];
            long fileBytes = 0;
            int count;
            while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                fileBytes += count;
                written += count;
                if (fileBytes > maxFile || written > maxTotal) throw new InvalidDataException("Archive décompressée trop volumineuse.");
                dest.Write(buffer, 0, count);
            }
            files.Add(file);
        }
        return files;
    }

}
