using System.Xml.Linq;
using FloatingTools.App.Models;

namespace FloatingTools.Tests.Views;

public sealed class QuickChatUiIntegrationContractTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void ToolRegistration_UsesNormalToolbarAndPanelInfrastructureExactlyOnce()
    {
        Assert.True(Enum.IsDefined(ToolId.QuickChat));
        var panel = XDocument.Load(FindSourcePath("Views", "PanelWindow.xaml"));
        var quickChatButtons = panel.Descendants(Presentation + "Button")
            .Where(element => (string?)element.Attribute("CommandParameter")
                == "{x:Static models:ToolId.QuickChat}")
            .ToArray();
        var quickChatViews = panel.Descendants()
            .Where(element => element.Name.LocalName == "QuickChatToolView")
            .ToArray();

        Assert.Single(quickChatButtons);
        Assert.Single(quickChatViews);
        Assert.Contains("PanelState.ActiveTool", quickChatViews[0].ToString());
        Assert.False(File.Exists(FindSourcePath("Views", "QuickChatWindow.xaml")));
        Assert.Contains("QuickChatIconTemplate", File.ReadAllText(
            FindSourcePath("Views", "ToolbarWindow.xaml")));
    }

    [Fact]
    public void Header_UsesSharedControlAndOneNewChatCommandPath()
    {
        var view = XDocument.Load(FindSourcePath("Views", "QuickChatToolView.xaml"));
        var header = view.Descendants()
            .Single(element => element.Name.LocalName == "ToolHeaderControl");

        Assert.Equal("Quick Chat", (string?)header.Attribute("Title"));
        Assert.Equal("{Binding NewChatCommand}",
            (string?)header.Attribute("SecondaryActionCommand"));
        Assert.Equal("+", (string?)header.Attribute("SecondaryActionContent"));
        Assert.Contains(view.Descendants(Presentation + "Button"), element =>
            (string?)element.Attribute("Command") == "{Binding NewChatCommand}"
            && element.Descendants(Presentation + "TextBlock")
                .Any(text => (string?)text.Attribute("Text") == "New Chat"));
        Assert.Equal(1, view.Descendants(Presentation + "Button").Count(element =>
            (string?)element.Attribute("Command") == "{Binding NewChatCommand}"));
    }

    [Fact]
    public void ExpandedHeader_IsExactlyNewChatAndSettingsAndSettingsIsADedicatedPage()
    {
        var view = XDocument.Load(FindSourcePath("Views", "QuickChatToolView.xaml"));
        var expanded = view.Descendants(Presentation + "Border")
            .Single(element => AttributeValue(element, "Name")
                == "QuickChatHeaderExpandedContent");
        var menuButtons = expanded.Descendants(Presentation + "Button").ToArray();

        Assert.Equal(2, menuButtons.Length);
        Assert.Equal(["New Chat", "Settings"],
            menuButtons.Select(element => (string?)element
                .Descendants(Presentation + "TextBlock").Last().Attribute("Text")));
        Assert.Equal("{Binding NewChatCommand}",
            (string?)menuButtons[0].Attribute("Command"));
        Assert.Equal("{Binding OpenSettingsCommand}",
            (string?)menuButtons[1].Attribute("Command"));

        var settingsPage = view.Descendants(Presentation + "Grid")
            .Single(element => (string?)element.Attribute("Visibility")
                == "{Binding IsSettingsPage, Converter={StaticResource BooleanToVisibilityConverter}}");
        Assert.Contains(settingsPage.Descendants(), element =>
            element.Name.LocalName == "SettingsPageShell"
            && (string?)element.Attribute("BackCommand") == "{Binding BackToChatCommand}");
        Assert.DoesNotContain(settingsPage.Descendants(Presentation + "Button"), element =>
            (string?)element.Attribute("Command") == "{Binding NewChatCommand}");
        Assert.Contains("Settings.UseAppCredentials", settingsPage.ToString());
        Assert.Contains("CommitAdditionalInstructionsCommand", settingsPage.ToString());
    }

    [Fact]
    public void ConversationAndComposer_BindOnlyExistingQuickChatViewModelSurface()
    {
        var view = XDocument.Load(FindSourcePath("Views", "QuickChatToolView.xaml"));
        var source = view.ToString();

        Assert.Contains("ItemsSource=\"{Binding Messages}\"", source);
        Assert.Contains("Text=\"{Binding DraftText, UpdateSourceTrigger=PropertyChanged}\"", source);
        Assert.Contains("Command=\"{Binding SendCommand}\"", source);
        Assert.Contains("Command=\"{Binding StopCommand}\"", source);
        Assert.Contains("ItemsSource=\"{Binding PendingAttachments}\"", source);
        Assert.Contains("Command=\"{Binding DataContext.RemovePendingAttachmentCommand, ElementName=QuickChatRoot}\"", source);
        Assert.Contains("Visibility=\"{Binding CanRetry, Converter={StaticResource BooleanToVisibilityConverter}}\"", source);
        Assert.Contains("Ask anything", source);
    }

    [Fact]
    public void Composer_IsUnifiedDirectionalAndKeepsExistingActionContracts()
    {
        var view = XDocument.Load(FindSourcePath("Views", "QuickChatToolView.xaml"));
        var source = view.ToString();
        var composer = view.Descendants()
            .Single(element => element.Name.LocalName == "DirectionalTextBox"
                && AttributeValue(element, "Name") == "ComposerTextBox");

        Assert.Equal("{Binding DraftText, UpdateSourceTrigger=PropertyChanged}",
            (string?)composer.Attribute("Text"));
        Assert.Contains("ItemsSource=\"{Binding PendingAttachments}\"", source);
        Assert.Contains("IsEnabled=\"{Binding CanAttachImage}\"", source);
        Assert.Contains("Command=\"{Binding SendCommand}\"", source);
        Assert.Contains("Command=\"{Binding StopCommand}\"", source);
        Assert.Contains("AllowDrop=\"True\"", source);
        Assert.Contains("PreviewDragOver=\"ComposerContainer_OnPreviewDragOver\"", source);
        Assert.Contains("PreviewDrop=\"ComposerContainer_OnPreviewDrop\"", source);
    }

    [Fact]
    public void Messages_UseSelectableParagraphsAndSoftHoverAssistantCopy()
    {
        var path = FindSourcePath("Views", "QuickChatToolView.xaml");
        var source = File.ReadAllText(path);
        var view = XDocument.Load(path);

        Assert.Contains("ItemsSource=\"{Binding Paragraphs}\"", source);
        Assert.Contains("FlowDirection=\"{Binding FlowDirection}\"", source);
        Assert.Contains("TextAlignment=\"{Binding TextAlignment}\"", source);
        Assert.Contains("Text=\"{Binding Text, Mode=OneWay}\"", source);
        Assert.Contains("QuickChatSelectableMessageTextStyle", source);
        Assert.Contains("x:Name=\"MessageSurface\"", source);
        Assert.Contains("Background=\"Transparent\"", source);
        Assert.Contains("Property=\"Background\" Value=\"{DynamicResource FloatingToolsBrushSurfaceMessageUser}\"", source);
        Assert.Contains("AutomationProperties.Name=\"Copy\"", source);
        Assert.DoesNotContain("Content=\"Copy\"", source);
        var copy = view.Descendants(Presentation + "Button")
            .Single(element => (string?)element.Attribute("ToolTip") == "Copy"
                && (string?)element.Attribute("Command")
                    == "{Binding DataContext.CopyMessageCommand, ElementName=QuickChatRoot}");
        var copyContract = copy.ToString();
        Assert.Contains("Property=\"Opacity\" Value=\"0\"", copyContract);
        Assert.Contains("Property=\"IsHitTestVisible\" Value=\"False\"", copyContract);
        Assert.Contains("Binding=\"{Binding IsAssistant}\" Value=\"True\"", copyContract);
        Assert.Contains("Binding=\"{Binding HasText}\" Value=\"True\"", copyContract);
        Assert.Contains("IsMouseOver", copyContract);
        Assert.Contains("ElementName=MessageHoverArea", copyContract);
        Assert.Contains("Property=\"Opacity\" Value=\"1\"", copyContract);
        Assert.Contains("Duration=\"0:0:0.18\"", copyContract);
        Assert.Contains("Duration=\"0:0:0.30\"", copyContract);
        Assert.Contains("HandoffBehavior=\"SnapshotAndReplace\"", copyContract);
        Assert.DoesNotContain("Property=\"Visibility\" Value=\"Collapsed\"", copyContract);
        var actionsArea = view.Descendants(Presentation + "Grid")
            .Single(element => AttributeValue(element, "Name") == "MessageActionsArea");
        Assert.Equal("24", (string?)actionsArea.Attribute("Height"));
        Assert.Contains("Visibility=\"{Binding CanRetry, Converter={StaticResource BooleanToVisibilityConverter}}\"", source);
        Assert.Contains("Visibility=\"{Binding IsInterrupted, Converter={StaticResource BooleanToVisibilityConverter}}\"", source);
        Assert.Contains("Visibility=\"{Binding IsError, Converter={StaticResource BooleanToVisibilityConverter}}\"", source);
    }

    [Fact]
    public void SelectableMessageText_IsReadOnlyBorderlessAndDoesNotReserveScrollingChrome()
    {
        var view = XDocument.Load(FindSourcePath("Views", "QuickChatToolView.xaml"));
        var style = view.Descendants(Presentation + "Style")
            .Single(element => (string?)element.Attribute(Xaml + "Key")
                == "QuickChatSelectableMessageTextStyle");
        var setters = style.Elements(Presentation + "Setter")
            .ToDictionary(
                element => (string)element.Attribute("Property")!,
                element => (string?)element.Attribute("Value"));

        Assert.Equal("True", setters["IsReadOnly"]);
        Assert.Equal("False", setters["IsReadOnlyCaretVisible"]);
        Assert.Equal("Wrap", setters["TextWrapping"]);
        Assert.Equal("True", setters["AcceptsReturn"]);
        Assert.Equal("Transparent", setters["Background"]);
        Assert.Equal("0", setters["BorderThickness"]);
        Assert.Equal("0", setters["Padding"]);
        Assert.Equal("Disabled", setters["VerticalScrollBarVisibility"]);
        Assert.Equal("Disabled", setters["HorizontalScrollBarVisibility"]);
        Assert.Equal("IBeam", setters["Cursor"]);
        Assert.Equal(
            "True",
            setters["sharedControls:TripleClickSelectAllBehavior.IsEnabled"]);
    }

    [Fact]
    public void ParagraphItemsAndTextStretchAcrossTheAvailableMessageWidth()
    {
        var view = XDocument.Load(FindSourcePath("Views", "QuickChatToolView.xaml"));
        var paragraphs = view.Descendants(Presentation + "ItemsControl")
            .Single(element => (string?)element.Attribute("ItemsSource")
                == "{Binding Paragraphs}");
        var paragraph = paragraphs.Descendants(Presentation + "TextBox")
            .Single(element => (string?)element.Attribute("Text")
                == "{Binding Text, Mode=OneWay}");

        Assert.Equal("Stretch", (string?)paragraphs.Attribute("HorizontalContentAlignment"));
        Assert.Contains(paragraphs.Descendants(Presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "HorizontalAlignment"
            && (string?)setter.Attribute("Value") == "Stretch");
        Assert.Equal("{StaticResource QuickChatSelectableMessageTextStyle}",
            (string?)paragraph.Attribute("Style"));
        Assert.Equal("{Binding FlowDirection}", (string?)paragraph.Attribute("FlowDirection"));
        Assert.Equal("{Binding TextAlignment}", (string?)paragraph.Attribute("TextAlignment"));
    }

    [Fact]
    public void EmptyStateAndScrollFollow_RemainMinimalAndStable()
    {
        var view = File.ReadAllText(FindSourcePath("Views", "QuickChatToolView.xaml"));
        var code = File.ReadAllText(FindSourcePath("Views", "QuickChatToolView.xaml.cs"));

        Assert.Equal(1, CountOccurrences(view, "Ask anything"));
        Assert.DoesNotContain("Quick questions, ideas, or explanations.", view);
        Assert.Contains("ScrollChanged=\"ConversationScrollViewer_OnScrollChanged\"", view);
        Assert.Contains("<= 36", code);
        Assert.Contains("ConversationScrollViewer.ScrollToEnd()", code);
    }

    [Fact]
    public void FeedHasScrollbarGutterAndImagesUseUnlockedLoadingPreviewAndHoverRemoval()
    {
        var view = File.ReadAllText(FindSourcePath("Views", "QuickChatToolView.xaml"));
        var code = File.ReadAllText(FindSourcePath("Views", "QuickChatToolView.xaml.cs"));

        Assert.Contains(
            "Padding=\"{StaticResource FloatingToolsSpacingScrollContentGutter}\"",
            view);
        Assert.Equal(2, CountOccurrences(view,
            "Converter={StaticResource LocalImagePathConverter}"));
        Assert.Contains("QuickChatPendingRemoveButtonStyle", view);
        Assert.Contains("ElementName=PendingImageHoverArea", view);
        Assert.Contains("Property=\"Visibility\" Value=\"Collapsed\"", view);
        Assert.Contains("MouseLeftButtonDown=\"AttachmentImage_OnMouseLeftButtonDown\"", view);
        Assert.Contains("new ImageViewerWindow(imagePath, monitor)", code);
        Assert.Contains("WindowPlacementCalculator.SelectMonitor", code);
        Assert.Contains("DeleteUnsentAttachmentAsync", File.ReadAllText(
            FindSourcePath("ViewModels", "QuickChatViewModel.cs")));
    }

    [Fact]
    public void HeaderAndMenuMatchTranslationVisualMetrics()
    {
        var quickChat = File.ReadAllText(FindSourcePath("Views", "QuickChatToolView.xaml"));
        var translation = File.ReadAllText(FindSourcePath("Views", "TranslationToolView.xaml"));

        foreach (var contract in new[]
        {
            "Property=\"Height\" Value=\"34\"",
            "Property=\"Padding\" Value=\"12,0\"",
            "Property=\"Background\" Value=\"{DynamicResource FloatingToolsBrushSurfaceHeader}\"",
            "Property=\"ChevronBrush\" Value=\"{DynamicResource FloatingToolsBrushForegroundMuted}\"",
            "Property=\"CornerRadius\" Value=\"0\"",
            "Property=\"Height\" Value=\"{StaticResource FloatingToolsSizeMenuItemHeight}\"",
            "Property=\"Margin\" Value=\"10,0\"",
            "Property=\"Padding\" Value=\"10,0\""
        })
        {
            Assert.Contains(contract, quickChat);
            Assert.Contains(contract, translation);
        }

        Assert.Contains("<StackPanel Margin=\"0,5\">", quickChat);
    }

    [Fact]
    public void SettingsStatusDoesNotReserveEmptyHeightAndSectionSpacingIsNatural()
    {
        var view = File.ReadAllText(FindSourcePath("Views", "QuickChatToolView.xaml"));

        Assert.Contains("QuickChatOptionalStatusTextStyle", view);
        Assert.Contains("Text=\"Additional instructions\"", view);
        Assert.Contains("Margin=\"0,14,0,6\"", view);
        Assert.DoesNotContain("MinHeight=\"100\"", view);
        Assert.DoesNotContain("Height=\"100\"", view);
    }

    [Fact]
    public void SettingsPageHeaderMatchesTranslationLeftAlignedLayout()
    {
        var view = XDocument.Load(FindSourcePath("Views", "QuickChatToolView.xaml"));
        var settingsPage = view.Descendants(Presentation + "Grid")
            .Single(element => (string?)element.Attribute("Visibility")
                == "{Binding IsSettingsPage, Converter={StaticResource BooleanToVisibilityConverter}}");
        var shell = settingsPage.Elements()
            .Single(element => element.Name.LocalName == "SettingsPageShell");
        Assert.Equal("Settings", (string?)shell.Attribute("Title"));
        Assert.Equal("{Binding BackToChatCommand}", (string?)shell.Attribute("BackCommand"));

        var sharedShell = XDocument.Load(
            FindSourcePath("SharedUi", "Styles", "SettingsPageShell.xaml"));
        var header = sharedShell.Descendants(Presentation + "Grid")
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "HeaderRow");
        Assert.Equal("40", (string?)header.Attribute("Height"));
        Assert.Equal("8,0,10,0", (string?)header.Attribute("Margin"));
        Assert.Equal(["34", "*"], header
            .Element(Presentation + "Grid.ColumnDefinitions")!
            .Elements(Presentation + "ColumnDefinition")
            .Select(column => (string?)column.Attribute("Width")));
        var back = header.Element(Presentation + "Button")!;
        Assert.Equal("30", (string?)back.Attribute("Width"));
        Assert.Equal("30", (string?)back.Attribute("Height"));
        var arrow = back.Element(Presentation + "Path")!;
        Assert.Equal("12", (string?)arrow.Attribute("Width"));
        Assert.Equal("12", (string?)arrow.Attribute("Height"));
        var title = header.Element(Presentation + "TextBlock")!;
        Assert.Equal("1", (string?)title.Attribute("Grid.Column"));
        Assert.Equal("13", (string?)title.Attribute("FontSize"));
        Assert.Equal("SemiBold", (string?)title.Attribute("FontWeight"));
        Assert.Null(title.Attribute("HorizontalAlignment"));

        var body = sharedShell.Descendants(Presentation + "ScrollViewer").Single();
        Assert.Equal(
            "{StaticResource FloatingToolsSpacingScrollContentGutter}",
            (string?)body.Attribute("Padding"));
    }

    [Fact]
    public void ExpandedSettings_AreQuickChatSpecificAndCommitInstructionsExplicitly()
    {
        var view = File.ReadAllText(FindSourcePath("Views", "QuickChatToolView.xaml"));
        var settings = File.ReadAllText(
            FindSourcePath("ViewModels", "QuickChatSettingsViewModel.cs"));

        Assert.Contains("Settings.UseAppCredentials", view);
        Assert.Contains("Settings.ModelOptions", view);
        Assert.Contains("Settings.SelectedModel", view);
        Assert.Contains("CommitAdditionalInstructionsCommand", view);
        Assert.Contains("Save instructions", view);
        Assert.DoesNotContain("ProductInstruction", view);
        Assert.Contains("_settings.Ai.QuickChat", settings);
        Assert.DoesNotContain("_settings.Ai.Translation", settings);
    }

    [Fact]
    public void FileAndKeyboardHandling_UseApprovedPngJpegAndEnterContracts()
    {
        var code = File.ReadAllText(
            FindSourcePath("Views", "QuickChatToolView.xaml.cs"));

        Assert.Contains("*.png;*.jpg;*.jpeg", code);
        Assert.DoesNotContain("*.webp", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Keyboard.Modifiers & ModifierKeys.Shift", code);
        Assert.Contains("e.Key != Key.Enter", code);
        Assert.Contains("AttachClipboardImageCommand", code);
        Assert.Contains("ConversationScrollViewer.ScrollableHeight", code);
        Assert.Contains("ConversationScrollViewer.ScrollToEnd()", code);
    }

    [Fact]
    public void AppComposition_UsesQuickChatScopeAndPreservesTranslationComposition()
    {
        var app = File.ReadAllText(FindSourcePath("App.xaml.cs"));
        var coordinator = File.ReadAllText(
            FindSourcePath("Services", "WindowCoordinator.cs"));

        Assert.Contains("new JsonQuickChatStore()", app);
        Assert.Contains("new LocalQuickChatImageStore()", app);
        Assert.Contains("AiToolId.QuickChat", app);
        Assert.Contains("new OpenAiQuickChatService(", app);
        Assert.Contains("new ActiveQuickChatConversation(", app);
        Assert.Contains("new WindowsClipboardImageProvider()", app);
        Assert.Contains("new SynchronizationContextUiDispatcher()", app);
        Assert.Contains("new OpenAiTranslationService(", app);
        Assert.Contains("new AiOpenAiConfigurationProvider(\n            aiConfigurationResolver);",
            app.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Contains("_activeQuickChatConversation.PrepareForExitAsync()", coordinator);
        Assert.Contains("_notesToolViewModel.PrepareForExitAsync()", coordinator);
    }

    [Fact]
    public void PanelSizing_RemainsSharedForAllTools()
    {
        var panelCode = File.ReadAllText(
            FindSourcePath("Views", "PanelWindow.xaml.cs"));

        Assert.Contains("PanelSizeCalculator.GetActiveToolSize(", panelCode);
        Assert.DoesNotContain("QuickChatWindow", panelCode);
        Assert.DoesNotContain("ToolId.QuickChat", panelCode);
    }

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

    private static int CountOccurrences(string source, string value) =>
        (source.Length - source.Replace(value, string.Empty, StringComparison.Ordinal).Length)
        / value.Length;

    private static string? AttributeValue(XElement element, string localName) =>
        element.Attributes()
            .SingleOrDefault(attribute => attribute.Name.LocalName == localName)
            ?.Value;
}
