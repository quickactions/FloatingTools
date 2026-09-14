using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FloatingTools.App.Models;
using FloatingTools.App.Views;
using FloatingTools.Tests.ViewModels;

namespace FloatingTools.Tests.Views;

/// <summary>
/// Stage 1.5 runtime cover for the migrated Calendar menu Search, plus the
/// theme-resolution guard for the shared search family.
///
/// Coordinates are PHYSICAL: transformed through TransformToAncestor into the
/// CalendarToolView root, which is declared FlowDirection="LeftToRight".
/// Measuring inside the mirrored Hebrew subtree would report a wrong-edge
/// result as correct.
/// </summary>
[Collection(WpfResourceCollection.Name)]
public sealed class SearchControlFamilyRuntimeTests
{
    [Theory]
    [InlineData(CalendarLanguageMode.Hebrew)]
    [InlineData(CalendarLanguageMode.English)]
    public void CalendarMenuSearch_EmptyAndUnfocused_ShowsTheWatermark(
        CalendarLanguageMode language)
        => RunMenuSearch(language, (_, search, watermark, _) =>
        {
            Assert.Equal(string.Empty, search.Text);
            Assert.False(search.IsKeyboardFocused);
            Assert.Equal(Visibility.Visible, watermark.Visibility);

            // Calendar follows its UI language, not the typed content.
            Assert.Equal(
                language == CalendarLanguageMode.Hebrew
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight,
                search.FlowDirection);
            Assert.Equal(search.FlowDirection, watermark.FlowDirection);
        });

    [Theory]
    [InlineData(CalendarLanguageMode.Hebrew)]
    [InlineData(CalendarLanguageMode.English)]
    public void CalendarMenuSearch_Focused_HidesWatermarkAndPutsCaretOnTheLanguageEdge(
        CalendarLanguageMode language)
        => RunMenuSearch(language, (view, search, watermark, focusSearch) =>
        {
            focusSearch();

            Assert.Same(search, Keyboard.FocusedElement);
            Assert.Equal(Visibility.Collapsed, watermark.Visibility);
            AssertCaretOnLanguageEdge(view, search, language);
        });

    [Theory]
    [InlineData(CalendarLanguageMode.Hebrew)]
    [InlineData(CalendarLanguageMode.English)]
    public void CalendarMenuSearch_Typed_KeepsWatermarkHiddenAndCaretOnTheLanguageEdge(
        CalendarLanguageMode language)
        => RunMenuSearch(language, (view, search, watermark, focusSearch) =>
        {
            focusSearch();
            search.Text = language == CalendarLanguageMode.Hebrew ? "פגישה" : "meeting";
            Drain(view);

            Assert.Equal(Visibility.Collapsed, watermark.Visibility);
            AssertCaretOnLanguageEdge(view, search, language);
        });

    [Fact]
    public void SharedSearchStyle_ResolvesItsTokensInDarkAndLight()
        => WpfTestApplication.Run(() =>
        {
            var field = new TextBox
            {
                Style = (Style)Application.Current.FindResource("FloatingToolsSearchTextBoxStyle")
            };
            var window = new Window
            {
                Content = field,
                Width = 300,
                Height = 120,
                Left = -10_000,
                Top = -10_000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                Drain(field);

                var surfaces = new List<Color>();
                foreach (var theme in new[] { "Dark", "Light" })
                {
                    window.Resources.MergedDictionaries.Clear();
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri(
                            "pack://application:,,,/FloatingTools.App;component/"
                            + $"SharedUi/Tokens/Colors.{theme}.xaml")
                    });
                    Drain(field);

                    foreach (var token in new[]
                    {
                        "FloatingToolsBrushSurfaceControl",
                        "FloatingToolsBrushBorderInput",
                        "FloatingToolsBrushBorderDefault",
                        "FloatingToolsBrushForegroundMuted",
                    })
                    {
                        Assert.IsType<SolidColorBrush>(window.FindResource(token));
                    }

                    Assert.Equal(
                        ((SolidColorBrush)window.FindResource("FloatingToolsBrushForegroundPrimary")).Color,
                        Assert.IsType<SolidColorBrush>(field.Foreground).Color);
                    surfaces.Add(
                        ((SolidColorBrush)window.FindResource("FloatingToolsBrushSurfaceControl")).Color);
                }

                // If these matched, the style would not actually be theme-aware.
                Assert.NotEqual(surfaces[0], surfaces[1]);
            }
            finally
            {
                window.Close();
                Drain(field);
            }
        });

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
        Assert.True(right - left > 1, "The menu search field was not laid out.");

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

    private static void RunMenuSearch(
        CalendarLanguageMode language,
        Action<FrameworkElement, TextBox, TextBlock, Action> assert)
        => WpfTestApplication.Run(() =>
        {
            var viewModel = CalendarEventsStageTwoTests.CreateCalendar(language);
            viewModel.ToggleHeaderCommand.Execute(null);
            var view = new CalendarToolView { DataContext = viewModel };
            var window = new Window
            {
                Content = view,
                Width = 300,
                Height = 500,
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

                var search = (TextBox)view.FindName("CalendarSearchTextBox");
                Assert.True(search.IsVisible, "The Calendar menu search field was not visible.");
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
