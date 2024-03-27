namespace Memento.Events;

/// <summary>
/// Represents a collection element index change event.
/// </summary>
public sealed class ElementIndexChangeEvent<T> : BaseEvent
{
    /// <summary>
    /// The collection this event occurs on.
    /// </summary>
    public IList<T> Collection { get; }

    /// <summary>
    /// The element whose index was changed.
    /// </summary>
    public T Element { get; }

    /// <summary>
    /// The index to be restored too when undo.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Creates the event.
    /// </summary>
    /// <param name="collection">The collection object.</param>
    /// <param name="element">The element whose index is being changed.</param>
    /// <param name="index"/>The index of the element in the collection. If not supplied, use current index of <paramref name="element"/> in the <paramref name="collection"/>.
    public ElementIndexChangeEvent(IList<T> collection, T element, int? index = null)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        index ??= collection.IndexOf(element);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "'{index}' must be a non-negative and non-zero value.");
        }

        Element = element;
        Index = index.Value;
    }

    protected internal override BaseEvent Rollback()
    {
        var reverse = new ElementIndexChangeEvent<T>(Collection, Element);
        Collection.Remove(Element);
        Collection.Insert(Index, Element);
        return reverse;
    }
}