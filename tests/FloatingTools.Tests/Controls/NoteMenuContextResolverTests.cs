using System.Threading;
using System.Windows.Controls;
using FloatingTools.App.Controls;
using FloatingTools.App.Models;

namespace FloatingTools.Tests.Controls;

public sealed class NoteMenuContextResolverTests
{
    [Fact]
    public void Resolve_FindsNoteForNestedExportSubmenuItem()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var note = new NoteDocument { Id = Guid.NewGuid() };
                var button = new Button { DataContext = note };
                var contextMenu = new ContextMenu { PlacementTarget = button };
                var export = new MenuItem { Header = "Export" };
                var pdf = new MenuItem { Header = "PDF" };
                export.Items.Add(pdf);
                contextMenu.Items.Add(export);

                Assert.Same(note, NoteMenuContextResolver.Resolve(pdf));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }
}
