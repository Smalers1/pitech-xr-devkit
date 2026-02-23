#if UNITY_EDITOR
using Pitech.XR.ContentDelivery;
using Pitech.XR.ContentDelivery.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class SettingsPage : IDevkitPage
    {
        private readonly AddressablesService addressablesService = new AddressablesService();

        private ObjectField addressablesConfigField;
        private VisualElement addressablesConfigFieldsRoot;
        private SerializedObject addressablesSerializedObject;

        public string Title => "Settings";

        public void BuildUI(VisualElement root)
        {
            root.Add(Section("DevKit", el =>
            {
                el.Add(new Label($"Version: {DevkitContext.Version}"));
                el.Add(new Label($"Timeline present: {DevkitContext.HasTimeline}"));
                el.Add(new Label($"TextMeshPro present: {DevkitContext.HasTextMeshPro}"));
                el.Add(new Label($"Addressables present: {DevkitContext.HasAddressables}"));
                el.Add(new Label($"CCD package present: {DevkitContext.HasCcdManagement}"));
            }));

            root.Add(Section("Addressables (Project)", BuildAddressablesSettingsSection));

            root.Add(Section("Project Settings", el =>
            {
                el.Add(Button("Open Project Settings", () => SettingsService.OpenProjectSettings("Project")));
            }));
        }

        private void BuildAddressablesSettingsSection(VisualElement el)
        {
            AddressablesModuleConfig config = addressablesService.EnsureConfigAsset(out _, out _);

            addressablesConfigField = new ObjectField("Module Config")
            {
                objectType = typeof(AddressablesModuleConfig),
                allowSceneObjects = false,
                value = config,
            };
            addressablesConfigField.RegisterValueChangedCallback(evt => BindAddressablesConfig(evt.newValue as AddressablesModuleConfig));
            el.Add(addressablesConfigField);

            el.Add(DevkitTheme.Body(
                "Project-specific Addressables defaults. Setup in Addressables Builder reads these values and applies them to profile paths.",
                dim: true));

            addressablesConfigFieldsRoot = new VisualElement();
            addressablesConfigFieldsRoot.style.marginTop = 6;
            el.Add(addressablesConfigFieldsRoot);

            var actions = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 8,
                    flexWrap = Wrap.Wrap,
                }
            };
            actions.Add(Button("Save Config", SaveAddressablesConfig));
            actions.Add(Button("Ping Config", () =>
            {
                if (addressablesConfigField != null && addressablesConfigField.value != null)
                {
                    EditorGUIUtility.PingObject(addressablesConfigField.value);
                }
            }));
            actions.Add(Button("Open Addressables Builder", AddressablesBuilderWindow.Open));
            el.Add(actions);

            BindAddressablesConfig(config);
        }

        private void BindAddressablesConfig(AddressablesModuleConfig config)
        {
            if (addressablesConfigFieldsRoot == null)
            {
                return;
            }

            addressablesConfigFieldsRoot.Clear();
            addressablesSerializedObject = null;

            if (config == null)
            {
                addressablesConfigFieldsRoot.Add(DevkitTheme.Body("Select an AddressablesModuleConfig asset.", dim: true));
                return;
            }

            addressablesSerializedObject = new SerializedObject(config);

            AddAddressablesProperty("provider");
            AddAddressablesProperty("environment");
            AddAddressablesProperty("catalogMode");
            AddAddressablesProperty("profileName");
            AddAddressablesProperty("groupNameTemplate");
            AddAddressablesProperty("remoteCatalogBaseUrl");
            AddAddressablesProperty("remoteLoadPathTemplate");
            AddAddressablesProperty("remoteCatalogUrlTemplate");
            AddAddressablesProperty("localWorkspaceRoot");
            AddAddressablesProperty("localReportsFolder");
            AddAddressablesProperty("allowOfflineCacheLaunch");
            AddAddressablesProperty("allowOlderCachedSameLab");
            AddAddressablesProperty("networkRequiredIfCacheMiss");
            AddAddressablesProperty("adapterTypeName");
        }

        private void AddAddressablesProperty(string propertyName)
        {
            if (addressablesSerializedObject == null || addressablesConfigFieldsRoot == null)
            {
                return;
            }

            SerializedProperty property = addressablesSerializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            var field = new PropertyField(property)
            {
                style =
                {
                    marginBottom = 4,
                }
            };
            field.Bind(addressablesSerializedObject);
            addressablesConfigFieldsRoot.Add(field);
        }

        private void SaveAddressablesConfig()
        {
            if (addressablesSerializedObject == null)
            {
                return;
            }

            addressablesSerializedObject.ApplyModifiedProperties();

            if (addressablesConfigField != null && addressablesConfigField.value is AddressablesModuleConfig cfg)
            {
                EditorUtility.SetDirty(cfg);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static VisualElement Section(string title, System.Action<VisualElement> fill)
        {
            var box = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.13f, 0.15f, 0.18f, 1f),
                    paddingTop = 10,
                    paddingBottom = 10,
                    paddingLeft = 10,
                    paddingRight = 10,
                    marginBottom = 10,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                }
            };
            var label = new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } };
            box.Add(label);
            var content = new VisualElement();
            box.Add(content);
            fill?.Invoke(content);
            return box;
        }

        static Button Button(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.marginRight = 6;
            button.style.marginBottom = 6;
            return button;
        }
    }
}
#endif
