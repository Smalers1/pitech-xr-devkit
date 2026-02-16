#if UNITY_EDITOR
using Pitech.XR.ContentDelivery;
using Pitech.XR.ContentDelivery.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    /// <summary>
    /// Dedicated content pipeline window for Addressables/CCD authoring.
    /// Keeps Guided Setup focused on scene wiring.
    /// </summary>
    public sealed class AddressablesBuilderWindow : EditorWindow
    {
        private readonly AddressablesService setupService = new AddressablesService();
        private readonly AddressablesValidationService validationService = new AddressablesValidationService();
        private readonly AddressablesBuildService buildService = new AddressablesBuildService();
        private readonly PublishReportService reportService = new PublishReportService();

        private ObjectField configField;
        private TextField labIdField;
        private TextField labVersionField;
        private ObjectField prefabField;
        private Label groupPreviewValueLabel;
        private Label keyPreviewValueLabel;
        private Label mappingStatusLabel;
        private Label nextStepLabel;
        private Label feedbackLabel;
        private Toggle validationGateToggle;
        private Button oneMinuteBuildButton;
        private Button setupButton;
        private Button mapPrefabButton;
        private Button validateButton;
        private Button buildButton;
        private Button openUploadFolderButton;

        private bool mappingIsValid;
        private bool validationPassed;
        private string lastUploadFolder = string.Empty;

        [MenuItem("Pi tech/Addressables Builder")]
        public static void Open()
        {
            var window = GetWindow<AddressablesBuilderWindow>();
            window.titleContent = new GUIContent("Addressables Builder", DevkitContext.TitleIcon);
            window.minSize = new Vector2(980f, 620f);
            window.Show();
        }

        [MenuItem("Pi tech/DevKit/Addressables Builder")]
        public static void OpenFromDevkitMenu()
        {
            Open();
        }

        private void OnEnable()
        {
            BuildUi();
            RefreshMappingPreview();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = DevkitTheme.Bg;

            var scroll = new ScrollView();
            scroll.style.paddingLeft = 12;
            scroll.style.paddingRight = 12;
            scroll.style.paddingTop = 10;
            scroll.style.paddingBottom = 10;
            rootVisualElement.Add(scroll);

            var header = DevkitTheme.Row();
            header.Add(new Label("Addressables Builder")
            {
                style =
                {
                    color = DevkitTheme.Text,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14
                }
            });
            header.Add(DevkitTheme.Flex());
            header.Add(DevkitTheme.Secondary("Refresh", () =>
            {
                EnsureConfigFieldValue();
                RefreshMappingPreview();
            }));
            scroll.Add(header);
            scroll.Add(DevkitTheme.VSpace(4));
            scroll.Add(DevkitTheme.Body(
                "1-minute flow: Setup -> select Prefab -> Map Prefab -> Validate -> Build.",
                dim: true));
            scroll.Add(DevkitTheme.VSpace(8));
            scroll.Add(DevkitTheme.Divider());
            scroll.Add(DevkitTheme.VSpace(10));

            var section = DevkitTheme.Section("Build Content");
            section.Add(DevkitWidgets.PillsRow(
                (ContentDeliveryCapability.HasAddressablesPackage ? DevkitWidgets.PillKind.Success : DevkitWidgets.PillKind.Warning,
                    ContentDeliveryCapability.HasAddressablesPackage ? "Addressables ready" : "Addressables missing"),
                (ContentDeliveryCapability.HasCcdPackage ? DevkitWidgets.PillKind.Success : DevkitWidgets.PillKind.Neutral,
                    ContentDeliveryCapability.HasCcdPackage ? "CCD package present" : "CCD optional"),
                (DevkitWidgets.PillKind.Neutral, "One remote group per lab")));
            section.Add(DevkitTheme.VSpace(8));

            configField = new ObjectField("Module Config")
            {
                objectType = typeof(AddressablesModuleConfig),
                allowSceneObjects = false
            };
            section.Add(configField);

            labIdField = new TextField("Lab Id (group key)") { value = "default" };
            section.Add(labIdField);

            labVersionField = new TextField("Lab version id") { value = string.Empty };
            section.Add(labVersionField);

            prefabField = new ObjectField("Prefab to include")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false
            };
            section.Add(prefabField);

            section.Add(CreateReadOnlyPreviewRow("Group preview", out groupPreviewValueLabel));
            section.Add(CreateReadOnlyPreviewRow("Address key preview", out keyPreviewValueLabel));

            mappingStatusLabel = DevkitTheme.Body("Mapping status: unknown", dim: true);
            section.Add(mappingStatusLabel);

            validationGateToggle = new Toggle("Require Validate gate before Build") { value = true };
            section.Add(validationGateToggle);

            nextStepLabel = DevkitTheme.Body("Next step: 1) Setup", dim: true);
            nextStepLabel.style.whiteSpace = WhiteSpace.Normal;
            section.Add(DevkitTheme.VSpace(4));
            section.Add(nextStepLabel);

            feedbackLabel = DevkitTheme.Body("Choose prefab and run Setup.", dim: true);
            feedbackLabel.style.whiteSpace = WhiteSpace.Normal;
            section.Add(DevkitTheme.VSpace(6));
            section.Add(feedbackLabel);

            section.Add(DevkitTheme.VSpace(8));
            setupButton = DevkitTheme.Primary("1) Setup", RunSetup);
            mapPrefabButton = DevkitTheme.Secondary("2) Map Prefab", RunMapPrefab);
            validateButton = DevkitTheme.Secondary("3) Validate", RunValidate);
            buildButton = DevkitTheme.Secondary("4) Build", RunBuild);
            oneMinuteBuildButton = DevkitTheme.Secondary("Fast: One Minute Build", RunOneMinuteBuild);
            openUploadFolderButton = DevkitTheme.Secondary("Open Upload Folder", OpenUploadFolder);
            section.Add(DevkitWidgets.Actions(
                setupButton,
                mapPrefabButton,
                validateButton,
                buildButton,
                oneMinuteBuildButton,
                openUploadFolderButton));

            scroll.Add(section);

            EnsureConfigFieldValue();
            RegisterFieldCallbacks();
        }

        private void RegisterFieldCallbacks()
        {
            configField.RegisterValueChangedCallback(_ =>
            {
                validationPassed = false;
                mappingIsValid = false;
                RefreshMappingPreview();
            });
            labIdField.RegisterValueChangedCallback(_ =>
            {
                validationPassed = false;
                mappingIsValid = false;
                RefreshMappingPreview();
            });
            prefabField.RegisterValueChangedCallback(_ =>
            {
                validationPassed = false;
                mappingIsValid = false;
                RefreshMappingPreview();
            });
            validationGateToggle.RegisterValueChangedCallback(_ => RefreshStepState());
        }

        private void EnsureConfigFieldValue()
        {
            if (configField == null || configField.value != null)
            {
                return;
            }

            AddressablesModuleConfig config = setupService.EnsureConfigAsset(out _, out _);
            if (config != null)
            {
                configField.SetValueWithoutNotify(config);
            }
        }

        private AddressablesModuleConfig EnsureSelectedConfig()
        {
            EnsureConfigFieldValue();
            return configField != null ? configField.value as AddressablesModuleConfig : null;
        }

        private string ResolveLabId()
        {
            return string.IsNullOrWhiteSpace(labIdField != null ? labIdField.value : null)
                ? "default"
                : labIdField.value.Trim();
        }

        private void RefreshMappingPreview()
        {
            if (groupPreviewValueLabel == null || keyPreviewValueLabel == null)
            {
                return;
            }

            var config = EnsureSelectedConfig();
            var prefab = prefabField != null ? prefabField.value as GameObject : null;
            string resolvedLabId = ResolveLabId();

            IAddressablesConventionAdapter adapter = AddressablesAdapterResolver.Resolve(config);
            groupPreviewValueLabel.text = adapter.BuildGroupName(config, resolvedLabId);

            if (prefab == null)
            {
                keyPreviewValueLabel.text = "Select a prefab to preview key.";
                if (mappingStatusLabel != null)
                {
                    mappingStatusLabel.text = "Mapping status: select prefab first.";
                }
                mappingIsValid = false;
                RefreshStepState();
                return;
            }

            AddressablesMarkPrefabResult preview = setupService.MarkPrefabAddressable(config, resolvedLabId, prefab, dryRun: true);
            keyPreviewValueLabel.text = preview != null ? preview.addressKey : string.Empty;
            bool mapped = setupService.IsPrefabMapped(config, resolvedLabId, prefab, out string mappedGroup, out string mappedKey);
            mappingIsValid = mapped;
            if (mapped)
            {
                groupPreviewValueLabel.text = mappedGroup;
                keyPreviewValueLabel.text = mappedKey;
            }

            if (mappingStatusLabel != null)
            {
                mappingStatusLabel.text = mapped
                    ? "Mapping status: prefab is mapped to the expected lab group."
                    : "Mapping status: prefab not mapped yet. Click 2) Map Prefab.";
            }
            RefreshStepState();
        }

        private void RefreshStepState()
        {
            bool hasSetup = setupService.HasInitializedAddressablesSettings();
            bool hasPrefab = prefabField != null && prefabField.value is GameObject;
            bool requireValidationGate = validationGateToggle != null && validationGateToggle.value;

            if (setupButton != null)
            {
                setupButton.SetEnabled(true);
            }

            if (mapPrefabButton != null)
            {
                mapPrefabButton.SetEnabled(hasSetup && hasPrefab);
            }

            if (validateButton != null)
            {
                validateButton.SetEnabled(hasSetup && mappingIsValid);
            }

            if (buildButton != null)
            {
                bool canBuild = hasSetup && mappingIsValid && (!requireValidationGate || validationPassed);
                buildButton.SetEnabled(canBuild);
            }

            if (oneMinuteBuildButton != null)
            {
                oneMinuteBuildButton.SetEnabled(hasPrefab);
            }

            if (openUploadFolderButton != null)
            {
                openUploadFolderButton.SetEnabled(!string.IsNullOrWhiteSpace(lastUploadFolder) && System.IO.Directory.Exists(lastUploadFolder));
            }

            if (nextStepLabel == null)
            {
                return;
            }

            if (!hasSetup)
            {
                nextStepLabel.text = "Next step: 1) Setup";
                return;
            }

            if (!hasPrefab)
            {
                nextStepLabel.text = "Next step: choose `Prefab to include`.";
                return;
            }

            if (!mappingIsValid)
            {
                nextStepLabel.text = "Next step: 2) Map Prefab";
                return;
            }

            if (requireValidationGate && !validationPassed)
            {
                nextStepLabel.text = "Next step: 3) Validate";
                return;
            }

            nextStepLabel.text = "Next step: 4) Build";
        }

        private void RunSetup()
        {
            var setup = setupService.EnsureInitialized(ResolveLabId());
            validationPassed = false;
            EnsureConfigFieldValue();
            RefreshMappingPreview();
            feedbackLabel.text = BuildSetupSummary(setup);
            DevkitHubWindow.TryRefresh();
        }

        private AddressablesMarkPrefabResult RunMapPrefabInternal(bool showDialogOnFailure)
        {
            var config = EnsureSelectedConfig();
            var prefab = prefabField != null ? prefabField.value as GameObject : null;
            AddressablesMarkPrefabResult map = setupService.MarkPrefabAddressable(config, ResolveLabId(), prefab, dryRun: false);
            RefreshMappingPreview();

            if (!map.success && showDialogOnFailure)
            {
                EditorUtility.DisplayDialog("Addressables Builder", map.summary, "OK");
            }

            if (map.success)
            {
                mappingIsValid = true;
                validationPassed = false;
                feedbackLabel.text =
                    $"{map.summary}\n" +
                    $"Group: {map.groupName}\n" +
                    $"Address key: {map.addressKey}";
                EditorGUIUtility.systemCopyBuffer = map.addressKey ?? string.Empty;
            }
            else
            {
                mappingIsValid = false;
            }

            RefreshStepState();

            return map;
        }

        private void RunMapPrefab()
        {
            RunMapPrefabInternal(showDialogOnFailure: true);
        }

        private AddressablesValidationResult RunValidateInternal(string actorLabel, out PublishTransactionReportData report)
        {
            var config = EnsureSelectedConfig();
            var validation = validationService.Validate(config, ResolveLabId());
            report = reportService.CreateDraft(
                config,
                PublishTransactionSource.GuidedSetup,
                actorLabel,
                ResolveLabId(),
                labVersionField != null ? labVersionField.value : string.Empty);
            reportService.ApplyValidation(report, validation, actorLabel);
            return validation;
        }

        private void RunValidate()
        {
            var config = EnsureSelectedConfig();
            AddressablesValidationResult validation = RunValidateInternal("addressables_builder_validate", out PublishTransactionReportData report);
            validationPassed = validation != null && validation.success;
            PublishReportWriteResult saved = reportService.Save(report, config);
            feedbackLabel.text =
                $"{BuildValidationSummary(validation)}\n" +
                $"State: {report.state}\n" +
                $"Report: {saved.jsonPath}";
            RefreshStepState();
        }

        private void RunBuild()
        {
            var config = EnsureSelectedConfig();
            if (config == null)
            {
                EditorUtility.DisplayDialog("Addressables Builder", "Module config is missing.", "OK");
                return;
            }

            AddressablesValidationResult validation = null;
            PublishTransactionReportData report = reportService.CreateDraft(
                config,
                PublishTransactionSource.HiddenBuild,
                "addressables_builder_build",
                ResolveLabId(),
                labVersionField != null ? labVersionField.value : string.Empty);

            if (validationGateToggle != null && validationGateToggle.value)
            {
                validation = validationService.Validate(config, ResolveLabId());
                validationPassed = validation.success;
                reportService.ApplyValidation(report, validation, "addressables_builder_build");
                if (!validation.success)
                {
                    PublishReportWriteResult failed = reportService.Save(report, config);
                    feedbackLabel.text =
                        $"{BuildValidationSummary(validation)}\n" +
                        $"Report: {failed.jsonPath}\n" +
                        "Build cancelled because validation failed.";
                    RefreshStepState();
                    return;
                }
            }

            reportService.ApplyBuildStart(report, "addressables_builder_build");
            AddressablesBuildResult build = buildService.Build(config, dryRun: false);
            lastUploadFolder = build != null
                ? (string.IsNullOrWhiteSpace(build.uploadPath) ? (build.outputPath ?? string.Empty) : build.uploadPath)
                : string.Empty;
            reportService.ApplyBuildResult(report, build, "addressables_builder_build");
            PublishReportWriteResult saved = reportService.Save(report, config);

            string validationLine = validation != null
                ? BuildValidationSummary(validation) + "\n"
                : string.Empty;
            feedbackLabel.text =
                validationLine +
                $"{BuildBuildSummary(build)}\n" +
                $"State: {report.state}\n" +
                $"Report: {saved.jsonPath}";
            RefreshStepState();
        }

        private void RunOneMinuteBuild()
        {
            var config = EnsureSelectedConfig();
            if (config == null)
            {
                EditorUtility.DisplayDialog("Addressables Builder", "Module config is missing.", "OK");
                return;
            }

            AddressablesSetupResult setup = setupService.EnsureInitialized(ResolveLabId());
            if (!setup.success)
            {
                feedbackLabel.text = BuildSetupSummary(setup);
                return;
            }

            AddressablesMarkPrefabResult map = RunMapPrefabInternal(showDialogOnFailure: false);
            if (!map.success)
            {
                feedbackLabel.text = $"{BuildSetupSummary(setup)}\n{map.summary}";
                RefreshStepState();
                return;
            }

            AddressablesValidationResult validation = validationService.Validate(config, ResolveLabId());
            validationPassed = validation.success;
            PublishTransactionReportData report = reportService.CreateDraft(
                config,
                PublishTransactionSource.HiddenBuild,
                "addressables_builder_one_minute",
                ResolveLabId(),
                labVersionField != null ? labVersionField.value : string.Empty);
            reportService.ApplyValidation(report, validation, "addressables_builder_one_minute");
            if (!validation.success)
            {
                PublishReportWriteResult failed = reportService.Save(report, config);
                feedbackLabel.text =
                    $"{BuildSetupSummary(setup)}\n" +
                    $"{BuildValidationSummary(validation)}\n" +
                    $"Report: {failed.jsonPath}\n" +
                    "One Minute Build stopped at validation.";
                RefreshStepState();
                return;
            }

            reportService.ApplyBuildStart(report, "addressables_builder_one_minute");
            AddressablesBuildResult build = buildService.Build(config, dryRun: false);
            lastUploadFolder = build != null
                ? (string.IsNullOrWhiteSpace(build.uploadPath) ? (build.outputPath ?? string.Empty) : build.uploadPath)
                : string.Empty;
            reportService.ApplyBuildResult(report, build, "addressables_builder_one_minute");
            PublishReportWriteResult saved = reportService.Save(report, config);

            feedbackLabel.text =
                $"{BuildSetupSummary(setup)}\n" +
                $"Mapped: {map.addressKey}\n" +
                $"{BuildValidationSummary(validation)}\n" +
                $"{BuildBuildSummary(build)}\n" +
                $"State: {report.state}\n" +
                $"Report: {saved.jsonPath}";
            RefreshMappingPreview();
            RefreshStepState();
        }

        private void OpenUploadFolder()
        {
            if (string.IsNullOrWhiteSpace(lastUploadFolder))
            {
                EditorUtility.DisplayDialog("Addressables Builder", "No upload folder is available yet. Run Build first.", "OK");
                return;
            }

            string normalized = lastUploadFolder.Replace("\\", "/");
            if (!System.IO.Directory.Exists(normalized))
            {
                EditorUtility.DisplayDialog("Addressables Builder", $"Upload folder does not exist:\n{normalized}", "OK");
                return;
            }

            EditorUtility.RevealInFinder(normalized);
        }

        private static string BuildSetupSummary(AddressablesSetupResult result)
        {
            if (result == null)
            {
                return "Setup did not return a result.";
            }

            return $"{result.summary}\nCapability: {result.capabilitySummary}";
        }

        private static string BuildValidationSummary(AddressablesValidationResult validation)
        {
            if (validation == null)
            {
                return "Validation did not return a result.";
            }

            return
                $"{validation.summary}\n" +
                $"Expected group: {validation.expectedGroupName}\n" +
                $"Profile: {validation.profileName}\n" +
                $"Errors: {validation.errorCount}, Warnings: {validation.warningCount}";
        }

        private static string BuildBuildSummary(AddressablesBuildResult build)
        {
            if (build == null)
            {
                return "Build did not return a result.";
            }

            string upload = string.IsNullOrWhiteSpace(build.uploadPath) ? build.outputPath : build.uploadPath;
            string internalPath = string.IsNullOrWhiteSpace(build.internalBuildPath) ? "-" : build.internalBuildPath;
            return
                $"{build.summary}\n" +
                $"Upload folder: {upload}\n" +
                $"Internal path: {internalPath}\n" +
                $"Content hash: {build.contentHash}\n" +
                $"Catalog hash: {build.catalogHash}\n" +
                $"Bundle size: {build.bundleSizeBytes} bytes";
        }

        private static VisualElement CreateReadOnlyPreviewRow(string title, out Label valueLabel)
        {
            var row = DevkitTheme.Row();
            row.style.marginBottom = 4;

            var titleLabel = new Label(title)
            {
                style =
                {
                    minWidth = 150,
                    color = DevkitTheme.SubText,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            row.Add(titleLabel);

            var valueContainer = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    minHeight = 22,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 2,
                    paddingBottom = 2,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f),
                    borderTopColor = new Color(0.20f, 0.22f, 0.26f, 1f),
                    borderRightColor = new Color(0.20f, 0.22f, 0.26f, 1f),
                    borderBottomColor = new Color(0.20f, 0.22f, 0.26f, 1f),
                    borderLeftColor = new Color(0.20f, 0.22f, 0.26f, 1f),
                    borderTopWidth = 1,
                    borderRightWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1
                }
            };

            valueLabel = new Label(string.Empty)
            {
                style =
                {
                    color = DevkitTheme.Text,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            valueContainer.Add(valueLabel);
            row.Add(valueContainer);
            return row;
        }
    }
}
#endif
