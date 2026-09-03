using System.Windows;
using System.Windows.Controls;
using FloatingTools.App.Models;

namespace FloatingTools.App.Controls;

public static class NoteMenuContextResolver
{
    public static NoteDocument? Resolve(MenuItem? menuItem)
    {
        if (menuItem is null)
        {
            return null;
        }

        if (menuItem.DataContext is NoteDocument note)
        {
            return note;
        }

        ItemsControl? owner = ItemsControl.ItemsControlFromItemContainer(menuItem);
        while (owner is MenuItem parentItem)
        {
            if (parentItem.DataContext is NoteDocument parentNote)
            {
                return parentNote;
            }

            owner = ItemsControl.ItemsControlFromItemContainer(parentItem);
        }

        return (owner as ContextMenu)?.PlacementTarget is FrameworkElement target
            ? target.DataContext as NoteDocument
            : null;
    }
}
