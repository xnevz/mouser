using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Clipboard;

namespace Mouser.UI.ViewModels;

public partial class SnippetItemViewModel : ViewModelBase
{
    public Snippet Model { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _body = string.Empty;
    [ObservableProperty] private string _folder = string.Empty;
    [ObservableProperty] private bool _isFavorite;

    public string Id => Model.Id;
    public long CreatedUtc => Model.CreatedUtc;

    public string PreviewBody
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Body)) return "(Empty snippet)";
            var clean = Body.Replace("\r", "").Replace("\n", " ").Trim();
            return clean.Length > 60 ? clean[..60] + "…" : clean;
        }
    }

    public SnippetItemViewModel(Snippet model)
    {
        Model = model;
        _name = model.Name;
        _body = model.Body;
        _folder = model.Folder;
        _isFavorite = model.IsFavorite;
    }

    partial void OnNameChanged(string value)
    {
        Model.Name = value;
    }

    partial void OnBodyChanged(string value)
    {
        Model.Body = value;
        OnPropertyChanged(nameof(PreviewBody));
    }

    partial void OnFolderChanged(string value)
    {
        Model.Folder = value;
    }

    partial void OnIsFavoriteChanged(bool value)
    {
        Model.IsFavorite = value;
    }

    public void NotifyPreviewChanged()
    {
        OnPropertyChanged(nameof(PreviewBody));
    }

    public Snippet ToModel()
    {
        return new Snippet
        {
            Id = Id,
            Name = Name,
            Body = Body,
            Folder = Folder,
            IsFavorite = IsFavorite,
            CreatedUtc = CreatedUtc
        };
    }
}

public partial class SnippetsViewModel : ViewModelBase
{
    private readonly ClipboardStorageService _storage;

    public ObservableCollection<SnippetItemViewModel> Snippets { get; } = new();

    [ObservableProperty]
    private SnippetItemViewModel? _selectedSnippet;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _caretIndex;

    public bool IsEmpty => Snippets.Count == 0;

    public SnippetsViewModel(ClipboardStorageService storage)
    {
        _storage = storage;
        _ = LoadSnippetsAsync();
    }

    partial void OnSearchTextChanged(string value) => _ = FilterSnippetsAsync();

    public async Task LoadSnippetsAsync()
    {
        var list = await _storage.GetSnippetsAsync();
        Snippets.Clear();
        foreach (var s in list)
        {
            Snippets.Add(new SnippetItemViewModel(s));
        }
        SelectedSnippet = Snippets.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task FilterSnippetsAsync()
    {
        var all = await _storage.GetSnippetsAsync();
        var selectedId = SelectedSnippet?.Id;
        Snippets.Clear();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? all
            : all.Where(s => s.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                             s.Body.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var s in filtered)
        {
            Snippets.Add(new SnippetItemViewModel(s));
        }
        SelectedSnippet = Snippets.FirstOrDefault(s => s.Id == selectedId) ?? Snippets.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    public async Task NewSnippetAsync()
    {
        int n = 1;
        string name = "New snippet";
        while (await _storage.GetSnippetByNameAsync(name) != null)
        {
            n++;
            name = $"New snippet {n}";
        }

        var snippet = new Snippet
        {
            Name = name,
            Body = string.Empty
        };

        await _storage.SaveSnippetAsync(snippet);
        var item = new SnippetItemViewModel(snippet);
        Snippets.Insert(0, item);
        SelectedSnippet = item;
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    public async Task DeleteSnippetAsync()
    {
        if (SelectedSnippet == null) return;
        var toDelete = SelectedSnippet;
        await _storage.DeleteSnippetAsync(toDelete.Id);
        Snippets.Remove(toDelete);
        SelectedSnippet = Snippets.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    public async Task SaveSnippetAsync()
    {
        if (SelectedSnippet == null) return;
        var model = SelectedSnippet.ToModel();
        await _storage.SaveSnippetAsync(model);
        SelectedSnippet.NotifyPreviewChanged();
    }

    [RelayCommand]
    public void InsertVariable(string? varToken)
    {
        if (SelectedSnippet == null || string.IsNullOrEmpty(varToken)) return;

        var body = SelectedSnippet.Body ?? string.Empty;
        if (CaretIndex >= 0 && CaretIndex <= body.Length)
        {
            SelectedSnippet.Body = body.Insert(CaretIndex, varToken);
            CaretIndex += varToken.Length;
        }
        else
        {
            SelectedSnippet.Body = body + varToken;
            CaretIndex = SelectedSnippet.Body.Length;
        }
    }
}
