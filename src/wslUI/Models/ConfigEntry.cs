using wslUI.Infrastructure;

namespace wslUI.Models;

public enum ConfigKind { Bool, Text, Number, Size, Choice, Path }

/// <summary>One known setting in .wslconfig or wsl.conf. A null Value means "not set" (WSL default applies).</summary>
public sealed class ConfigEntry : ObservableObject
{
    public const string DefaultLabel = "Default";

    public ConfigEntry(string section, string key, ConfigKind kind, string description, string? example = null, params string[] choices)
    {
        Section = section;
        Key = key;
        Kind = kind;
        Description = description;
        Example = example ?? "";
        Choices = kind == ConfigKind.Bool
            ? [DefaultLabel, "true", "false"]
            : [DefaultLabel, .. choices];
    }

    public string Section { get; }
    public string Key { get; }
    public ConfigKind Kind { get; }
    public string Description { get; }
    public string Example { get; }
    public string[] Choices { get; }

    /// <summary>Shown inside an empty text box: makes clear that empty means "WSL default".</summary>
    public string Placeholder => Example.Length > 0 ? $"Default  ·  e.g. {Example}" : "Default";

    public bool IsChoice => Kind is ConfigKind.Bool or ConfigKind.Choice;
    public bool IsText => !IsChoice;
    public bool IsPath => Kind == ConfigKind.Path;

    /// <summary>Raised when the user edits the value.</summary>
    public event Action? Changed;

    private string? _value;
    public string? Value
    {
        get => _value;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? null : value;
            if (!Set(ref _value, v)) return;
            OnPropertyChanged(nameof(ChoiceValue));
            OnPropertyChanged(nameof(IsSet));
            Changed?.Invoke();
        }
    }

    public bool IsSet => _value is not null;

    /// <summary>Value for the ComboBox (maps null to "(default)").</summary>
    public string ChoiceValue
    {
        get => _value is null ? DefaultLabel : (Choices.FirstOrDefault(c => c.Equals(_value, StringComparison.OrdinalIgnoreCase)) ?? _value);
        set => Value = value == DefaultLabel ? null : value;
    }

    /// <summary>Sets the value without raising <see cref="Changed"/> (used when loading).</summary>
    public void Load(string? value)
    {
        _value = string.IsNullOrWhiteSpace(value) ? null : value;
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(ChoiceValue));
        OnPropertyChanged(nameof(IsSet));
    }
}
