using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

public sealed class TranslationToolViewStyleTests
{
    [Fact]
    public void TranslationMenuItemStyle_UsesSharedButtonFoundationAcrossTheButton()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var style = document.Descendants(presentation + "Style")
            .Single(element =>
                (string?)element.Attribute(x + "Key") == "MenuItemButtonStyle");
        Assert.Equal(
            "{StaticResource FloatingToolsSharedButtonBaseStyle}",
            (string?)style.Attribute("BasedOn"));
        Assert.Equal(
            4,
            document.Descendants(presentation + "Button").Count(element =>
                (string?)element.Attribute("Style")
                == "{StaticResource MenuItemButtonStyle}"));
    }

    [Fact]
    public void SettingsControls_UseDarkCustomTemplates()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        var sharedComboBoxes = XDocument.Load(FindSharedSettingsComboBoxesPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var comboStyle = FindStyle(
            sharedComboBoxes, presentation, x, "SettingsComboBoxStyle");
        Assert.Equal("{StaticResource SettingsComboBoxStyle}",
            (string?)FindStyle(sharedComboBoxes, presentation, x, "CompactFormComboBoxStyle").Attribute("BasedOn"));
        var passwordStyle = FindStyle(
            document, presentation, x, "SettingsPasswordBoxStyle");

        Assert.Equal(
            "{DynamicResource FloatingToolsBrushSurfaceInput}",
            GetSetterValue(comboStyle, presentation, "Background"));
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushForegroundPrimary}",
            GetSetterValue(comboStyle, presentation, "Foreground"));
        Assert.NotEmpty(comboStyle.Descendants(presentation + "ControlTemplate"));
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushSurfaceInput}",
            GetSetterValue(passwordStyle, presentation, "Background"));
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushForegroundPrimary}",
            GetSetterValue(passwordStyle, presentation, "Foreground"));
        Assert.NotEmpty(passwordStyle.Descendants(presentation + "ControlTemplate"));

        Assert.Equal(
            3,
            document.Descendants(presentation + "ComboBox").Count(element =>
                (string?)element.Attribute("Style")
                == "{StaticResource CompactFormComboBoxStyle}"));
        Assert.Single(document.Descendants(presentation + "PasswordBox"));
        Assert.All(
            document.Descendants(presentation + "PasswordBox"),
            passwordBox => Assert.Equal(
                "{StaticResource SettingsPasswordBoxStyle}",
                (string?)passwordBox.Attribute("Style")));
    }

    [Fact]
    public void TranslationSettings_ExcludeApplicationAiAndRetainTranslationAi()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        Assert.DoesNotContain(document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Application AI");
        Assert.Contains(document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Translation AI");
        Assert.DoesNotContain(document.Descendants(presentation + "TextBlock"),
            element => ((string?)element.Attribute("Text"))
                ?.Contains("Settings.Application") == true);
        Assert.Contains(document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("Command")
                == "{Binding Settings.TestConnectionCommand}");
    }

    [Fact]
    public void TranslationAiSettings_ExposeIndependentCredentialAndModelInheritance()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var credentialsToggle = document.Descendants(presentation + "CheckBox")
            .Single(element => (string?)element.Attribute("Content")
                == "Use app credentials");
        var modelSelector = document.Descendants(presentation + "ComboBox")
            .Single(element => (string?)element.Attribute("ItemsSource")
                == "{Binding Settings.TranslationModelOptions}");

        Assert.Equal(
            "{Binding Settings.UseAppCredentials, Mode=TwoWay}",
            (string?)credentialsToggle.Attribute("IsChecked"));
        Assert.Equal("Model", (string?)modelSelector.Attribute("SelectedValuePath"));
        Assert.Equal(
            "{Binding Settings.SelectedTranslationAiModel, Mode=TwoWay}",
            (string?)modelSelector.Attribute("SelectedValue"));
        AssertChoiceDisplayTemplate(modelSelector, presentation);
        Assert.Contains(
            document.Descendants(presentation + "StackPanel"),
            element => ((string?)element.Attribute("Visibility"))
                ?.Contains("Settings.IsUsingCustomCredentials") == true);
        Assert.Contains(
            document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Using app credentials");
    }

    [Fact]
    public void ChoiceSelectors_RenderDisplayNameForSelectedAndDropDownItems()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var modelSelector = document.Descendants(presentation + "ComboBox")
            .Single(element => (string?)element.Attribute("ItemsSource")
                == "{Binding Settings.TranslationModelOptions}");
        var languageSelector = document.Descendants(presentation + "ComboBox")
            .Single(element => (string?)element.Attribute("ItemsSource")
                == "{Binding Settings.LanguageModes}");

        AssertChoiceDisplayTemplate(modelSelector, presentation);
        var itemContainerStyle = modelSelector
            .Element(presentation + "ComboBox.ItemContainerStyle")
            ?.Element(presentation + "Style");
        Assert.Equal(
            "{StaticResource SettingsComboBoxItemStyle}",
            (string?)itemContainerStyle?.Attribute("BasedOn"));
        Assert.Contains(
            itemContainerStyle!.Elements(presentation + "Setter"),
            setter => (string?)setter.Attribute("Property") == "IsEnabled"
                && (string?)setter.Attribute("Value") == "{Binding IsSelectable}");
        AssertChoiceDisplayTemplate(languageSelector, presentation);
        Assert.Null(modelSelector.Attribute("DisplayMemberPath"));
        Assert.Null(languageSelector.Attribute("DisplayMemberPath"));
        Assert.Equal("Model", (string?)modelSelector.Attribute("SelectedValuePath"));
        Assert.Equal("Value", (string?)languageSelector.Attribute("SelectedValuePath"));
    }

    [Fact]
    public void AiToLanguageSpacing_CollapsesEmptyStatusAndUsesStandardSectionRhythm()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var status = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name")
                == "SettingsConnectionStatusText");
        var languageHeader = document.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == "Language");
        var triggers = status.Descendants(presentation + "DataTrigger").ToArray();

        Assert.Contains(triggers, trigger =>
            (string?)trigger.Attribute("Value") == "{x:Null}"
            && TriggerCollapses(trigger, presentation));
        Assert.Contains(triggers, trigger =>
            (string?)trigger.Attribute("Value") == string.Empty
            && TriggerCollapses(trigger, presentation));
        Assert.Equal("0,14,0,6", (string?)languageHeader.Attribute("Margin"));
        Assert.Null(status.Attribute("Height"));
        Assert.Null(status.Attribute("MinHeight"));
        Assert.Null(languageHeader.Attribute("Height"));
        Assert.Null(languageHeader.Attribute("MinHeight"));
        Assert.DoesNotContain(
            document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Writing Model");
    }

    [Fact]
    public void SharedMenu_RendersAboveEveryInternalPageAndIncludesFeed()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var menu = document.Descendants(presentation + "Border")
            .Single(element =>
                (string?)element.Attribute("Visibility")
                == "{Binding IsAppMenuOpen, Converter={StaticResource BooleanToVisibilityConverter}}");

        Assert.Equal("100", (string?)menu.Attribute("Panel.ZIndex"));
        Assert.Contains(
            menu.Descendants(presentation + "Button"),
            button => (string?)button.Attribute("Command")
                == "{Binding OpenFeedCommand}");
    }

    [Fact]
    public void Composer_ExposesCompactCaptureTextAction()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var captureButton = document.Descendants(presentation + "Button")
            .Single(element => (string?)element.Attribute("Command")
                == "{Binding CaptureTextCommand}");

        Assert.Equal(
            "{StaticResource IconButtonStyle}",
            (string?)captureButton.Attribute("Style"));
        Assert.Equal("Capture Text", (string?)captureButton.Attribute("ToolTip"));
    }

    [Fact]
    public void Composer_UsesFullWidthEditorWithBottomActionRow()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var composer = document.Descendants()
            .Single(element =>
                (string?)element.Attribute(x + "Name") == "ComposerTextBox");
        var editorHost = Assert.IsType<XElement>(composer.Parent);
        var composerGrid = Assert.IsType<XElement>(editorHost.Parent);
        var actionRow = composerGrid.Elements(presentation + "StackPanel")
            .Single(element =>
                (string?)element.Attribute("Grid.Row") == "1");
        var commands = actionRow.Elements(presentation + "Button")
            .Select(button => (string?)button.Attribute("Command"))
            .ToArray();

        Assert.Equal("0", (string?)editorHost.Attribute("Grid.Row"));
        Assert.Empty(composerGrid.Elements(presentation + "Grid.ColumnDefinitions"));
        Assert.Equal(
            2,
            composerGrid.Element(presentation + "Grid.RowDefinitions")!
                .Elements(presentation + "RowDefinition").Count());
        Assert.Collection(
            commands,
            command => Assert.Equal("{Binding CaptureTextCommand}", command),
            command => Assert.Equal("{Binding ClearInputCommand}", command),
            command => Assert.Equal("{Binding SendCommand}", command));
        Assert.Equal("Wrap", (string?)composer.Attribute("TextWrapping"));
        Assert.Equal("144", (string?)composer.Attribute("MaxHeight"));
        Assert.Equal(
            "Auto",
            (string?)composer.Attribute("VerticalScrollBarVisibility"));
    }

    [Fact]
    public void TranslationScrolling_UsesSharedScrollbarAndNormalizedWheelBehavior()
    {
        var translation = File.ReadAllText(FindTranslationToolViewPath());
        var shared = XDocument.Load(FindSharedScrollBarsPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.Contains(
            "controls:SmoothWheelScrollBehavior.IsEnabled\" Value=\"True\"",
            translation);
        Assert.Contains(
            "BasedOn=\"{StaticResource FloatingToolsSharedScrollViewerStyle}\"",
            translation);
        Assert.DoesNotContain("DockSideToFeedScrollBarColumnConverter", translation);
        var style = shared.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key")
                == "FloatingToolsSharedMinimalScrollBarStyle");
        Assert.Contains(style.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Width"
            && (string?)setter.Attribute("Value")
                == "{StaticResource FloatingToolsSizeScrollBarWidth}");
        var thumb = shared.Descendants(presentation + "Border")
            .Single(element => (string?)element.Attribute(x + "Name") == "ThumbSurface");
        Assert.Equal(
            "{StaticResource FloatingToolsSizeScrollThumbWidth}",
            (string?)thumb.Attribute("Width"));
    }

    [Fact]
    public void TranslationChrome_UsesSharedFoundationsWhileRetainingApprovedSpecializations()
    {
        var document = XDocument.Load(FindTranslationToolViewPath());
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        foreach (var styleKey in new[]
                 {
                     "IconButtonStyle",
                     "ActionButtonStyle",
                     "MenuItemButtonStyle",
                     "BarButtonStyle"
                 })
        {
            var style = FindStyle(document, presentation, x, styleKey);
            Assert.Equal(
                "{StaticResource FloatingToolsSharedButtonBaseStyle}",
                (string?)style.Attribute("BasedOn"));
        }

        var iconStyle = FindStyle(document, presentation, x, "IconButtonStyle");
        var actionStyle = FindStyle(document, presentation, x, "ActionButtonStyle");
        var barStyle = FindStyle(document, presentation, x, "BarButtonStyle");

        Assert.Equal(
            "{StaticResource FloatingToolsSharedToolTipStyle}",
            (string?)document.Descendants(presentation + "Style")
                .Single(style =>
                    style.Attribute(x + "Key") is null
                    && (string?)style.Attribute("TargetType") == "{x:Type ToolTip}")
                .Attribute("BasedOn"));
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushSurfaceHover}",
            FindTriggerSetterValue(iconStyle, presentation, "IsMouseOver"));
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushSurfaceControl}",
            FindTriggerSetterValue(actionStyle, presentation, "IsMouseOver"));
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushSurfaceHeader}",
            GetSetterValue(barStyle, presentation, "Background"));
    }

    private static XElement FindStyle(
        XDocument document,
        XNamespace presentation,
        XNamespace x,
        string key) =>
        document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == key);

    private static string? GetSetterValue(
        XElement style,
        XNamespace presentation,
        string property) =>
        (string?)style.Elements(presentation + "Setter")
            .Single(element => (string?)element.Attribute("Property") == property)
            .Attribute("Value");

    private static string? FindTriggerSetterValue(
        XElement style,
        XNamespace presentation,
        string triggerProperty) =>
        (string?)style.Descendants(presentation + "Trigger")
            .Single(trigger => (string?)trigger.Attribute("Property") == triggerProperty)
            .Elements(presentation + "Setter")
            .Single(setter => (string?)setter.Attribute("Property") == "Background")
            .Attribute("Value");

    private static void AssertChoiceDisplayTemplate(
        XElement comboBox,
        XNamespace presentation)
    {
        var itemTemplate = comboBox.Element(presentation + "ComboBox.ItemTemplate")
            ?.Element(presentation + "DataTemplate");
        var label = itemTemplate?.Descendants(presentation + "TextBlock").Single();

        Assert.NotNull(itemTemplate);
        Assert.Equal("{Binding DisplayName}", (string?)label?.Attribute("Text"));
    }

    private static bool TriggerCollapses(
        XElement trigger,
        XNamespace presentation) =>
        trigger.Elements(presentation + "Setter").Any(setter =>
            (string?)setter.Attribute("Property") == "Visibility"
            && (string?)setter.Attribute("Value") == "Collapsed");

    private static string FindTranslationToolViewPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solution = Path.Combine(directory.FullName, "FloatingTools.sln");
            if (File.Exists(solution))
            {
                return Path.Combine(
                    directory.FullName,
                    "src",
                    "FloatingTools.App",
                    "Views",
                    "TranslationToolView.xaml");
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }

    private static string FindSharedScrollBarsPath() =>
        Path.Combine(
            Path.GetDirectoryName(FindTranslationToolViewPath())!,
            "..",
            "SharedUi",
            "Styles",
            "ScrollBars.xaml");

    private static string FindSharedSettingsComboBoxesPath() =>
        Path.Combine(
            Path.GetDirectoryName(FindTranslationToolViewPath())!,
            "..",
            "SharedUi",
            "Styles",
            "SettingsComboBoxes.xaml");
}
