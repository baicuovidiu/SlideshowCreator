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

    ImageSource? thumbnail;
    public ImageSource? Thumbnail { get => thumbnail; set { thumbnail = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void Changed(string? name = null) => OnPropertyChanged(name);
    void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
