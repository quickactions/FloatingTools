using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FloatingTools.App.SharedUi.Controls;

namespace FloatingTools.Tests.SharedUi.Controls;

public sealed class TripleClickSelectAllBehaviorTests
{
    [Fact]
    public void HandleClick_ClickCountThree_SelectsEntireTextAndMarksHandled()
        => RunInSta(() =>
        {
            var textBox = new TextBox { Text = "Hello selectable world" };
            var args = RaiseLeftButtonDown(textBox, clickCount: 3);

            TripleClickSelectAllBehavior.HandleClick(textBox, args);

            Assert.Equal(textBox.Text, textBox.SelectedText);
            Assert.Equal("Hello selectable world", textBox.Text);
            Assert.True(args.Handled);
        });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void HandleClick_ClickCountOneOrTwo_LeavesSelectionAndHandledUntouched(int clickCount)
        => RunInSta(() =>
        {
            var textBox = new TextBox { Text = "Hello selectable world" };
            textBox.Select(0, 0);
            var args = RaiseLeftButtonDown(textBox, clickCount);

            TripleClickSelectAllBehavior.HandleClick(textBox, args);

            Assert.Equal(0, textBox.SelectionLength);
            Assert.False(args.Handled);
            Assert.Equal("Hello selectable world", textBox.Text);
        });

    [Fact]
    public void AttachedBehavior_WhenEnabled_SelectsAllOnRealTripleClickEvent()
        => RunInSta(() =>
        {
            var textBox = new TextBox { Text = "Hello selectable world" };
            TripleClickSelectAllBehavior.SetIsEnabled(textBox, true);

            var args = RaiseLeftButtonDown(textBox, clickCount: 3);
            textBox.RaiseEvent(args);

            Assert.Equal(textBox.Text, textBox.SelectedText);
            Assert.True(args.Handled);
        });

    [Fact]
    public void AttachedBehavior_WhenNotEnabled_DoesNotSelectAllOnTripleClick()
        => RunInSta(() =>
        {
            var textBox = new TextBox { Text = "Hello selectable world" };
            textBox.Select(0, 0);

            var args = RaiseLeftButtonDown(textBox, clickCount: 3);
            textBox.RaiseEvent(args);

            Assert.Equal(0, textBox.SelectionLength);
            Assert.False(args.Handled);
        });

    [Fact]
    public void AttachedBehavior_OnlyAffectsTheTargetTextBoxNotASibling()
        => RunInSta(() =>
        {
            var target = new TextBox { Text = "Target text content" };
            var sibling = new TextBox { Text = "Sibling text content" };
            TripleClickSelectAllBehavior.SetIsEnabled(target, true);
            TripleClickSelectAllBehavior.SetIsEnabled(sibling, true);
            sibling.Select(0, 0);

            var args = RaiseLeftButtonDown(target, clickCount: 3);
            target.RaiseEvent(args);

            Assert.Equal(target.Text, target.SelectedText);
            Assert.Equal(0, sibling.SelectionLength);
        });

    [Fact]
    public void AttachedBehavior_DisablingRemovesTheHandler()
        => RunInSta(() =>
        {
            var textBox = new TextBox { Text = "Hello selectable world" };
            TripleClickSelectAllBehavior.SetIsEnabled(textBox, true);
            TripleClickSelectAllBehavior.SetIsEnabled(textBox, false);
            textBox.Select(0, 0);

            var args = RaiseLeftButtonDown(textBox, clickCount: 3);
            textBox.RaiseEvent(args);

            Assert.Equal(0, textBox.SelectionLength);
            Assert.False(args.Handled);
        });

    private static MouseButtonEventArgs RaiseLeftButtonDown(UIElement target, int clickCount)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            Source = target
        };
        SetClickCount(args, clickCount);
        return args;
    }

    private static void SetClickCount(MouseButtonEventArgs args, int clickCount)
    {
        var property = typeof(MouseButtonEventArgs).GetProperty(
            nameof(MouseButtonEventArgs.ClickCount),
            BindingFlags.Public | BindingFlags.Instance)!;
        var setter = property.GetSetMethod(nonPublic: true)!;
        setter.Invoke(args, [clickCount]);
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null)
        {
            throw new Xunit.Sdk.XunitException(exception.ToString());
        }
    }
}
