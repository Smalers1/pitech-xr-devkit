// Packages/com.pitech.xr.devkit/Editor/Core.Editor/Hub/DevkitHubWindow.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Pitech.XR.Core.Editor.Pages;

namespace Pitech.XR.Core.Editor
{
    public sealed class DevkitHubWindow : EditorWindow
    {
        private readonly List<IDevkitPage> _pages = new()
        {
            new DashboardPage(),
            new ModulesPage(),
            new ToolsPage(),
            new SettingsPage()
        };

        private int _activeIndex;
        private VisualElement _contentRoot;

        [MenuItem("Pi tech/DevKit Hub", priority = 1)]
        public static void Open()
        {
            var w = GetWindow<DevkitHubWindow>();
            w.titleContent = new GUIContent("Pi tech XR DevKit", DevkitContext.TitleIcon);
            w.Show();
        }

        private void OnEnable()
        {
            rootVisualElement.styleSheets.Clear();
            rootVisualElement.Clear();

            rootVisualElement.style.flexDirection = FlexDirection.Row;

            var sidebar = BuildSidebar();
            _contentRoot = BuildContentArea();

            rootVisualElement.Add(sidebar);
            rootVisualElement.Add(_contentRoot);

            RebuildActive();
        }

        private VisualElement BuildSidebar()
        {
            var side = new VisualElement
            {
                style =
                {
                    width = 220,
                    paddingTop = 12,
                    paddingLeft = 10,
                    paddingRight = 10,
                    backgroundColor = new Color(0.11f,0.12f,0.14f)
                }
            };

            if (DevkitContext.SidebarLogo != null)
            {
                var img = new Image { image = DevkitContext.SidebarLogo, scaleMode = ScaleMode.ScaleToFit };
                img.style.height = 48;
                img.style.marginBottom = 8;
                side.Add(img);
            }
            else
            {
                var label = new Label("Pi tech XR DevKit")
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 14,
                        marginBottom = 8
                    }
                };
                side.Add(label);
            }

            for (int i = 0; i < _pages.Count; i++)
            {
                int idx = i;
                var b = new Button(() =>
                {
                    _activeIndex = idx;
                    RebuildActive();
                })
                {
                    text = _pages[i].Title
                };

                b.style.marginBottom = 4;
                b.style.unityTextAlign = TextAnchor.MiddleLeft;

                side.Add(b);
            }

            var ver = new Label(Core.Editor.DevkitContext.Version)
            {
                style =
                {
                    marginTop = 10,
                    color = new Color(0.6f,0.6f,0.6f),
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            side.Add(ver);

            return side;
        }

        private static VisualElement BuildContentArea()
        {
            var content = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    paddingTop = 12,
                    paddingLeft = 14,
                    paddingRight = 14
                }
            };
            return content;
        }

        private void RebuildActive()
        {
            if (_activeIndex < 0 || _activeIndex >= _pages.Count) _activeIndex = 0;
            _contentRoot.Clear();
            _pages[_activeIndex].Build(_contentRoot);
        }
    }
}
