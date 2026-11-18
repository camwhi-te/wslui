using wslUI.Infrastructure;
using wslUI.Services;

namespace wslUI.ViewModels;

public abstract class PageViewModel : ObservableObject
{
    protected PageViewModel(MainViewModel main, string title, string glyph)
    {
        Main = main;
        Title = title;
        Glyph = glyph;
    }

    public MainViewModel Main { get; }
    public WslService Wsl => Main.Wsl;
    public string Title { get; }

    /// <summary>Segoe Fluent Icons glyph.</summary>
    public string Glyph { get; }

    public virtual void OnNavigatedTo() { }
}
