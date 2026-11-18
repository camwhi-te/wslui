using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using wslUI.Infrastructure;
using wslUI.Models;
using wslUI.Services;

namespace wslUI.ViewModels;

/// <summary>
/// Shared form + raw-text editor for INI-style WSL config files. Unknown keys and comments are preserved.
/// </summary>
public abstract class ConfigEditorViewModel : PageViewModel
{
    private IniDocument _doc = IniDocument.Parse("");
    private bool _suppressDirty;

    protected ConfigEditorViewModel(MainViewModel main, string title, string glyph, List<ConfigEntry> schema) : base(main, title, glyph)
    {
        foreach (var e in schema)
        {
            e.Changed += () => { if (!_suppressDirty) IsDirty = true; };
            Entries.Add(e);
        }
        EntriesView = CollectionViewSource.GetDefaultView(Entries);
        EntriesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ConfigEntry.Section)));

        LoadCommand = new AsyncCommand(LoadAsync, CanLoad);
        SaveCommand = new AsyncCommand(SaveAsync, CanLoad);
        BrowsePathCommand = new RelayCommand(p =>
        {
            if (p is ConfigEntry e && Dialogs.OpenFile("All files|*.*") is { } file)
                e.Value = EscapePath(file);
        });
        ResetEntryCommand = new RelayCommand(p => { if (p is ConfigEntry e) e.Value = null; });
    }

    public ObservableCollection<ConfigEntry> Entries { get; } = [];
    public ICollectionView EntriesView { get; }

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand BrowsePathCommand { get; }
    public ICommand ResetEntryCommand { get; }

    public abstract string Subtitle { get; }
    public virtual bool IsDistroScoped => false;
    public abstract ICommand ApplyCommand { get; }
    public abstract string ApplyText { get; }

    protected virtual bool CanLoad() => true;

    /// <summary>Path values in .wslconfig need escaped backslashes.</summary>
    protected virtual string EscapePath(string path) => path;

    private string _rawText = "";
    public string RawText
    {
        get => _rawText;
        set { if (Set(ref _rawText, value) && !_suppressDirty) IsDirty = true; }
    }

    private int _tabIndex;
    /// <summary>0 = form, 1 = raw. Switching tabs syncs the two representations.</summary>
    public int TabIndex
    {
        get => _tabIndex;
        set
        {
            if (_tabIndex == value) return;
            if (value == 1) SetRaw(BuildFromForm());
            else LoadText(RawText, keepDirty: true);
            Set(ref _tabIndex, value);
        }
    }

    private bool _isDirty;
    public bool IsDirty { get => _isDirty; set => Set(ref _isDirty, value); }

    private string _status = "";
    public string Status { get => _status; set => Set(ref _status, value); }

    protected abstract Task<string?> ReadAsync();
    protected abstract Task WriteAsync(string text);
    protected virtual string Newline => "\n";

    private void SetRaw(string text)
    {
        _suppressDirty = true;
        RawText = text;
        _suppressDirty = false;
    }

    private void LoadText(string text, bool keepDirty = false)
    {
        var dirty = IsDirty;
        _suppressDirty = true;
        _doc = IniDocument.Parse(text);
        foreach (var e in Entries) e.Load(_doc.Get(e.Section, e.Key));
        RawText = text;
        _suppressDirty = false;
        IsDirty = keepDirty && dirty;
    }

    private string BuildFromForm()
    {
        foreach (var e in Entries) _doc.Set(e.Section, e.Key, e.Value);
        return _doc.ToString(Newline);
    }

    public async Task LoadAsync()
    {
        if (IsDirty && !Dialogs.Confirm("Discard unsaved changes and reload?")) return;
        var text = await ReadAsync();
        if (text is null) return;
        LoadText(text);
        IsDirty = false;
        Status = $"Loaded at {DateTime.Now:HH:mm:ss}";
    }

    private async Task SaveAsync()
    {
        var text = TabIndex == 0 ? BuildFromForm() : RawText;
        await WriteAsync(text);
        LoadText(text);
        IsDirty = false;
        Status = $"Saved at {DateTime.Now:HH:mm:ss}";
        await AfterSaveAsync();
    }

    protected virtual Task AfterSaveAsync() => Task.CompletedTask;
}
