// Packages/com.pitech.xr.devkit/Editor/Core.Editor/Pages/DashboardPage.cs
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor.Pages
{
    internal sealed class DashboardPage : IDevkitPage
    {
        public string Title => "Dashboard";

        public void Build(VisualElement root)
        {
            root.Clear();

            var header = new Label("Pi tech XR DevKit")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    marginBottom = 6
                }
            };
            root.Add(header);

            var ver = new Label($"Version: {Core.Editor.DevkitContext.Version}")
            {
                style = { marginBottom = 10, color = new Color(0.75f, 0.75f, 0.75f) }
            };
            root.Add(ver);

            var card = MakeCard("Project Setup");
            root.Add(card);

            card.Add(MakePillRow(new[]
            {
                ("Unity Timeline", true),
                ("TextMeshPro", true),
                ("Scenario module", false),
                ("Stats module", false)
            }));

            card.Add(new Label("Enable modules you need, then use Quick Actions to create assets & scene objects.")
            {
                style = { marginTop = 6, marginBottom = 2, color = new Color(0.8f,0.8f,0.8f) }
            });
        }

        static VisualElement MakeCard(string title)
        {
            var box = new VisualElement();
            box.style.paddingTop = 10;
            box.style.paddingBottom = 10;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;
            box.style.marginBottom = 10;
            box.style.borderBottomLeftRadius = 6;
            box.style.borderBottomRightRadius = 6;
            box.style.borderTopLeftRadius = 6;
            box.style.borderTopRightRadius = 6;
            box.style.backgroundColor = new Color(0.14f, 0.16f, 0.19f);

            var label = new Label(title)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6
                }
            };
            box.Add(label);
            return box;
        }

        static VisualElement MakePillRow((string label, bool ok)[] items)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            foreach (var it in items)
            {
                var pill = new Label(it.label)
                {
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        paddingLeft = 8,
                        paddingRight = 8,
                        paddingTop = 2,
                        paddingBottom = 2,
                        marginRight = 6,
                        backgroundColor = it.ok ? new Color(0.20f, 0.44f, 0.24f) : new Color(0.44f, 0.20f, 0.20f),
                        borderBottomLeftRadius = 12,
                        borderBottomRightRadius = 12,
                        borderTopLeftRadius = 12,
                        borderTopRightRadius = 12
                    }
                };
                row.Add(pill);
            }

            return row;
        }
    }
}
