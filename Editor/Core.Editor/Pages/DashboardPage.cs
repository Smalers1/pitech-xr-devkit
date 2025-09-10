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
            var health = new ProjectHealthService();

            // ===== System Status (ribbon) =====
            {
                var (fOk, fTotal, _) = health.CheckFolders();
                var (rOk, rTotal, _) = health.CheckSceneRoots();
                var (sOk, sTotal, _) = health.CheckSettings();
                var (hasTimeline, hasTMP) = health.CheckModules();

                var chips = DevkitWidgets.StatusChips(
                    (fOk == fTotal, $"Folders {fOk}/{fTotal}"),
                    (rOk > 0, $"Scene roots {rOk}/{rTotal}"),
                    (sOk == sTotal, $"Settings {sOk}/{sTotal}"),
                    (hasTimeline, "Timeline"),
                    (hasTMP, "TextMeshPro")
                );

                var ribbon = DevkitWidgets.StatusRibbon(
                    chips,
                    health.OverallProgress01(),
                    "Green means you are good to go. Use the tiles to fix anything missing."
                );

                var wrap = DevkitTheme.Section("System Status");
                wrap.Add(ribbon);
                root.Add(wrap);
            }


            // ===== KPI row (cards) =====
            {
                var grid = DevkitTheme.Row();
                var (fOk, fTotal, _) = health.CheckFolders();
                var (rOk, rTotal, _) = health.CheckSceneRoots();
                var (sOk, sTotal, _) = health.CheckSettings();
                grid.Add(DevkitWidgets.Kpi("Folders", $"{fOk}/{fTotal}", fOk == fTotal ? "Complete" : "Missing some"));
                grid.Add(DevkitWidgets.Kpi("Scene roots", $"{rOk}/{rTotal}", rOk > 0 ? "Present" : "None"));
                grid.Add(DevkitWidgets.Kpi("Settings", $"{sOk}/{sTotal}", sOk == sTotal ? "OK" : "Needs fixes"));
                root.Add(grid);
            }

            // ===== Tiles =====
            var section = DevkitTheme.Section("Project Setup");
            {
                var grid = DevkitWidgets.TileGrid();

                var psvc = new ProjectSetupService();
                var ssvc = new ScenarioService();
                var stsvc = new StatsService();

                // Folders
                {
                    var actions = DevkitWidgets.Actions(
                        DevkitTheme.Primary("Create project folders", psvc.SetupFolders)
                    );
                    grid.Add(DevkitWidgets.Card(
                        "Folders",
                        "Scaffold Assets with recommended subfolders.",
                        DevkitWidgets.Actions(DevkitTheme.Primary("Create project folders", psvc.SetupFolders))
                    ));

                }

                // Scene
                {
                    var actions = DevkitWidgets.Actions(
                        DevkitTheme.Secondary("Create Main scene", psvc.CreateMainScene),
                        DevkitTheme.Secondary("Create Scene Categories…", SceneCategoriesWindow.Open)
                    );
                    grid.Add(DevkitWidgets.Card(
                        "Scene",
                        "Prepare a clean starting scene structure.",
                        DevkitWidgets.Actions(
                            DevkitTheme.Secondary("Create Main scene", psvc.CreateMainScene),
                            DevkitTheme.Secondary("Create Scene Categories…", SceneCategoriesWindow.Open)
                        )
                    ));
                }

                // Settings
                {
                    var actions = DevkitWidgets.Actions(
                        DevkitTheme.Secondary("Apply recommended settings", health.FixRecommended)
                    );
                    grid.Add(DevkitWidgets.Card(
                        "Settings",
                        "Linear color space, Force Text and visible meta files.",
                        DevkitWidgets.Actions(DevkitTheme.Secondary("Apply recommended settings", health.FixRecommended))
                    ));
                }

                // Stats
                {
                    var actions = DevkitWidgets.Actions(
                        DevkitTheme.Primary("Create StatsConfig asset", stsvc.CreateConfig)
                    );
                    grid.Add(DevkitWidgets.Card(
                        "Stats",
                        "Create a StatsConfig in the selected folder.",
                        DevkitWidgets.Actions(DevkitTheme.Primary("Create StatsConfig asset", stsvc.CreateConfig))
                    ));
                }

                // Scenario
                {
                    var actions = DevkitWidgets.Actions(
                        DevkitTheme.Secondary("Create Scenario GameObject", ssvc.CreateScenarioGameObject),
                        DevkitTheme.Secondary("Open Scenario Graph", ssvc.OpenGraph)
                    );
                    grid.Add(DevkitWidgets.Card(
                        "Scenario",
                        "Runtime object and authoring graph.",
                        DevkitWidgets.Actions(
                            DevkitTheme.Secondary("Create Scenario GameObject", ssvc.CreateScenarioGameObject),
                            DevkitTheme.Secondary("Open Scenario Graph", ssvc.OpenGraph)
                        )
                    ));
                }

                section.Add(grid);
            }
            root.Add(section);
        }


    }
}
#endif
