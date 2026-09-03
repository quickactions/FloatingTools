using FloatingTools.App.Models;
using FloatingTools.App.Services;

namespace FloatingTools.Tests.Services;

public sealed class PanelSizeCalculatorTests
{
    [Fact]
    public void Standard_UsesExpectedFixedSize()
    {
        var result = PanelSizeCalculator.GetRequestedActiveToolSize(
            PanelSizePreset.Standard);

        Assert.Equal(new ToolSize(300, 500), result);
    }

    [Fact]
    public void Large_UsesExpectedFixedSize()
    {
        var result = PanelSizeCalculator.GetRequestedActiveToolSize(
            PanelSizePreset.Large);

        Assert.Equal(new ToolSize(560, 760), result);
    }

    [Fact]
    public void ActiveToolSize_IsLimitedToAvailableWorkingArea()
    {
        var result = PanelSizeCalculator.GetActiveToolSize(
            PanelSizePreset.Large,
            availableWidthDip: 480,
            availableHeightDip: 620);

        Assert.Equal(new ToolSize(480, 620), result);
    }

    [Fact]
    public void ActiveToolSize_NeverFallsBelowMinimumWhenMinimumFits()
    {
        var result = PanelSizeCalculator.GetActiveToolSize(
            PanelSizePreset.Standard,
            availableWidthDip: 900,
            availableHeightDip: PanelSizeCalculator.MinimumActiveToolHeight);

        Assert.Equal(
            PanelSizeCalculator.MinimumActiveToolHeight,
            result.Height);
    }

    [Fact]
    public void RequiredHeight_UsesFullPresetWhenItFits()
    {
        Assert.Equal(
            500,
            PanelSizeCalculator.GetRequiredActiveToolHeight(
                PanelSizePreset.Standard,
                maximumAvailableHeightDip: 900));
        Assert.Equal(
            760,
            PanelSizeCalculator.GetRequiredActiveToolHeight(
                PanelSizePreset.Large,
                maximumAvailableHeightDip: 900));
    }

    [Fact]
    public void RequiredHeight_UsesAllAvailableSpaceOnExceptionallySmallScreen()
    {
        var result = PanelSizeCalculator.GetRequiredActiveToolHeight(
            PanelSizePreset.Standard,
            maximumAvailableHeightDip: 280);

        Assert.Equal(280, result);
    }

    [Fact]
    public void InvalidPreset_FallsBackToStandard()
    {
        var result = PanelSizeCalculator.GetRequestedActiveToolSize(
            (PanelSizePreset)999);

        Assert.Equal(new ToolSize(300, 500), result);
        Assert.Equal(
            PanelSizePreset.Standard,
            PanelSizeCalculator.NormalizePreset((PanelSizePreset)999));
    }

    [Fact]
    public void GetToolMenuHeight_UsesOneCompactRowForOneTool()
    {
        var result = PanelSizeCalculator.GetToolMenuHeight(toolCount: 1);

        Assert.Equal(118, result);
    }

    [Fact]
    public void GetToolMenuHeight_AddsARowAfterFourTools()
    {
        var result = PanelSizeCalculator.GetToolMenuHeight(toolCount: 5);

        Assert.Equal(166, result);
    }

    [Fact]
    public void ToolMenu_UsesExpectedCompactWidth()
    {
        Assert.Equal(220, PanelSizeCalculator.ToolMenuWidth);
    }

    [Fact]
    public void ToolMenuIconSize_MatchesTheFortyFourPixelTilePlusItsTwoPixelMargin()
    {
        Assert.Equal(48, PanelSizeCalculator.ToolMenuIconSize);
    }
}
