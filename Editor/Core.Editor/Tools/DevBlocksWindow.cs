#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class DevBlocksWindow : EditorWindow
    {
        const string DefaultCreateFolder = "Assets/Pi tech/Dev Blocks";

        readonly HashSet<string> _selectedCategories = new();
        string _search = "";

        ScrollView _scroll;
        VisualElement _grid;
        VisualElement _chipsRow;
        int _columns = 3;
        float _gridWidth;
        float _cardWidth;
        const float CardGap = 12f;
        const float MinCardWidth = 290f;

        // Create form state
        ObjectField _createPrefab;
        TextField _createName;
        PopupField<string> _createCategory;
        List<string> _createCategoryRaw = new();
        int _createCategoryIndex;

        // Cached data
        readonly List<DevBlockItem> _items = new();

        [MenuItem("Pi tech/Dev Blocks")]
        public static void Open()
        {
            var w = GetWindow<DevBlocksWindow>();
            w.titleContent = new GUIContent("Dev Blocks", DevkitContext.TitleIcon);
            w.minSize = new Vector2(920, 560);
            w.Show();
        }

        void OnEnable()
        {
            BuildUI();
            RefreshItems();
            RebuildGrid();
            RefreshCategoryChips();
            // Preload previews (helps grid feel snappy after first scroll).
            EditorApplication.delayCall += Repaint;
        }

        void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = DevkitTheme.Bg;

            _scroll = new ScrollView();
            _scroll.style.paddingLeft = 12;
            _scroll.style.paddingRight = 12;
            _scroll.style.paddingTop = 10;
            _scroll.style.paddingBottom = 10;
            rootVisualElement.Add(_scroll);

            // Header
            var header = DevkitTheme.Row();
            header.Add(new Label("Dev Blocks")
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
                RefreshItems();
                RebuildGrid();
            }));
            header.Add(DevkitTheme.Primary("Create a Dev Block", () =>
            {
                // Scroll to bottom where create section lives (last element).
                _scroll.ScrollTo(_scroll.contentContainer[_scroll.contentContainer.childCount - 1]);
            }));

            _scroll.Add(header);
            _scroll.Add(DevkitTheme.Divider());
            _scroll.Add(DevkitTheme.VSpace(8));

            // Overview (Meta-style)
            var overview = DevkitTheme.Section("Overview");
            overview.Add(DevkitTheme.Body("Dev Blocks is a reusable prefab library for building scenes fast.", dim: true));
            overview.Add(DevkitTheme.VSpace(6));
            overview.Add(DevkitTheme.Body("• Add a Dev Block to the current scene with the + button."));
            overview.Add(DevkitTheme.Body("• Filter by category or search by name."));
            overview.Add(DevkitTheme.Body("• Create new Dev Blocks from any prefab in your project."));
            _scroll.Add(overview);
            _scroll.Add(DevkitTheme.VSpace(6));

            // Filters
            _scroll.Add(BuildFilters());
            _scroll.Add(DevkitTheme.VSpace(10));

            // Grid
            var library = DevkitTheme.Section("Library");
            _grid = DevkitWidgets.TileGrid();
            _grid.style.justifyContent = Justify.FlexStart;
            library.Add(_grid);
            _scroll.Add(library);

            _grid.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                _gridWidth = evt.newRect.width;
                int newCols = ColumnsForWidth(_gridWidth);
                if (newCols != _columns || Mathf.Abs(_gridWidth - evt.oldRect.width) > 0.5f)
                {
                    _columns = newCols;
                    RebuildGrid();
                }
            });

            // Create section (same window)
            _scroll.Add(BuildCreateSection());
        }

        VisualElement BuildFilters()
        {
            var wrap = DevkitTheme.Section("Browse");

            // Search + category menu
            var row = DevkitTheme.Row();

            var search = new TextField { value = _search };
            search.style.flexGrow = 1;
            search.label = "Search";
            search.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? "";
                RebuildGrid();
            });
            row.Add(search);

            row.Add(DevkitTheme.HSpace(10));

            var catBtn = DevkitTheme.Secondary("Categories ▾", ShowCategoriesMenu);
            row.Add(catBtn);

            wrap.Add(row);
            wrap.Add(DevkitTheme.VSpace(8));

            _chipsRow = DevkitTheme.WrapRow();
            wrap.Add(_chipsRow);

            RefreshCategoryChips();

            return wrap;
        }

        VisualElement BuildCreateSection()
        {
            var section = DevkitTheme.Section("Create a Dev Block");
            section.Add(DevkitTheme.Body("Drop a prefab, pick category, create. The Dev Block asset will be saved under `Assets/Pi tech/Dev Blocks`.", dim: true));
            section.Add(DevkitTheme.VSpace(10));

            // Drag-drop zone
            var drop = new VisualElement();
            drop.style.height = 58;
            drop.style.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1);
            drop.style.borderTopLeftRadius = drop.style.borderTopRightRadius =
                drop.style.borderBottomLeftRadius = drop.style.borderBottomRightRadius = 10;
            drop.style.borderBottomWidth = drop.style.borderTopWidth = drop.style.borderLeftWidth = drop.style.borderRightWidth = 1;
            var outline = new Color(1, 1, 1, 0.08f);
            drop.style.borderBottomColor = drop.style.borderTopColor = drop.style.borderLeftColor = drop.style.borderRightColor = outline;
            drop.style.justifyContent = Justify.Center;
            drop.style.alignItems = Align.Center;
            drop.Add(new Label("Drag & drop a Prefab here") { style = { color = DevkitTheme.SubText, unityTextAlign = TextAnchor.MiddleCenter } });

            drop.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            });
            drop.RegisterCallback<DragPerformEvent>(_ =>
            {
                DragAndDrop.AcceptDrag();
                var obj = DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0
                    ? DragAndDrop.objectReferences[0]
                    : null;
                if (obj is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go))
                    SetCreatePrefab(go);
                else
                    EditorUtility.DisplayDialog("Dev Blocks", "Please drop a Prefab asset.", "OK");
            });

            section.Add(drop);
            section.Add(DevkitTheme.VSpace(10));

            // Form fields
            var form = new VisualElement { style = { flexDirection = FlexDirection.Column } };

            _createPrefab = new ObjectField("Prefab") { objectType = typeof(GameObject), allowSceneObjects = false };
            _createPrefab.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is GameObject go) SetCreatePrefab(go);
            });
            form.Add(_createPrefab);

            _createName = new TextField("Name") { value = "" };
            form.Add(_createName);

            _createCategoryRaw = SceneCategoriesService.DefaultCategories.ToList();
            var displayCats = _createCategoryRaw.Select(PrettyCategory).ToList();
            _createCategoryIndex = Mathf.Clamp(_createCategoryRaw.IndexOf("--- INTERACTABLES ---"), 0, _createCategoryRaw.Count - 1);
            _createCategory = new PopupField<string>("Category", displayCats, _createCategoryIndex);
            _createCategory.RegisterValueChangedCallback(evt =>
            {
                _createCategoryIndex = displayCats.IndexOf(evt.newValue);
                if (_createCategoryIndex < 0) _createCategoryIndex = 0;
            });
            form.Add(_createCategory);

            section.Add(form);
            section.Add(DevkitTheme.VSpace(10));

            section.Add(DevkitWidgets.Actions(
                DevkitTheme.Secondary("Select Folder", () =>
                {
                    var picked = EditorUtility.OpenFolderPanel("Dev Blocks Folder", "Assets", "");
                    if (string.IsNullOrEmpty(picked)) return;
                    // We keep the default path in v1; this button is just UX affordance for later.
                    EditorUtility.DisplayDialog("Dev Blocks", $"Default save location is:\n{DefaultCreateFolder}\n\n(We can add a project setting to customize this.)", "OK");
                }),
                DevkitTheme.Primary("Create Dev Block", CreateDevBlock)
            ));

            return section;
        }

        void SetCreatePrefab(GameObject prefab)
        {
            if (!prefab) return;
            if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                EditorUtility.DisplayDialog("Dev Blocks", "Please assign a Prefab asset (not a scene object).", "OK");
                return;
            }

            _createPrefab.SetValueWithoutNotify(prefab);
            if (string.IsNullOrWhiteSpace(_createName.value))
                _createName.SetValueWithoutNotify(prefab.name);
        }

        void CreateDevBlock()
        {
            var prefab = _createPrefab.value as GameObject;
            if (!prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                EditorUtility.DisplayDialog("Dev Blocks", "Pick a Prefab asset first.", "OK");
                return;
            }

            EnsureFolderPath(DefaultCreateFolder);

            string niceName = (_createName.value ?? "").Trim();
            if (string.IsNullOrEmpty(niceName)) niceName = prefab.name;

            var asset = CreateInstance<DevBlockItem>();
            asset.prefab = prefab;
            asset.displayName = niceName;
            asset.category = _createCategoryRaw[Mathf.Clamp(_createCategoryIndex, 0, _createCategoryRaw.Count - 1)];

            var path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultCreateFolder}/{SanitizeFileName(niceName)}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            RefreshItems();
            RebuildGrid();
        }

        void RefreshItems()
        {
            _items.Clear();
            var guids = AssetDatabase.FindAssets("t:DevBlockItem");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<DevBlockItem>(path);
                if (item != null) _items.Add(item);
            }

            // Stable ordering for nice UX.
            _items.Sort((a, b) => string.Compare(a?.EffectiveName, b?.EffectiveName, StringComparison.OrdinalIgnoreCase));
        }

        void ShowCategoriesMenu()
        {
            var menu = new GenericMenu();

            // Prefer known anchors, but also include any custom categories used by items.
            var all = new HashSet<string>(SceneCategoriesService.DefaultCategories);
            foreach (var it in _items)
                if (it != null && !string.IsNullOrWhiteSpace(it.category))
                    all.Add(it.category.Trim());

            var sorted = all.ToList();
            sorted.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));

            menu.AddItem(new GUIContent("Select All"), _selectedCategories.Count == sorted.Count, () =>
            {
                _selectedCategories.Clear();
                foreach (var c in sorted) _selectedCategories.Add(c);
                RefreshCategoryChips();
                RebuildGrid();
            });
            menu.AddItem(new GUIContent("Clear"), _selectedCategories.Count == 0, () =>
            {
                _selectedCategories.Clear();
                RefreshCategoryChips();
                RebuildGrid();
            });
            menu.AddSeparator("");

            foreach (var cat in sorted)
            {
                bool on = _selectedCategories.Contains(cat);
                menu.AddItem(new GUIContent(cat), on, () =>
                {
                    if (on) _selectedCategories.Remove(cat);
                    else _selectedCategories.Add(cat);
                    RefreshCategoryChips();
                    RebuildGrid();
                });
            }

            menu.ShowAsContext();
        }

        void RefreshCategoryChips()
        {
            _chipsRow?.Clear();
            if (_chipsRow == null) return;

            // Show category chips (toggle buttons) like Building Blocks.
            var all = new HashSet<string>(SceneCategoriesService.DefaultCategories);
            foreach (var it in _items)
                if (it != null && !string.IsNullOrWhiteSpace(it.category))
                    all.Add(it.category.Trim());

            var sorted = all.ToList();
            sorted.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));

            foreach (var cat in sorted)
            {
                var name = PrettyCategory(cat);
                bool selected = _selectedCategories.Contains(cat);
                var chip = new Button(() =>
                {
                    if (selected) _selectedCategories.Remove(cat);
                    else _selectedCategories.Add(cat);
                    RefreshCategoryChips();
                    RebuildGrid();
                });
                chip.text = "";

                chip.style.paddingLeft = chip.style.paddingRight = 8;
                chip.style.paddingTop = chip.style.paddingBottom = 4;
                chip.style.marginRight = 6;
                chip.style.marginBottom = 6;
                chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius =
                    chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 8;
                chip.style.fontSize = 11;
                chip.style.unityFontStyleAndWeight = FontStyle.Bold;

                if (selected)
                {
                    chip.style.backgroundColor = DevkitTheme.Brand;
                    chip.style.color = Color.white;
                }
                else
                {
                    chip.style.backgroundColor = new Color(0.20f, 0.22f, 0.26f, 1f);
                    chip.style.color = DevkitTheme.Text;
                }

                var chipRow = DevkitTheme.Row();
                var icon = GetCategoryIcon(cat);
                if (icon != null)
                {
                    var img = new Image { image = icon };
                    img.style.width = 12;
                    img.style.height = 12;
                    img.style.marginRight = 6;
                    chipRow.Add(img);
                }
                chipRow.Add(new Label(name) { style = { color = selected ? Color.white : DevkitTheme.Text, fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold } });
                chip.Add(chipRow);
                _chipsRow.Add(chip);
            }
        }

        void RebuildGrid()
        {
            if (_grid == null) return;
            _grid.Clear();

            var filtered = _items
                .Where(it => it != null && it.HasPrefab)
                // If no categories are selected, show all (user starts with none selected).
                .Where(it => _selectedCategories.Count == 0 || _selectedCategories.Contains((it.category ?? "").Trim()))
                .Where(it =>
                {
                    if (string.IsNullOrWhiteSpace(_search)) return true;
                    var q = _search.Trim();
                    if (it.EffectiveName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (!string.IsNullOrWhiteSpace(it.description) && it.description.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    foreach (var t in it.TagsSafe)
                        if (!string.IsNullOrWhiteSpace(t) && t.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    return false;
                })
                .ToList();

            if (filtered.Count == 0)
            {
                _grid.Add(DevkitWidgets.Card(
                    "No Dev Blocks found",
                    "Create one below (drag a prefab + click Create).",
                    DevkitWidgets.Actions(DevkitTheme.Secondary("Create a Dev Block", () =>
                    {
                        _scroll.ScrollTo(_scroll.contentContainer[_scroll.contentContainer.childCount - 1]);
                    }))
                ));
                return;
            }

            // Calculate exact widths so we never accidentally wrap to 2 and leave a dead strip on the right.
            _cardWidth = CalcCardWidth(_gridWidth, _columns);
            for (int i = 0; i < filtered.Count; i++)
                _grid.Add(BlockCard(filtered[i], i));
        }

        VisualElement BlockCard(DevBlockItem item, int index)
        {
            // Custom card to match Meta Building Blocks layout.
            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = DevkitTheme.Panel2,
                    borderTopLeftRadius = 12, borderTopRightRadius = 12,
                    borderBottomLeftRadius = 12, borderBottomRightRadius = 12,
                    paddingLeft = 12, paddingRight = 12, paddingTop = 10, paddingBottom = 10,
                    marginBottom = 8,
                    borderBottomWidth = 1, borderTopWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderBottomColor = new Color(0.10f,0.12f,0.16f,1),
                    borderTopColor    = new Color(0.10f,0.12f,0.16f,1),
                    borderLeftColor   = new Color(0.10f,0.12f,0.16f,1),
                    borderRightColor  = new Color(0.10f,0.12f,0.16f,1),
                    position = Position.Relative
                }
            };

            // Preview container (with overlay + button)
            var preview = new VisualElement();
            preview.style.height = 120;
            preview.style.backgroundColor = new Color(0, 0, 0, 0.15f);
            preview.style.borderTopLeftRadius = preview.style.borderTopRightRadius =
                preview.style.borderBottomLeftRadius = preview.style.borderBottomRightRadius = 10;
            preview.style.position = Position.Relative;
            preview.style.overflow = Overflow.Hidden;

            var img = new Image();
            img.scaleMode = ScaleMode.ScaleToFit;
            img.style.height = 120;
            img.style.unityBackgroundImageTintColor = Color.white;

            // Use mini thumbnail immediately, then swap to preview once generated.
            if (item.prefab != null)
            {
                var mini = AssetPreview.GetMiniThumbnail(item.prefab) as Texture2D;
                img.image = mini;

                IVisualElementScheduledItem scheduled = null;
                scheduled = img.schedule.Execute(() =>
                {
                    if (item.prefab == null) { scheduled?.Pause(); return; }
                    var previewTex = AssetPreview.GetAssetPreview(item.prefab);
                    if (previewTex != null)
                    {
                        img.image = previewTex;
                        scheduled?.Pause();
                    }
                }).Every(250);
            }

            preview.Add(img);

            card.Add(preview);

            card.Add(DevkitTheme.VSpace(8));
            card.Add(new Label(item.EffectiveName)
            {
                style = { color = DevkitTheme.Text, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12 }
            });

            var meta = DevkitTheme.WrapRow();
            meta.style.marginTop = 6;
            meta.Add(CategoryPill(item.category));
            card.Add(meta);

            var addBtn = SmallIconButton(EditorGUIUtility.IconContent("Toolbar Plus"), () => AddToScene(item));
            addBtn.tooltip = "Add to scene";
            addBtn.style.position = Position.Absolute;
            addBtn.style.right = 10;
            addBtn.style.bottom = 10;
            card.Add(addBtn);
            // Responsive columns (3/2/1) with consistent spacing.
            // Use explicit pixel width so margins don't cause wrapping.
            card.style.flexBasis = _cardWidth;
            card.style.width = _cardWidth;
            card.style.maxWidth = _cardWidth;
            card.style.flexGrow = 0;
            card.style.flexShrink = 0;
            card.style.minWidth = 0;
            bool endOfRow = _columns > 0 && ((index + 1) % _columns == 0);
            card.style.marginRight = endOfRow ? 0 : CardGap;
            card.style.marginBottom = CardGap;
            return card;
        }

        static string PrettyCategory(string raw)
            => string.IsNullOrWhiteSpace(raw) ? "Uncategorized" : raw.Replace("---", "").Trim();

        static Texture2D GetCategoryIcon(string raw)
        {
            var key = (raw ?? "").ToUpperInvariant();
            if (key.Contains("AUDIO")) return TryIcon("AudioSource Icon");
            if (key.Contains("CAMERAS")) return TryIcon("Camera Icon");
            if (key.Contains("DEBUG")) return TryIcon("console.warnicon.sml");
            if (key.Contains("ENVIRONMENT")) return TryIcon("TerrainInspector.TerrainToolRaise");
            if (key.Contains("INTERACTABLES")) return TryIcon("d_MoveTool");
            if (key.Contains("LIGHTING")) return TryIcon("Light Icon");
            if (key.Contains("SCENE MANAGERS")) return TryIcon("GameManager Icon");
            if (key.Contains("TIMELINES")) return TryIcon("TimelineAsset Icon");
            if (key.Contains("UI")) return TryIcon("Canvas Icon");
            if (key.Contains("VFX")) return TryIcon("ParticleSystem Icon");
            return TryIcon("Prefab Icon");
        }

        static Texture2D TryIcon(string name)
        {
            var c = EditorGUIUtility.IconContent(name);
            return c != null ? c.image as Texture2D : null;
        }

        static Texture2D FindTextureByName(string assetName)
        {
            var guids = AssetDatabase.FindAssets(assetName);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) return tex;
            }
            return null;
        }

        static Texture2D _addIcon;

        static int ColumnsForWidth(float width)
        {
            // Pick the largest column count that still respects a minimum card width.
            for (int cols = 3; cols >= 1; cols--)
            {
                float w = CalcCardWidth(width, cols);
                if (w >= MinCardWidth) return cols;
            }
            return 1;
        }

        static float CalcCardWidth(float gridWidth, int cols)
        {
            cols = Mathf.Max(1, cols);
            if (gridWidth <= 0) return MinCardWidth;
            float totalGaps = CardGap * Mathf.Max(0, cols - 1);
            float w = (gridWidth - totalGaps) / cols;
            return Mathf.Floor(w);
        }

        static Button SmallButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.height = 24;
            b.style.minWidth = 0;
            b.style.paddingLeft = b.style.paddingRight = 8;
            b.style.paddingTop = b.style.paddingBottom = 3;
            b.style.fontSize = 11;
            b.style.backgroundColor = new Color(0.18f, 0.20f, 0.24f, 1);
            b.style.color = DevkitTheme.Text;
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
                b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;
            return b;
        }

        static Button SmallIconButton(GUIContent icon, Action onClick)
        {
            if (_addIcon == null)
                _addIcon = FindTextureByName("Add Button");

            var b = new Button(onClick);
            b.style.width = 56;
            b.style.height = 44;
            b.style.paddingLeft = b.style.paddingRight = 0;
            b.style.paddingTop = b.style.paddingBottom = 0;
            b.style.backgroundColor = DevkitTheme.Brand;
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
                b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;
            b.style.justifyContent = Justify.Center;
            b.style.alignItems = Align.Center;

            var tex = _addIcon != null ? _addIcon : icon.image;
            var img = new Image { image = tex };
            img.style.width = 30;
            img.style.height = 30;
            b.Add(img);

            // Hover effect (Meta-like)
            var normal = DevkitTheme.Brand;
            var hover = new Color(
                Mathf.Clamp01(normal.r + 0.08f),
                Mathf.Clamp01(normal.g + 0.08f),
                Mathf.Clamp01(normal.b + 0.08f),
                1f
            );
            b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = hover);
            b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = normal);

            return b;
        }

        static VisualElement CategoryPill(string rawCategory)
        {
            var pill = new VisualElement();
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.backgroundColor = new Color(0.20f, 0.22f, 0.26f, 1f);
            pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius =
                pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 7;
            pill.style.paddingLeft = 8;
            pill.style.paddingRight = 8;
            pill.style.paddingTop = 3;
            pill.style.paddingBottom = 3;

            var icon = GetCategoryIcon(rawCategory);
            if (icon != null)
            {
                var img = new Image { image = icon };
                img.style.width = 12;
                img.style.height = 12;
                img.style.marginRight = 6;
                pill.Add(img);
            }

            pill.Add(new Label(PrettyCategory(rawCategory))
            {
                style = { color = DevkitTheme.Text, fontSize = 11, unityFontStyleAndWeight = FontStyle.Bold }
            });

            return pill;
        }

        void AddToScene(DevBlockItem item)
        {
            if (item == null || item.prefab == null) return;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("Dev Blocks", "Open a scene first.", "OK");
                return;
            }

            // Always ensure the anchor exists (safe + keeps hierarchy tidy).
            var anchor = EnsureRoot(scene, string.IsNullOrWhiteSpace(item.category) ? "--- ENVIRONMENT ---" : item.category.Trim());
            if (anchor == null)
            {
                EditorUtility.DisplayDialog("Dev Blocks", "Could not create/find the scene anchor root.", "OK");
                return;
            }

            // Suggested fixes (prompt)
            var setup = new GuidedSetupService();
            var missingManagers = FindMissingSuggestedManagers(setup, item);

            if (missingManagers.Count > 0)
            {
                string msg = "This Dev Block suggests the following scene setup:\n\n"
                             + string.Join("\n", missingManagers.Select(m => $"- {m}"))
                             + "\n\nFix now?";

                int choice = EditorUtility.DisplayDialogComplex("Dev Blocks", msg, "Fix & Add", "Add Anyway", "Cancel");
                if (choice == 2) return; // cancel
                if (choice == 0)
                    FixSuggestedManagers(setup, missingManagers);
            }

            // Instantiate under anchor at origin
            var inst = PrefabUtility.InstantiatePrefab(item.prefab, scene) as GameObject;
            if (!inst)
            {
                EditorUtility.DisplayDialog("Dev Blocks", "Failed to instantiate prefab.", "OK");
                return;
            }

            Undo.RegisterCreatedObjectUndo(inst, "Add Dev Block");
            inst.transform.SetParent(anchor, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = inst;
        }

        static List<string> FindMissingSuggestedManagers(GuidedSetupService setup, DevBlockItem item)
        {
            var missing = new List<string>();
            if (item.suggestedManagerComponentTypes == null) return missing;

            foreach (var fullName in item.suggestedManagerComponentTypes)
            {
                var t = (fullName ?? "").Trim();
                if (string.IsNullOrEmpty(t)) continue;
                var found = setup.FindFirstInScene(t) as Component;
                if (!found) missing.Add(t);
            }
            return missing;
        }

        static void FixSuggestedManagers(GuidedSetupService setup, List<string> missingManagerTypes)
        {
            if (missingManagerTypes == null || missingManagerTypes.Count == 0) return;

            // Ensure managers root exists first.
            setup.EnsureManagersRoot();

            foreach (var fullTypeName in missingManagerTypes)
            {
                var t = GuidedSetupService.FindType(fullTypeName);
                if (t == null) continue;
                string goName = t.Name;
                setup.CreateUnderManagersRoot(fullTypeName, goName, $"Create {goName}");
            }
        }

        static Transform EnsureRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create Scene Anchor");
            EditorSceneManager.MarkSceneDirty(scene);
            return created.transform;
        }

        static void EnsureFolderPath(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            // Create nested folders from "Assets/..."
            var parts = path.Split('/');
            if (parts.Length == 0) return;
            if (parts[0] != "Assets") throw new Exception("Dev Blocks folder must be under Assets.");

            string cur = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static string SanitizeFileName(string raw)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                raw = raw.Replace(c, '_');
            return raw.Trim();
        }
    }
}
#endif


