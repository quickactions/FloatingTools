using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(FloatingTools.Tests.WpfResourceCollection.Name)]
public sealed class SelectableTextRuntimeTests
{
    [Theory]
    [InlineData("Translation", "SelectableEntryTextStyle")]
    [InlineData("QuickChat", "QuickChatSelectableMessageTextStyle")]
    public void ReadOnlyStyle_BlocksEditingPreservesWrappingAndSupportsCopy(
        string tool,
        string styleKey)
        => WpfTestApplication.Run(() =>
        {
            var style = ResolveStyle(tool, styleKey);
            var textBox = new TextBox { Style = style, Width = 220 };
            textBox.SetCurrentValue(TextBox.TextProperty, WrappingText);

            var window = new Window
            {
                Content = textBox,
                Width = 240,
                Height = 200,
                Left = -10_000,
                Top = -10_000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                // No editable-looking chrome; layout stays plain.
                Assert.True(textBox.IsReadOnly);
                Assert.False(textBox.IsReadOnlyCaretVisible);
                Assert.Equal(TextWrapping.Wrap, textBox.TextWrapping);
                Assert.Equal(new Thickness(0), textBox.BorderThickness);
                Assert.Equal(new Thickness(0), textBox.Padding);
                Assert.Equal(ScrollBarVisibility.Disabled, textBox.VerticalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Disabled, textBox.HorizontalScrollBarVisibility);

                // Wrapping/layout preserved: known-long text spans multiple lines.
                Assert.True(textBox.LineCount > 1,
                    $"Expected wrapped text to span multiple lines, got {textBox.LineCount}.");

                // Read-only: native editing commands are no-ops, source text never mutates.
                var originalText = textBox.Text;
                textBox.Focus();
                textBox.CaretIndex = 3;
                EditingCommands.Backspace.Execute(null, textBox);
                EditingCommands.Delete.Execute(null, textBox);
                EditingCommands.EnterParagraphBreak.Execute(null, textBox);
                Assert.Equal(originalText, textBox.Text);

                // Mouse-drag-equivalent selection plus Ctrl+C still works on read-only text.
                textBox.Select(0, 5);
                Assert.Equal(5, textBox.SelectionLength);
                Assert.Equal(originalText[..5], textBox.SelectedText);
                Assert.True(ApplicationCommands.Copy.CanExecute(null, textBox));
                ApplicationCommands.Copy.Execute(null, textBox);
                Assert.Equal(textBox.SelectedText, Clipboard.GetText());
            }
            finally
            {
                window.Close();
            }
        });

    private static Style ResolveStyle(string tool, string styleKey)
    {
        UserControl view = tool switch
        {
            "Translation" => new TranslationToolView(),
            "QuickChat" => new QuickChatToolView(),
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
        };

        return (Style)view.Resources[styleKey];
    }

    private const string WrappingText =
        "This is a long line of message or translation text intended to wrap across "
        + "several visual lines so wrapping and read-only selection can be verified reliably.";
}
