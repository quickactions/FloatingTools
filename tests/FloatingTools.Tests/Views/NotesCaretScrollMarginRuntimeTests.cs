using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FloatingTools.App.Controls;
using FloatingTools.App.Models;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class NotesCaretScrollMarginRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaretAtBottom_KeepsBottomMarginInEnglishAndHebrew(bool hebrew) =>
        RunWithLongNote(hebrew, (_, content, viewer, editor) =>
        {
            editor.CaretIndex = editor.Text.Length;
            RequestCaret(editor);
            DrainLayout();

            var caret = GetCaretInContent(editor, content);
            Assert.True(
                caret.Bottom - viewer.VerticalOffset
                    <= viewer.ViewportHeight - CaretScrollMarginBehavior.DefaultBottomMargin + 0.75,
                $"Caret bottom {caret.Bottom - viewer.VerticalOffset:F2}, viewport {viewer.ViewportHeight:F2}.");
        });

    [Fact]
    public void CaretMovedNearTop_KeepsTopMargin() =>
        RunWithLongNote(false, (_, content, viewer, editor) =>
        {
            viewer.ScrollToEnd();
            DrainLayout();
            editor.CaretIndex = editor.GetCharacterIndexFromLineIndex(1);
            RequestCaret(editor);
            DrainLayout();

            var caret = GetCaretInContent(editor, content);
            Assert.True(
                caret.Top - viewer.VerticalOffset
                    >= CaretScrollMarginBehavior.DefaultTopMargin - 0.75,
                $"Caret top {caret.Top - viewer.VerticalOffset:F2}.");
        });

    [Fact]
    public void CaretAlreadyComfortable_DoesNotChangeOffset() =>
        RunWithLongNote(false, (_, content, viewer, editor) =>
        {
            viewer.ScrollToVerticalOffset(Math.Min(100, viewer.ScrollableHeight));
            DrainLayout();
            var desiredTop = viewer.VerticalOffset + 40;
            editor.CaretIndex = FindCaretNearestContentTop(editor, content, desiredTop);
            var before = viewer.VerticalOffset;

            RequestCaret(editor);
            DrainLayout();

            Assert.Equal(before, viewer.VerticalOffset, 3);
        });

    private static void RunWithLongNote(
        bool hebrew,
        Action<NotesToolView, Grid, ScrollViewer, NoteDirectionalTextBox> assertion)
    {
        WpfTestApplication.Run(() =>
        {
            var line = hebrew ? "שורת טקסט ארוכה לבדיקת גלילת הסמן" : "A long line of text for caret scrolling";
            var text = string.Join(Environment.NewLine, Enumerable.Repeat(line, 80));
            var view = new NotesToolView
            {
                DataContext = new BodyContext
                {
                    ActiveBlocks = [new TextNoteBlock { Text = text }]
                }
            };
            var window = new Window
            {
                Content = view,
                Width = 340,
                Height = 320,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Left = -10000,
                Top = -10000
            };
            try
            {
                window.Show();
                DrainLayout();
                var content = Descendants(view).OfType<Grid>()
                    .Single(element => element.Name == "NotePageContentGrid");
                var viewer = Descendants(view).OfType<ScrollViewer>()
                    .Single(element => element.Name == "NotePageScrollViewer");
                var editor = Descendants(view).OfType<NoteDirectionalTextBox>()
                    .Single(element => element.Name == "TextBlockEditor");
                Assert.True(viewer.ScrollableHeight > 0);

                assertion(view, content, viewer, editor);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RequestCaret(TextBox editor)
    {
        var rect = editor.GetRectFromCharacterIndex(editor.CaretIndex, true);
        editor.BringIntoView(rect);
    }

    private static Rect GetCaretInContent(TextBox editor, Visual content)
    {
        var rect = editor.GetRectFromCharacterIndex(editor.CaretIndex, true);
        return editor.TransformToAncestor(content).TransformBounds(rect);
    }

    private static int FindCaretNearestContentTop(TextBox editor, Visual content, double desiredTop)
    {
        var bestIndex = 0;
        var bestDistance = double.MaxValue;
        for (var line = 0; line < editor.LineCount; line++)
        {
            var index = editor.GetCharacterIndexFromLineIndex(line);
            var top = GetCaretInContentAt(editor, content, index).Top;
            var distance = Math.Abs(top - desiredTop);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static Rect GetCaretInContentAt(TextBox editor, Visual content, int index) =>
        editor.TransformToAncestor(content)
            .TransformBounds(editor.GetRectFromCharacterIndex(index, true));

    private static void DrainLayout() =>
        Dispatcher.CurrentDispatcher.Invoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => { }));

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    public sealed class BodyContext
    {
        public bool IsMenuOpen { get; set; }
        public string ActiveTitle => "Caret test";
        public TextNoteBlock[] ActiveBlocks { get; set; } = [];
    }
}
