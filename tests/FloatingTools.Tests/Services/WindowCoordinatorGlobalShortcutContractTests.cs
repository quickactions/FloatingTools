namespace FloatingTools.Tests.Services;

public sealed class WindowCoordinatorGlobalShortcutContractTests
{
    [Fact]
    public void ExitWorkflow_IncludesGlobalHotkeyCleanupStep()
    {
        var code = ReadWindowCoordinatorSource();

        Assert.Contains("\"Global hotkey cleanup\"", code);
        Assert.Contains("_globalHotkeyService.Dispose()", code);
    }

    [Fact]
    public void ShowToolbar_RegistersGlobalHotkeysWithCorrectIdsKeysAndCallbacks()
    {
        var code = ReadWindowCoordinatorSource();
        var showToolbarBody = ExtractMethodBody(code, "public void ShowToolbar()");
        Assert.Contains("EnsureGlobalHotkeysRegistered();", showToolbarBody);

        var registerBody = ExtractMethodBody(
            code, "private void EnsureGlobalHotkeysRegistered()");
        Assert.Contains("if (_globalHotkeysRegistered)", registerBody);
        Assert.Contains("ShowHideHotkeyId", registerBody);
        Assert.Contains("ExtractTextHotkeyId", registerBody);
        Assert.Contains("Key.H", registerBody);
        Assert.Contains("Key.T", registerBody);
        Assert.Contains("ModifierKeys.Control | ModifierKeys.Alt", registerBody);
        Assert.Contains("ToggleApplicationVisibility", registerBody);
        Assert.Contains("TriggerExtractTextFromScreen", registerBody);
    }

    [Fact]
    public void HotkeyIds_AreDistinct()
    {
        var code = ReadWindowCoordinatorSource();

        Assert.Contains("internal const int ShowHideHotkeyId = 1;", code);
        Assert.Contains("internal const int ExtractTextHotkeyId = 2;", code);
    }

    [Theory]
    [InlineData("private void ToggleApplicationVisibility()")]
    [InlineData("private void HideApplicationVisibility()")]
    [InlineData("private void RestoreApplicationVisibility()")]
    [InlineData("private async void TriggerExtractTextFromScreen()")]
    public void VisibilityAndCaptureMethods_NeverMutatePanelStateOrReachTheExitPath(
        string methodSignature)
    {
        var code = ReadWindowCoordinatorSource();
        var body = ExtractMethodBody(code, methodSignature);

        Assert.DoesNotContain("PanelState =", body);
        Assert.DoesNotContain("CloseAll(", body);
        Assert.DoesNotContain("RequestExitAsync(", body);
        Assert.DoesNotContain("Shutdown(", body);
        Assert.DoesNotContain("ExitRequested", body);
    }

    [Fact]
    public void ExtractTextHotkey_RoutesToTheExistingCaptureTextCommandWithoutDuplicatingCaptureOrOcr()
    {
        var code = ReadWindowCoordinatorSource();
        var body = ExtractMethodBody(code, "private async void TriggerExtractTextFromScreen()");

        Assert.Contains(
            "_translationToolViewModel.CaptureTextCommand.ExecuteAsync(null)", body);
        Assert.Contains(
            "_viewModel.SelectToolCommand.Execute(ToolId.Translation)", body);
        Assert.DoesNotContain("IScreenTextCaptureService", body);
        Assert.DoesNotContain("ScreenCaptureOverlayWindow", body);
        Assert.DoesNotContain("ILocalOcrService", body);
    }

    [Fact]
    public void ExtractTextHotkey_OnlyShowsTranslationWhenCaptureAppliedUsefulText()
    {
        var code = ReadWindowCoordinatorSource();
        var body = ExtractMethodBody(code, "private async void TriggerExtractTextFromScreen()");

        var guardIndex = body.IndexOf(
            "LastCaptureProducedText", StringComparison.Ordinal);
        var selectIndex = body.IndexOf(
            "_viewModel.SelectToolCommand.Execute(ToolId.Translation)",
            StringComparison.Ordinal);

        Assert.True(guardIndex >= 0, "Captures without applied text must be guarded.");
        Assert.True(selectIndex >= 0, "A useful capture still opens Translation.");
        Assert.True(
            guardIndex < selectIndex,
            "The useful-text guard must run before the tool switch.");
        Assert.DoesNotContain("LastCaptureStatus", body);
        Assert.Contains("if (_visibilitySession.IsHidden)", body);
        Assert.Contains("_capturePanelWasVisible = true;", body);
        Assert.Contains("if (!_restoreCapturePanelOnNormal)", body);
        Assert.Contains("ShowPanel();", body);
    }

    [Fact]
    public void HideApplicationVisibility_HidesBothWindowsAndRemembersPanelVisibility()
    {
        var code = ReadWindowCoordinatorSource();
        var body = ExtractMethodBody(code, "private void HideApplicationVisibility()");

        Assert.Contains("_visibilitySession.Hide(PanelWindow.IsVisible)", body);
        Assert.Contains("ToolbarWindow.MinimizeUi()", body);
        Assert.DoesNotContain("ToolbarWindow.Hide()", body);
    }

    [Fact]
    public void RestoreApplicationVisibility_RestoresPanelOnlyWhenItWasVisibleBeforeHide()
    {
        var code = ReadWindowCoordinatorSource();
        var body = ExtractMethodBody(code, "private void RestoreApplicationVisibility()");

        Assert.Contains("_visibilitySession.Show()", body);
        Assert.Contains("_visibilitySession.PanelWasVisibleBeforeHide", body);
        Assert.Contains("ShowPanel();", body);
        Assert.Contains("ShowToolbar();", body);
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
