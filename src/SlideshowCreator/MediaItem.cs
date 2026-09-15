using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SlideshowCreator;

public sealed class MediaItem : INotifyPropertyChanged
{
    public string Path { get; set; } = "";
    public string Name => System.IO.Path.GetFileName(Path);
    public string Type { get; set; } = "Foto";
    public DateTime Date { get; set; }
    public double Duration { get; set; } = 4;
    public double TrimIn { get; set; }
    public bool Mute { get; set; }

    // MVP 0.1.5 - non destructive slideshow composition metadata.
    // These values live in the project only; source media is never modified.
    string transition = "Crossfade";
    public string Transition { get => transition; set { transition = value; OnPropertyChanged(); } }

    double transitionDuration = 0.65;
    public double TransitionDuration { get => transitionDuration; set { transitionDuration = Math.Clamp(value, 0, 2.5); OnPropertyChanged(); } }

    int plan = 1;
    public int Plan { get => plan; set { plan = Math.Clamp(value, 1, 3); OnPropertyChanged(); } }

    string layout = "Full";
    public string Layout { get => layout; set { layout = value; OnPropertyChanged(); } }

    ImageSource? thumbnail;
    public ImageSource? Thumbnail { get => thumbnail; set { thumbnail = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void Changed(string? name = null) => OnPropertyChanged(name);
    void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
