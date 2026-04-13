using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PackTracker.Presentation.Behaviors;

/// <summary>
/// Auto-scrolls message hosts to the latest item when their bound collection grows.
/// </summary>
public static class AutoScrollBehavior
{
    private static readonly ConditionalWeakTable<DependencyObject, NotifyCollectionChangedEventHandler> HandlerTable = new();

    public static readonly DependencyProperty AutoScrollSourceProperty =
        DependencyProperty.RegisterAttached(
            "AutoScrollSource",
            typeof(IEnumerable),
            typeof(AutoScrollBehavior),
            new PropertyMetadata(null, OnAutoScrollSourceChanged));

    public static IEnumerable? GetAutoScrollSource(DependencyObject obj) =>
        (IEnumerable?)obj.GetValue(AutoScrollSourceProperty);

    public static void SetAutoScrollSource(DependencyObject obj, IEnumerable? value) =>
        obj.SetValue(AutoScrollSourceProperty, value);

    private static void OnAutoScrollSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        Unsubscribe(d, e.OldValue as INotifyCollectionChanged);

        if (e.NewValue is not INotifyCollectionChanged collection)
            return;

        NotifyCollectionChangedEventHandler handler = (_, args) =>
        {
            if (args.Action is NotifyCollectionChangedAction.Add
                or NotifyCollectionChangedAction.Reset
                or NotifyCollectionChangedAction.Replace)
            {
                ScrollToEnd(d);
            }
        };

        HandlerTable.Remove(d);
        HandlerTable.Add(d, handler);
        collection.CollectionChanged += handler;

        ScrollToEnd(d);
    }

    private static void Unsubscribe(DependencyObject d, INotifyCollectionChanged? collection)
    {
        if (collection is null)
            return;

        if (HandlerTable.TryGetValue(d, out var handler))
        {
            collection.CollectionChanged -= handler;
            HandlerTable.Remove(d);
        }
    }

    private static void ScrollToEnd(DependencyObject host)
    {
        switch (host)
        {
            case ListBox listBox:
                listBox.Dispatcher.BeginInvoke(() =>
                {
                    if (listBox.Items.Count > 0)
                        listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
                }, DispatcherPriority.Background);
                break;

            case ScrollViewer scrollViewer:
                scrollViewer.Dispatcher.BeginInvoke(scrollViewer.ScrollToEnd, DispatcherPriority.Background);
                break;
        }
    }
}
