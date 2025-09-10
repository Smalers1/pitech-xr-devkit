#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class SceneCategoriesWindow : EditorWindow
    {
        readonly List<(string name, Toggle toggle)> _rows = new();
        TextField _custom;
        SceneCategoriesService _svc;

        [MenuItem("Pi tech/Scene/Create Scene Categories…")]
        public static void Open() => GetWindow<SceneCategoriesWindow>("Scene Categories").Show();

        void OnEnable()
        {
            _svc = new SceneCategoriesService();
            BuildUI();
        }

        void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = DevkitTheme.Bg;
            rootVisualElement.style.paddingLeft = 12;
            rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            // Header
            var header = DevkitTheme.Row();
            header.Add(new Label("Create Scene Categories") { style = { color = DevkitTheme.Text, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13 } });
            header.Add(DevkitTheme.Flex());
            header.Add(DevkitTheme.Secondary("Docs", () => EditorApplication.ExecuteMenuItem("Window/Package Manager"))); // placeholder hook
            rootVisualElement.Add(header);
            rootVisualElement.Add(DevkitTheme.Divider());

            // Checklist
            var section = DevkitTheme.Section("Select categories");
            foreach (var cat in SceneCategoriesService.DefaultCategories)
            {
                var row = DevkitTheme.Row();
                var t = new Toggle(cat) { value = true };
                t.style.color = DevkitTheme.Text;
                row.Add(t);
                section.Add(row);
                _rows.Add((cat, t));
            }
            rootVisualElement.Add(section);

            // Add custom
            var add = DevkitTheme.Section("Add custom category");
            var addRow = DevkitTheme.Row();
            _custom = new TextField { label = "Name", value = "" };
            _custom.style.flexGrow = 1;
            addRow.Add(_custom);
            addRow.Add(DevkitTheme.Secondary("Add", AddCustom));
            add.Add(addRow);
            rootVisualElement.Add(add);

            // Footer actions
            var actions = DevkitTheme.Row();
            actions.Add(DevkitTheme.Secondary("Select all", () => SetAll(true)));
            actions.Add(DevkitTheme.Secondary("Clear", () => SetAll(false)));
            actions.Add(DevkitTheme.Flex());
            actions.Add(DevkitTheme.Primary("Create selected", CreateSelected));
            rootVisualElement.Add(actions);
        }

        void AddCustom()
        {
            var name = (_custom?.value ?? "").Trim();
            if (string.IsNullOrEmpty(name)) return;
            var row = DevkitTheme.Row();
            var t = new Toggle(name) { value = true };
            t.style.color = DevkitTheme.Text;
            row.Add(t);
            // Insert into the first section (index 1 after header+divider)
            rootVisualElement[2].Add(row);
            _rows.Add((name, t));
            _custom.value = "";
        }

        void SetAll(bool v)
        {
            foreach (var r in _rows) r.toggle.value = v;
        }

        void CreateSelected()
        {
            var sel = _rows.Where(r => r.toggle.value).Select(r => r.name).ToArray();
            if (sel.Length == 0) { EditorUtility.DisplayDialog("Scene Categories", "Pick at least one category.", "OK"); return; }
            _svc.CreateSelected(sel);
            Close();
        }
    }
}
#endif
