using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using FloatingTools.App.Models;
using FloatingTools.App.Views;
using FloatingTools.Tests.ViewModels;

namespace FloatingTools.Tests.Views;

/// <summary>
/// Stage 2: with no day selected, Add event states what is needed instead of
/// opening a second date-entry surface.
/// </summary>
[Collection(WpfResourceCollection.Name)]
public sealed class CalendarAddEventHintTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData(CalendarLanguageMode.Hebrew, "בחר יום או תאריך כדי להוסיף אירוע")]
    [InlineData(CalendarLanguageMode.English, "Select a day or date to add an event")]
    public void HintTextFollowsTheCalendarUiLanguage(
        CalendarLanguageMode language, string expected)
    {
        var viewModel = CalendarEventsStageTwoTests.CreateCalendar(language);

        Assert.Equal(expected, viewModel.SelectDayToAddEventHint);
    }

    [Fact]
    public void HintTextRetranslatesWhenTheCalendarLanguageChanges()
    {
        var viewModel = CalendarEventsStageTwoTests.CreateCalendar(CalendarLanguageMode.English);
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.SelectDayToAddEventHint))
            {
                changed.Add(e.PropertyName);
            }
        };

        viewModel.SelectedLanguage = CalendarLanguageMode.Hebrew;

        // The label lives in ApplyLanguage's notify list; without it the hint
        // would silently keep the old language.
        Assert.NotEmpty(changed);
        Assert.Equal("בחר יום או תאריך כדי להוסיף אירוע", viewModel.SelectDayToAddEventHint);
    }

    [Fact]
    public void SelectingADayClearsTheHintAndRestoresNormalDayPanelBehaviour()
    {
        var viewModel = CalendarEventsStageTwoTests.CreateCalendar();
        var day = viewModel.DisplayedDate;

        viewModel.BeginQuickAddCommand.Execute(null);
        Assert.True(viewModel.IsAddEventHintVisible);

        viewModel.SelectDateCommand.Execute(day);

        Assert.False(viewModel.IsAddEventHintVisible);
        Assert.Equal(day, viewModel.SelectedDate);

        // The selected-day Add Event flow is untouched.
        viewModel.BeginAddEventCommand.Execute(null);
        Assert.True(viewModel.IsAddingEvent);
        Assert.True(viewModel.IsEventEditorOpen);
        Assert.False(viewModel.IsAddEventHintVisible);
    }

    [Fact]
    public void ClearingTheSelectionAlsoClearsTheHint()
    {
        var viewModel = CalendarEventsStageTwoTests.CreateCalendar();

        viewModel.BeginQuickAddCommand.Execute(null);
        Assert.True(viewModel.IsAddEventHintVisible);

        viewModel.ClearSelectionCommand.Execute(null);

        Assert.False(viewModel.IsAddEventHintVisible);
    }

    [Fact]
    public void OpeningContextualEventsIsUnaffectedByTheHint()
    {
        var viewModel = CalendarEventsStageTwoTests.CreateCalendar();

        viewModel.BeginQuickAddCommand.Execute(null);
        viewModel.OpenContextualEventsCommand.Execute(null);

        Assert.True(viewModel.IsContextualEventsOpen);
        Assert.NotNull(viewModel.ContextualEventsViewModel);
    }

    [Fact]
    public void NoDatePromptCommandsOrStateRemainOnTheViewModel()
    {
        var type = typeof(FloatingTools.App.ViewModels.CalendarToolViewModel);

        foreach (var removed in new[]
        {
            "IsQuickAddDatePromptOpen",
            "QuickAddDateText",
            "QuickAddValidationMessage",
            "QuickAddDatePrompt",
            "QuickAddDatePlaceholder",
            "ContinueQuickAddDateCommand",
            "CancelQuickAddDateCommand",
        })
        {
            Assert.Null(type.GetProperty(removed));
        }
    }

    [Fact]
    public void NoDatePromptControlsRemainInTheCalendarMarkup()
    {
        var document = XDocument.Load(FindSourcePath("Views/CalendarToolView.xaml"));
        var code = File.ReadAllText(FindSourcePath("Views/CalendarToolView.xaml.cs"));

        Assert.DoesNotContain(
            document.Descendants(),
            element => (string?)element.Attribute(X + "Name") == "QuickAddDateTextBox");
        foreach (var binding in new[]
        {
            "ContinueQuickAddDateCommand",
            "CancelQuickAddDateCommand",
            "QuickAddDateText",
            "QuickAddValidationMessage",
            "IsQuickAddDatePromptOpen",
        })
        {
            Assert.DoesNotContain(binding, document.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(binding, code, StringComparison.Ordinal);
        }

        var hint = document.Descendants(Presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(X + "Name") == "AddEventHintText");
        Assert.Equal("{Binding SelectDayToAddEventHint}", (string?)hint.Attribute("Text"));
        Assert.Equal(
            "{Binding IsAddEventHintVisible, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)hint.Attribute("Visibility"));
        // Theme-aware, and a readable instruction rather than a muted hint.
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushForegroundSecondary}",
            (string?)hint.Attribute("Foreground"));
    }

    [Fact]
    public void HintForegroundResolvesInDarkAndLight()
        => WpfTestApplication.Run(() =>
        {
            var viewModel = CalendarEventsStageTwoTests.CreateCalendar();
            viewModel.BeginQuickAddCommand.Execute(null);
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

                var hint = (TextBlock)view.FindName("AddEventHintText");
                Assert.True(hint.IsVisible);

                var colors = new List<Color>();
                foreach (var theme in new[] { "Dark", "Light" })
                {
                    window.Resources.MergedDictionaries.Clear();
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri(
                            "pack://application:,,,/FloatingTools.App;component/"
                            + $"SharedUi/Tokens/Colors.{theme}.xaml")
                    });
                    Drain(view);

                    var expected = (SolidColorBrush)window.FindResource(
                        "FloatingToolsBrushForegroundSecondary");
                    Assert.Equal(
                        expected.Color,
                        Assert.IsType<SolidColorBrush>(hint.Foreground).Color);
                    colors.Add(expected.Color);
                }

                Assert.NotEqual(colors[0], colors[1]);
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

    private static string FindSourcePath(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return Path.Combine(
                    directory.FullName,
                    "src",
                    "FloatingTools.App",
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }
}
