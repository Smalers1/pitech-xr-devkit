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

            // ===== System Status (pills + crisp pill meter) =====
            {
                // Gather status
                var (fOk, fTotal, _) = health.CheckFolders();
                var (rOk, rTotal, _) = health.CheckSceneRoots();
                var (sOk, sTotal, _) = health.CheckSettings();
                var (hasTimeline, hasTMP) = health.CheckModules();

                // Section container
                var status = DevkitTheme.Section("System Status");

                // Chips row
                var chips = DevkitTheme.Row();
                chips.Add(DevkitWidgets.StatusChip(fOk == fTotal, $"Folders {fOk}/{fTotal}"));
                chips.Add(DevkitTheme.VSpace(10));
                chips.Add(DevkitWidgets.StatusChip(rOk == rTotal, $"Scene roots {rOk}/{rTotal}"));
                chips.Add(DevkitTheme.VSpace(10));
                chips.Add(DevkitWidgets.StatusChip(sOk == sTotal, $"Settings {sOk}/{sTotal}"));
                chips.Add(DevkitTheme.VSpace(10));
                chips.Add(DevkitWidgets.StatusChip(hasTimeline, "Timeline"));
                chips.Add(DevkitTheme.VSpace(10));
                chips.Add(DevkitWidgets.StatusChip(hasTMP, "TextMeshPro"));
                status.Add(chips);

                // Progress value 0..1
                float progress01 = Mathf.Clamp01(
                    (fOk + rOk + sOk) / (float)(fTotal + rTotal + sTotal)
                );

                // Perfect pill meter (no sprites, no tapered ends)
                const int h = 16;              // height in px
                int r = h / 2;                 // radius = half height

                var track = new VisualElement();
                track.style.marginTop = 8;
                track.style.height = h;
                track.style.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                track.style.borderTopLeftRadius = r; track.style.borderTopRightRadius = r;
                track.style.borderBottomLeftRadius = r; track.style.borderBottomRightRadius = r;
                track.style.overflow = Overflow.Hidden;      // clip child neatly
                track.style.position = Position.Relative;    // for overlay label

                var fill = new VisualElement();
                fill.style.height = Length.Percent(100);
                fill.style.width = Length.Percent(progress01 * 100f);
                fill.style.backgroundColor = new Color(0.34f, 0.60f, 1.0f); // brand blue
                fill.style.borderTopLeftRadius = r; fill.style.borderTopRightRadius = r;
                fill.style.borderBottomLeftRadius = r; fill.style.borderBottomRightRadius = r;
                track.Add(fill);

                // Percent overlay (centered inside)
                var pct = new Label($"{Mathf.RoundToInt(progress01 * 100)}%");
                pct.style.position = Position.Absolute;
                pct.style.left = 0;                     // span full width
                pct.style.right = 0;
                pct.style.top = -1;                     // tiny optical nudge
                pct.style.unityTextAlign = TextAnchor.MiddleCenter;
                pct.style.color = new Color(0.90f, 0.94f, 1f);
                pct.style.unityFontStyleAndWeight = FontStyle.Bold;
                pct.pickingMode = PickingMode.Ignore;   // ignore mouse
                track.Add(pct);

                status.Add(track);
                status.Add(DevkitTheme.VSpace(6));
                status.Add(DevkitTheme.Body("Green means you are good to go. Use the cards to fix anything missing.", dim: true));
                root.Add(status);
            }

            // ===== Project Setup (2×2 grid): Folders | Scene, Settings | Scenario =====
            {
                var psvc = new ProjectSetupService();
                var scen = new ScenarioService();
                var mgr = new SceneManagerService();

                var section = DevkitTheme.Section("Project Setup");
                var grid = DevkitWidgets.CardGridTwoCol(out var colL, out var colR);

                // Row 1 — left: Folders
                colL.Add(DevkitWidgets.Card(
                    "Folders",
                    "Scaffold Assets with recommended subfolders.",
                    DevkitWidgets.Actions(
                        DevkitTheme.Primary("Create project folders", psvc.SetupFolders)
                    )
                ));

                // Row 1 — right: Scene
                colR.Add(DevkitWidgets.Card(
                    "Scene",
                    "Prepare a clean starting scene structure.",
                    DevkitWidgets.Actions(
                        DevkitTheme.Secondary("Create Main scene", psvc.CreateMainScene),
                        DevkitTheme.Secondary("Create Scene Categories…", SceneCategoriesWindow.Open),
                        DevkitTheme.Secondary("Create Scene Manager", mgr.CreateSceneManager)
                    )
                ));

                // Row 2 — left: Settings
                colL.Add(DevkitWidgets.Card(
                    "Settings",
                    "Linear color space, Force Text and visible meta files.",
                    DevkitWidgets.Actions(
                        DevkitTheme.Secondary("Apply recommended settings", health.FixRecommended)
                    )
                ));

                // Row 2 — right: Scenario
                colR.Add(DevkitWidgets.Card(
                    "Scenario",
                    "Runtime object and authoring graph.",
                    DevkitWidgets.Actions(
                        DevkitTheme.Secondary("Create Scenario GameObject", scen.CreateScenarioGameObject),
                        DevkitTheme.Secondary("Open Scenario Graph", scen.OpenGraph)
                    )
                ));

                section.Add(grid);
                root.Add(section);
            }
        }
    }
}
#endif
