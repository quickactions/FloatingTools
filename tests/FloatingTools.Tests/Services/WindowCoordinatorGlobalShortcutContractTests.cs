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
        Assert.Contains("CaptureOnlyHotkeyId", registerBody);
        Assert.Contains("Key.H", registerBody);
        Assert.Contains("Key.T", registerBody);
        Assert.Contains("Key.C", registerBody);
        Assert.Contains("ModifierKeys.Control | ModifierKeys.Alt", registerBody);
        Assert.Contains("ToggleApplicationVisibility", registerBody);
        Assert.Contains("TriggerExtractTextFromScreen", registerBody);
        Assert.Contains("TriggerCaptureAndTranslateFromScreen", registerBody);
    }

    [Fact]
    public void HotkeyIds_AreDistinct()
    {
        var code = ReadWindowCoordinatorSource();

        Assert.Contains("internal const int ShowHideHotkeyId = 1;", code);
        Assert.Contains("internal const int ExtractTextHotkeyId = 2;", code);
        Assert.Contains("internal const int CaptureOnlyHotkeyId = 3;", code);
    }

    [Theory]
    [InlineData("private void ToggleApplicationVisibility()")]
    [InlineData("private void HideApplicationVisibility()")]
    [InlineData("private void RestoreApplicationVisibility()")]
    [InlineData("private async void TriggerExtractTextFromScreen()")]
    [InlineData("private async void TriggerCaptureAndTranslateFromScreen()")]
    [InlineData("private async Task CaptureFromHotkeyAsync(bool translate)")]
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
        var captureOnly = ExtractMethodBody(
            code, "private async void TriggerExtractTextFromScreen()");
        var captureAndTranslate = ExtractMethodBody(
            code, "private async void TriggerCaptureAndTranslateFromScreen()");
        var shared = ExtractMethodBody(
            code, "private async Task CaptureFromHotkeyAsync(bool translate)");

        Assert.Contains("CaptureFromHotkeyAsync(translate: false)", captureOnly);
        Assert.Contains("CaptureFromHotkeyAsync(translate: true)", captureAndTranslate);
        Assert.Contains("_translationToolViewModel.CaptureTextForShortcutAsync()", shared);
        Assert.Contains("_viewModel.SelectToolCommand.Execute(ToolId.Translation)", shared);
        Assert.Contains("_translationToolViewModel.SendCommand.ExecuteAsync(null)", shared);
        Assert.DoesNotContain("IScreenTextCaptureService", shared);
        Assert.DoesNotContain("ScreenCaptureOverlayWindow", shared);
        Assert.DoesNotContain("ILocalOcrService", shared);
    }

    [Fact]
    public void ExtractTextHotkey_OnlyShowsTranslationWhenCaptureAppliedUsefulText()
    {
        var code = ReadWindowCoordinatorSource();
        var body = ExtractMethodBody(
            code, "private async Task CaptureFromHotkeyAsync(bool translate)");

        var guardIndex = body.IndexOf("outcome.AppliedText is null", StringComparison.Ordinal);
        var selectIndex = body.IndexOf(
            "_viewModel.SelectToolCommand.Execute(ToolId.Translation)",
            StringComparison.Ordinal);

        Assert.True(guardIndex >= 0, "Captures without applied text must be guarded.");
        Assert.True(selectIndex > guardIndex, "A useful capture still opens Translation.");
        Assert.Contains("outcome.SelectionCompleted", body);
        Assert.Contains("ShowCaptureStatus(\"No text detected.\", wasHidden)", body);
        Assert.Contains("_capturePanelWasVisible = true;", body);
        Assert.Contains("if (!_restoreCapturePanelOnNormal)", body);
        Assert.Contains("ShowPanel();", body);
        Assert.Contains("if (!translate || IsStopping)", body);
        Assert.Contains("_translationToolViewModel.InputText = outcome.AppliedText;", body);
        Assert.Contains("_translationToolViewModel.SendCommand.CanExecute(null)", body);
    }

    [Fact]
    public void CompletedSelectionRestoresAnExplicitlyHiddenApplication()
    {
        var code = ReadWindowCoordinatorSource();
        var body = ExtractMethodBody(
            code, "private void OnCaptureFinished(object? sender, CaptureFinishedEventArgs e)");

        Assert.Contains("if (e.SelectionCompleted) RestoreApplicationVisibility();", body);
    }

    [Fact]
    public void RegistrationFailuresReportBothNewShortcuts()
    {
        var code = ReadWindowCoordinatorSource();
        var body = ExtractMethodBody(
            code, "private void UpdateGlobalShortcutsStatus(");

        Assert.Contains("Capture & Translate (Ctrl+Alt+T)", body);
        Assert.Contains("Capture Text (Ctrl+Alt+C)", body);
        Assert.Contains("Show/Hide (Ctrl+Alt+H)", body);
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
