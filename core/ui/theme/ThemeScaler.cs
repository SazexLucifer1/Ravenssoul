using System;
using Godot;

namespace Utopia;

/// <summary>
/// Scales every font size in a Theme for the "text size" accessibility option.
/// Base sizes are remembered in theme metadata so scaling never compounds.
/// </summary>
public static class ThemeScaler
{
    public static readonly StringName MetaKey = "base_font_sizes";

    public static void Apply(Theme theme, float scale)
    {
        if (theme == null)
            return;
        if (!theme.HasMeta(MetaKey))
        {
            var bases = new Godot.Collections.Dictionary();
            foreach (string typeName in theme.GetFontSizeTypeList())
                foreach (string sizeName in theme.GetFontSizeList(typeName))
                    bases[$"{typeName}/{sizeName}"] = theme.GetFontSize(sizeName, typeName);
            bases["__default__"] = theme.DefaultFontSize;
            theme.SetMeta(MetaKey, bases);
        }
        var stored = theme.GetMeta(MetaKey).AsGodotDictionary();
        foreach ((Variant keyVariant, Variant baseSize) in stored)
        {
            string key = keyVariant.AsString();
            int scaled = (int)Math.Round(baseSize.AsInt32() * scale, MidpointRounding.AwayFromZero);
            if (key == "__default__")
            {
                theme.DefaultFontSize = scaled;
            }
            else
            {
                string[] parts = key.Split('/');
                theme.SetFontSize(parts[1], parts[0], scaled);
            }
        }
    }
}
