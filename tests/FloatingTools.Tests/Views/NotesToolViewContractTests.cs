using System.Xml.Linq;

namespace FloatingTools.Tests.Views;

public sealed class NotesToolViewContractTests
{
    [Fact]
    public void TextTemplate_HasNoHyperlinkMouseMoveHandler()
    {
        var xaml = File.ReadAllText(FindPath("NotesToolView.xaml"));
        var code = File.ReadAllText(FindPath("NotesToolView.xaml.cs"));

        Assert.DoesNotContain("TextBlockEditor_OnPreviewMouseMove", xaml);
        Assert.DoesNotContain("TextBlockEditor_OnPreviewMouseMove", code);
        Assert.DoesNotContain("GetHyperlinkAt", code);
    }

    [Fact]
    public void Editor_IsLightMultilineAndFillsTheFlexibleRow()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace controls = "clr-namespace:FloatingTools.App.Controls";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var editor = document.Descendants(controls + "NoteDirectionalTextBox")
            .Single(element => (string?)element.Attribute("Text")
                == "{Binding Text, UpdateSourceTrigger=PropertyChanged}");

        Assert.Equal("White", (string?)editor.Attribute("Background"));
        Assert.Equal("True", (string?)editor.Attribute("AcceptsReturn"));
        Assert.Equal("Disabled", (string?)editor.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Wrap", (string?)editor.Attribute("TextWrapping"));
        Assert.Equal("{Binding Text, UpdateSourceTrigger=PropertyChanged}",
            (string?)editor.Attribute("Text"));
        Assert.Contains(document.Descendants(presentation + "ScrollViewer"),
            element => (string?)element.Attribute(x + "Name") == "NotePageScrollViewer"
                && (string?)element.Attribute("VerticalScrollBarVisibility") == "Auto");
    }

    [Fact]
    public void NotesNaturalLanguageInputs_UseTheSharedNotesDirectionalControl()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace controls = "clr-namespace:FloatingTools.App.Controls";

        Assert.Contains(document.Descendants(controls + "NoteDirectionalTextBox"),
            element => (string?)element.Attribute("Text")
                == "{Binding Text, UpdateSourceTrigger=PropertyChanged}");
        Assert.Contains(document.Descendants(controls + "NoteDirectionalTextBox"),
            element => (string?)element.Attribute("Text")
                == "{Binding SearchQuery, UpdateSourceTrigger=PropertyChanged}");
        Assert.Contains(document.Descendants(controls + "NoteDirectionalTextBox"),
            element => ((string?)element.Attribute("Text"))?.Contains("RenameText", StringComparison.Ordinal)
                == true);
    }

    [Fact]
    public void NotesMenu_HasTitleSearchAndCompactRenameDeleteActions()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace controls = "clr-namespace:FloatingTools.App.Controls";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.Contains(document.Descendants(controls + "NoteDirectionalTextBox"),
            element => (string?)element.Attribute("Text")
                == "{Binding SearchQuery, UpdateSourceTrigger=PropertyChanged}");
        Assert.Contains(document.Descendants(presentation + "MenuItem"),
            element => (string?)element.Attribute("Header") == "Rename");
        Assert.Contains(document.Descendants(presentation + "MenuItem"),
            element => (string?)element.Attribute("Header") == "Delete");
        Assert.Contains(document.Descendants(presentation + "Button"),
            element => (string?)element.Attribute("Content") == "⋯");
        Assert.Contains(document.Descendants(presentation + "DataTrigger"),
            element => (string?)element.Attribute("Binding") == "{Binding IsActive}"
                && (string?)element.Attribute("Value") == "True");
        var contextMenuStyle = document.Descendants(presentation + "Style")
            .Single(style => (string?)style.Attribute(x + "Key") == "NotesContextMenuStyle");
        Assert.Equal(
            "{StaticResource FloatingToolsSharedContextMenuStyle}",
            (string?)contextMenuStyle.Attribute("BasedOn"));
    }

    [Fact]
    public void NotesView_HandlesOnlyLocalKeyboardShortcuts()
    {
        var xaml = File.ReadAllText(FindPath("NotesToolView.xaml"));
        var codeBehind = File.ReadAllText(FindPath("NotesToolView.xaml.cs"));

        Assert.Contains("PreviewKeyDown=\"NotesToolView_OnPreviewKeyDown\"", xaml);
        Assert.Contains("ModifierKeys.Control | ModifierKeys.Shift", codeBehind);
        Assert.Contains("NewNoteCommand", codeBehind);
        Assert.Contains("NewTemporaryNoteCommand", codeBehind);
        Assert.Contains("CanUndo: true", codeBehind);
        Assert.Contains("UndoDocumentOperationAsync", codeBehind);
    }

    [Fact]
    public void UrlPaste_UsesNativeTextPasteWithoutLinkInterception()
    {
        var code = File.ReadAllText(FindPath("NotesToolView.xaml.cs"));

        Assert.DoesNotContain("TryParseExactHttpUrl", code);
        Assert.DoesNotContain("InsertConfirmedLinkBlockAfter", code);
        Assert.DoesNotContain("Clipboard.ContainsText()", code);
    }

    [Fact]
    public void TextAndInsertionMenus_ExposeOnlyPlainTextBlockActions()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var textTemplate = document.Descendants(presentation + "DataTemplate")
            .Single(template => (string?)template.Attribute("DataType")
                == "{x:Type models:TextNoteBlock}");
        var headers = textTemplate.Descendants(presentation + "MenuItem")
            .Select(item => (string?)item.Attribute("Header"))
            .ToArray();
        var code = File.ReadAllText(FindPath("NotesToolView.xaml.cs"));

        Assert.Contains("Copy block", headers);
        Assert.Contains("Cut block", headers);
        Assert.Contains("Delete block", headers);
        Assert.DoesNotContain("Copy", headers);
        Assert.DoesNotContain("Cut", headers);
        var insertionMenu = document.Descendants(presentation + "ContextMenu")
            .Single(menu => menu.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                    && attribute.Value == "NotesInsertionContextMenu"));
        var insertionHeaders = insertionMenu.Descendants(presentation + "MenuItem")
            .Select(item => (string?)item.Attribute("Header") ?? string.Empty)
            .ToArray();
        Assert.Equal(["Add text block", "Add link block"], insertionHeaders);
        Assert.DoesNotContain("Make link", document.ToString());
        Assert.DoesNotContain("ConfirmTextLinkEditorAsync", code);
    }

    [Fact]
    public void DocumentInsertionZones_AreWhiteHitAreasWithHoverOnlyIndicator()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var styles = document.Descendants(presentation + "Style").ToArray();
        var intermediate = styles.Single(element =>
            (string?)element.Attribute(x + "Key") == "NotesIntermediateInsertionZoneStyle");
        var trailing = styles.Single(element =>
            (string?)element.Attribute(x + "Key") == "NotesTrailingInsertionZoneStyle");

        Assert.Contains(intermediate.Elements(presentation + "Setter"),
            setter => (string?)setter.Attribute("Property") == "Height"
                && (string?)setter.Attribute("Value") == "16");
        Assert.Contains(intermediate.Elements(presentation + "Setter"),
            setter => (string?)setter.Attribute("Property") == "Background"
                && (string?)setter.Attribute("Value") == "Transparent");
        Assert.Contains(intermediate.Elements(presentation + "EventSetter"),
            setter => (string?)setter.Attribute("Event") == "MouseLeftButtonDown"
                && (string?)setter.Attribute("Handler")
                    == "InsertionZone_OnMouseLeftButtonDown");
        Assert.Contains(trailing.Elements(presentation + "Setter"),
            setter => (string?)setter.Attribute("Property") == "MinHeight"
                && (string?)setter.Attribute("Value") == "34");
        Assert.DoesNotContain(intermediate.Elements(presentation + "Setter")
                .SelectMany(setter => setter.Descendants(presentation + "GradientStop")),
            stop => (string?)stop.Attribute("Color") != "Transparent");
        Assert.Contains(intermediate.Descendants(presentation + "Trigger"),
            trigger => (string?)trigger.Attribute("Property") == "IsMouseOver"
                && trigger.Descendants(presentation + "GradientStop").Any());
        Assert.Contains(document.Descendants(presentation + "Border"),
            border => (string?)border.Attribute(x + "Name") == "TrailingInsertionZone"
                && (string?)border.Attribute("Style")
                    == "{StaticResource NotesTrailingInsertionZoneStyle}"
                && (string?)border.Attribute("MouseLeftButtonDown")
                    == "TrailingInsertionZone_OnMouseLeftButtonDown");
        Assert.DoesNotContain(document.ToString(), "NotesInsertionGapStyle");
        Assert.DoesNotContain(document.ToString(), "NotesFinalInsertionGapStyle");
    }

    [Fact]
    public void DocumentCanvas_FillsViewportAndImageRowsOwnTheirFullWidth()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var canvas = document.Descendants(presentation + "Grid")
            .Single(element => (string?)element.Attribute("MinHeight")
                == "{Binding ViewportHeight, ElementName=NotePageScrollViewer}");
        var items = canvas.Elements(presentation + "ItemsControl").Single();
        var trailing = canvas.Elements(presentation + "Border")
            .Single(element => (string?)element.Attribute(x + "Name")
                == "TrailingInsertionZone");
        var imageTemplate = document.Descendants(presentation + "DataTemplate")
            .Single(template => (string?)template.Attribute("DataType")
                == "{x:Type models:ImageNoteBlock}");
        var imageRow = imageTemplate.Descendants(presentation + "Grid")
            .First(grid => (string?)grid.Attribute("MouseLeftButtonDown")
                == "ImageBlock_OnMouseLeftButtonDown");

        Assert.Equal("0", (string?)items.Attribute("Grid.Row"));
        Assert.Equal("1", (string?)trailing.Attribute("Grid.Row"));
        Assert.Equal("Transparent", (string?)imageRow.Attribute("Background"));
        Assert.Equal("Stretch", (string?)imageRow.Attribute("HorizontalAlignment"));
        Assert.NotNull(imageRow.Element(presentation + "Grid.ContextMenu"));
    }

    [Fact]
    public void TextEditor_HasNoHyperlinkPopupOrHitTesting()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        var code = File.ReadAllText(FindPath("NotesToolView.xaml.cs"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace controls = "clr-namespace:FloatingTools.App.Controls";
        var editor = document.Descendants(controls + "NoteDirectionalTextBox")
            .Single(element => (string?)element.Attribute("Text")
                == "{Binding Text, UpdateSourceTrigger=PropertyChanged}");

        Assert.Equal("True", (string?)editor.Attribute("IsInactiveSelectionHighlightEnabled"));
        Assert.DoesNotContain(document.Descendants(presentation + "Popup"),
            popup => ((string?)popup.Attribute("IsOpen"))?.Contains("Link", StringComparison.Ordinal) == true);
        Assert.DoesNotContain("CaptureLinkPopupAnchor", code);
        Assert.DoesNotContain("TryGetTextRangeBounds", code);
    }

    [Fact]
    public void WhitePageClick_DoesNotCreateTextBlockAndLeavesLinkListInteractionUntouched()
    {
        var xaml = File.ReadAllText(FindPath("NotesToolView.xaml"));
        var codeBehind = File.ReadAllText(FindPath("NotesToolView.xaml.cs"));

        Assert.Contains(
            "PreviewMouseLeftButtonDown=\"NotePage_OnPreviewMouseLeftButtonDown\"",
            xaml);
        Assert.Contains("NotesPageFocusResolver.Resolve", codeBehind);
        Assert.DoesNotContain("EnsureFinalEditableTextBlock", codeBehind);
        Assert.Contains("FindDataContext<ImageNoteBlock>", codeBehind);
        Assert.Contains("FindDataContext<LinkListNoteBlock>", codeBehind);
        Assert.Contains("FindInsertionZone(source)", codeBehind);
    }

    [Fact]
    public void SelectedImage_HandlesDeleteAndBackspaceWithoutTakingTextBoxBackspace()
    {
        var codeBehind = File.ReadAllText(FindPath("NotesToolView.xaml.cs"));

        Assert.Contains("e.Key is Key.Delete or Key.Back", codeBehind);
        Assert.Contains("Keyboard.FocusedElement is not TextBoxBase", codeBehind);
        Assert.Contains("DeleteSelectedImageAsync", codeBehind);
        Assert.Contains("e.Key == Key.C", codeBehind);
        Assert.Contains("e.Key == Key.X", codeBehind);
        Assert.Contains("CopySelectedImage", codeBehind);
        Assert.Contains("InsertClipboardImageAfterBlockAsync", codeBehind);
    }

    [Fact]
    public void NotesText_UsesNativeDoubleClickAndCustomLogicalLineTripleClick()
    {
        var xaml = File.ReadAllText(FindPath("NotesToolView.xaml"));
        var codeBehind = File.ReadAllText(FindPath("NotesToolView.xaml.cs"));
        var textHandlerStart = codeBehind.IndexOf(
            "TextBlockEditor_OnPreviewMouseLeftButtonDown",
            StringComparison.Ordinal);
        var textHandlerEnd = codeBehind.IndexOf(
            "NotePage_OnPreviewMouseLeftButtonDown",
            textHandlerStart,
            StringComparison.Ordinal);
        var textHandler = codeBehind[textHandlerStart..textHandlerEnd];

        Assert.Contains(
            "PreviewMouseLeftButtonDown=\"TextBlockEditor_OnPreviewMouseLeftButtonDown\"",
            xaml);
        Assert.Contains("e.ClickCount != 3", textHandler);
        Assert.DoesNotContain("e.ClickCount == 2", textHandler);
        Assert.Contains("NotesTextSelection.GetLogicalLine", textHandler);
    }

    [Fact]
    public void ImageResize_UsesTransientLayoutPreviewAndCommitsOnceAtCompletion()
    {
        var codeBehind = File.ReadAllText(FindPath("NotesToolView.xaml.cs"));
        var dragDelta = codeBehind.IndexOf(
            "ImageResizeThumb_OnDragDelta",
            StringComparison.Ordinal);
        var dragComplete = codeBehind.IndexOf(
            "ImageResizeThumb_OnDragCompleted",
            StringComparison.Ordinal);
        var deltaBody = codeBehind[dragDelta..dragComplete];

        var xaml = File.ReadAllText(FindPath("NotesToolView.xaml"));
        Assert.Contains("ResponsiveImageDimensionConverter", xaml);
        Assert.Contains("<Binding Path=\"LayoutWidth\" />", xaml);
        Assert.Contains("ConverterParameter=\"Width\"", xaml);
        Assert.Contains("ConverterParameter=\"Height\"", xaml);
        Assert.Contains("SetResizePreview(proposedWidth)", deltaBody);
        Assert.DoesNotContain("RenderTransform", codeBehind);
        Assert.DoesNotContain("SaveNowAsync", deltaBody);
        Assert.DoesNotContain("DisplayWidth =", deltaBody);
        Assert.Contains("CommitImageResizeAsync", codeBehind[dragComplete..]);
    }

    [Fact]
    public void PanelToolMenu_ContainsTranslationAndNotes()
    {
        var xaml = File.ReadAllText(FindPath("PanelWindow.xaml"));
        Assert.Contains("ToolId.Translation", xaml);
        Assert.Contains("ToolId.Notes", xaml);

        // Both tools are hosted lazily, so the panel names their hosts rather
        // than declaring the views inline.
        Assert.Contains("x:Name=\"TranslationTool\"", xaml);
        Assert.Contains("x:Name=\"NotesTool\"", xaml);
    }

    [Fact]
    public void NotesScrollbars_UseNarrowDarkArrowlessTemplate()
    {
        var document = XDocument.Load(FindSharedUiStylePath("ScrollBars.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var style = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key")
                == "FloatingToolsSharedMinimalScrollBarStyle");

        Assert.Contains(style.Elements(presentation + "Setter"),
            setter => (string?)setter.Attribute("Property") == "Width"
                && (string?)setter.Attribute("Value")
                    == "{StaticResource FloatingToolsSizeScrollBarWidth}");
        var thumb = document.Descendants(presentation + "Border")
            .Single(element => (string?)element.Attribute(x + "Name") == "ThumbSurface");
        Assert.Equal(
            "{StaticResource FloatingToolsSizeScrollThumbWidth}",
            (string?)thumb.Attribute("Width"));
        Assert.Single(style.Descendants(presentation + "Track"));
        Assert.DoesNotContain(
            style.Descendants(presentation + "RepeatButton"),
            button => button.Elements().Any());

        var notes = File.ReadAllText(FindPath("NotesToolView.xaml"));
        Assert.Contains("SmoothWheelScrollBehavior.IsEnabled=\"True\"", notes);
        Assert.Equal(
            2,
            notes.Split("Style=\"{StaticResource FloatingToolsSharedScrollViewerStyle}\"")
                .Length - 1);
    }

    [Fact]
    public void ImageContextMenu_ExposesDarkCopyCutDeleteActions()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var headers = document.Descendants(presentation + "MenuItem")
            .Select(item => (string?)item.Attribute("Header"))
            .ToArray();

        Assert.Contains("Copy", headers);
        Assert.Contains("Cut", headers);
        Assert.Contains("Delete", headers);
        Assert.Contains(document.Descendants(presentation + "ContextMenu"),
            menu => (string?)menu.Attribute("Style") == "{StaticResource NotesContextMenuStyle}"
                && menu.Descendants(presentation + "MenuItem")
                    .Any(item => (string?)item.Attribute("Header") == "Copy"));

        var contextMenuStyle = document.Descendants(presentation + "Style")
            .Single(style => (string?)style.Attribute(x + "Key") == "NotesContextMenuStyle");
        Assert.Equal(
            "{StaticResource FloatingToolsSharedContextMenuStyle}",
            (string?)contextMenuStyle.Attribute("BasedOn"));
    }

    [Fact]
    public void NotePage_UsesContinuousTextAndImageBlockTemplates()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace controls = "clr-namespace:FloatingTools.App.Controls";

        Assert.Contains(document.Descendants(presentation + "DataTemplate"),
            template => (string?)template.Attribute("DataType") == "{x:Type models:TextNoteBlock}"
                && template.Descendants(controls + "NoteDirectionalTextBox").Any());
        Assert.Contains(document.Descendants(presentation + "DataTemplate"),
            template => (string?)template.Attribute("DataType") == "{x:Type models:ImageNoteBlock}"
                && template.Descendants(presentation + "Image").Any()
                && template.Descendants(presentation + "Thumb").Any());
        Assert.Contains(document.Descendants(presentation + "ItemsControl"),
            control => (string?)control.Attribute("ItemsSource") == "{Binding ActiveBlocks}");
        Assert.DoesNotContain(document.Descendants(presentation + "TextBlock"),
            element => ((string?)element.Attribute("Text")) is "Text block" or "Image block");
        var editor = document.Descendants(controls + "NoteDirectionalTextBox")
            .Single(element => (string?)element.Attribute("Text")
                == "{Binding Text, UpdateSourceTrigger=PropertyChanged}");
        Assert.Equal("16,5", (string?)editor.Attribute("Padding"));
        Assert.Equal("31", (string?)editor.Attribute("MinHeight"));
        Assert.DoesNotContain("IsAfterImage", document.ToString());
    }

    [Fact]
    public void NotesRuntime_HasNoLegacyLinkTemplateOrInlineHyperlinkUi()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var code = File.ReadAllText(FindPath("NotesToolView.xaml.cs"));
        var viewerXaml = File.ReadAllText(FindPath("ImageViewerWindow.xaml"));
        var viewerCode = File.ReadAllText(FindPath("ImageViewerWindow.xaml.cs"));

        var templateTypes = document.Descendants(presentation + "DataTemplate")
            .Select(template => (string?)template.Attribute("DataType"))
            .Where(type => type?.StartsWith("{x:Type models:", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.Contains("{x:Type models:TextNoteBlock}", templateTypes);
        Assert.Contains("{x:Type models:ImageNoteBlock}", templateTypes);
        Assert.Contains("{x:Type models:LinkListNoteBlock}", templateTypes);
        Assert.DoesNotContain("{x:Type models:LinkNoteBlock}", templateTypes);
        Assert.DoesNotContain("Hyperlink", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("e.ClickCount == 2", code);
        Assert.Contains("new ImageViewerWindow(image.AssetPath", code);
        Assert.Contains("WindowStyle=\"None\"", viewerXaml);
        Assert.Contains("Stretch=\"Uniform\"", viewerXaml);
        Assert.Contains("Key.Escape", viewerCode);
        Assert.Contains("CloseButton_OnClick", viewerCode);
    }

    [Fact]
    public void LinkListRuntime_UsesOneDedicatedTokenControlAndLeavesTextPlain()
    {
        var notes = XDocument.Load(FindPath("NotesToolView.xaml"));
        var control = XDocument.Load(FindControlPath("LinkListBlockControl.xaml"));
        var code = File.ReadAllText(FindControlPath("LinkListBlockControl.xaml.cs"));
        var notesCode = File.ReadAllText(FindPath("NotesToolView.xaml.cs"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace controls = "clr-namespace:FloatingTools.App.Controls";

        Assert.Single(notes.Descendants(presentation + "DataTemplate"), template =>
            (string?)template.Attribute("DataType") == "{x:Type models:LinkListNoteBlock}");
        Assert.Contains(notes.Descendants(presentation + "MenuItem"), item =>
            (string?)item.Attribute("Header") == "Add link block");
        Assert.NotEmpty(notes.Descendants(controls + "LinkListBlockControl"));
        Assert.Contains(notes.Descendants(controls + "LinkListBlockControl"), element =>
            ((string?)element.Attribute("ClampBoundsElement"))?.Contains("NotesContentSurfaceElement") == true);
        Assert.Contains(notes.Descendants(controls + "LinkListBlockControl"), element =>
            ((string?)element.Attribute("PopupHost"))?.Contains("NotesPopupHostElement") == true);
        Assert.Contains(notes.Descendants(presentation + "Border"), element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Name"
                && attribute.Value == "NotesContentSurface"));
        Assert.Contains(notes.Descendants()
            .Where(element => element.Name.LocalName == "AnchoredPopupHost"),
            element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name"
                && attribute.Value == "NotesPopupHost"));
        Assert.DoesNotContain("Make link", notes.ToString());

        var linkText = control.Descendants(presentation + "TextBlock")
            .Single(item => (string?)item.Attribute("Text") == "{Binding VisibleName}");
        Assert.Equal("#FF0563C1", (string?)linkText.Attribute("Foreground"));
        Assert.Equal("Underline", (string?)linkText.Attribute("TextDecorations"));
        Assert.Equal("Hand", (string?)linkText.Attribute("Cursor"));
        Assert.Equal("Wrap", (string?)linkText.Attribute("TextWrapping"));
        Assert.NotNull(linkText.Attribute("MaxWidth"));
        Assert.Contains("AncestorType={x:Type ItemsControl}", (string?)linkText.Attribute("MaxWidth"));
        Assert.Equal("Stretch", (string?)control.Descendants(presentation + "ItemsControl")
            .Single(item => item.Attributes().Any(attribute => attribute.Name.LocalName == "Name"
                && attribute.Value == "LinkItemsHost"))
            .Attribute("HorizontalAlignment"));
        var draft = control.Descendants(presentation + "TextBox")
            .Single(item => (string?)item.Attribute("Name") == "DraftToken"
                || item.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "DraftToken"));
        Assert.Equal("0", (string?)draft.Attribute("BorderThickness"));
        Assert.DoesNotContain("IsOpen=\"True\"", control.ToString());
        Assert.Contains("FocusDraft", code);
        Assert.Contains("LinkListItemActionsViewModel", code);
        Assert.Contains("OpenLinkItemCommand", code);
        Assert.Contains("CopyLinkItemCommand", code);
        Assert.Contains("EditLinkItemCommand", code);
        Assert.Contains("DeleteLinkItemCommand", code);
        Assert.Contains("clickCount == 2", code);
        Assert.Contains("ModifierKeys.Control", code);
        Assert.Contains("Clipboard.ContainsText()", code);
        Assert.Contains("DeleteLastLinkItemOrBlockAsync", code);
        Assert.Contains("BlockContextMenu_OnOpened", code);
        Assert.Contains("new PopupAnchorRequest(target, content, clampBoundsElement)", code);
        Assert.Contains("PreferredPlacement = PopupAnchorPreferredPlacement.Below", code);
        Assert.Contains("CloseOnScrollOf = notesScrollViewer", code);
        Assert.Contains("SingleInstance = true", code);
        Assert.DoesNotContain("Owner.OpenLinkItemAsync", code);
        Assert.DoesNotContain("Owner.CopyLinkItem", code);
        Assert.DoesNotContain("Owner.EditLinkItemAsync", code);
        Assert.DoesNotContain("Owner.DeleteLinkItemAsync", code);
        Assert.DoesNotContain("TransformToVisual", code);
        Assert.DoesNotContain("HorizontalOffset", code);
        Assert.DoesNotContain("VerticalOffset", code);
        Assert.Contains("NotesContextMenuStyle", code);
        Assert.Contains("NotesActionMenuItemStyle", code);
        Assert.Contains("NotesMenuSurfaceStyle", code);
        Assert.Contains("PopupAnchorService", code);
        Assert.DoesNotContain("LinkToolbarPopup", code);
        Assert.DoesNotContain("NotesScrollViewer_OnScrollChanged", code);
        Assert.DoesNotContain("_activeToolbarOwner", code);
        Assert.DoesNotContain("NotesToolView_OnPreviewMouseLeftButtonDown", notesCode);
        Assert.Empty(control.Descendants(presentation + "Popup"));
        var actionToolbar = control.Descendants(presentation + "StackPanel")
            .Single(item => (string?)item.Attribute(x + "Name") == "ActionToolbar");
        Assert.Equal("Horizontal", (string?)actionToolbar.Attribute("Orientation"));
        var toolTips = actionToolbar.Descendants(presentation + "Button")
            .Select(item => (string?)item.Attribute("ToolTip"))
            .OfType<string>()
            .ToArray();
        Assert.Equal(["Open", "Copy", "Edit", "Delete"], toolTips);
    }

    [Fact]
    public void InlineRename_HandlesEnterEscapeAndLostFocus()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace controls = "clr-namespace:FloatingTools.App.Controls";
        var rename = document.Descendants(controls + "NoteDirectionalTextBox")
            .Single(element => (string?)element.Attribute("PreviewKeyDown")
                == "RenameTextBox_OnPreviewKeyDown");

        Assert.Equal("RenameTextBox_OnLostKeyboardFocus",
            (string?)rename.Attribute("LostKeyboardFocus"));
        Assert.Equal("RenameTextBox_OnIsVisibleChanged",
            (string?)rename.Attribute("IsVisibleChanged"));
    }

    [Fact]
    public void NotesMenu_IsFlatAndRenameUsesCustomDarkInputTemplate()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var menu = document.Descendants(presentation + "Border")
            .Single(element => (string?)element.Attribute("Visibility")
                == "{Binding IsMenuOpen, Converter={StaticResource BooleanToVisibilityConverter}}");
        var inputStyle = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == "NotesInputTextBoxStyle");

        Assert.Equal("0", (string?)menu.Attribute("BorderThickness"));
        Assert.Equal("0", (string?)menu.Attribute("CornerRadius"));
        Assert.Equal(
            "{DynamicResource FloatingToolsBrushSurfaceHeader}",
            (string?)menu.Attribute("Background"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedTextBoxStyle}",
            (string?)inputStyle.Attribute("BasedOn"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedTextBoxStyle}",
            (string?)inputStyle.Attribute("BasedOn"));
        Assert.Contains(inputStyle.Descendants(presentation + "Border"),
            border => (string?)border.Attribute("BorderBrush")
                == "{DynamicResource FloatingToolsBrushTransparent}");
    }

    [Fact]
    public void NoteActionsMenu_ExposesPdfAndWordExports()
    {
        var document = XDocument.Load(FindPath("NotesToolView.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var export = document.Descendants(presentation + "MenuItem")
            .Single(item => (string?)item.Attribute("Header") == "Export");
        var menuStyle = document.Descendants(presentation + "Style")
            .Single(style => (string?)style.Attribute(x + "Key") == "NotesActionMenuItemStyle");
        var template = menuStyle.Descendants(presentation + "ControlTemplate").Single();
        var submenuPopup = template.Descendants(presentation + "Popup")
            .Single(popup => (string?)popup.Attribute(x + "Name") == "PART_Popup");

        Assert.True(export.Elements(presentation + "MenuItem").Count() == 2);
        Assert.Null(export.Attribute("Click"));
        Assert.Equal("True", (string?)export.Attribute("StaysOpenOnClick"));
        Assert.Contains(export.Elements(presentation + "MenuItem"), item =>
            (string?)item.Attribute("Header") == "PDF"
            && (string?)item.Attribute("Click") == "ExportPdfMenuItem_OnClick");
        Assert.Contains(export.Elements(presentation + "MenuItem"), item =>
            (string?)item.Attribute("Header") == "Word (.docx)"
            && (string?)item.Attribute("Click") == "ExportWordMenuItem_OnClick");
        Assert.Equal("Right", (string?)submenuPopup.Attribute("Placement"));
        Assert.Equal(
            "{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}",
            (string?)submenuPopup.Attribute("IsOpen"));
        Assert.Equal(
            "{StaticResource FloatingToolsSharedMenuItemStyle}",
            (string?)menuStyle.Attribute("BasedOn"));
        Assert.Equal("Auto", menuStyle.Elements(presentation + "Setter")
            .Single(setter => (string?)setter.Attribute("Property") == "Height")
            .Attribute("Value")?.Value);
        Assert.Contains(template.Descendants(presentation + "StackPanel"),
            panel => (string?)panel.Attribute("IsItemsHost") == "True");
        Assert.Contains(template.Descendants(presentation + "Trigger"),
            trigger => (string?)trigger.Attribute("Property") == "HasItems"
                && (string?)trigger.Attribute("Value") == "True");
    }

    [Fact]
    public void NoteExportService_ShowsFriendlyFailureAndSafeDebugDiagnostic()
    {
        var service = File.ReadAllText(FindServicePath("WindowsNoteExportService.cs"));

        Assert.Contains("Could not export note.", service);
        Assert.Contains("Notes export failed:", service);
        Assert.DoesNotContain("note.Title", service.Split("Debug.WriteLine", StringSplitOptions.None).Last());
    }

    private static string FindPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return Path.Combine(directory.FullName, "src", "FloatingTools.App", "Views", fileName);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }

    private static string FindControlPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
                return Path.Combine(directory.FullName, "src", "FloatingTools.App", "Controls", fileName);
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }

    private static string FindResourcePath(string fileName)
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
                    "Resources",
                    fileName);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }

    private static string FindSharedUiStylePath(string fileName)
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
                    "SharedUi",
                    "Styles",
                    fileName);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }

    private static string FindServicePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FloatingTools.sln")))
            {
                return Path.Combine(directory.FullName, "src", "FloatingTools.App", "Services", fileName);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the FloatingTools solution.");
    }
}
