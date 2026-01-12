#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    internal static class DevkitTheme
    {
        public static readonly Color Bg       = new(0.11f, 0.13f, 0.16f, 1);
        public static readonly Color Panel    = new(0.14f, 0.17f, 0.21f, 1);
        public static readonly Color Panel2   = new(0.10f, 0.12f, 0.15f, 1);
        public static readonly Color Text     = new(0.92f, 0.95f, 0.98f, 1);
        public static readonly Color SubText  = new(0.75f, 0.80f, 0.86f, 1);
        public static readonly Color Brand    = new(0.27f, 0.56f, 0.99f, 1);

        public static VisualElement Row()
            => new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
        
        public static VisualElement WrapRow()
            => new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexWrap = Wrap.Wrap } };

        public static VisualElement Section(string title)
        {
            var ve = new VisualElement();
            ve.style.backgroundColor = Panel;
            ve.style.paddingLeft = ve.style.paddingRight = 14;
            ve.style.paddingTop = ve.style.paddingBottom = 12;
            ve.style.marginBottom = 12;
            ve.style.borderTopLeftRadius = ve.style.borderTopRightRadius =
            ve.style.borderBottomLeftRadius = ve.style.borderBottomRightRadius = 10;
            var t = new Label(title) { style = { color = Text, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13 } };
            ve.Add(t); ve.Add(VSpace(6));
            return ve;
        }

        public static Label Body(string text, bool dim=false)
            => new(text) { style = { color = dim ? SubText : Text, fontSize = 12 } };

        public static VisualElement VSpace(float h) => new() { style = { height = h } };
        public static VisualElement HSpace(float w) => new() { style = { width = w } };

        public static VisualElement Flex() { var f = new VisualElement(); f.style.flexGrow = 1; return f; }

        public static Button Primary(string text, System.Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.backgroundColor = Brand; b.style.color = Color.white; b.style.fontSize = 12;
            b.style.paddingLeft = b.style.paddingRight = 10; b.style.paddingTop = b.style.paddingBottom = 6;
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;
            return b;
        }
        public static Button Secondary(string text, System.Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.backgroundColor = new Color(0.18f,0.20f,0.24f,1); b.style.color = Text; b.style.fontSize = 12;
            b.style.paddingLeft = b.style.paddingRight = 10; b.style.paddingTop = b.style.paddingBottom = 6;
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;
            return b;
        }

        public static VisualElement Divider()
            => new VisualElement { style = { height = 1, backgroundColor = new Color(1,1,1,0.06f), marginTop = 6, marginBottom = 6 } };
    }
}
#endif
