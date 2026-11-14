using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mouser.Core.Clipboard;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public class TextTransformTests
{
    [Fact]
    public void Upper_TransformsCorrectly_AndHandlesNullEmpty()
    {
        Assert.Equal("HELLO WORLD", TextTransforms.Upper("hello world"));
        Assert.Equal("123 ABC", TextTransforms.Upper("123 abc"));
        Assert.Equal(string.Empty, TextTransforms.Upper(null));
        Assert.Equal(string.Empty, TextTransforms.Upper(string.Empty));
    }

    [Fact]
    public void Lower_TransformsCorrectly_AndHandlesNullEmpty()
    {
        Assert.Equal("hello world", TextTransforms.Lower("HELLO WORLD"));
        Assert.Equal("123 abc", TextTransforms.Lower("123 ABC"));
        Assert.Equal(string.Empty, TextTransforms.Lower(null));
        Assert.Equal(string.Empty, TextTransforms.Lower(string.Empty));
    }

    [Fact]
    public void TitleCase_TransformsCorrectly_AndHandlesNullEmpty()
    {
        Assert.Equal("Hello World", TextTransforms.TitleCase("hello world"));
        Assert.Equal("Hello World", TextTransforms.TitleCase("HELLO WORLD"));
        Assert.Equal("The Quick Brown Fox", TextTransforms.TitleCase("tHe qUiCk bRoWn fOx"));
        Assert.Equal(string.Empty, TextTransforms.TitleCase(null));
        Assert.Equal(string.Empty, TextTransforms.TitleCase(string.Empty));
    }

    [Fact]
    public void Trim_TransformsCorrectly_AndHandlesNullEmpty()
    {
        Assert.Equal("hello", TextTransforms.Trim("  hello  "));
        Assert.Equal("hello world", TextTransforms.Trim("\t hello world \r\n"));
        Assert.Equal(string.Empty, TextTransforms.Trim("   "));
        Assert.Equal(string.Empty, TextTransforms.Trim(null));
        Assert.Equal(string.Empty, TextTransforms.Trim(string.Empty));
    }

    [Fact]
    public void RemoveLineBreaks_CollapsesSpacesAndBreaks_AndHandlesNullEmpty()
    {
        Assert.Equal("hello world", TextTransforms.RemoveLineBreaks("hello\r\nworld"));
        Assert.Equal("hello world", TextTransforms.RemoveLineBreaks("hello\nworld"));
        Assert.Equal("hello world", TextTransforms.RemoveLineBreaks("hello\rworld"));
        Assert.Equal("hello world foo", TextTransforms.RemoveLineBreaks("hello\r\n\r\nworld\n\n\nfoo"));
        Assert.Equal("hello world", TextTransforms.RemoveLineBreaks("  hello   world  "));
        Assert.Equal("hello world", TextTransforms.RemoveLineBreaks("\r\n  hello \r\n  world \r\n"));
        Assert.Equal(string.Empty, TextTransforms.RemoveLineBreaks("   \r\n\n  "));
        Assert.Equal(string.Empty, TextTransforms.RemoveLineBreaks(null));
        Assert.Equal(string.Empty, TextTransforms.RemoveLineBreaks(string.Empty));
    }

    [Fact]
    public void SortLines_SortsOrdinalIgnoreCase_AndHandlesNullEmpty()
    {
        string input = "banana\nApple\ncherry\napple";
        string result = TextTransforms.SortLines(input);
        var lines = result.Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Equal("Apple", lines[0], ignoreCase: true);
        Assert.Equal("apple", lines[1], ignoreCase: true);
        Assert.Equal("banana", lines[2]);
        Assert.Equal("cherry", lines[3]);

        Assert.Equal("a\nb\nc", TextTransforms.SortLines("c\r\nb\ra"));
        Assert.Equal(string.Empty, TextTransforms.SortLines(null));
        Assert.Equal(string.Empty, TextTransforms.SortLines(string.Empty));
    }

    [Fact]
    public void DedupeLines_PreservesFirstOccurrenceOrder_AndHandlesNullEmpty()
    {
        string input = "banana\napple\nbanana\ncherry\napple";
        string result = TextTransforms.DedupeLines(input);
        Assert.Equal("banana\napple\ncherry", result);

        Assert.Equal("a\nb\nc", TextTransforms.DedupeLines("a\r\nb\r\na\r\nc\r\nb"));
        Assert.Equal(string.Empty, TextTransforms.DedupeLines(null));
        Assert.Equal(string.Empty, TextTransforms.DedupeLines(string.Empty));
    }

    [Fact]
    public void TryJsonPretty_ValidJson_ReturnsTrueAndIndented()
    {
        string compact = "{\"name\":\"mouser\",\"count\":42}";
        bool success = TextTransforms.TryJsonPretty(compact, out string formatted);
        Assert.True(success);
        Assert.Contains("\n", formatted);
        Assert.Contains("\"name\": \"mouser\"", formatted);

        bool nullSuccess = TextTransforms.TryJsonPretty(null, out string nullFormatted);
        Assert.False(nullSuccess);
        Assert.Equal(string.Empty, nullFormatted);
    }

    [Fact]
    public void TryJsonPretty_InvalidJson_ReturnsFalseAndOriginal()
    {
        string invalid = "not a json string";
        bool success = TextTransforms.TryJsonPretty(invalid, out string formatted);
        Assert.False(success);
        Assert.Equal(invalid, formatted);
    }

    [Fact]
    public void JsonPretty_ValidAndInvalidJson()
    {
        string json = "{\"a\":1}";
        string pretty = TextTransforms.JsonPretty(json);
        Assert.Contains("\n", pretty);
        Assert.Contains("\"a\": 1", pretty);

        string invalid = "{unquoted: key}";
        Assert.Equal(invalid, TextTransforms.JsonPretty(invalid));
        Assert.Equal(string.Empty, TextTransforms.JsonPretty(null));
        Assert.Equal(string.Empty, TextTransforms.JsonPretty(string.Empty));
    }

    [Fact]
    public void JsonMinify_ValidAndInvalidJson()
    {
        string indented = "{\n  \"name\": \"mouser\",\n  \"count\": 42\n}";
        string minified = TextTransforms.JsonMinify(indented);
        Assert.Equal("{\"name\":\"mouser\",\"count\":42}", minified);

        string invalid = "invalid json";
        Assert.Equal(invalid, TextTransforms.JsonMinify(invalid));
        Assert.Equal(string.Empty, TextTransforms.JsonMinify(null));
        Assert.Equal(string.Empty, TextTransforms.JsonMinify(string.Empty));
    }

    [Fact]
    public void UrlEncode_EncodesCorrectly_AndHandlesNullEmpty()
    {
        Assert.Equal("hello%20world", TextTransforms.UrlEncode("hello world"));
        Assert.Equal("a%26b%3Dc%23d", TextTransforms.UrlEncode("a&b=c#d"));
        Assert.Equal(string.Empty, TextTransforms.UrlEncode(null));
        Assert.Equal(string.Empty, TextTransforms.UrlEncode(string.Empty));
    }

    [Fact]
    public void UrlDecode_DecodesCorrectly_AndHandlesInvalid()
    {
        Assert.Equal("hello world", TextTransforms.UrlDecode("hello%20world"));
        Assert.Equal("a&b=c#d", TextTransforms.UrlDecode("a%26b%3Dc%23d"));
        Assert.Equal("invalid%2", TextTransforms.UrlDecode("invalid%2"));
        Assert.Equal(string.Empty, TextTransforms.UrlDecode(null));
        Assert.Equal(string.Empty, TextTransforms.UrlDecode(string.Empty));
    }

    [Fact]
    public void Base64Encode_EncodesCorrectly_AndHandlesNullEmpty()
    {
        Assert.Equal("SGVsbG8gV29ybGQ=", TextTransforms.Base64Encode("Hello World"));
        Assert.Equal(string.Empty, TextTransforms.Base64Encode(null));
        Assert.Equal(string.Empty, TextTransforms.Base64Encode(string.Empty));
    }

    [Fact]
    public void Base64Decode_DecodesCorrectly_AndHandlesInvalid()
    {
        Assert.Equal("Hello World", TextTransforms.Base64Decode("SGVsbG8gV29ybGQ="));
        Assert.Equal("not-valid-base64-!@#$", TextTransforms.Base64Decode("not-valid-base64-!@#$"));
        Assert.Equal(string.Empty, TextTransforms.Base64Decode(null));
        Assert.Equal(string.Empty, TextTransforms.Base64Decode(string.Empty));
    }

    [Fact]
    public void StripFormatting_ReturnsStringUnchanged_AndHandlesNull()
    {
        Assert.Equal("Plain text 123", TextTransforms.StripFormatting("Plain text 123"));
        Assert.Equal(string.Empty, TextTransforms.StripFormatting(null));
        Assert.Equal(string.Empty, TextTransforms.StripFormatting(string.Empty));
    }

    [Fact]
    public void All_ContainsAllExpectedTransforms()
    {
        var names = TextTransforms.All.Select(t => t.Name).ToList();
        Assert.Contains("Upper", names);
        Assert.Contains("Lower", names);
        Assert.Contains("TitleCase", names);
        Assert.Contains("Trim", names);
        Assert.Contains("RemoveLineBreaks", names);
        Assert.Contains("SortLines", names);
        Assert.Contains("DedupeLines", names);
        Assert.Contains("JsonPretty", names);
        Assert.Contains("JsonMinify", names);
        Assert.Contains("UrlEncode", names);
        Assert.Contains("UrlDecode", names);
        Assert.Contains("Base64Encode", names);
        Assert.Contains("Base64Decode", names);
        Assert.Contains("StripFormatting", names);
        Assert.Equal(14, TextTransforms.All.Count);

        // Every transform in All can execute on non-empty string without throwing
        foreach (var (name, apply) in TextTransforms.All)
        {
            var res = apply("sample text\nline 2");
            Assert.NotNull(res);
        }
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_ApplyTransform_UpdatesTransformResult_AndClears()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mouser_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var settings = new SettingsStore(Path.Combine(dir, "settings.json"));
        var storage = new ClipboardStorageService(Path.Combine(dir, "clipboard.db"));
        await storage.InitializeAsync();

        try
        {
            var vm = new ClipboardHistoryViewModel(storage, settings);
            var fakeClip = new FakeClipboard();
            vm.AttachClipboard(fakeClip);

            var payload = new ClipboardPayload
            {
                Id = "1",
                ContentType = ClipboardContentType.Text,
                TextContent = "hello world"
            };
            var item = new ClipboardItemViewModel(payload);
            vm.SelectedClip = item;

            Assert.False(vm.HasTransformResult);
            Assert.Null(vm.TransformResult);

            vm.ApplyTransformCommand.Execute("Upper");

            Assert.True(vm.HasTransformResult);
            Assert.Equal("HELLO WORLD", vm.TransformResult);

            // Copy result
            await vm.CopyTransformResultCommand.ExecuteAsync(null);
            Assert.Equal("HELLO WORLD", fakeClip.Text);
            Assert.Equal("Transformed text copied to clipboard.", vm.StatusMessage);

            // Clear transform
            vm.ClearTransformCommand.Execute(null);
            Assert.False(vm.HasTransformResult);
            Assert.Null(vm.TransformResult);

            // Changing selected clip clears transform result
            vm.ApplyTransformCommand.Execute("Lower");
            Assert.True(vm.HasTransformResult);
            vm.SelectedClip = null;
            Assert.False(vm.HasTransformResult);
            Assert.Null(vm.TransformResult);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public async Task QuickPasteViewModel_CycleTransform_CyclesAndPastesTransformedText()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mouser_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var storage = new ClipboardStorageService(Path.Combine(dir, "clipboard.db"));
        await storage.InitializeAsync();

        try
        {
            await storage.SaveClipAsync(new ClipboardPayload { TextContent = "hello quick paste" });

            var vm = new QuickPasteViewModel(storage);
            await vm.LoadAsync();
            Assert.NotNull(vm.Selected);

            string? pastedText = null;
            bool wasRich = false;
            vm.PasteRequested += (text, rich) =>
            {
                pastedText = text;
                wasRich = rich;
            };

            // Initially no transform active
            Assert.Null(vm.ActiveTransformName);
            vm.PasteSelected(false);
            Assert.Equal("hello quick paste", pastedText);

            // Cycle transform 1 step (to Upper)
            vm.CycleTransform(1);
            Assert.Equal("Upper", vm.ActiveTransformName);
            vm.PasteSelected(false);
            Assert.Equal("HELLO QUICK PASTE", pastedText);

            // Cycle transform backward
            vm.CycleTransform(-1);
            Assert.Null(vm.ActiveTransformName);
            vm.PasteSelected(false);
            Assert.Equal("hello quick paste", pastedText);

            // ApplyNextTransform helper
            vm.ApplyNextTransform();
            Assert.Equal("Upper", vm.ActiveTransformName);
            vm.ApplyNextTransform();
            Assert.Equal("Lower", vm.ActiveTransformName);
            vm.PasteSelected(false);
            Assert.Equal("hello quick paste", pastedText);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public async Task QuickPasteViewModel_Snippet_AppliesActiveTransform()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mouser_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var storage = new ClipboardStorageService(Path.Combine(dir, "clipboard.db"));
        await storage.InitializeAsync();

        try
        {
            await storage.SaveSnippetAsync(new Snippet { Name = "Greeting", Body = "hello {{clipboard}}" });

            var vm = new QuickPasteViewModel(storage)
            {
                CurrentClipboard = "world"
            };
            vm.Query = "!Greeting";
            await vm.LoadAsync();
            Assert.NotNull(vm.Selected);

            string? pastedText = null;
            vm.PasteRequested += (text, rich) => pastedText = text;

            vm.CycleTransform(1); // Upper
            Assert.Equal("Upper", vm.ActiveTransformName);
            vm.PasteSelected(false);
            Assert.Equal("HELLO WORLD", pastedText);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_NonTextClip_IgnoresTransform()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mouser_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var settings = new SettingsStore(Path.Combine(dir, "settings.json"));
        var storage = new ClipboardStorageService(Path.Combine(dir, "clipboard.db"));
        await storage.InitializeAsync();

        try
        {
            var vm = new ClipboardHistoryViewModel(storage, settings);
            var fakeClip = new FakeClipboard();
            vm.AttachClipboard(fakeClip);

            var payload = new ClipboardPayload
            {
                Id = "img1",
                ContentType = ClipboardContentType.Image,
                TextContent = "Image 100x100"
            };
            var item = new ClipboardItemViewModel(payload);
            vm.SelectedClip = item;

            vm.ApplyTransformCommand.Execute("Upper");
            Assert.False(vm.HasTransformResult);
            Assert.Null(vm.TransformResult);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void TextTransforms_NullAndEmpty_AllSafelyHandled()
    {
        foreach (var (name, apply) in TextTransforms.All)
        {
            var nullRes = apply(null!);
            Assert.NotNull(nullRes);

            var emptyRes = apply(string.Empty);
            Assert.NotNull(emptyRes);
        }
    }
}
