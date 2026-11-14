using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mouser.Core.Clipboard;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public class SnippetTests
{
    private static readonly DateTime TestTime = new(2026, 11, 14, 15, 30, 45);

    [Fact]
    public void Expand_Date_Time_DateTime_ReplacedCorrectly()
    {
        var (text1, offset1) = SnippetExpander.Expand("Date: {{date}}", clipboard: "", now: TestTime);
        Assert.Equal("Date: 2026-11-14", text1);
        Assert.Null(offset1);

        var (text2, offset2) = SnippetExpander.Expand("Time: {{time}}", clipboard: "", now: TestTime);
        Assert.Equal("Time: 15:30", text2);
        Assert.Null(offset2);

        var (text3, offset3) = SnippetExpander.Expand("DateTime: {{datetime}}", clipboard: "", now: TestTime);
        Assert.Equal("DateTime: 2026-11-14 15:30", text3);
        Assert.Null(offset3);
    }

    [Fact]
    public void Expand_Clipboard_WithDifferentValues()
    {
        var (text1, _) = SnippetExpander.Expand("Clip: {{clipboard}}", clipboard: "hello world");
        Assert.Equal("Clip: hello world", text1);

        var (text2, _) = SnippetExpander.Expand("Clip: {{clipboard}}", clipboard: "");
        Assert.Equal("Clip: ", text2);

        var (text3, _) = SnippetExpander.Expand("Clip: {{clipboard}}", clipboard: null!);
        Assert.Equal("Clip: ", text3);
    }

    [Fact]
    public void Expand_Uuid_ProducesValidGuid()
    {
        var (text, offset) = SnippetExpander.Expand("ID: {{uuid}}", clipboard: "");
        Assert.StartsWith("ID: ", text);
        Assert.Null(offset);

        string guidPart = text.Substring(4);
        Assert.True(Guid.TryParse(guidPart, out var parsedGuid));
        Assert.NotEqual(Guid.Empty, parsedGuid);
    }

    [Fact]
    public void Expand_Prompt_CallsCallbackAndSubstitutesAnswerOrEmpty()
    {
        string? PromptCallback(string label) => label switch
        {
            "Name" => "Alice",
            "Missing" => null,
            _ => ""
        };

        var (text1, _) = SnippetExpander.Expand("Hello {{prompt:Name}}!", clipboard: "", prompt: PromptCallback);
        Assert.Equal("Hello Alice!", text1);

        var (text2, _) = SnippetExpander.Expand("Hello {{prompt:Missing}}!", clipboard: "", prompt: PromptCallback);
        Assert.Equal("Hello !", text2);

        // When prompt is null (no callback provided)
        var (text3, _) = SnippetExpander.Expand("Hello {{prompt:Name}}!", clipboard: "", prompt: null);
        Assert.Equal("Hello !", text3);
    }

    [Fact]
    public void Expand_CursorOffset_CalculatesCharactersAfterCursor()
    {
        var (text, offset) = SnippetExpander.Expand("Hello {{cursor}} world", clipboard: "");
        Assert.Equal("Hello  world", text);
        Assert.Equal(6, offset);
    }

    [Fact]
    public void Expand_CursorAtEnd_CursorOffsetIsZero()
    {
        var (text, offset) = SnippetExpander.Expand("Hello world{{cursor}}", clipboard: "");
        Assert.Equal("Hello world", text);
        Assert.Equal(0, offset);
    }

    [Fact]
    public void Expand_CursorAtStart_CursorOffsetIsTotalLength()
    {
        var (text, offset) = SnippetExpander.Expand("{{cursor}}Hello world", clipboard: "");
        Assert.Equal("Hello world", text);
        Assert.Equal(11, offset);
    }

    [Fact]
    public void Expand_MultipleCursors_UsesFirstForOffset()
    {
        var (text, offset) = SnippetExpander.Expand("A{{cursor}}B{{cursor}}C", clipboard: "");
        Assert.Equal("ABC", text);
        // First cursor is after 'A', with 'BC' following (length 2)
        Assert.Equal(2, offset);
    }

    [Fact]
    public void Expand_UnknownVariable_StaysLiteral()
    {
        var (text, offset) = SnippetExpander.Expand("{{foo}} and {{bar}}", clipboard: "");
        Assert.Equal("{{foo}} and {{bar}}", text);
        Assert.Null(offset);
    }

    [Fact]
    public void Expand_MultipleVariablesCombined()
    {
        var (text, offset) = SnippetExpander.Expand(
            "Logged on {{date}} at {{time}} by {{prompt:User}}: '{{clipboard}}' [ref: {{uuid}}] {{cursor}}End",
            clipboard: "copied text",
            prompt: s => s == "User" ? "Admin" : "",
            now: TestTime);

        Assert.Contains("Logged on 2026-11-14 at 15:30 by Admin: 'copied text' [ref: ", text);
        Assert.EndsWith(" End", text);
        Assert.Equal(3, offset); // "End" has 3 characters
    }

    [Fact]
    public void Expand_NoVariables_TextUnchangedAndCursorOffsetNull()
    {
        var (text, offset) = SnippetExpander.Expand("Plain text without variables.", clipboard: "some clip");
        Assert.Equal("Plain text without variables.", text);
        Assert.Null(offset);
    }

    [Fact]
    public void Expand_NestedAndUnclosedBraces_HandledGracefully()
    {
        // Unclosed opening braces
        var (text1, _) = SnippetExpander.Expand("Unclosed {{date here", clipboard: "", now: TestTime);
        Assert.Equal("Unclosed {{date here", text1);

        // Single braces
        var (text2, _) = SnippetExpander.Expand("Single {date} brace", clipboard: "", now: TestTime);
        Assert.Equal("Single {date} brace", text2);

        // Unmatched closing braces
        var (text3, _) = SnippetExpander.Expand("Unmatched }} here", clipboard: "", now: TestTime);
        Assert.Equal("Unmatched }} here", text3);

        // Triple braces {{{date}}}
        var (text4, _) = SnippetExpander.Expand("{{{date}}}", clipboard: "", now: TestTime);
        Assert.Equal("{2026-11-14}", text4);

        // Nested braces
        var (text5, _) = SnippetExpander.Expand("{{foo {{date}} bar}}", clipboard: "", now: TestTime);
        Assert.Equal("{{foo 2026-11-14 bar}}", text5);
    }

    [Fact]
    public void Expand_NullOrEmptyBody_ReturnsEmptyAndNullOffset()
    {
        var (t1, o1) = SnippetExpander.Expand("", clipboard: "test");
        Assert.Equal(string.Empty, t1);
        Assert.Null(o1);

        var (t2, o2) = SnippetExpander.Expand(null!, clipboard: "test");
        Assert.Equal(string.Empty, t2);
        Assert.Null(o2);
    }

    [Fact]
    public async Task Storage_SnippetCrud_SaveGetDelete_WorksCorrectly()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mouser_snippet_test_{Guid.NewGuid():N}.db");
        try
        {
            await using var storage = new ClipboardStorageService(dbPath);
            await storage.InitializeAsync();

            var snip1 = new Snippet
            {
                Name = "Greeting",
                Body = "Hello {{prompt:Name}}, welcome!",
                Folder = "Templates",
                IsFavorite = false
            };
            await storage.SaveSnippetAsync(snip1);

            var snip2 = new Snippet
            {
                Name = "Signature",
                Body = "Best regards,\nAbdelkarim",
                Folder = "Templates",
                IsFavorite = true
            };
            await storage.SaveSnippetAsync(snip2);

            var all = await storage.GetSnippetsAsync();
            Assert.Equal(2, all.Count);
            // Favorite should come first
            Assert.Equal("Signature", all[0].Name);
            Assert.True(all[0].IsFavorite);
            Assert.Equal("Greeting", all[1].Name);
            Assert.False(all[1].IsFavorite);

            // Lookup by name (case-insensitive)
            var found = await storage.GetSnippetByNameAsync("greeting");
            Assert.NotNull(found);
            Assert.Equal(snip1.Id, found.Id);
            Assert.Equal(snip1.Body, found.Body);

            // Update snippet
            snip1.Body = "Updated body";
            snip1.IsFavorite = true;
            await storage.SaveSnippetAsync(snip1);

            var updated = await storage.GetSnippetByNameAsync("Greeting");
            Assert.NotNull(updated);
            Assert.Equal("Updated body", updated.Body);
            Assert.True(updated.IsFavorite);

            // Delete snippet
            await storage.DeleteSnippetAsync(snip1.Id);
            var afterDelete = await storage.GetSnippetsAsync();
            Assert.Single(afterDelete);
            Assert.Equal("Signature", afterDelete[0].Name);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task QuickPaste_QueryStartingWithExclamation_SearchesSnippets()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mouser_qp_snippet_{Guid.NewGuid():N}.db");
        try
        {
            await using var storage = new ClipboardStorageService(dbPath);
            await storage.InitializeAsync();

            await storage.SaveSnippetAsync(new Snippet { Name = "Greeting", Body = "Hello there!" });
            await storage.SaveSnippetAsync(new Snippet { Name = "GitStatus", Body = "git status" });
            await storage.SaveClipAsync(new ClipboardPayload { TextContent = "Regular clipboard clip" });

            var paste = new QuickPasteViewModel(storage);

            // Normal search: shows clips
            paste.Query = "";
            await paste.LoadAsync();
            Assert.Single(paste.Items);
            Assert.Equal("Regular clipboard clip", paste.Items[0].Content);

            // ! query: shows snippets only
            paste.Query = "!";
            await paste.LoadAsync();
            Assert.Equal(2, paste.Items.Count);
            Assert.All(paste.Items, item => Assert.IsType<SnippetQuickPasteItemViewModel>(item));

            // ! with search term
            paste.Query = "!git";
            await paste.LoadAsync();
            var matched = Assert.Single(paste.Items);
            Assert.Equal("GitStatus", ((SnippetQuickPasteItemViewModel)matched).Snippet.Name);

            // ! with non-matching term
            paste.Query = "!nonexistent";
            await paste.LoadAsync();
            Assert.Empty(paste.Items);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task QuickPaste_PasteSnippet_ExpandsVariablesAndSetsCursorOffset()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mouser_qp_paste_{Guid.NewGuid():N}.db");
        try
        {
            await using var storage = new ClipboardStorageService(dbPath);
            await storage.InitializeAsync();

            await storage.SaveSnippetAsync(new Snippet
            {
                Name = "NoteTemplate",
                Body = "Note: {{clipboard}} {{cursor}} [end]"
            });

            var paste = new QuickPasteViewModel(storage)
            {
                CurrentClipboard = "my active clip",
                Query = "!Note"
            };

            await paste.LoadAsync();
            var item = Assert.Single(paste.Items);
            Assert.Same(item, paste.Selected);

            string? pastedText = null;
            bool? isRich = null;
            paste.PasteRequested += (text, rich) =>
            {
                pastedText = text;
                isRich = rich;
            };

            paste.PasteSelected(false);

            Assert.Equal("Note: my active clip  [end]", pastedText);
            Assert.False(isRich);
            // " [end]" is 6 chars after the cursor placeholder
            Assert.Equal(6, paste.LastCursorOffset);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task SnippetsViewModel_LifecycleAndCommands()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mouser_vm_snippet_{Guid.NewGuid():N}.db");
        try
        {
            await using var storage = new ClipboardStorageService(dbPath);
            await storage.InitializeAsync();

            var vm = new SnippetsViewModel(storage);
            await vm.LoadSnippetsAsync();
            Assert.True(vm.IsEmpty);

            // Create new snippet
            await vm.NewSnippetCommand.ExecuteAsync(null);
            Assert.False(vm.IsEmpty);
            Assert.NotNull(vm.SelectedSnippet);
            Assert.Equal("New snippet", vm.SelectedSnippet.Name);

            // Insert variables
            vm.CaretIndex = 0;
            vm.InsertVariableCommand.Execute("{{date}}");
            Assert.Equal("{{date}}", vm.SelectedSnippet.Body);

            vm.CaretIndex = 8;
            vm.InsertVariableCommand.Execute(" {{time}}");
            Assert.Equal("{{date}} {{time}}", vm.SelectedSnippet.Body);

            // Edit name and save
            vm.SelectedSnippet.Name = "Custom Date Snippet";
            await vm.SaveSnippetCommand.ExecuteAsync(null);

            var inDb = await storage.GetSnippetByNameAsync("Custom Date Snippet");
            Assert.NotNull(inDb);
            Assert.Equal("{{date}} {{time}}", inDb.Body);

            // Search filter
            vm.SearchText = "Custom";
            await Task.Delay(20);
            Assert.Single(vm.Snippets);

            vm.SearchText = "NotMatchingAnything";
            await Task.Delay(20);
            Assert.Empty(vm.Snippets);

            vm.SearchText = "";
            await Task.Delay(20);
            Assert.Single(vm.Snippets);

            // Delete
            await vm.DeleteSnippetCommand.ExecuteAsync(null);
            Assert.True(vm.IsEmpty);
            Assert.Null(vm.SelectedSnippet);

            var remaining = await storage.GetSnippetsAsync();
            Assert.Empty(remaining);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task MainViewModel_Navigation_SnippetsTab()
    {
        var app = new AppFixture();
        await using var _ = app;

        var vm = app.Main;
        Assert.False(vm.IsSnippetsTab);

        vm.NavigateCommand.Execute("Snippets");

        Assert.Equal(NavigationTab.Snippets, vm.CurrentTab);
        Assert.True(vm.IsSnippetsTab);
        Assert.Same(vm.Snippets, vm.CurrentPage);
    }
}
