#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    /// <summary>
    /// Small UI helpers layered on top of DevkitTheme.
    /// Keep this minimal and reusable for Dashboard/Tools.
    /// </summary>
    internal static class DevkitWidgets
    {
        // Accent (brand blue)
        static readonly Color Accent = new Color(0.34f, 0.60f, 1.0f, 1f);

        // ---------- Status chips ----------
        public static VisualElement StatusChips(params (bool ok, string label)[] items)
        {
            var row = DevkitTheme.Row();
            for (int i = 0; i < items.Length; i++)
            {
                row.Add(StatusChip(items[i].ok, items[i].label));
                if (i < items.Length - 1) row.Add(DevkitTheme.VSpace(10));
            }
            return row;
        }

        public static VisualElement StatusChip(bool ok, string label)
        {
            var r = DevkitTheme.Row();
            var dot = new VisualElement
            {
                style =
                {
                    width = 10, height = 10,
                    borderTopLeftRadius = 5, borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5, borderBottomRightRadius = 5,
                    backgroundColor = ok ? new Color(0.30f,0.90f,0.50f,1) : new Color(0.95f,0.35f,0.35f,1),
                    marginRight = 6
                }
            };
            r.Add(dot);
            r.Add(new Label(label) { style = { color = DevkitTheme.Text } });
            return r;
        }

        // ---------- Status bar with progress ----------
        public static VisualElement StatusBar(VisualElement chips, float progress01, string caption)
        {
            var bar = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.13f,0.16f,0.20f,1),
                    borderTopLeftRadius = 18, borderTopRightRadius = 18,
                    borderBottomLeftRadius = 18, borderBottomRightRadius = 18,
                    paddingLeft = 14, paddingRight = 14, paddingTop = 12, paddingBottom = 12,
                    marginBottom = 10
                }
            };

            bar.Add(chips);
            bar.Add(DevkitTheme.VSpace(8));
            bar.Add(ProgressBar(progress01));
            bar.Add(DevkitTheme.VSpace(4));
            bar.Add(DevkitTheme.Body(caption, dim: true));
            return bar;
        }

        public static VisualElement ProgressBar(float value01, string label = null)
        {
            value01 = Mathf.Clamp01(value01);

            var track = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.12f,0.14f,0.18f,1),
                    height = 10,
                    borderTopLeftRadius = 999, borderTopRightRadius = 999,
                    borderBottomLeftRadius = 999, borderBottomRightRadius = 999
                }
            };

            var fill = new VisualElement
            {
                style =
                {
                    backgroundColor = Accent,
                    height = 10,
                    width = Length.Percent(value01 * 100f),
                    borderTopLeftRadius = 999, borderTopRightRadius = 999,
                    borderBottomLeftRadius = 999, borderBottomRightRadius = 999
                }
            };
            track.Add(fill);

            if (string.IsNullOrEmpty(label)) return track;

            var wrap = new VisualElement { style = { flexDirection = FlexDirection.Column } };
            wrap.Add(track);
            var row = DevkitTheme.Row(); row.style.marginTop = 6;
            row.Add(new Label(label) { style = { color = DevkitTheme.Text } });
            wrap.Add(row);
            return wrap;
        }

        // ---------- KPI card ----------
        public static VisualElement Kpi(string title, string value, string hint = null)
        {
            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = DevkitTheme.Panel2,
                    borderTopLeftRadius = 16, borderTopRightRadius = 16,
                    borderBottomLeftRadius = 16, borderBottomRightRadius = 16,
                    paddingLeft = 14, paddingRight = 14, paddingTop = 12, paddingBottom = 12,
                    marginRight = 8, marginBottom = 8,
                    minWidth = 200, flexGrow = 1
                }
            };

            var t = new Label(title) { style = { color = new Color(0.75f, 0.80f, 0.88f, 1) } };
            var v = new Label(value) { style = { color = DevkitTheme.Text, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 20 } };
            card.Add(t);
            card.Add(v);
            if (!string.IsNullOrEmpty(hint))
                card.Add(new Label(hint) { style = { color = new Color(0.55f, 0.60f, 0.68f, 1) } });
            return card;
        }

        // ---------- Tiles ----------
        public static VisualElement TileGrid()
        {
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            return grid;
        }

        public static VisualElement Tile(string title, string subtitle, VisualElement actions, VisualElement body = null)
        {
            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = DevkitTheme.Panel2,
                    borderTopLeftRadius = 16, borderTopRightRadius = 16,
                    borderBottomLeftRadius = 16, borderBottomRightRadius = 16,
                    paddingLeft = 12, paddingRight = 12, paddingTop = 10, paddingBottom = 10,
                    marginRight = 8, marginBottom = 8,
                    flexBasis = Length.Percent(50), // two columns on wide
                    flexGrow = 1,
                    minWidth = 320
                }
            };

            var head = DevkitTheme.Row();
            head.Add(new Label(title) { style = { color = DevkitTheme.Text, unityFontStyleAndWeight = FontStyle.Bold } });
            card.Add(head);

            if (!string.IsNullOrEmpty(subtitle))
            {
                card.Add(DevkitTheme.VSpace(4));
                card.Add(DevkitTheme.Body(subtitle, dim: true));
            }

            if (body != null)
            {
                card.Add(DevkitTheme.VSpace(8));
                card.Add(body);
            }

            if (actions != null)
            {
                card.Add(DevkitTheme.VSpace(8));
                card.Add(actions);
            }

            return card;
        }

        public static VisualElement Actions(params VisualElement[] buttons)
        {
            var r = DevkitTheme.Row();
            foreach (var b in buttons) { r.Add(b); r.Add(DevkitTheme.VSpace(6)); }
            return r;
        }

        // --- ADD: “Ribbon” status bar with chips + thick progress + caption ---
        public static VisualElement StatusRibbon(VisualElement chips, float progress01, string caption)
        {
            var wrap = new VisualElement
            {
                style =
        {
            backgroundColor = new Color(0.13f,0.16f,0.20f,1),
            borderTopLeftRadius = 18, borderTopRightRadius = 18,
            borderBottomLeftRadius = 18, borderBottomRightRadius = 18,
            paddingLeft = 14, paddingRight = 14, paddingTop = 12, paddingBottom = 12,
            marginBottom = 10
        }
            };

            wrap.Add(chips);
            wrap.Add(DevkitTheme.VSpace(10));

            // thick progress bar (feels like SaaS status)
            var track = new VisualElement
            {
                style =
        {
            backgroundColor = new Color(0.12f,0.14f,0.18f,1),
            height = 14,
            borderTopLeftRadius = 999, borderTopRightRadius = 999,
            borderBottomLeftRadius = 999, borderBottomRightRadius = 999
        }
            };
            var fill = new VisualElement
            {
                style =
        {
            backgroundColor = Accent,
            height = 14,
            width = Length.Percent(Mathf.Clamp01(progress01) * 100f),
            borderTopLeftRadius = 999, borderTopRightRadius = 999,
            borderBottomLeftRadius = 999, borderBottomRightRadius = 999
        }
            };
            track.Add(fill);
            wrap.Add(track);

            if (!string.IsNullOrEmpty(caption))
            {
                wrap.Add(DevkitTheme.VSpace(6));
                wrap.Add(DevkitTheme.Body(caption, dim: true));
            }
            return wrap;
        }

        // --- ADD: Card (rounded tile with soft border, actions row, optional body) ---
        public static VisualElement Card(string title, string subtitle, VisualElement actions, VisualElement body = null)
        {
            var card = new VisualElement
            {
                style =
        {
            backgroundColor = DevkitTheme.Panel2,
            borderTopLeftRadius = 18, borderTopRightRadius = 18,
            borderBottomLeftRadius = 18, borderBottomRightRadius = 18,
            paddingLeft = 14, paddingRight = 14, paddingTop = 12, paddingBottom = 12,
            marginRight = 10, marginBottom = 10,
            // Faux “depth”: thin outline darker than bg
            borderBottomWidth = 1, borderTopWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
            borderBottomColor = new Color(0.10f,0.12f,0.16f,1),
            borderTopColor    = new Color(0.10f,0.12f,0.16f,1),
            borderLeftColor   = new Color(0.10f,0.12f,0.16f,1),
            borderRightColor  = new Color(0.10f,0.12f,0.16f,1),
            // Responsive layout
            flexBasis = Length.Percent(50),
            flexGrow = 1,
            minWidth = 360
        }
            };

            var head = DevkitTheme.Row();
            head.Add(new Label(title) { style = { color = DevkitTheme.Text, unityFontStyleAndWeight = FontStyle.Bold } });
            card.Add(head);

            if (!string.IsNullOrEmpty(subtitle))
            {
                card.Add(DevkitTheme.VSpace(4));
                card.Add(DevkitTheme.Body(subtitle, dim: true));
            }

            if (body != null)
            {
                card.Add(DevkitTheme.VSpace(8));
                card.Add(body);
            }

            if (actions != null)
            {
                card.Add(DevkitTheme.VSpace(10));
                card.Add(actions);
            }

            return card;
        }

    }
}
#endif
