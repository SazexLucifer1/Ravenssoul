using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Utopia;

/// <summary>
/// Sets explicit focus neighbors so keyboard/controller navigation never
/// reaches a dead end. Call after building a list of interactive controls.
/// </summary>
public static class FocusChain
{
    /// <summary>Links controls top-to-bottom. With <paramref name="wrap"/>, the last wraps to the first.</summary>
    public static void LinkVertical(IList<Control> controls, bool wrap = true) =>
        Link(controls, wrap, Control.PropertyName.FocusNeighborTop, Control.PropertyName.FocusNeighborBottom);

    /// <summary>Links controls in visual left-to-right order (mirrored for RTL layouts).</summary>
    public static void LinkHorizontal(IList<Control> controls, bool wrap = true)
    {
        var ordered = controls.ToList();
        if (ordered.Count > 0 && ordered[0].IsLayoutRtl())
            ordered.Reverse();
        Link(ordered, wrap, Control.PropertyName.FocusNeighborLeft, Control.PropertyName.FocusNeighborRight);
    }

    /// <summary>Returns only the controls that can currently take focus.</summary>
    public static List<Control> Focusable(IEnumerable<Control> controls) =>
        controls.Where(c => c.Visible && c.FocusMode != Control.FocusModeEnum.None).ToList();

    private static void Link(IList<Control> controls, bool wrap, StringName prevProp, StringName nextProp)
    {
        int count = controls.Count;
        for (int i = 0; i < count; i++)
        {
            Control control = controls[i];
            int prevIndex = i > 0 ? i - 1 : (wrap ? count - 1 : i);
            int nextIndex = i < count - 1 ? i + 1 : (wrap ? 0 : i);
            control.Set(prevProp, control.GetPathTo(controls[prevIndex]));
            control.Set(nextProp, control.GetPathTo(controls[nextIndex]));
            control.FocusPrevious = control.GetPathTo(controls[prevIndex]);
            control.FocusNext = control.GetPathTo(controls[nextIndex]);
        }
    }
}
