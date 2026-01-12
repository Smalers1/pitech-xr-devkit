#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class DevkitHubWindow : EditorWindow
    {
        enum PageKind { Dashboard, GuidedSetup, Docs, Settings }

        readonly Dictionary<PageKind, IDevkitPage> _pages = new()
        {
            { PageKind.Dashboard,  new DashboardPage() },
            { PageKind.GuidedSetup,new GuidedSetupPage() },
            { PageKind.Docs,       new DocsPage() },
            { PageKind.Settings,   new SettingsPage() },
        };

        VisualElement _content;
        PageKind _current = PageKind.Dashboard;

        [MenuItem("Pi tech/DevKit")]
        public static void Open()
        {
            var w = GetWindow<DevkitHubWindow>();
            w.titleContent = new GUIContent("Pi tech XR DevKit", DevkitContext.TitleIcon);
            w.minSize = new Vector2(860, 520);
            w.Show();
        }

        void OnEnable() => BuildUI();

        void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = DevkitTheme.Bg;

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            rootVisualElement.Add(row);

            // Sidebar
            var side = new VisualElement();
            side.style.width = 220;
            side.style.backgroundColor = DevkitTheme.Panel2;
            side.style.paddingLeft = 12;
            side.style.paddingRight = 12;
            side.style.paddingTop = 12;
            side.style.paddingBottom = 12;
            side.style.flexShrink = 0;

            // Logo + title
            var logoRow = DevkitTheme.Row();
            if (DevkitContext.SidebarLogo != null)
            {
                var logo = new Image { image = DevkitContext.SidebarLogo };
                logo.style.width = 90; logo.style.height = 60; logo.style.marginRight = 8;
                logoRow.Add(logo);
            }
            side.Add(logoRow);
            side.Add(DevkitTheme.VSpace(8));
            side.Add(DevkitTheme.Divider());
            side.Add(DevkitTheme.VSpace(8));

            // Nav buttons
            side.Add(NavButton("Dashboard", PageKind.Dashboard));
            side.Add(DevkitTheme.VSpace(6));
            side.Add(NavButton("Guided Setup", PageKind.GuidedSetup));
            side.Add(DevkitTheme.VSpace(6));
            side.Add(NavButton("Docs", PageKind.Docs));
            side.Add(DevkitTheme.VSpace(6));
            side.Add(NavButton("Settings", PageKind.Settings));

            // Top bar
            var top = DevkitTheme.Row();
            top.style.paddingLeft = 12; top.style.paddingRight = 12;
            top.style.paddingTop = 8; top.style.paddingBottom = 8;
            var hdr = new Label($"Pi tech XR DevKit � {DevkitContext.Version}")
            {
                style = { color = DevkitTheme.Text, unityFontStyleAndWeight = FontStyle.Bold }
            };
            top.Add(hdr);
            top.Add(DevkitTheme.Flex());
            top.style.backgroundColor = DevkitTheme.Panel2;

            // Content
            _content = new ScrollView();
            _content.style.flexGrow = 1;
            _content.style.paddingLeft = 12; _content.style.paddingRight = 12; _content.style.paddingTop = 8; _content.style.paddingBottom = 8;

            var right = new VisualElement { style = { flexGrow = 1 } };
            right.Add(top);
            right.Add(_content);

            row.Add(side);
            row.Add(right);

            ShowPage(_current);
        }

        Button NavButton(string text, PageKind page)
        {
            var b = DevkitTheme.Secondary(text, () => ShowPage(page));
            b.style.width = Length.Percent(100);
            return b;
        }

        void ShowPage(PageKind page)
        {
            _current = page;
            _content.Clear();
            if (_pages.TryGetValue(page, out var p))
                p.BuildUI(_content); // uses your IDevkitPage contract :contentReference[oaicite:13]{index=13}
        }
    }
}
#endif
