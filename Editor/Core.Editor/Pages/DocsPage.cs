#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class DocsPage : IDevkitPage
    {
        public string Title => "Docs";

        public void BuildUI(VisualElement root)
        {
            var section = DevkitTheme.Section("Docs");
            section.Add(DevkitTheme.Body("Step-by-step how-to guides for using the DevKit in any scene.", dim: true));
            section.Add(DevkitTheme.VSpace(10));

            var grid = DevkitWidgets.TileGrid();
            grid.Add(QuickStart());
            grid.Add(SceneManagerDoc());
            grid.Add(ScenarioDoc());
            grid.Add(StatsDoc());
            grid.Add(InteractablesDoc());
            grid.Add(QuizDoc());

            section.Add(grid);
            root.Add(section);
        }

        static VisualElement QuickStart()
        {
            var setup = new GuidedSetupService();
            return DevkitWidgets.Card(
                "Quick Start (2 minutes)",
                "Minimal setup to run a Scenario.",
                DevkitWidgets.Actions(
                    DevkitTheme.Secondary("Create Managers Root", () => setup.EnsureManagersRoot()),
                    DevkitTheme.Secondary("Create Scene Manager", () => setup.CreateUnderManagersRoot("Pitech.XR.Scenario.SceneManager", "Scene Manager", "Create Scene Manager")),
                    DevkitTheme.Secondary("Create Scenario", () => setup.CreateUnderManagersRoot("Pitech.XR.Scenario.Scenario", "Scenario", "Create Scenario")),
                    DevkitTheme.Primary("Open Scenario Graph", () => EditorApplication.ExecuteMenuItem("Pi tech/Scenario Graph"))
                ),
                HowTo(
                    "Open a scene (e.g. `Assets/Scenes/Testing`).",
                    "Create `--- SCENE MANAGERS ---` root.",
                    "Create Scene Manager and Scenario, then assign Scenario in the SceneManager inspector.",
                    "Open Scenario Graph and author steps.",
                    "Press Play to test flow."
                )
            );
        }

        static VisualElement SceneManagerDoc()
        {
            return DevkitWidgets.Card(
                "Scene Manager",
                "Orchestrates Scenario and optional modules.",
                DevkitWidgets.Actions(
                    DevkitTheme.Secondary("Select in scene", () => PingFirst("Pitech.XR.Scenario.SceneManager"))
                ),
                HowTo(
                    "Add one Scene Manager per scene (recommended).",
                    "Assign a `Scenario` to the `scenario` field.",
                    "Optional: assign Stats, Interactables, and/or Quiz fields only if you use those step types.",
                    "Use `Auto Start` to run immediately on Play; otherwise call `Restart()`."
                )
            );
        }

        static VisualElement ScenarioDoc()
        {
            return DevkitWidgets.Card(
                "Scenario + Graph",
                "Node-based authoring with persisted GUID routing.",
                DevkitWidgets.Actions(
                    DevkitTheme.Secondary("Open Scenario Graph", () => EditorApplication.ExecuteMenuItem("Pi tech/Scenario Graph"))
                ),
                HowTo(
                    "Add steps via the Scenario inspector list or right-click in the graph.",
                    "Wire steps by dragging from output ports to input ports.",
                    "Use empty `nextGuid` to mean 'next in list'.",
                    "If you reorder steps, re-check routing (graph makes it obvious).",
                    "Use the Step “Edit…” popup for focused editing."
                )
            );
        }

        static VisualElement StatsDoc()
        {
            return DevkitWidgets.Card(
                "Stats (optional)",
                "Configurable KPI system with optional UI bindings.",
                DevkitWidgets.Actions(
                    DevkitTheme.Secondary("Create StatsConfig asset", () => new StatsService().CreateConfig())
                ),
                HowTo(
                    "Create a `StatsConfig` asset and define ranges/defaults.",
                    "Add a `StatsUIController` if you want UI sliders/text.",
                    "Assign `statsConfig` and/or `statsUI` on SceneManager (optional).",
                    "In Question/Selection/Quiz steps, add StatEffects or use Quiz stats keys.",
                    "If you leave Stats unassigned, Scenario still runs normally."
                )
            );
        }

        static VisualElement InteractablesDoc()
        {
            return DevkitWidgets.Card(
                "Interactables (optional)",
                "SelectablesManager + SelectionLists for selection-based steps.",
                DevkitWidgets.Actions(
                    DevkitTheme.Secondary("Open Inspector", () => EditorApplication.ExecuteMenuItem("Window/General/Inspector"))
                ),
                HowTo(
                    "Add `SelectablesManager` and set `collectRoot` (optional) then 'Collect From Children'.",
                    "Add `SelectionLists` and assign the SelectablesManager reference.",
                    "Create lists (nursing procedures) and pick correct colliders per list.",
                    "Selection steps reference SelectionLists and a list key/index.",
                    "Optional: assign `infoButton` + per-list `infoPanel` for the 'i' panel workflow."
                )
            );
        }

        static VisualElement QuizDoc()
        {
            return DevkitWidgets.Card(
                "Quiz (optional)",
                "Scriptable quiz data + optional UI controller for question steps.",
                DevkitWidgets.Actions(
                    DevkitTheme.Secondary("Create QuizAsset", () => new QuizService().CreateAsset())
                ),
                HowTo(
                    "Create a `QuizAsset` and add questions + answers.",
                    "Add a `QuizUIController` if you want out-of-box UI.",
                    "Assign `quiz` and/or `quizUI` on the SceneManager (optional).",
                    "In Scenario, add a `Quiz` step and pick a Question from the dropdown.",
                    "Use 'When Complete' to choose single Next or Correct/Wrong branching.",
                    "If Quiz UI is missing at runtime, the step logs a warning and continues."
                )
            );
        }

        static VisualElement HowTo(params string[] steps)
        {
            var col = new VisualElement();
            col.style.flexDirection = FlexDirection.Column;
            for (int i = 0; i < steps.Length; i++)
            {
                if (i > 0) col.Add(DevkitTheme.VSpace(6));
                col.Add(DevkitTheme.Body($"{i + 1}. {steps[i]}", dim: false));
            }
            return col;
        }

        static void PingFirst(string fullTypeName)
        {
            var t = GuidedSetupService.FindType(fullTypeName);
            if (t == null) return;
            var objs = Resources.FindObjectsOfTypeAll(t);
            if (objs != null && objs.Length > 0) EditorGUIUtility.PingObject(objs[0]);
        }
    }
}
#endif


