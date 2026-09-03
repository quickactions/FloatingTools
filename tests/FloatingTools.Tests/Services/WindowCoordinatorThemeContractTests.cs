namespace FloatingTools.Tests.Services;

public sealed class WindowCoordinatorThemeContractTests
{
    [Fact]
    public void ExitWorkflow_IncludesThemeWatcherCleanupStep()
    {
        var code = ReadWindowCoordinatorSource();

        Assert.Contains("\"Theme watcher cleanup\"", code);
        Assert.Contains("_themeService.Dispose()", code);
    }

    [Fact]
    public void ShowToolbar_StartsTheThemeWatcherExactlyOnce()
    {
        var code = ReadWindowCoordinatorSource();
        var showToolbarBody = ExtractMethodBody(code, "public void ShowToolbar()");
        Assert.Contains("EnsureThemeWatcherStarted();", showToolbarBody);

        var startBody = ExtractMethodBody(code, "private void EnsureThemeWatcherStarted()");
        Assert.Contains("if (_themeWatcherStarted)", startBody);
        Assert.Contains("_themeService.StartLiveWatcher(handle)", startBody);
    }

    [Fact]
    public void EnsureThemeWatcherStarted_NeverMutatesPanelStateOrReachesTheExitPath()
    {
        var code = ReadWindowCoordinatorSource();
        var body = ExtractMethodBody(code, "private void EnsureThemeWatcherStarted()");

        Assert.DoesNotContain("PanelState =", body);
        Assert.DoesNotContain("CloseAll(", body);
        Assert.DoesNotContain("RequestExitAsync(", body);
        Assert.DoesNotContain("Shutdown(", body);
    }

    private static string ExtractMethodBody(string code, string methodSignature)
    {
        var signatureIndex = code.IndexOf(methodSignature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Method signature not found: {methodSignature}");

        var openBraceIndex = code.IndexOf('{', signatureIndex + methodSignature.Length);
        Assert.True(openBraceIndex >= 0, $"Opening brace not found for: {methodSignature}");

        var depth = 0;
        for (var index = openBraceIndex; index < code.Length; index++)
        {
            if (code[index] == '{')
            {
                depth++;
            }
            else if (code[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return code[openBraceIndex..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Unbalanced braces for method: {methodSignature}");
    }

    private static string ReadWindowCoordinatorSource() =>
        File.ReadAllText(FindSourcePath("Services", "WindowCoordinator.cs"));

    private static string FindSourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return Path.Combine(
                    [directory.FullName, "src", "FloatingTools.App", .. parts]);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }
}
