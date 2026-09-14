using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.ViewModels;
using FloatingTools.App.Views;
using FloatingTools.Tests.ViewModels;

namespace FloatingTools.Tests.Views;

/// <summary>
/// Stage 1 runtime cover for the Full Events search watermark.
///
/// Every X value here is PHYSICAL: it is produced by transforming through
/// <see cref="System.Windows.Media.Visual.TransformToAncestor"/> into the
/// CalendarToolView root, which is declared FlowDirection="LeftToRight".
/// Measuring inside the mirrored Hebrew subtree instead would report a
/// wrong-edge result as correct, which is why the pre-existing direction
/// tests cannot distinguish the two cases.
/// </summary>
[Collection(WpfResourceCollection.Name)]
public sealed class CalendarWatermarkRuntimeTests
{
    [Theory]
    [InlineData(CalendarLanguageMode.Hebrew)]
    [InlineData(CalendarLanguageMode.English)]
    public void EmptyUnfocusedSearch_ShowsWatermarkThatInheritsTheFieldDirection(
        CalendarLanguageMode language)
        => RunEventsSearch(language, (view, search, watermark, _) =>
        {
            Assert.Equal(string.Empty, search.Text);
            Assert.False(search.IsKeyboardFocused);
            Assert.Equal(Visibility.Visible, watermark.Visibility);

            // The watermark must take direction from its templated parent
            // rather than declaring any of its own.
            Assert.Equal(search.FlowDirection, watermark.FlowDirection);
            Assert.Equal(
                language == CalendarLanguageMode.Hebrew
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight,
                watermark.FlowDirection);
        });

    [Theory]
    [InlineData(CalendarLanguageMode.Hebrew)]
    [InlineData(CalendarLanguageMode.English)]
    public void FocusingEmptySearch_HidesWatermarkAndLeavesTheCaretOnTheLanguageEdge(
        CalendarLanguageMode language)
        => RunEventsSearch(language, (view, search, watermark, focusSearch) =>
        {
            focusSearch();

            Assert.Same(search, Keyboard.FocusedElement);
            // The whole point of Stage 1: an empty focused field shows the
            // caret alone, so it cannot strike through the first glyph.
            Assert.Equal(Visibility.Collapsed, watermark.Visibility);
            AssertCaretOnLanguageEdge(view, search, language);
        });

    [Theory]
    [InlineData(CalendarLanguageMode.Hebrew)]
    [InlineData(CalendarLanguageMode.English)]
    public void TypingKeepsTheWatermarkHiddenAndTheCaretOnTheLanguageEdge(
        CalendarLanguageMode language)
        => RunEventsSearch(language, (view, search, watermark, focusSearch) =>
        {
            focusSearch();
            search.Text = language == CalendarLanguageMode.Hebrew ? "שלום" : "hello";
            Drain(view);

            Assert.Equal(Visibility.Collapsed, watermark.Visibility);
            AssertCaretOnLanguageEdge(view, search, language);
        });

    [Fact]
    public void FocusedCaretInset_IsSymmetricAcrossLanguagesAndNotDoublePadded()
    {
        var hebrew = MeasureFocusedCaretInset(CalendarLanguageMode.Hebrew);
        var english = MeasureFocusedCaretInset(CalendarLanguageMode.English);

        Assert.True(
            Math.Abs(hebrew - english) <= 1.0,
            $"Hebrew caret inset {hebrew:0.0} and English {english:0.0} should match.");

        // The field declares Padding="7,0,30,0". Applied once, the caret lands
        // roughly 9 dip inside the editing edge. Binding Padding to
        // PART_ContentHost's Margin as well applied it twice and pushed this to
        // 16 dip, which read as an unwanted gap on the Hebrew editing side.
        Assert.True(
            hebrew <= 12.0 && english <= 12.0,
            $"Caret inset should reflect one application of the 7 dip padding, "
            + $"got Hebrew {hebrew:0.0} / English {english:0.0}.");
    }

    private static double MeasureFocusedCaretInset(CalendarLanguageMode language)
    {
        var inset = double.NaN;
        RunEventsSearch(language, (view, search, _, focusSearch) =>
        {
            focusSearch();

            var surface = (System.Windows.Controls.Border)
                search.Template.FindName("InputSurface", search);
            var transform = surface.TransformToAncestor(view);
            var edgeA = transform.Transform(new Point(0, 0)).X;
            var edgeB = transform.Transform(new Point(surface.ActualWidth, 0)).X;
            var stroke = surface.BorderThickness.Left;
            var innerLeft = Math.Min(edgeA, edgeB) + stroke;
            var innerRight = Math.Max(edgeA, edgeB) - stroke;

            var caret = search.GetRectFromCharacterIndex(0);
            var caretX = search.TransformToAncestor(view)
                .Transform(new Point(caret.X, caret.Y)).X;

            inset = language == CalendarLanguageMode.Hebrew
                ? innerRight - caretX
                : caretX - innerLeft;
        });

        Assert.False(double.IsNaN(inset), "The caret inset was not measured.");
        return inset;
    }

    private static void AssertCaretOnLanguageEdge(
        FrameworkElement view,
        TextBox search,
        CalendarLanguageMode language)
    {
        var transform = search.TransformToAncestor(view);
        var edgeA = transform.Transform(new Point(0, 0)).X;
        var edgeB = transform.Transform(new Point(search.ActualWidth, 0)).X;
        var left = Math.Min(edgeA, edgeB);
        var right = Math.Max(edgeA, edgeB);
        Assert.True(right - left > 1, "The search field was not laid out.");

        var caret = search.GetRectFromCharacterIndex(0);
        var caretX = transform.Transform(new Point(caret.X, caret.Y)).X;
        var ratio = (caretX - left) / (right - left);

        if (language == CalendarLanguageMode.Hebrew)
        {
            Assert.True(
                ratio > 0.66,
                $"Hebrew caret should sit on the physical right edge; ratio was {ratio:0.000}.");
        }
        else
        {
            Assert.True(
                ratio < 0.34,
                $"English caret should sit on the physical left edge; ratio was {ratio:0.000}.");
        }
    }

    private static void RunEventsSearch(
        CalendarLanguageMode language,
        Action<FrameworkElement, TextBox, TextBlock, Action> assert)
        => WpfTestApplication.Run(() =>
        {
            var viewModel = CalendarEventsStageTwoTests.CreateCalendar(language);
            viewModel.OpenEventsCommand.Execute(null);
            var view = new CalendarToolView { DataContext = viewModel };
            // Wide enough that the responsive toolbar keeps the search field
            // visible without simulating the compact icon click.
            var window = new Window
            {
                Content = view,
                Width = 560,
                Height = 760,
                Left = -10_000,
                Top = -10_000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                Drain(view);

                var search = (TextBox)view.FindName("FullEventsSearchTextBox");
                Assert.True(search.IsVisible, "The Full Events search field was not visible.");
                var watermark = (TextBlock)search.Template.FindName("Watermark", search);
                Assert.NotNull(watermark);

                assert(view, search, watermark, () =>
                {
                    view.FocusSearch();
                    Drain(view);
                });
            }
            finally
            {
                window.Close();
                Drain(view);
            }
        });

    private static void Drain(FrameworkElement view)
    {
        Dispatcher.CurrentDispatcher.Invoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => { }));
        view.UpdateLayout();
    }
}
