using System.Reflection;

namespace Memento.Events;

/// <summary>
/// Represents a property value change event.
/// </summary>
public sealed class PropertyChangeEvent : BaseEvent
{
    /// <summary>
    /// The target object this event occurs on.
    /// </summary>
    public object TargetObject { get; }

    /// <summary>
    /// The name of the changed property.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// The value to be restored to when undo.
    /// </summary>
    public object? PropertyValue { get; }

    private readonly PropertyInfo _propertyInfo;

    /// <summary>
    /// Creates the event.
    /// </summary>
    /// <param name="target">The target object whose property is changed.</param>
    /// <param name="propertyName">The name of the property being changed.</param>
    /// <param name="propertyValue">The value of the property. If not supplied, use the current value of <paramref name="propertyName"/> in <paramref name="target"/></param>
    public PropertyChangeEvent(object target, string propertyName, object? propertyValue = null)
        : this(target, propertyName, propertyValue, GetPropertyInfo(target, propertyName))
    {
    }

    private PropertyChangeEvent(object target, string propertyName, object? propertyValue, PropertyInfo? propertyInfo)
    {
        TargetObject = target ?? throw new ArgumentNullException(nameof(target));
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _propertyInfo = propertyInfo ?? throw new ArgumentException("Property not found.", nameof(propertyName));
        PropertyValue = propertyValue ?? propertyInfo.GetValue(target, null);
    }

    protected internal override Task<BaseEvent> Rollback(CancellationToken cancellationToken)
    {
        var reverse = new PropertyChangeEvent(TargetObject, PropertyName, null, _propertyInfo);
        _propertyInfo.SetValue(TargetObject, PropertyValue, null);
        return Task.FromResult<BaseEvent>(reverse);
    }

    private static PropertyInfo? GetPropertyInfo(object target, string propertyName) =>
        target.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
}