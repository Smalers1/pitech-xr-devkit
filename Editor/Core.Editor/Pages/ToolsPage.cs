#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class ToolsPage : IDevkitPage
    {
        public string Title => "Tools";

        public void BuildUI(VisualElement root)
        {
            // Scene Categories
            {
                var section = DevkitTheme.Section("Scene Categories");
                section.Add(DevkitTheme.Body("Create tidy root groups in the active scene: Lighting, Scene Managers, Environment, Interactables, Timelines, UI, Audio, VFX, Cameras and Debug.", dim: true));
                section.Add(DevkitTheme.VSpace(6));
                section.Add(DevkitWidgets.Actions(
                    DevkitTheme.Primary("Open", SceneCategoriesWindow.Open)
                ));
                root.Add(section);
            }

            // Managers
            {
                var mgr = new SceneManagerService();
                var section = DevkitTheme.Section("Managers");
                section.Add(DevkitTheme.Body("Quickly create common managers under the '--- SCENE MANAGERS ---' root.", dim: true));
                section.Add(DevkitTheme.VSpace(6));
                section.Add(DevkitWidgets.Actions(
                    DevkitTheme.Secondary("Create Scene Manager", mgr.CreateSceneManager),
                    DevkitTheme.Secondary("Create StatsUIController", mgr.CreateStatsUIController)
                ));
                root.Add(section);
            }

            // Stats
            {
                var stats = new StatsService();
                var section = DevkitTheme.Section("Stats");
                section.Add(DevkitTheme.Body("Create a StatsConfig asset in the selected folder.", dim: true));
                section.Add(DevkitTheme.VSpace(6));
                section.Add(DevkitWidgets.Actions(
                    DevkitTheme.Primary("Create StatsConfig asset", stats.CreateConfig)
                ));
                root.Add(section);
            }

            // Utilities
            {
                var section = DevkitTheme.Section("Utilities");
                var row = DevkitTheme.Row();
                row.Add(DevkitTheme.Secondary("Open Package Manager", () => EditorApplication.ExecuteMenuItem("Window/Package Manager")));
                row.Add(DevkitTheme.VSpace(6));
                row.Add(DevkitTheme.Secondary("Reimport All", () => AssetDatabase.ImportAsset("Assets", ImportAssetOptions.ForceUpdate)));
                section.Add(row);
                root.Add(section);
            }
        }
    }
}
#endif
