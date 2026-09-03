namespace FloatingTools.Tests.Views;

public sealed class CalendarStageOneHeadlessContractTests
{
    [Fact]
    public void StageOneInfrastructure_RemainsIndependentFromStageTwoPresentation()
    {
        var root = FindSolutionRoot();
        var app = Path.Combine(root, "src", "FloatingTools.App");
        var stageOneFiles = new[]
        {
            Path.Combine(app, "Models", "CalendarEntry.cs"),
            Path.Combine(app, "Models", "CalendarHoliday.cs"),
            Path.Combine(app, "Models", "CalendarSettings.cs"),
            Path.Combine(app, "Models", "CalendarState.cs"),
            Path.Combine(app, "Services", "CalendarDateParser.cs"),
            Path.Combine(app, "Services", "CalendarEntrySearchService.cs"),
            Path.Combine(app, "Services", "CalendarLanguageResolver.cs"),
            Path.Combine(app, "Services", "HebrewCalendarHolidayProvider.cs"),
            Path.Combine(app, "Services", "ICalendarStore.cs"),
            Path.Combine(app, "Services", "IHolidayProvider.cs"),
            Path.Combine(app, "Services", "JsonCalendarStore.cs")
        };

        Assert.All(stageOneFiles, path => Assert.True(File.Exists(path), path));
        var stageOneSource = string.Join(
            Environment.NewLine,
            stageOneFiles.Select(File.ReadAllText));
        Assert.DoesNotContain("FloatingTools.App.Views", stageOneSource);
        Assert.DoesNotContain("FloatingTools.App.ViewModels", stageOneSource);
        Assert.DoesNotContain("System.Windows.Controls", stageOneSource);
    }

    private static string FindSolutionRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return directory.FullName;
            }
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }
}
