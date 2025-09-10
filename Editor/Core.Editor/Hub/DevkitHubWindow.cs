#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    /// <summary>Pi tech XR DevKit – Hub Window (clean: no stub types, no forward refs).</summary>
    public sealed class DevkitHubWindow : EditorWindow
    {
        const string kWindowTitle = "Pi tech XR DevKit";

        VisualElement _root;
        VisualElement _sidebar;
        VisualElement _content;

        // Registered pages
        readonly Dictionary<string, IDevkitPage> _pages = new();
        string _currentKey;

        [MenuItem("Pi tech/DevKit Hub", priority = 0)]
        public static void Open() => GetWindow<DevkitHubWindow>();

        void OnEnable()
        {
            titleContent = new GUIContent(kWindowTitle, DevkitContext.TitleIcon);

            _root = rootVisualElement;
            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.paddingLeft = 8;
            _root.style.paddingRight = 8;
            _root.style.paddingTop = 6;
            _root.style.paddingBottom = 8;

            BuildHeader();
            BuildMainArea();
            RegisterPages();
            ShowPage("Dashboard");
        }

        void BuildHeader()
        {
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var title = new Label(kWindowTitle);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16;
            title.style.marginLeft = 2;

            header.Add(title);
            header.Add(new VisualElement { style = { flexGrow = 1 } }); // spacer
            header.Add(MakeLinkButton("Docs", () => Application.OpenURL("https://pitech.gr")));
            header.Add(MakeLinkButton("Community", () => Application.OpenURL("https://pitech.gr")));

            _root.Clear();
            _root.Add(header);
            _root.Add(MakeSpacer(6));
        }

        void BuildMainArea()
        {
            var main = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1
                }
            };
            _root.Add(main);

            // Sidebar
            _sidebar = new VisualElement
            {
                style =
                {
                    width = 260,
                    minWidth = 220,
                    maxWidth = 320,
                    marginRight = 10,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 12,
                    paddingBottom = 12,
                    flexDirection = FlexDirection.Column,
                    backgroundColor = new Color(0.13f, 0.15f, 0.18f, 1f),
                    borderTopLeftRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomRightRadius = 6
                }
            };
            main.Add(_sidebar);

            // Sidebar logo
            if (DevkitContext.SidebarLogo != null)
            {
                var img = new Image { image = DevkitContext.SidebarLogo };
                img.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                img.style.height = 60;
                img.style.marginBottom = 12;
                _sidebar.Add(img);
            }

            _sidebar.Add(MakeSectionLabel("Navigate"));
            _sidebar.Add(MakeNavButton("Dashboard", () => ShowPage("Dashboard")));
            _sidebar.Add(MakeNavButton("Modules", () => ShowPage("Modules")));
            _sidebar.Add(MakeNavButton("Tools", () => ShowPage("Tools")));
            _sidebar.Add(MakeNavButton("Settings", () => ShowPage("Settings")));
            _sidebar.Add(new VisualElement { style = { flexGrow = 1 } });
            _sidebar.Add(MakeNavButton("About", () => ShowPage("About")));

            // Content
            _content = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = new Color(0.11f, 0.12f, 0.14f, 1f),
                    paddingTop = 12,
                    paddingBottom = 12,
                    paddingLeft = 12,
                    paddingRight = 12,
                    borderTopLeftRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomRightRadius = 6
                }
            };
            main.Add(_content);
        }

        void RegisterPages()
        {
            _pages.Clear();

            // These page types must exist once in the same namespace (you already created them):
            // DashboardPage, ModulesPage, ToolsPage, SettingsPage, AboutPage.
            SafeAdd("Dashboard", new DashboardPage());
            SafeAdd("Modules", new ModulesPage());
            SafeAdd("Tools", new ToolsPage());
            SafeAdd("Settings", new SettingsPage());
            SafeAdd("About", new AboutPage());
        }

        void SafeAdd(string key, IDevkitPage page)
        {
            if (page != null) _pages[key] = page;
        }

        void ShowPage(string key)
        {
            if (string.Equals(_currentKey, key, StringComparison.Ordinal)) return;

            _content.Clear();
            if (_pages.TryGetValue(key, out var page))
            {
                _currentKey = key;
                try { page.BuildUI(_content); }
                catch (Exception ex)
                {
                    _content.Add(new HelpBox($"Failed to build page '{key}':\n{ex}", HelpBoxMessageType.Error));
                }
            }
            else
            {
                _content.Add(new HelpBox($"Page not found: {key}", HelpBoxMessageType.Warning));
            }
        }

        // UI helpers
        static VisualElement MakeSpacer(float h) =>
            new VisualElement { style = { height = h, flexShrink = 0 } };

        static Label MakeSectionLabel(string text)
        {
            var l = new Label(text);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginBottom = 6;
            return l;
        }

        static Button MakeNavButton(string label, Action onClick)
        {
            var btn = new Button(onClick) { text = label };
            btn.style.marginBottom = 6;
            btn.style.height = 28;
            btn.style.justifyContent = Justify.Center;
            return btn;
        }

        static Button MakeLinkButton(string label, Action onClick)
        {
            var b = new Button(onClick) { text = label };
            b.style.marginLeft = 6;
            b.style.height = 20;
            return b;
        }
    }
}
#endif
