using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using FloatingTools.App.Views;

namespace FloatingTools.Tests.Views;

[Collection(WpfResourceCollection.Name)]
public sealed class TextEditorScrollBarRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Composer_UsesSharedMinimalScrollbarWithoutChangingEditing(bool quickChat) =>
        WpfTestApplication.Run(() =>
        {
            UserControl view = quickChat ? new QuickChatToolView() : new TranslationToolView();
            var window = new Window
            {
                Content = view,
                Width = 340,
                Height = 380,
                Left = -10000,
                Top = -10000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                var editor = Descendants(view).OfType<TextBox>()
                    .Single(item => item.Name == "ComposerTextBox");
                editor.Text = string.Join(Environment.NewLine,
                    Enumerable.Repeat("A long editable composer line.", 30));
                window.UpdateLayout();

                var scrollbar = Descendants(editor).OfType<ScrollBar>()
                    .Single(item => item.Orientation == Orientation.Vertical);
                var shared = (Style)Application.Current.FindResource(
                    "FloatingToolsSharedMinimalScrollBarStyle");
                Assert.Same(shared, scrollbar.Style?.BasedOn);
                Assert.Equal(ScrollBarVisibility.Auto, editor.VerticalScrollBarVisibility);
                Assert.True(scrollbar.IsVisible);
                Assert.NotNull(Descendants(scrollbar).OfType<Thumb>().SingleOrDefault());

                editor.CaretIndex = 4;
                editor.SelectedText = "X";
                Assert.Contains("A loXng", editor.Text);
                scrollbar.Value = Math.Min(10, scrollbar.Maximum);
                Assert.True(scrollbar.Value > 0);
            }
            finally
            {
                window.Close();
            }
        });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}