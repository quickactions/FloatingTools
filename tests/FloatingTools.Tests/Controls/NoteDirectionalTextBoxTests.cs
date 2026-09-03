using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using FloatingTools.App.Controls;

namespace FloatingTools.Tests.Controls;

public sealed class NoteDirectionalTextBoxTests
{
    [Theory]
    [InlineData("שלום", FlowDirection.RightToLeft, TextAlignment.Right)]
    [InlineData("hello", FlowDirection.LeftToRight, TextAlignment.Left)]
    [InlineData("123 שלום", FlowDirection.RightToLeft, TextAlignment.Right)]
    [InlineData("... hello", FlowDirection.LeftToRight, TextAlignment.Left)]
    [InlineData("שלום hello", FlowDirection.RightToLeft, TextAlignment.Right)]
    [InlineData("hello שלום", FlowDirection.LeftToRight, TextAlignment.Left)]
    [InlineData("123 !!!", FlowDirection.LeftToRight, TextAlignment.Left)]
    public void Text_UpdatesPhysicalDirectionAndAlignment(
        string text,
        FlowDirection expectedDirection,
        TextAlignment expectedAlignment)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var textBox = new NoteDirectionalTextBox { Text = text };
                Assert.Equal(expectedDirection, textBox.FlowDirection);
                Assert.Equal(expectedAlignment, textBox.TextAlignment);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
