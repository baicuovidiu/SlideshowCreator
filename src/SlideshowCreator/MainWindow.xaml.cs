using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SlideshowCreator;

public partial class MainWindow : Window
{
    public ObservableCollection<MediaItem> Media { get; } = new();
    string? music;
    static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".mts", ".m2ts", ".mpg", ".mpeg", ".wmv", ".webm", ".3gp", ".ts", ".vob", ".mxf" };
    static readonly HashSet<string> PhotoExt = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".webp", ".heic", ".heif", ".arw", ".cr2", ".cr3", ".nef", ".nrw", ".orf", ".rw2", ".raf", ".dng", ".pef", ".srw" };

    public MainWindow() { InitializeComponent(); DataContext = this; }

    async void AddMedia_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Multiselect = true, Filter = "Foto / RAW / video|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp;*.heic;*.heif;*.arw;*.cr2;*.cr3;*.nef;*.nrw;*.orf;*.rw2;*.raf;*.dng;*.pef;*.srw;*.mp4;*.mov;*.mkv;*.avi;*.m4v;*.mts;*.m2ts;*.mpg;*.mpeg;*.wmv;*.webm;*.3gp;*.ts;*.vob;*.mxf|Toate fișierele|*.*" };
        if (d.ShowDialog() == true) await ImportFilesAsync(d.FileNames);
    }

    async Task ImportFilesAsync(IEnumerable<string> paths)
    {
        ImportDropZone.Visibility = Visibility.Collapsed;
        foreach (var p in paths.Where(File.Exists))
        {
            var ext = Path.GetExtension(p);
            bool video = VideoExt.Contains(ext), photo = PhotoExt.Contains(ext);
            if (!video && !photo) continue;
            var item = new MediaItem { Path = p, Type = video ? "Video" : "Foto", Date = File.GetLastWriteTime(p), Duration = video ? await Task.Run(() => Probe(p)) : 4 };
            item.Thumbnail = await Task.Run(() => video ? CreateVideoThumb(p) : LoadPhotoThumb(p));
            Media.Add(item);
            CountText.Text = $"{Media.Count} elemente";
            StatusText.Text = "Importat: " + Path.GetFileName(p);
        }
        if (Media.Count == 0) ImportDropZone.Visibility = Visibility.Visible;
    }

    static double Probe(string p)
    {
        try { var s = Run("ffprobe.exe", $"-v error -show_entries format=duration -of default=nw=1:nk=1 \"{p}\"", out var c); if (c == 0 && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return Math.Max(.1, n); } catch { }
        return 5;
    }

    static ImageSource? LoadPhotoThumb(string path)
    {
        try { var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.DecodePixelWidth = 240; b.UriSource = new Uri(path); b.EndInit(); b.Freeze(); return b; } catch { return null; }
    }

    static ImageSource? CreateVideoThumb(string path)
    {
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "SlideshowCreatorThumbs"); Directory.CreateDirectory(dir);
            var jpg = Path.Combine(dir, Math.Abs(path.GetHashCode()) + ".jpg");
            if (!File.Exists(jpg)) Run("ffmpeg.exe", $"-y -ss 0.5 -i \"{path}\" -frames:v 1 -vf \"scale=240:-2:force_original_aspect_ratio=decrease\" -q:v 3 \"{jpg}\"", out _);
            if (!File.Exists(jpg)) return null;
            var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.UriSource = new Uri(jpg); b.EndInit(); b.Freeze(); return b;
        }
        catch { return null; }
    }

    void AddMusic_Click(object sender, RoutedEventArgs e) { var d = new OpenFileDialog { Filter = "Audio|*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus|Toate fișierele|*.*" }; if (d.ShowDialog() == true) { music = d.FileName; MusicLabel.Text = "Muzică: " + Path.GetFileName(music); } }
    void Sort_Click(object sender, RoutedEventArgs e) { var a = Media.OrderBy(x => x.Date).ToList(); Media.Clear(); foreach (var x in a) Media.Add(x); StatusText.Text = "Sortare cronologică terminată"; }
    void Remove_Click(object sender, RoutedEventArgs e) { if (MediaList.SelectedItem is MediaItem m) Media.Remove(m); CountText.Text = $"{Media.Count} elemente"; if (Media.Count == 0) ImportDropZone.Visibility = Visibility.Visible; }

    void MediaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MediaList.SelectedItem is not MediaItem m) return;
        DurationBox.Text = m.Duration.ToString("0.##", CultureInfo.InvariantCulture); TrimInBox.Text = m.TrimIn.ToString("0.##", CultureInfo.InvariantCulture); MuteBox.IsChecked = m.Mute;
        VideoPreview.Stop(); VideoPreview.Visibility = Visibility.Collapsed; PhotoPreview.Visibility = Visibility.Collapsed;
        if (m.Type == "Foto") { try { PhotoPreview.Source = new BitmapImage(new Uri(m.Path)); PhotoPreview.Visibility = Visibility.Visible; } catch { PhotoPreview.Source = m.Thumbnail; PhotoPreview.Visibility = Visibility.Visible; } }
        else { try { VideoPreview.Source = new Uri(m.Path); VideoPreview.Visibility = Visibility.Visible; } catch { } }
    }

    void Property_Changed(object sender, RoutedEventArgs e)
    {
        if (MediaList.SelectedItem is not MediaItem m) return;
        if (double.TryParse(DurationBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) m.Duration = Math.Max(.1, d);
        if (double.TryParse(TrimInBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var t)) m.TrimIn = Math.Max(0, t);
        m.Mute = MuteBox.IsChecked == true; m.Changed(nameof(MediaItem.Duration));
    }

    void Preview_Click(object sender, RoutedEventArgs e) { if (MediaList.SelectedItem is MediaItem m && m.Type == "Video") { try { VideoPreview.Position = TimeSpan.FromSeconds(m.TrimIn); VideoPreview.Play(); } catch { } } }

    async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (Media.Count == 0) { MessageBox.Show("Importă mai întâi materialele."); return; }
        var d = new SaveFileDialog { Filter = "Video MP4|*.mp4", FileName = "Slideshow.mp4", AddExtension = true, DefaultExt = ".mp4" }; if (d.ShowDialog() != true) return;
        try
        {
            IsEnabled = false; ExportProgress.Visibility = Visibility.Visible; StatusText.Text = "Export în lucru...";
            await Task.Run(() => Export(d.FileName)); StatusText.Text = "Export finalizat"; MessageBox.Show("Export MP4 finalizat.\n\n" + d.FileName, "Slideshow Creator 0.1.4");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Eroare export"); }
        finally { IsEnabled = true; ExportProgress.Visibility = Visibility.Collapsed; }
    }

    void Export(string dest)
    {
        var dir = Path.Combine(Path.GetTempPath(), "SlideshowCreator", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            if (Media.All(m => m.Type == "Foto"))
            {
                ExportPhotosSinglePass(dest, dir);
                return;
            }

            // Mixed photo/video path. It remains single-encode, but the large photo-only
            // benchmark uses ffconcat below so 120+ paths never overflow Windows command-line limits.
            var inputs = new StringBuilder();
            var filters = new StringBuilder();
            var concatInputs = new StringBuilder();
            int i = 0;
            foreach (var m in Media)
            {
                if (m.Type == "Foto") inputs.Append($" -loop 1 -t {F(m.Duration)} -i \"{m.Path}\"");
                else inputs.Append($" -ss {F(m.TrimIn)} -t {F(m.Duration)} -i \"{m.Path}\"");
                filters.Append($"[{i}:v]scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:black,fps=30,format=yuv420p,setpts=PTS-STARTPTS[v{i}];");
                concatInputs.Append($"[v{i}]");
                i++;
            }
            filters.Append($"{concatInputs}concat=n={Media.Count}:v=1:a=0[outv]");
            var musicIndex = Media.Count;
            var audioInput = music == null ? "" : $" -stream_loop -1 -i \"{music}\"";
            var audioMap = music == null ? " -an" : $" -map {musicIndex}:a -c:a aac -b:a 256k -shortest";
            Ffmpeg($"-y{inputs}{audioInput} -filter_complex \"{filters}\" -map \"[outv]\"{audioMap} -c:v h264_nvenc -preset p4 -cq 19 -b:v 0 -movflags +faststart \"{dest}\"");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    void ExportPhotosSinglePass(string dest, string dir)
    {
        var list = Path.Combine(dir, "photos.ffconcat");
        using (var w = new StreamWriter(list, false, new UTF8Encoding(false)))
        {
            w.WriteLine("ffconcat version 1.0");
            foreach (var m in Media)
            {
                w.WriteLine("file '" + FfconcatPath(m.Path) + "'");
                w.WriteLine("duration " + F(m.Duration));
            }
            // concat demuxer applies the final duration only when a following file exists.
            w.WriteLine("file '" + FfconcatPath(Media[^1].Path) + "'");
        }
        var vf = "scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:black,fps=30,format=yuv420p";
        var audioInput = music == null ? "" : $" -stream_loop -1 -i \"{music}\"";
        var audioMap = music == null ? " -an" : " -map 1:a -c:a aac -b:a 256k -shortest";
        Ffmpeg($"-y -f concat -safe 0 -i \"{list}\"{audioInput} -map 0:v{audioMap} -vf \"{vf}\" -c:v h264_nvenc -preset p4 -cq 19 -b:v 0 -movflags +faststart \"{dest}\"");
    }

    static string FfconcatPath(string path) => path.Replace("'", "'\\''");
    static string F(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
    static void Ffmpeg(string args) { var t = Run("ffmpeg.exe", args, out var c); if (c != 0) throw new Exception("FFmpeg a oprit exportul.\n\n" + (t.Length > 1600 ? t[^1600..] : t)); }
    static string Run(string exe, string args, out int code)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Tools", exe); if (!File.Exists(path)) throw new FileNotFoundException("Lipsește " + exe, path);
        using var p = new Process(); p.StartInfo = new ProcessStartInfo(path, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        var sb = new StringBuilder(); p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); }; p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit(); code = p.ExitCode; lock (sb) return sb.ToString();
    }

    void Window_PreviewDragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    async void Window_Drop(object sender, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] files) await ImportFilesAsync(files); }
}
