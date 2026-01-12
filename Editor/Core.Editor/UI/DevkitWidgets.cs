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
                if (i < items.Length - 1) row.Add(DevkitTheme.HSpace(10));
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
            var r = DevkitTheme.WrapRow();

            foreach (var b in buttons)
            {
                if (b == null) continue;

                // Make actions wrap nicely instead of overflowing/clipping.
                b.style.marginRight = 0;
                b.style.marginBottom = 0;
                b.style.flexShrink = 0;

                // Unity 2022 UIElements doesn't support rowGap/columnGap on IStyle.
                // Use margins on children for consistent spacing.
                b.style.marginRight = 8;
                b.style.marginBottom = 8;

                if (b is Button btn)
                {
                    btn.style.whiteSpace = WhiteSpace.NoWrap;
                    btn.style.minWidth = 140;
                }

                r.Add(b);
            }

            return r;
        }

        // --- ADD: �Ribbon� status bar with chips + thick progress + caption ---
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
            // Slightly sharper than big "bubbly" rounding
            borderTopLeftRadius = 12, borderTopRightRadius = 12,
            borderBottomLeftRadius = 12, borderBottomRightRadius = 12,
            paddingLeft = 14, paddingRight = 14, paddingTop = 12, paddingBottom = 12,
            marginRight = 10, marginBottom = 10,
            // Faux �depth�: thin outline darker than bg
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
        public static VisualElement CardGridTwoCol(out VisualElement left, out VisualElement right)
        {
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexGrow = 1;

            left = new VisualElement();
            left.style.flexGrow = 1;

            right = new VisualElement();
            right.style.flexGrow = 1;
            right.style.marginLeft = 10; // gutter

            grid.Add(left);
            grid.Add(right);
            return grid;
        }

        // === Pills (chip-style) ======================================================
        public enum PillKind { Success, Warning, Error, Neutral }

        public static VisualElement Pill(string text, PillKind kind)
        {
            Color bg, fg;
            switch (kind)
            {
                case PillKind.Success: bg = new Color(0.12f, 0.32f, 0.22f, 1f); fg = new Color(0.76f, 0.95f, 0.85f, 1f); break;
                case PillKind.Warning: bg = new Color(0.32f, 0.28f, 0.10f, 1f); fg = new Color(0.95f, 0.90f, 0.70f, 1f); break;
                case PillKind.Error: bg = new Color(0.35f, 0.12f, 0.14f, 1f); fg = new Color(0.98f, 0.78f, 0.82f, 1f); break;
                default: bg = new Color(0.20f, 0.22f, 0.26f, 1f); fg = new Color(0.84f, 0.88f, 0.94f, 1f); break;
            }

            var pill = new VisualElement();
            pill.style.backgroundColor = bg;
            // Tags (NOT full pills): subtle radius reads more "pro" and less bubbly.
            const int r = 7;
            pill.style.borderTopLeftRadius = r; pill.style.borderTopRightRadius = r;
            pill.style.borderBottomLeftRadius = r; pill.style.borderBottomRightRadius = r;
            pill.style.paddingLeft = 8; pill.style.paddingRight = 8; pill.style.paddingTop = 3; pill.style.paddingBottom = 3;

            // Thin outline for contrast against dark cards
            pill.style.borderBottomWidth = 1; pill.style.borderTopWidth = 1; pill.style.borderLeftWidth = 1; pill.style.borderRightWidth = 1;
            var outline = new Color(1f, 1f, 1f, 0.06f);
            pill.style.borderBottomColor = outline;
            pill.style.borderTopColor = outline;
            pill.style.borderLeftColor = outline;
            pill.style.borderRightColor = outline;

            var label = new Label(text)
            {
                style =
                {
                    color = fg,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11
                }
            };
            pill.Add(label);
            return pill;
        }

        public static VisualElement PillsRow(params (PillKind kind, string text)[] items)
        {
            var row = DevkitTheme.Row();
            for (int i = 0; i < items.Length; i++)
            {
                row.Add(Pill(items[i].text, items[i].kind));
                if (i < items.Length - 1) row.Add(DevkitTheme.HSpace(8));
            }
            return row;
        }

        // === Fancy progress with shine + percent label ===============================
        public static VisualElement ProgressBarPro(float value01, string rightLabel = null, float height = 16)
        {
            value01 = Mathf.Clamp01(value01);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            // Track
            var track = new VisualElement();
            track.style.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1);
            track.style.flexGrow = 1;
            track.style.height = height;
            track.style.borderTopLeftRadius = 999; track.style.borderTopRightRadius = 999;
            track.style.borderBottomLeftRadius = 999; track.style.borderBottomRightRadius = 999;
            track.style.position = Position.Relative;

            // Fill
            var fill = new VisualElement();
            fill.style.backgroundColor = Accent;
            fill.style.height = height;
            fill.style.width = Length.Percent(value01 * 100f);
            fill.style.borderTopLeftRadius = 999; fill.style.borderTopRightRadius = 999;
            fill.style.borderBottomLeftRadius = 999; fill.style.borderBottomRightRadius = 999;
            track.Add(fill);

            // Shine overlay
            var shine = new VisualElement();
            shine.style.position = Position.Absolute;
            shine.style.left = 0; shine.style.right = 0; shine.style.top = 0;
            shine.style.height = height * 0.45f;
            shine.style.backgroundColor = new Color(1, 1, 1, 0.06f);
            shine.style.borderTopLeftRadius = 999; shine.style.borderTopRightRadius = 999;
            shine.style.borderBottomLeftRadius = 999; shine.style.borderBottomRightRadius = 999;
            track.Add(shine);

            row.Add(track);

            if (!string.IsNullOrEmpty(rightLabel))
            {
                var percent = new Label(rightLabel)
                {
                    style =
            {
                color = new Color(0.84f, 0.88f, 0.94f, 1),
                unityFontStyleAndWeight = FontStyle.Bold,
                marginLeft = 10
            }
                };
                row.Add(percent);
            }

            return row;
        }

        // === Status header card (pills + progress + caption) =========================
        public static VisualElement StatusHeader(VisualElement pillsRow, float progress01, string caption)
        {
            var root = new VisualElement
            {
                style =
        {
            backgroundColor = new Color(0.13f, 0.16f, 0.20f, 1),
            borderTopLeftRadius = 18, borderTopRightRadius = 18,
            borderBottomLeftRadius = 18, borderBottomRightRadius = 18,
            paddingLeft = 14, paddingRight = 14, paddingTop = 12, paddingBottom = 12
        }
            };

            root.Add(pillsRow);
            root.Add(DevkitTheme.VSpace(10));
            root.Add(ProgressBarPro(progress01, $"{Mathf.RoundToInt(progress01 * 100)}%"));
            root.Add(DevkitTheme.VSpace(6));
            root.Add(DevkitTheme.Body(caption, dim: true));
            return root;
        }

        // === Two-column grid you already added earlier (keep) ========================
        // public static VisualElement CardGridTwoCol(out VisualElement left, out VisualElement right) { ... }

        // === Card helper (rounded, soft border) you already added earlier (keep) =====
        // public static VisualElement Card(string title, string subtitle, VisualElement actions, VisualElement body = null) { ... }


    }
}
#endif
