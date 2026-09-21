using Microsoft.Win32;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
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
            var mediaDate = photo ? await Task.Run(() => ReadPhotoCaptureDate(p)) : await Task.Run(() => ReadVideoCaptureDate(p));
            var item = new MediaItem { Path = p, Type = video ? "Video" : "Foto", SourceDate = mediaDate, Duration = video ? await Task.Run(() => Probe(p)) : 4 };
            item.Thumbnail = await Task.Run(() => video ? CreateVideoThumb(p) : LoadPhotoThumb(p));
            Media.Add(item);
            CountText.Text = $"{Media.Count} elemente";
            StatusText.Text = "Importat: " + Path.GetFileName(p);
        }
        if (Media.Count == 0) ImportDropZone.Visibility = Visibility.Visible;
    }

    static DateTime ReadPhotoCaptureDate(string path)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (subIfd != null && subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original)) return original;
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 != null && ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var modified)) return modified;
        }
        catch { }
        return File.GetLastWriteTime(path);
    }

    static bool HasEmbeddedExifDate(string path)
    {
        try { var directories = ImageMetadataReader.ReadMetadata(path); var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault(); return subIfd != null && subIfd.ContainsTag(ExifDirectoryBase.TagDateTimeOriginal); }
        catch { return false; }
    }

    static DateTime ReadVideoCaptureDate(string path)
    {
        try
        {
            var s = Run("ffprobe.exe", $"-v error -show_entries format_tags=creation_time -of default=nw=1:nk=1 \"{path}\"", out var code).Trim();
            if (code == 0 && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)) return dto.LocalDateTime;
        }
        catch { }
        return File.GetLastWriteTime(path);
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
            var dir = Path.Combine(Path.GetTempPath(), "SlideshowCreatorThumbs"); System.IO.Directory.CreateDirectory(dir);
            var jpg = Path.Combine(dir, Math.Abs(path.GetHashCode()) + ".jpg");
            if (!File.Exists(jpg)) Run("ffmpeg.exe", $"-y -ss 0.5 -i \"{path}\" -frames:v 1 -vf \"scale=240:-2:force_original_aspect_ratio=decrease\" -q:v 3 \"{jpg}\"", out _);
            if (!File.Exists(jpg)) return null;
            var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.UriSource = new Uri(jpg); b.EndInit(); b.Freeze(); return b;
        }
        catch { return null; }
    }

    void AddMusic_Click(object sender, RoutedEventArgs e) { var d = new OpenFileDialog { Filter = "Audio|*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.opus|Toate fișierele|*.*" }; if (d.ShowDialog() == true) { music = d.FileName; MusicLabel.Text = "Muzică: " + Path.GetFileName(music); } }
    void Sort_Click(object sender, RoutedEventArgs e) { var a = Media.OrderBy(x => x.EffectiveDate).ToList(); Media.Clear(); foreach (var x in a) Media.Add(x); StatusText.Text = "Sortare cronologică terminată"; }
    void Remove_Click(object sender, RoutedEventArgs e) { if (MediaList.SelectedItem is MediaItem m) Media.Remove(m); CountText.Text = $"{Media.Count} elemente"; if (Media.Count == 0) ImportDropZone.Visibility = Visibility.Visible; }

    void MediaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MediaList.SelectedItem is not MediaItem m) return;
        DurationBox.Text = m.Duration.ToString("0.##", CultureInfo.InvariantCulture); TrimInBox.Text = m.TrimIn.ToString("0.##", CultureInfo.InvariantCulture); TimeOffsetBox.Text = m.CaptureTimeOffsetSeconds.ToString("0.##", CultureInfo.InvariantCulture); MuteBox.IsChecked = m.Mute;
        VideoPreview.Stop(); VideoPreview.Visibility = Visibility.Collapsed; PhotoPreview.Visibility = Visibility.Collapsed;
        if (m.Type == "Foto") { try { PhotoPreview.Source = new BitmapImage(new Uri(m.Path)); PhotoPreview.Visibility = Visibility.Visible; } catch { PhotoPreview.Source = m.Thumbnail; PhotoPreview.Visibility = Visibility.Visible; } }
        else { try { VideoPreview.Source = new Uri(m.Path); VideoPreview.Visibility = Visibility.Visible; } catch { } }
    }

    void Property_Changed(object sender, RoutedEventArgs e)
    {
        if (MediaList.SelectedItem is not MediaItem m) return;
        if (double.TryParse(DurationBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) m.Duration = Math.Max(.1, d);
        if (double.TryParse(TrimInBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var t)) m.TrimIn = Math.Max(0, t);
        if (double.TryParse(TimeOffsetBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var offset)) m.CaptureTimeOffsetSeconds = offset;
        m.Mute = MuteBox.IsChecked == true; m.Changed(nameof(MediaItem.Duration)); m.Changed(nameof(MediaItem.EffectiveDate));
    }

    void Preview_Click(object sender, RoutedEventArgs e) { if (MediaList.SelectedItem is MediaItem m && m.Type == "Video") { try { VideoPreview.Position = TimeSpan.FromSeconds(m.TrimIn); VideoPreview.Play(); } catch { } } }

    async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (Media.Count == 0) { MessageBox.Show("Importă mai întâi materialele."); return; }
        var d = new SaveFileDialog { Filter = "Video MP4|*.mp4", FileName = "Slideshow.mp4", AddExtension = true, DefaultExt = ".mp4" }; if (d.ShowDialog() != true) return;
        try { IsEnabled = false; ExportProgress.Visibility = Visibility.Visible; StatusText.Text = "Export în lucru..."; await Task.Run(() => Export(d.FileName)); StatusText.Text = "Export finalizat"; MessageBox.Show("Export MP4 finalizat.\n\n" + d.FileName, "Slideshow Creator 0.1.4"); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Eroare export"); }
        finally { IsEnabled = true; ExportProgress.Visibility = Visibility.Collapsed; }
    }

    void Export(string dest)
    {
        var dir = Path.Combine(Path.GetTempPath(), "SlideshowCreator", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(dir);
        try
        {
            ValidateTimeline(); var expectedDuration = Media.Sum(m => m.Duration); var videoEncoder = SelectVideoEncoder(dir);
            if (Media.All(m => m.Type == "Foto"))
            {
                if (CarouselModeBox.SelectedIndex == 1) { expectedDuration = CalculateCarouselDuration(); ExportPhotosCarousel(dest, dir, videoEncoder); }
                else ExportPhotosSinglePass(dest, dir, videoEncoder);
                ValidateExport(dest, expectedDuration);
                return;
            }
            var inputs = new StringBuilder(); var filters = new StringBuilder(); var concatInputs = new StringBuilder(); int i = 0;
            foreach (var m in Media)
            {
                if (m.Type == "Foto") inputs.Append($" -loop 1 -t {F(m.Duration)} -i \"{m.Path}\""); else inputs.Append($" -ss {F(m.TrimIn)} -t {F(m.Duration)} -i \"{m.Path}\"");
                filters.Append($"[{i}:v]scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:black,fps=30,format=yuv420p,setpts=PTS-STARTPTS[v{i}];"); concatInputs.Append($"[v{i}]"); i++;
            }
            filters.Append($"{concatInputs}concat=n={Media.Count}:v=1:a=0[outv]"); var musicIndex = Media.Count; var audioInput = music == null ? "" : $" -stream_loop -1 -i \"{music}\""; var audioMap = music == null ? " -an" : $" -map {musicIndex}:a -c:a aac -b:a 256k -shortest";
            Ffmpeg($"-y{inputs}{audioInput} -filter_complex \"{filters}\" -map \"[outv]\"{audioMap} {videoEncoder} -movflags +faststart \"{dest}\""); ValidateExport(dest, expectedDuration);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    void ValidateTimeline()
    {
        foreach (var m in Media)
        {
            if (!File.Exists(m.Path)) throw new FileNotFoundException("Material lipsă din proiect.", m.Path);
            if (!double.IsFinite(m.Duration) || m.Duration < .1) throw new Exception("Durată invalidă: " + Path.GetFileName(m.Path));
            if (!double.IsFinite(m.TrimIn) || m.TrimIn < 0) throw new Exception("Trim IN invalid: " + Path.GetFileName(m.Path));
            if (m.Type == "Video") { var sourceDuration = Probe(m.Path); if (m.TrimIn >= sourceDuration || m.TrimIn + m.Duration > sourceDuration + .05) throw new Exception($"Trim-ul depășește clipul: {Path.GetFileName(m.Path)} (sursă {F(sourceDuration)}s, IN {F(m.TrimIn)}s, durată {F(m.Duration)}s)."); }
        }
    }

    double CalculateCarouselDuration()
    {
        double total = 0; int segment = 0;
        for (int i = 0; i < Media.Count; )
        {
            if (i + 2 < Media.Count && segment % 5 == 2) { total += Math.Min(Media[i].Duration, Math.Min(Media[i + 1].Duration, Media[i + 2].Duration)); segment++; i += 3; continue; }
            if (i + 3 < Media.Count && segment % 5 == 4) { total += Math.Min(Math.Min(Media[i].Duration, Media[i + 1].Duration), Math.Min(Media[i + 2].Duration, Media[i + 3].Duration)); segment++; i += 4; continue; }
            total += i + 1 < Media.Count ? Math.Min(Media[i].Duration, Media[i + 1].Duration) : Media[i].Duration;
            segment++; i += i + 1 < Media.Count ? 2 : 1;
        }
        return total;
    }

    void ExportPhotosCarousel(string dest, string dir, string videoEncoder)
    {
        // First useful CARUSEL: pairs of complete photos, alternating 60/40 and 40/60.
        // No crop: each source is scaled with force_original_aspect_ratio=decrease and padded inside its plane.
        var inputs = new StringBuilder(); var filters = new StringBuilder(); var segments = new StringBuilder();
        for (int i = 0; i < Media.Count; i++) inputs.Append($" -loop 1 -t {F(Media[i].Duration)} -i \"{Media[i].Path}\"");
        int segment = 0;
        for (int i = 0; i < Media.Count; )
        {
            var a = Media[i]; var duration = a.Duration;
            if (i + 2 < Media.Count && segment % 5 == 2)
            {
                duration = Math.Min(Media[i].Duration, Math.Min(Media[i + 1].Duration, Media[i + 2].Duration));
                filters.Append($"[{i}:v]scale=1152:1080:force_original_aspect_ratio=decrease,pad=1152:1080:(ow-iw)/2:(oh-ih)/2:black,fps=30,setpts=PTS-STARTPTS[big{segment}];");
                filters.Append($"[{i + 1}:v]scale=768:540:force_original_aspect_ratio=decrease,pad=768:540:(ow-iw)/2:(oh-ih)/2:black,fps=30,setpts=PTS-STARTPTS[s1_{segment}];");
                filters.Append($"[{i + 2}:v]scale=768:540:force_original_aspect_ratio=decrease,pad=768:540:(ow-iw)/2:(oh-ih)/2:black,fps=30,setpts=PTS-STARTPTS[s2_{segment}];");
                filters.Append($"[s1_{segment}][s2_{segment}]vstack=inputs=2[side{segment}];[big{segment}][side{segment}]hstack=inputs=2,trim=duration={F(duration)},setpts=PTS-STARTPTS,format=yuv420p[s{segment}];");
                segments.Append($"[s{segment}]"); segment++; i += 3; continue;
            }
            if (i + 3 < Media.Count && segment % 5 == 4)
            {
                duration = Math.Min(Math.Min(Media[i].Duration, Media[i + 1].Duration), Math.Min(Media[i + 2].Duration, Media[i + 3].Duration));
                for (int q = 0; q < 4; q++) filters.Append($"[{i + q}:v]scale=960:540:force_original_aspect_ratio=decrease,pad=960:540:(ow-iw)/2:(oh-ih)/2:black,fps=30,setpts=PTS-STARTPTS[q{segment}_{q}];");
                filters.Append($"[q{segment}_0][q{segment}_1]hstack=inputs=2[top{segment}];[q{segment}_2][q{segment}_3]hstack=inputs=2[bot{segment}];[top{segment}][bot{segment}]vstack=inputs=2,trim=duration={F(duration)},setpts=PTS-STARTPTS,format=yuv420p[s{segment}];");
                segments.Append($"[s{segment}]"); segment++; i += 4; continue;
            }
            if (i + 1 >= Media.Count)
            {
                filters.Append($"[{i}:v]scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:black,fps=30,format=yuv420p,setpts=PTS-STARTPTS[s{segment}];");
            }
            else
            {
                var b = Media[i + 1]; duration = Math.Min(a.Duration, b.Duration);
                var family = segment % 4;
                if (family < 2)
                {
                    var left = family == 0 ? 1152 : 768; var right = 1920 - left;
                    filters.Append($"[{i}:v]scale={left}:1080:force_original_aspect_ratio=decrease,pad={left}:1080:(ow-iw)/2:(oh-ih)/2:black,fps=30,setpts=PTS-STARTPTS[a{segment}];");
                    filters.Append($"[{i + 1}:v]scale={right}:1080:force_original_aspect_ratio=decrease,pad={right}:1080:(ow-iw)/2:(oh-ih)/2:black,fps=30,setpts=PTS-STARTPTS[b{segment}];");
                    filters.Append($"[a{segment}][b{segment}]hstack=inputs=2,trim=duration={F(duration)},setpts=PTS-STARTPTS,format=yuv420p[s{segment}];");
                }
                else
                {
                    var top = family == 2 ? 648 : 432; var bottom = 1080 - top;
                    filters.Append($"[{i}:v]scale=1920:{top}:force_original_aspect_ratio=decrease,pad=1920:{top}:(ow-iw)/2:(oh-ih)/2:black,fps=30,setpts=PTS-STARTPTS[a{segment}];");
                    filters.Append($"[{i + 1}:v]scale=1920:{bottom}:force_original_aspect_ratio=decrease,pad=1920:{bottom}:(ow-iw)/2:(oh-ih)/2:black,fps=30,setpts=PTS-STARTPTS[b{segment}];");
                    filters.Append($"[a{segment}][b{segment}]vstack=inputs=2,trim=duration={F(duration)},setpts=PTS-STARTPTS,format=yuv420p[s{segment}];");
                }
            }
            segments.Append($"[s{segment}]"); segment++; i += (i + 1 < Media.Count ? 2 : 1);
        }
        filters.Append($"{segments}concat=n={segment}:v=1:a=0[carouselbase];");
        // Give the assembled CARUSEL continuous motion without cropping any source photo.
        // The canvas remains 1920x1080; motion is a subtle whole-scene translation, not a Ken Burns crop/zoom.
        var totalDuration = CalculateCarouselDuration();
        var fadeOutStart = Math.Max(0, totalDuration - 0.45);
        filters.Append($"[carouselbase]pad=1940:1100:10:10:black,crop=1920:1080:x='10+8*sin(t*1.1)':y='10+8*cos(t*0.9)',fade=t=in:st=0:d=0.35,fade=t=out:st={F(fadeOutStart)}:d=0.45[outv]");
        var script = Path.Combine(dir, "carousel-filter.txt"); File.WriteAllText(script, filters.ToString(), new UTF8Encoding(false));
        var musicIndex = Media.Count; var audioInput = music == null ? "" : $" -stream_loop -1 -i \"{music}\""; var audioMap = music == null ? " -an" : $" -map {musicIndex}:a -c:a aac -b:a 256k -shortest";
        Ffmpeg($"-y{inputs}{audioInput} -filter_complex_script \"{script}\" -map \"[outv]\"{audioMap} {videoEncoder} -movflags +faststart \"{dest}\"");
    }

    void ExportPhotosSinglePass(string dest, string dir, string videoEncoder)
    {
        var inputs = new StringBuilder(); var filters = new StringBuilder(); var concatInputs = new StringBuilder();
        for (int i = 0; i < Media.Count; i++) { var m = Media[i]; inputs.Append($" -loop 1 -t {F(m.Duration)} -i \"{m.Path}\""); filters.Append($"[{i}:v]scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:black,fps=30,format=yuv420p,setpts=PTS-STARTPTS[v{i}];"); concatInputs.Append($"[v{i}]"); }
        filters.Append($"{concatInputs}concat=n={Media.Count}:v=1:a=0[outv]"); var filterScript = Path.Combine(dir, "photos-filter.txt"); File.WriteAllText(filterScript, filters.ToString(), new UTF8Encoding(false)); var musicIndex = Media.Count; var audioInput = music == null ? "" : $" -stream_loop -1 -i \"{music}\""; var audioMap = music == null ? " -an" : $" -map {musicIndex}:a -c:a aac -b:a 256k -shortest"; var command = $"-y{inputs}{audioInput} -filter_complex_script \"{filterScript}\" -map \"[outv]\"{audioMap} {videoEncoder} -movflags +faststart \"{dest}\"";
        if (command.Length > 30000) throw new Exception("Exportul conține prea multe căi de fișiere pentru limita Windows. Mută temporar fotografiile într-un folder cu o cale mai scurtă."); Ffmpeg(command);
    }

    static string SelectVideoEncoder(string dir)
    {
        var probe = Path.Combine(dir, "nvenc-probe.mp4");
        try { Run("ffmpeg.exe", $"-y -f lavfi -i color=c=black:s=64x64:r=1 -frames:v 1 -c:v h264_nvenc -preset p4 \"{probe}\"", out var code); if (code == 0 && File.Exists(probe) && new FileInfo(probe).Length > 0) return "-c:v h264_nvenc -preset p4 -cq 19 -b:v 0"; }
        catch { }
        finally { try { if (File.Exists(probe)) File.Delete(probe); } catch { } }
        return "-c:v libx264 -preset fast -crf 19";
    }

    static void ValidateExport(string path, double expectedDuration)
    {
        if (!File.Exists(path) || new FileInfo(path).Length < 1024) throw new Exception("Export invalid: fișierul MP4 lipsește sau este gol.");
        var output = Run("ffprobe.exe", $"-v error -select_streams v:0 -show_entries stream=codec_name,width,height -show_entries format=duration -of default=noprint_wrappers=1 \"{path}\"", out var code); var durationLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(x => x.StartsWith("duration=", StringComparison.OrdinalIgnoreCase)); var durationOk = durationLine != null && double.TryParse(durationLine.AsSpan("duration=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out var actualDuration) && actualDuration >= expectedDuration - .25 && actualDuration <= expectedDuration + .75;
        if (code != 0 || !output.Contains("codec_name=h264", StringComparison.OrdinalIgnoreCase) || !output.Contains("width=1920", StringComparison.OrdinalIgnoreCase) || !output.Contains("height=1080", StringComparison.OrdinalIgnoreCase) || !durationOk) throw new Exception($"Export invalid: verificarea ffprobe a eșuat (durată așteptată {F(expectedDuration)}s).\n" + output);
    }

    static string F(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
    static void Ffmpeg(string args) { var t = Run("ffmpeg.exe", args, out var c); if (c != 0) throw new Exception("FFmpeg a oprit exportul.\n\n" + (t.Length > 1600 ? t[^1600..] : t)); }
    static string Run(string exe, string args, out int code)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Tools", exe); if (!File.Exists(path)) throw new FileNotFoundException("Lipsește " + exe, path);
        using var p = new Process(); p.StartInfo = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }; foreach (var arg in SplitArguments(args)) p.StartInfo.ArgumentList.Add(arg); var sb = new StringBuilder(); p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); }; p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); }; p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit(); code = p.ExitCode; lock (sb) return sb.ToString();
    }

    static IEnumerable<string> SplitArguments(string commandLine)
    {
        var current = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < commandLine.Length; i++) { var ch = commandLine[i]; if (ch == '"') { quoted = !quoted; continue; } if (char.IsWhiteSpace(ch) && !quoted) { if (current.Length > 0) { yield return current.ToString(); current.Clear(); } continue; } current.Append(ch); }
        if (quoted) throw new ArgumentException("Linie FFmpeg cu ghilimele neînchise."); if (current.Length > 0) yield return current.ToString();
    }

    void Window_PreviewDragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    async void Window_Drop(object sender, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] f) await ImportFilesAsync(f); }
}

public class MediaItem
{
    public string Path { get; set; } = "";
    public string Name => System.IO.Path.GetFileName(Path);
    public string Type { get; set; } = "";
    public DateTime SourceDate { get; set; }
    public double CaptureTimeOffsetSeconds { get; set; }
    public DateTime EffectiveDate => CaptureTimeOffset.Apply(SourceDate, CaptureTimeOffsetSeconds);
    public DateTime Date { get => EffectiveDate; set => SourceDate = value; }
    public double Duration { get; set; } = 4;
    public double TrimIn { get; set; }
    public bool Mute { get; set; }
    public ImageSource? Thumbnail { get; set; }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public void Changed(string n) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
}