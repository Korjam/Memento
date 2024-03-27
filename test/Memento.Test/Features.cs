using Memento.Events;
using Memento.Test.Stubs;

namespace Memento.Test;

public class Features : IDisposable
{
    private Mementor m;

    public Features()
    {
        m = Session.New();
    }

    public void Dispose()
    {
        Session.End();
    }

    [Fact]
    public void Should_initialize_correctly()
    {
        UndoCount(0).RedoCount(0);
        Assert.True(m.IsTrackingEnabled);
    }

    [Fact]
    public async Task Should_undo_redo_property_change()
    {
        var c = new Circle();
        for (int i = 0; i < 10; i++)
        {
            c.Radius = i + 1;
            UndoCount(i + 1).RedoCount(0);
        }
        for (int i = 9; i >= 0; i--)
        {
            await m.Undo();
            Assert.Equal(i, c.Radius);
            UndoCount(i).RedoCount(9 - i + 1);
        }
        for (int i = 0; i < 10; i++)
        {
            await m.Redo();
            Assert.Equal(i + 1, c.Radius);
            UndoCount(i + 1).RedoCount(9 - i);
        }
    }

    [Fact]
    public async Task Should_allow_provide_property_value()
    {
        var c = new Circle();
        UndoCount(0);
        m.PropertyChange(c, () => c.Radius, 10);
        await m.Undo();
        Assert.Equal(10, c.Radius);
    }

    [Fact]
    public async Task Should_undo_redo_complex_property_change()
    {
        var c = new Circle();
        for (int i = 0; i < 10; i++)
        {
            c.Center = new Point(i + 1, i + 1);
            UndoCount(i + 1).RedoCount(0);
        }
        for (int i = 9; i >= 0; i--)
        {
            await m.Undo();
            Assert.Equal(new Point(i, i), c.Center);
            UndoCount(i).RedoCount(9 - i + 1);
        }
        for (int i = 0; i < 10; i++)
        {
            await m.Redo();
            Assert.Equal(new Point(i + 1, i + 1), c.Center);
            UndoCount(i + 1).RedoCount(9 - i);
        }
    }

    [Fact]
    public async Task Should_undo_multiple_properties_change()
    {
        var c = new Circle { Radius = 10, Center = new Point(10, 10) };
        UndoCount(2);

        await m.Undo();
        Assert.Equal(new Point(0, 0), c.Center);
        UndoCount(1);

        await m.Undo();
        Assert.Equal(0, c.Radius);
        UndoCount(0);
    }

    [Fact]
    public void Should_reset_to_initial_states()
    {
        new Circle { Radius = 10, Center = new Point(10, 10) };
        UndoCount(2);

        m.Reset();
        UndoCount(0).RedoCount(0);
    }

    [Fact]
    public async Task Should_clear_redo_after_a_forward_change()
    {
        var c = new Circle { Radius = 10 };
        UndoCount(1).RedoCount(0);

        await m.Undo();
        UndoCount(0).RedoCount(1);

        c.Radius++;
        UndoCount(1).RedoCount(0);
    }

    [Fact]
    public async Task Should_be_able_to_piggy_back_undo_redo()
    {
        var c = new Circle { Radius = 10 };
        UndoCount(1).RedoCount(0);

        await m.Undo();
        Assert.Equal(0, c.Radius);
        UndoCount(0).RedoCount(1);

        await m.Redo();
        Assert.Equal(10, c.Radius);
        UndoCount(1).RedoCount(0);

        await m.Undo();
        Assert.Equal(0, c.Radius);
        UndoCount(0).RedoCount(1);

        await m.Redo();
        Assert.Equal(10, c.Radius);
        UndoCount(1).RedoCount(0);
    }

    [Fact]
    public async Task Should_undo_redo_whole_batch()
    {
        var circles = new Circle[10];
        for (int i = 0; i < circles.Length; i++)
        {
            circles[i] = new Circle();
        }

        m.Batch(() =>
        {
            foreach (Circle circle in circles)
            {
                circle.Radius = 5;
                circle.Center = new Point(5, 5);
            }
        });
        UndoCount(1);

        await m.Undo();
        foreach (Circle circle in circles)
        {
            Assert.Equal(0, circle.Radius);
            Assert.Equal(new Point(0, 0), circle.Center);
        }
        RedoCount(1);

        await m.Redo();
        foreach (Circle circle in circles)
        {
            Assert.Equal(5, circle.Radius);
            Assert.Equal(new Point(5, 5), circle.Center);
        }
    }

    [Fact]
    public void Should_throw_if_nesting_batches()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            m.Batch(() => m.Batch(() => new Circle() { Radius = 5 }));
        });
    }

    [Fact]
    public void Should_throw_if_nesting_batches_via_explicit_calls()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            m.BeginBatch();
            m.BeginBatch();
            m.EndBatch();
        });
    }

    [Fact]
    public void Should_throw_if_end_batch_without_starting_one()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            m.EndBatch();
        });
    }

    [Fact]
    public void Should_not_throw_if_end_batch_after_starting_one()
    {
        for (var i = 0; i < 10; i++)
        {
            m.BeginBatch();
            m.EndBatch();
        }
    }

    [Fact]
    public void Should_track_based_on_enabling_setting()
    {
        m.IsTrackingEnabled = false;
        new Circle { Radius = 5 };
        UndoCount(0);

        m.IsTrackingEnabled = true;
        new Circle { Radius = 5 };
        UndoCount(1);
    }

    [Fact]
    public void Should_not_track_during_a_none_tracking_execution()
    {
        Assert.True(m.IsTrackingEnabled);
        m.ExecuteNoTrack(() =>
        {
            Assert.False(m.IsTrackingEnabled);
            new Circle { Radius = 5, Center = new Point(5, 5) };
        });
        Assert.True(m.IsTrackingEnabled);
        UndoCount(0);
    }

    [Fact]
    public void Should_allow_nested_disabling_tracking()
    {
        Assert.True(m.IsTrackingEnabled);
        m.ExecuteNoTrack(() =>
        {
            new Circle { Radius = 5, Center = new Point(5, 5) };
            m.ExecuteNoTrack(() => { new Circle { Radius = 5, Center = new Point(5, 5) }; });
        });
        Assert.True(m.IsTrackingEnabled);
        UndoCount(0);
    }

    [Fact]
    public void Should_restore_to_previous_tracking_states_in_a_recursive_manner()
    {
        m.IsTrackingEnabled = false;
        m.ExecuteNoTrack(() =>
        {
            m.IsTrackingEnabled = true;
            m.ExecuteNoTrack(() => { });
            Assert.True(m.IsTrackingEnabled);
        });
        Assert.False(m.IsTrackingEnabled);
    }

    [Fact]
    public void Should_allow_temporary_enabling_during_no_track_context()
    {
        m.ExecuteNoTrack(() =>
        {
            var c = new Circle { Radius = 5 };
            m.IsTrackingEnabled = true;
            c.Radius++;
            m.IsTrackingEnabled = false;
            c.Radius++;
        });
        UndoCount(1);
    }

    [Fact]
    public async Task Should_undo_redo_collection_addition()
    {
        var screen = new Screen();
        var circle = new Circle();
        screen.Add(circle);
        UndoCount(1);

        await m.Undo();
        Assert.Empty(screen.Shapes);

        await m.Redo();
        Assert.Same(circle, screen.Shapes[0]);
    }

    [Fact]
    public async Task Should_undo_redo_collection_removal()
    {
        var screen = new Screen();
        var circle = new Circle();
        screen.Add(circle);
        m.Reset();

        screen.Remove(circle);
        UndoCount(1);

        await m.Undo();
        Assert.Same(circle, screen.Shapes[0]);

        await m.Redo();
        Assert.Empty(screen.Shapes);
    }

    [Fact]
    public async Task Should_undo_redo_collection_position_change()
    {
        var screen = new Screen();
        Circle circle1, circle2;
        screen.Add(circle1 = new Circle());
        screen.Add(circle2 = new Circle());
        m.Reset();

        screen.MoveToFront(1);
        Assert.Same(circle2, screen.Shapes[0]);
        Assert.Same(circle1, screen.Shapes[1]);

        await m.Undo();
        Assert.Same(circle1, screen.Shapes[0]);
        Assert.Same(circle2, screen.Shapes[1]);

        await m.Redo();
        Assert.Same(circle2, screen.Shapes[0]);
        Assert.Same(circle1, screen.Shapes[1]);
    }

    [Fact]
    public void Should_throw_when_removing_non_existent_element()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var screen = new Screen();
            screen.Remove(new Circle());
        });
    }

    [Fact]
    public void Should_throw_when_changing_position_of_non_existent_element()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var screen = new Screen();
            screen.Add(new Circle());
            m.ElementIndexChange(screen.Shapes, new Circle());
        });
    }

    [Fact]
    public async Task Should_undo_redo_collection_changes_in_batch()
    {
        var screen = new Screen();
        m.Batch(() =>
        {
            var circle = new Circle();
            screen.Add(new Circle { Radius = 10 });
            screen.Add(circle);
            screen.MoveToFront(1);
            screen.Remove(circle);
        });
        Assert.Single(screen.Shapes);
        UndoCount(1);

        await m.Undo();
        Assert.Empty(screen.Shapes);
    }

    [Fact]
    public async Task Should_undo_redo_collection_changes_in_explicit_batch()
    {
        var screen = new Screen();
        m.BeginBatch();
        try
        {
            var circle = new Circle();
            screen.Add(new Circle { Radius = 10 });
            screen.Add(circle);
            screen.MoveToFront(1);
            screen.Remove(circle);
        }
        finally
        {
            m.EndBatch();
        }
        Assert.Single(screen.Shapes);
        UndoCount(1);

        await m.Undo();
        Assert.Empty(screen.Shapes);
    }

    [Fact]
    public async Task Should_fire_events()
    {
        int count = 0;
        m.Changed += (_, args) => count++;

        var circle = new Circle { Radius = 5 };
        Assert.Equal(1, count);

        circle.Center = new Point(5, 5);
        Assert.Equal(2, count);

        m.Batch(() => new Circle { Radius = 5, Center = new Point(5, 5) });
        Assert.Equal(3, count);

        await m.Undo();
        Assert.Equal(4, count);

        await m.Redo();
        Assert.Equal(5, count);

        m.IsTrackingEnabled = false;
        new Circle { Radius = 5 };
        Assert.Equal(5, count);
        m.IsTrackingEnabled = true;

        m.ExecuteNoTrack(() => new Circle { Radius = 5, Center = new Point() });
        Assert.Equal(5, count);

        m.Reset();
        Assert.Equal(6, count);
    }

    [Fact]
    public async Task Should_fire_property_change_event()
    {
        new Circle { Radius = 10 };
        m.Changed += (_, args) =>
        {
            Assert.Equal(typeof(PropertyChangeEvent), args.Event?.GetType());
            Assert.Equal(0, ((PropertyChangeEvent)args.Event!).PropertyValue);
        };
        await m.Undo();
    }

    [Fact]
    public async Task Should_fire_collection_addition_event()
    {
        var screen = new Screen();
        var circle = new Circle();

        int count = 0;
        m.Changed += (_, args) =>
        {
            Assert.Equal(typeof(ElementAdditionEvent<Circle>), args.Event?.GetType());
            Assert.Same(screen.Shapes, ((ElementAdditionEvent<Circle>)args.Event!).Collection);
            Assert.Same(circle, ((ElementAdditionEvent<Circle>)args.Event).Element);
            count++;
        };
        screen.Add(circle);
        await m.Undo();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Should_fire_collection_removal_event()
    {
        var screen = new Screen();
        var circle = new Circle();
        screen.Add(circle);

        int count = 0;
        m.Changed += (_, args) =>
        {
            Assert.Equal(typeof(ElementRemovalEvent<Circle>), args.Event?.GetType());
            Assert.Same(screen.Shapes, ((ElementRemovalEvent<Circle>)args.Event!).Collection);
            Assert.Same(circle, ((ElementRemovalEvent<Circle>)args.Event).Element);
            Assert.Equal(0, ((ElementRemovalEvent<Circle>)args.Event).Index);
            count++;
        };
        screen.Remove(circle);
        await m.Undo();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Should_fire_collection_element_position_change_event()
    {
        var screen = new Screen();
        var circle = new Circle();
        screen.Add(new Circle());
        screen.Add(circle);

        int count = 0;
        m.Changed += (_, args) =>
        {
            Assert.Equal(typeof(ElementIndexChangeEvent<Circle>), args.Event?.GetType());
            Assert.Same(screen.Shapes, ((ElementIndexChangeEvent<Circle>)args.Event!).Collection);
            Assert.Same(circle, ((ElementIndexChangeEvent<Circle>)args.Event).Element);
            Assert.Equal(1, ((ElementIndexChangeEvent<Circle>)args.Event).Index);
            count++;
        };
        screen.MoveToFront(1);
        await m.Undo();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Should_fire_batch_event()
    {
        int count = 0;
        m.Changed += (_, args) =>
        {
            Assert.Equal(typeof(BatchEvent), args.Event?.GetType());
            Assert.Equal(2, ((BatchEvent)args.Event!).Count);
            var events = ((BatchEvent)args.Event).ToArray();
            Assert.Equal(typeof(PropertyChangeEvent), events[0].GetType());
            Assert.Equal(typeof(PropertyChangeEvent), events[1].GetType());
            count++;
        };
        m.Batch(() => new Circle { Center = new Point(5, 5), Radius = 5 });
        await m.Undo();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Should_handle_custom_event()
    {
        var reverseEvent = new CustomEvent();
        var @event = new CustomEvent(reverseEvent);
        m.MarkEvent(@event);
        UndoCount(1);

        await m.Undo();
        Assert.True(@event.IsRolledback);

        m.Changed += (_, args) => Assert.Same(reverseEvent, args.Event);
        await m.Redo();
    }

    [Fact]
    public async Task Should_throw_if_invalid_rollback_return_type()
    {
        m.Batch(() =>
        {
            new Circle() { Radius = 10, Center = new Point(10, 10) };
        });

        BatchEvent? batchEvent = null;
        MementorChanged changed = (_, args) =>
        {
            batchEvent = (BatchEvent?)args.Event;
        };
        m.Changed += changed;
        await m.Undo();
        Assert.NotNull(batchEvent);

        m.Changed -= changed;
        var customEvent = new InvalidCustomEvent(batchEvent);
        m.MarkEvent(customEvent);

        try
        {
            await m.Undo();
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
        }

        m.Reset();
        m.Batch(() =>
        {
            m.MarkEvent(customEvent);
            m.MarkEvent(customEvent);
        });
        try
        {
            await m.Undo();
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
        }
    }

    #region Helper

    private Features UndoCount(int c)
    {
        Assert.Equal(c, m.UndoCount);
        Assert.Equal(c > 0, m.CanUndo);
        return this;
    }

    private Features RedoCount(int c)
    {
        Assert.Equal(c, m.RedoCount);
        Assert.Equal(c > 0, m.CanRedo);
        return this;
    }

    #endregion
}