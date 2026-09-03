using System.Windows;
using System.Threading;
using FloatingTools.App.Controls;

namespace FloatingTools.Tests.Views;

public sealed class DirectionalTextBoxTests
{
    [Fact]
    public void TextChanges_UpdateTheActualTextBoxDirectionAndAlignment()
    {
        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                var textBox = new DirectionalTextBox
                {
                    Text = "שלום"
                };

                Assert.Equal(FlowDirection.RightToLeft, textBox.FlowDirection);
                Assert.Equal(TextAlignment.Right, textBox.TextAlignment);

                textBox.Text = "Hello";

                Assert.Equal(FlowDirection.LeftToRight, textBox.FlowDirection);
                Assert.Equal(TextAlignment.Left, textBox.TextAlignment);
            }
            catch (Exception exception)
            {
                threadException = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(threadException);
    }
}
