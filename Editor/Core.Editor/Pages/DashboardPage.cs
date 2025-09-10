#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class DashboardPage : IDevkitPage
    {
        public string Title => "Dashboard";

        public void BuildUI(VisualElement root)
        {
            // ===== Project Setup =====
            var setup = DevkitTheme.Section("Project Setup");
            {
                // Status pills
                var srow = DevkitTheme.Row();
                srow.Add(Pill(DevkitContext.HasTimeline, "Unity Timeline"));
                srow.Add(DevkitTheme.VSpace(8));
                srow.Add(Pill(DevkitContext.HasTextMeshPro, "TextMeshPro"));
                setup.Add(srow);
                setup.Add(DevkitTheme.VSpace(8));
                setup.Add(DevkitTheme.Body("Create folders, seed assets and apply recommended settings.", dim: true));

                setup.Add(DevkitTheme.VSpace(10));
                var svc = new ProjectSetupService(); // folders + settings + main scene :contentReference[oaicite:14]{index=14} :contentReference[oaicite:15]{index=15} :contentReference[oaicite:16]{index=16}
                var btns1 = DevkitTheme.Row();
                btns1.Add(DevkitTheme.Primary("Create project folders", svc.SetupFolders));
                btns1.Add(DevkitTheme.VSpace(6));
                btns1.Add(DevkitTheme.Secondary("Create Main scene", svc.CreateMainScene));
                setup.Add(btns1);

                setup.Add(DevkitTheme.VSpace(6));
                var btns2 = DevkitTheme.Row();
                var categoriesBtn = new Button(() => SceneCategoriesWindow.Open()) { text = "Create Scene Categories…" };
                categoriesBtn.style.width = Length.Percent(100);
                root.Add(categoriesBtn);
                btns2.Add(DevkitTheme.Secondary("Apply recommended settings", svc.ApplyRecommendedSettings));
                setup.Add(btns2);
            }
            root.Add(setup);

            // ===== Quick Actions =====
            var quick = DevkitTheme.Section("Quick Actions");
            {
                var stats = new StatsService();       // create StatsConfig asset where selection points :contentReference[oaicite:17]{index=17}
                var scen = new ScenarioService();    // create Scenario GO + open graph :contentReference[oaicite:18]{index=18} :contentReference[oaicite:19]{index=19}

                quick.Add(DevkitTheme.Primary("Create StatsConfig asset", stats.CreateConfig));
                quick.Add(DevkitTheme.VSpace(6));

                var row = DevkitTheme.Row();
                row.Add(DevkitTheme.Secondary("Create Scenario GameObject", scen.CreateScenarioGameObject));
                row.Add(DevkitTheme.Flex());
                row.Add(DevkitTheme.Secondary("Open Scenario Graph", scen.OpenGraph));
                quick.Add(row);
            }
            root.Add(quick);

            // ===== Utilities =====
            var utils = DevkitTheme.Section("Utilities");
            {
                var row = DevkitTheme.Row();
                row.Add(DevkitTheme.Secondary("Open Package Manager", () => EditorApplication.ExecuteMenuItem("Window/Package Manager")));
                row.Add(DevkitTheme.VSpace(6));
                row.Add(DevkitTheme.Secondary("Reimport All", () => AssetDatabase.ImportAsset("Assets", ImportAssetOptions.ForceUpdate)));
                utils.Add(row);
            }
            root.Add(utils);
        }

        // --- small UI helpers ---
        static VisualElement Pill(bool ok, string label)
        {
            var row = DevkitTheme.Row();
            var dot = new VisualElement
            {
                style =
                {
                    width = 10, height = 10,
                    borderTopLeftRadius = 5, borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5, borderBottomRightRadius = 5,
                    backgroundColor = ok ? new Color(0.3f, 0.9f, 0.5f) : new Color(0.95f, 0.35f, 0.35f),
                    marginRight = 6
                }
            };
            row.Add(dot);
            row.Add(new Label(label) { style = { color = DevkitTheme.Text } });
            return row;
        }
    }
}
#endif
