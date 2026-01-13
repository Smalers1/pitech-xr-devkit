#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Pitech.XR.Scenario;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UIElements;
using Pitech.XR.Interactables;

// Avoid Button clash (UGUI vs UIElements)
using UGUIButton = UnityEngine.UI.Button;
using UIEButton = UnityEngine.UIElements.Button;

public class ScenarioGraphWindow : EditorWindow
{
    [SerializeField]
    Scenario scenario;
    [SerializeField]
    string authoringScenarioGlobalId;
    [SerializeField]
    string authoringScenarioScenePath;
    [SerializeField]
    string authoringScenarioGameObjectName;
    ScenarioGraphView view;
    readonly Dictionary<string, StepNode> nodes = new();
    readonly Dictionary<string, StickyNote> notes = new();
    readonly HashSet<StepNode> movedNodesSinceMouseDown = new HashSet<StepNode>();
    // NOTE: We intentionally do NOT represent nested steps as GraphView nodes.
    // Nested steps are rendered as UI tiles inside the Group node to avoid z-order/picking issues in GraphView.
    Vector2 mouseWorld;
    bool _suppressNestedMoveWrites;

    // ---- Group tile layout (nested steps) ----
    // Two columns, compact tiles inside the Group node (UI tiles, not GraphView nodes).
    const float GroupTileW = 210f;
    const float GroupTileH = 96f;
    const int GroupTileColumns = 2;
    const float GroupTileGapX = 10f;
    const float GroupTileGapY = 10f;
    const float GroupTilesPadX = 14f;
    const float GroupTilesPadY = 12f;
    const float GroupHeaderH = 54f;     // title + ports row
    const float GroupSettingsApproxH = 150f; // base IMGUI group settings height (extra lines added dynamically)
    const float GroupSettingsCollapsedMinH = 70f; // keep header + some breathing room so it never becomes unclickable

    // Base node sizing (non-group)
    const float StepNodeWidth = 200f;
    const float StepNodeWidthExpanded = 260f;

    string _activeGuid;
    string _prevGuid;

    Color _edgeDefaultColor = new Color(0.7f, 0.7f, 0.7f);
    int _edgeDefaultWidth = 2;

    bool _isLoading;
    // GraphView can emit movedElements *after* we set _isLoading=false (layout pass).
    // During that short window we must not write graphPos back, otherwise Refresh can overwrite positions (often to 0,0).
    int _suppressGraphPosWritesFrames;
    bool _wasPlaying;
    bool _pendingFullRouteSync;
    bool _hasUserFramed; // if user clicked Frame All, we allow re-framing; otherwise preserve view on refresh

    // Persist GraphView pan/zoom across refresh/reload. Some Unity versions reset it on ClearGraph().
    [SerializeField] Vector3 _savedViewPos = Vector3.zero;
    [SerializeField] Vector3 _savedViewScale = Vector3.one;
    bool _hasSavedView;

    struct PendingNoteEdit
    {
        public string text;
        public double dueTime;
    }

    readonly Dictionary<string, PendingNoteEdit> _pendingNoteEdits = new Dictionary<string, PendingNoteEdit>();

    [MenuItem("Pi tech/Scenario Graph")]
    public static void OpenWindow() => GetWindow<ScenarioGraphWindow>("Scenario Graph");

    public static void Open(Scenario sc)
    {
        var w = GetWindow<ScenarioGraphWindow>("Scenario Graph");
        w.Load(sc);
    }

    static void Dirty(UnityEngine.Object o, string undo)
    {
        if (!o) return;
        Undo.RecordObject(o, undo);
        EditorUtility.SetDirty(o);
        if (o is Component c) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
    }

    void OnEnable()
    {
        // minimal toolbar (works across 2021/2022/2023)
        var bar = new VisualElement();
        bar.style.flexDirection = FlexDirection.Row;
        bar.style.paddingLeft = 6; bar.style.paddingTop = 4; bar.style.paddingBottom = 4;

        bar.Add(new UIEButton(() => { if (scenario) Load(scenario); }) { text = "Refresh" });

        var frame = new UIEButton(() =>
        {
            _hasUserFramed = true;
            view?.FrameAll();
        })
        { text = "Frame All" };
        frame.style.marginLeft = 6;
        bar.Add(frame);

        var rearrange = new UIEButton(() => AutoLayout())
        {
            text = "Rearrange"
        };
        rearrange.style.marginLeft = 6;
        bar.Add(rearrange);

        rootVisualElement.Add(bar);

        // If we lost the object reference across playmode/domain reload, try to resolve authoring scenario.
        if (!scenario) TryResolveAuthoringScenario();

        view = new ScenarioGraphView();
        view.OnContextAdd += ShowCreateMenu;
        view.OnMouseWorld += p => mouseWorld = p;
        view.OnEdgeDropped += ScheduleFullRouteSync;
        view.OnMouseUp += HandleMouseUp;
        view.OnMouseDown += () => movedNodesSinceMouseDown.Clear();
        view.OnViewTransformChanged += SaveViewTransform;
        rootVisualElement.Add(view);

        // NEW: start listening to playmode updates
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        _wasPlaying = Application.isPlaying;

        if (scenario != null) Load(scenario);
    }

    void OnDisable()
    {
        view?.RemoveFromHierarchy();
        nodes.Clear();

        // NEW: stop listening
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }


    // ---------- load graph from component ----------
    void Load(Scenario sc)
    {
        // Capture current view transform before ClearGraph (Unity may reset it).
        SaveViewTransform();

        scenario = sc;
        // Persist selection across playmode/domain reload.
        try
        {
            if (scenario)
            {
                authoringScenarioGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(scenario).ToString();
                authoringScenarioScenePath = scenario.gameObject.scene.path;
                authoringScenarioGameObjectName = scenario.gameObject.name;
            }
        }
        catch { /* best-effort */ }
        titleContent = new GUIContent(sc ? $"Scenario Graph • {sc.gameObject.name}" : "Scenario Graph");

        _isLoading = true; // begin guarded load
        _suppressGraphPosWritesFrames = 2; // cover current + next layout tick
        
        _activeGuid = null;
        _prevGuid = null;
        UpdateNodeHighlights(null, null);

        view.ClearGraph();
        nodes.Clear();
        notes.Clear();
        if (!scenario || scenario.steps == null)
        {
            _isLoading = false;
            return;
        }

        // ensure guids
        bool addedGuids = false;
        foreach (var s in scenario.steps)
        {
            if (s != null && string.IsNullOrEmpty(s.guid))
            {
                s.guid = Guid.NewGuid().ToString();
                addedGuids = true;
            }

            // ensure groups always have a list so container sizing works
            if (s is GroupStep g && g.steps == null)
            {
                g.steps = new List<Step>();
                addedGuids = true;
            }
        }

        if (addedGuids)
        {
            EditorUtility.SetDirty(scenario);
            if (scenario is Component c)
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        }

        // nodes (top-level only)
        for (int i = 0; i < scenario.steps.Count; i++)
        {
            var s = scenario.steps[i];
            if (s == null) continue;

            var node = new StepNode(
                this,
                scenario,
                s,
                i,
                view,
                () => { if (!_isLoading) ScheduleFullRouteSync(); },
                OnSkipRequested,
                DeleteStep
            );

            // IMPORTANT: do not treat (0,0) as "unset" (it's a valid graph position).
            // Only auto-place when the stored position is invalid (NaN/Infinity).
            var fallbackPos = new Vector2(80 + 340 * i, 220);
            var pos = IsValidGraphPos(s.graphPos) ? s.graphPos : fallbackPos;
            float width = StepNodeWidth;
            if (s is GroupStep gs)
            {
                int c = gs.steps != null ? gs.steps.Count : 0;
                width = GetGroupPreferredWidth(c);
            }
            var h = node.GetHeight();
            if (s is GroupStep) h = Mathf.Max(h, 320f); // always show as container, even when empty
            node.SetPositionSilent(new Rect(pos, new Vector2(width, h)));
            view.AddElement(node);
            nodes[s.guid] = node;
        }

        // Grow group containers to fit their nested nodes so children are visually "inside".
        foreach (var st in scenario.steps)
        {
            if (st is not GroupStep grp) continue;
            if (!nodes.TryGetValue(grp.guid, out var grpNode) || grpNode == null) continue;
            FitGroupToChildren(grp, grpNode);
        }



        // draw edges from persisted data
        foreach (var s in scenario.steps)
        {
            if (s is TimelineStep tl && !string.IsNullOrEmpty(tl.nextGuid) && nodes.TryGetValue(tl.guid, out var tlNode))
                Connect(tlNode.outNext, tl.nextGuid);

            if (s is CueCardsStep cc && !string.IsNullOrEmpty(cc.nextGuid) && nodes.TryGetValue(cc.guid, out var ccNode))
                Connect(ccNode.outNext, cc.nextGuid);

            if (s is QuestionStep q && q.choices != null && nodes.TryGetValue(q.guid, out var src))
            {
                for (int c = 0; c < q.choices.Count; c++)
                {
                    var next = q.choices[c]?.nextGuid;
                    if (!string.IsNullOrEmpty(next) && src.outChoices != null && c < src.outChoices.Count)
                        Connect(src.outChoices[c], next);
                }
            }
            if (s is InsertStep ins && !string.IsNullOrEmpty(ins.nextGuid) && nodes.TryGetValue(ins.guid, out var insNode))
                Connect(insNode.outNext, ins.nextGuid);
            
            if (s is EventStep ev && !string.IsNullOrEmpty(ev.nextGuid) && nodes.TryGetValue(ev.guid, out var evNode))
                Connect(evNode.outNext, ev.nextGuid);

            if (s is GroupStep grp && !string.IsNullOrEmpty(grp.nextGuid) && nodes.TryGetValue(grp.guid, out var grpNode))
                Connect(grpNode.outNext, grp.nextGuid);
        }
        // Selection edges (Correct/Wrong)
        foreach (var s in scenario.steps)
        {
            if (s is SelectionStep sel && nodes.TryGetValue(sel.guid, out var selNode))
            {
                if (!string.IsNullOrEmpty(sel.correctNextGuid)) Connect(selNode.outCorrect, sel.correctNextGuid);
                if (!string.IsNullOrEmpty(sel.wrongNextGuid)) Connect(selNode.outWrong, sel.wrongNextGuid);
            }
        }

        // --- Z-order pass (must be AFTER edges are created) ---
        // edges behind nodes (so they don't steal pointer events)
        foreach (var e in view.graphElements.ToList().OfType<Edge>())
            e.SendToBack();


        _isLoading = false; // end guarded load

        // Notes (editor-only)
#if UNITY_EDITOR
        if (scenario != null && scenario.GraphNotes != null)
        {
            foreach (var n in scenario.GraphNotes)
                AddOrUpdateNoteElement(n);
        }
#endif

        // Restore view transform (pan/zoom) after graph rebuild.
        RestoreViewTransform();

        view.graphViewChanged = change =>
        {
            // During Load() we programmatically add/move elements; GraphView can report movedElements.
            // Never write back graphPos/Undo during loading, otherwise Refresh can overwrite saved positions (often to 0,0).
            if (_isLoading || _suppressGraphPosWritesFrames > 0) return change;

            // positions
            if (change.movedElements != null)
            {
                foreach (var el in change.movedElements)
                {
                    if (el is not StepNode sn) continue;

                    sn.step.graphPos = sn.GetPosition().position;
                    Dirty(scenario, "Move Node");

                    movedNodesSinceMouseDown.Add(sn);
                }
            }

            // edges created/removed
            if (!_isLoading && scenario != null)
            {
                bool routesChanged = false;

                if (change.elementsToRemove != null)
                    routesChanged |= ApplyRouteRemovals(change.elementsToRemove);

                if (change.edgesToCreate != null)
                    routesChanged |= ApplyRouteCreates(change.edgesToCreate);

                if (routesChanged)
                    Dirty(scenario, "Route Change");
            }

            return change;
        };
    }

    void HandleMouseUp()
    {
        if (_isLoading || scenario == null) { movedNodesSinceMouseDown.Clear(); return; }
        // Fallback: GraphView doesn't always report movedElements, so use current selection if needed.
        if (movedNodesSinceMouseDown.Count == 0)
        {
            if (view != null)
            {
                foreach (var sel in view.selection)
                    if (sel is StepNode sn) movedNodesSinceMouseDown.Add(sn);
            }
        }

        if (movedNodesSinceMouseDown.Count == 0) return;

        // If the user dragged one or more nodes over a GroupStep container node, move those steps into the group.
        var groupTargets = nodes.Values.Where(n => n != null && !n.IsNested && n.step is GroupStep).ToList();
        if (groupTargets.Count == 0) { movedNodesSinceMouseDown.Clear(); return; }

        bool changed = false;
        var removeFromView = new List<GraphElement>();

        foreach (var moved in movedNodesSinceMouseDown.ToList())
        {
            if (moved == null || moved.step == null) continue;
            if (moved.step is GroupStep) continue; // don't drag groups into groups (yet)

            var movedRect = moved.GetPosition();
            var center = movedRect.center;

            // Top-level steps can be moved into a group.
            int topIndex = scenario.steps.IndexOf(moved.step);
            if (topIndex < 0) continue;

            StepNode target = null;
            foreach (var gnode in groupTargets)
            {
                if (gnode == null) continue;
                var grect = gnode.GetPosition();
                // forgiving: any overlap counts, not just center
                if (grect.Overlaps(movedRect))
                {
                    target = gnode;
                    break;
                }
            }

            if (target?.step is not GroupStep grp) continue;

            Dirty(scenario, "Move Step Into Group");
            scenario.steps.RemoveAt(topIndex);
            grp.steps ??= new List<Step>();

            // IMPORTANT: if anything was routed into this step, re-route it into the group instead.
            RedirectIncomingRoutes(moved.step.guid, grp.guid);

            // If the group has no Next yet and this step was a linear step, adopt its next as the group's next.
            if (string.IsNullOrEmpty(grp.nextGuid))
            {
                var adopted = TryGetLinearNextGuid(moved.step);
                if (!string.IsNullOrEmpty(adopted))
                    grp.nextGuid = adopted;
            }

            // UX: always append in order. Tiles represent ordering clearly.
            grp.steps.Add(moved.step);
            changed = true;

            // IMPORTANT: visually remove the old top-level node immediately to avoid "stale" nodes
            // hanging around until the delayed Load() happens.
            removeFromView.Add(moved);
        }

        movedNodesSinceMouseDown.Clear();

        if (changed)
        {
            // Remove moved nodes (and their attached edges) immediately for clean UX.
            if (view != null && removeFromView.Count > 0)
            {
                // Remove edges connected to nodes we're removing
                var toRemove = new HashSet<GraphElement>(removeFromView);
                foreach (var e in view.graphElements.ToList().OfType<Edge>())
                {
                    if (e?.input?.node is StepNode inNode && toRemove.Contains(inNode)) { view.RemoveElement(e); continue; }
                    if (e?.output?.node is StepNode outNode && toRemove.Contains(outNode)) { view.RemoveElement(e); continue; }
                }

                foreach (var ge in removeFromView)
                    if (ge != null) view.RemoveElement(ge);
            }

            // Rebuild graph from serialized truth AFTER GraphView finishes its drag cycle.
            // (Immediate rebuild can leave SelectionDragger in a bad state and cause weird scrolling.)
            view?.ClearSelection();
            EditorApplication.delayCall += () =>
            {
                if (this != null && scenario != null)
                    Load(scenario);
            };
        }
    }

    static string TryGetLinearNextGuid(Step s)
    {
        if (s is TimelineStep tl) return tl.nextGuid;
        if (s is CueCardsStep cc) return cc.nextGuid;
        if (s is InsertStep ins) return ins.nextGuid;
        if (s is EventStep ev) return ev.nextGuid;
        return null;
    }

    void RedirectIncomingRoutes(string fromGuid, string toGuid)
    {
        if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid) || scenario == null) return;
        if (scenario.steps == null) return;

        foreach (var st in scenario.steps)
        {
            if (st == null) continue;

            if (st is TimelineStep tl && tl.nextGuid == fromGuid) tl.nextGuid = toGuid;
            else if (st is CueCardsStep cc && cc.nextGuid == fromGuid) cc.nextGuid = toGuid;
            else if (st is InsertStep ins && ins.nextGuid == fromGuid) ins.nextGuid = toGuid;
            else if (st is EventStep ev && ev.nextGuid == fromGuid) ev.nextGuid = toGuid;
            else if (st is GroupStep g && g.nextGuid == fromGuid) g.nextGuid = toGuid;
            else if (st is QuestionStep q && q.choices != null)
            {
                foreach (var ch in q.choices)
                    if (ch != null && ch.nextGuid == fromGuid)
                        ch.nextGuid = toGuid;
            }
            else if (st is SelectionStep sel)
            {
                if (sel.correctNextGuid == fromGuid) sel.correctNextGuid = toGuid;
                if (sel.wrongNextGuid == fromGuid) sel.wrongNextGuid = toGuid;
            }
        }
    }

    void FitGroupToChildren(GroupStep grp, StepNode grpNode)
    {
        if (grp == null || grpNode == null) return;

        int count = grp.steps != null ? grp.steps.Count : 0;
        int rows = Mathf.CeilToInt(count / (float)GroupTileColumns);

        float tilesH = count == 0
            ? 64f
            : (rows * GroupTileH) + Mathf.Max(0, rows - 1) * GroupTileGapY + GroupTilesPadY;

        float reqW = GetGroupPreferredWidth(count);
        // Make sure the "Nested Steps" preview lines are never clipped.
        // Base + per-line (up to 8 shown) + small buffer.
        float expandedSettingsH = GroupSettingsApproxH + Mathf.Min(8, count) * 18f + 24f;
        float settingsH = grpNode.GroupSettingsExpanded ? expandedSettingsH : GroupSettingsCollapsedMinH;
        float reqH = Mathf.Max(260f, GroupHeaderH + tilesH + settingsH);

        // IMPORTANT: always anchor to serialized graphPos.
        // GraphView can temporarily report a rect at (0,0) during Refresh/layout which would "snap" the group.
        var newRect = new Rect(grp.graphPos, new Vector2(reqW, reqH));
        grpNode.SetPositionSilent(newRect);
    }

    static float GetGroupPreferredWidth(int tileCount)
    {
        // 0-1 tiles => 1 col, 2+ tiles => 2 cols
        int cols = tileCount <= 1 ? 1 : GroupTileColumns;
        float contentW = (cols * GroupTileW) + ((cols - 1) * GroupTileGapX);
        // padding on both sides + a small buffer for borders/ports
        float w = contentW + GroupTilesPadX * 2 + 24f;
        return Mathf.Max(420f, w);
    }

    void ScheduleResizeGroup(string groupGuid)
    {
        if (string.IsNullOrEmpty(groupGuid)) return;
        EditorApplication.delayCall += () =>
        {
            if (this == null || scenario == null) return;
            var grp = scenario.steps.OfType<GroupStep>().FirstOrDefault(g => g != null && g.guid == groupGuid);
            if (grp == null) return;
            if (!nodes.TryGetValue(groupGuid, out var node) || node == null) return;
            FitGroupToChildren(grp, node);
        };
    }

    void ScheduleFullRouteSync()
    {
        if (_pendingFullRouteSync) return;
        _pendingFullRouteSync = true;

        // Defer one tick so GraphView has actually updated/remapped ports/edges.
        EditorApplication.delayCall += () =>
        {
            _pendingFullRouteSync = false;
            if (this == null || view == null) return;
            SyncRoutesFromGraph();
        };
    }

    void SyncRoutesFromGraph()
    {
        if (!scenario || _isLoading) return;

        // Compute desired routing from current edges; then apply.
        // (We avoid clearing everything up-front during an in-flight GraphView change.)

        // Default everything to "linear next" (empty string)
        foreach (var st in scenario.steps)
        {
            if (st is TimelineStep tl) tl.nextGuid = "";
            else if (st is CueCardsStep cc) cc.nextGuid = "";
            else if (st is InsertStep ins) ins.nextGuid = "";
            else if (st is EventStep ev) ev.nextGuid = "";
            else if (st is GroupStep g) g.nextGuid = "";
            else if (st is QuestionStep q && q.choices != null)
            {
                foreach (var ch in q.choices)
                    if (ch != null) ch.nextGuid = "";
            }

            if (st is SelectionStep sl)
            {
                sl.correctNextGuid = "";
                sl.wrongNextGuid = "";
            }
        }

        // Apply from edges that exist in the view
        foreach (var e in view.graphElements.ToList().OfType<Edge>())
            ApplyRouteCreate(e);

        Dirty(scenario, "Route Change");
    }

    bool ApplyRouteCreates(IEnumerable<Edge> edgesToCreate)
    {
        bool changed = false;
        foreach (var e in edgesToCreate)
            changed |= ApplyRouteCreate(e);
        return changed;
    }

    bool ApplyRouteCreate(Edge e)
    {
        if (e == null) return false;
        var outMeta = PortMeta.From(e.output);
        var inNode = e.input?.node as StepNode;
        if (outMeta == null || inNode?.step == null) return false;
        if (string.IsNullOrEmpty(inNode.step.guid)) return false;

        string dstGuid = inNode.step.guid;
        bool changed = false;

        if (outMeta.owner is TimelineStep otl)
        {
            if (otl.nextGuid != dstGuid) { otl.nextGuid = dstGuid; changed = true; }
        }
        else if (outMeta.owner is CueCardsStep occ)
        {
            if (occ.nextGuid != dstGuid) { occ.nextGuid = dstGuid; changed = true; }
        }
        else if (outMeta.owner is InsertStep oins)
        {
            if (oins.nextGuid != dstGuid) { oins.nextGuid = dstGuid; changed = true; }
        }
        else if (outMeta.owner is EventStep oev)
        {
            if (oev.nextGuid != dstGuid) { oev.nextGuid = dstGuid; changed = true; }
        }
        else if (outMeta.owner is GroupStep og)
        {
            if (og.nextGuid != dstGuid) { og.nextGuid = dstGuid; changed = true; }
        }
        else if (outMeta.owner is QuestionStep oq &&
                 outMeta.choiceIndex >= 0 &&
                 oq.choices != null &&
                 outMeta.choiceIndex < oq.choices.Count &&
                 oq.choices[outMeta.choiceIndex] != null)
        {
            var ch = oq.choices[outMeta.choiceIndex];
            if (ch.nextGuid != dstGuid) { ch.nextGuid = dstGuid; changed = true; }
        }
        else if (outMeta.owner is SelectionStep osl)
        {
            // -2 => Correct, -3 => Wrong
            if (outMeta.choiceIndex == -2)
            {
                if (osl.correctNextGuid != dstGuid) { osl.correctNextGuid = dstGuid; changed = true; }
            }
            else if (outMeta.choiceIndex == -3)
            {
                if (osl.wrongNextGuid != dstGuid) { osl.wrongNextGuid = dstGuid; changed = true; }
            }
        }

        return changed;
    }

    bool ApplyRouteRemovals(IEnumerable<GraphElement> removed)
    {
        bool changed = false;

        foreach (var el in removed)
        {
            if (el is not Edge e) continue;

            var outMeta = PortMeta.From(e.output);
            if (outMeta == null) continue;

            // Output ports are Capacity.Single in this graph, so removal means "clear that route".
            if (outMeta.owner is TimelineStep otl)
            {
                if (!string.IsNullOrEmpty(otl.nextGuid)) { otl.nextGuid = ""; changed = true; }
            }
            else if (outMeta.owner is CueCardsStep occ)
            {
                if (!string.IsNullOrEmpty(occ.nextGuid)) { occ.nextGuid = ""; changed = true; }
            }
            else if (outMeta.owner is InsertStep oins)
            {
                if (!string.IsNullOrEmpty(oins.nextGuid)) { oins.nextGuid = ""; changed = true; }
            }
            else if (outMeta.owner is EventStep oev)
            {
                if (!string.IsNullOrEmpty(oev.nextGuid)) { oev.nextGuid = ""; changed = true; }
            }
            else if (outMeta.owner is GroupStep og)
            {
                if (!string.IsNullOrEmpty(og.nextGuid)) { og.nextGuid = ""; changed = true; }
            }
            else if (outMeta.owner is QuestionStep oq &&
                     outMeta.choiceIndex >= 0 &&
                     oq.choices != null &&
                     outMeta.choiceIndex < oq.choices.Count &&
                     oq.choices[outMeta.choiceIndex] != null)
            {
                var ch = oq.choices[outMeta.choiceIndex];
                if (!string.IsNullOrEmpty(ch.nextGuid)) { ch.nextGuid = ""; changed = true; }
            }
            else if (outMeta.owner is SelectionStep osl)
            {
                if (outMeta.choiceIndex == -2)
                {
                    if (!string.IsNullOrEmpty(osl.correctNextGuid)) { osl.correctNextGuid = ""; changed = true; }
                }
                else if (outMeta.choiceIndex == -3)
                {
                    if (!string.IsNullOrEmpty(osl.wrongNextGuid)) { osl.wrongNextGuid = ""; changed = true; }
                }
            }
        }

        return changed;
    }

    void AutoLayout()
    {
        if (scenario == null || scenario.steps == null || nodes.Count == 0)
            return;

        // Build incoming edge count and adjacency
        var incoming = new Dictionary<string, int>();
        var neighbors = new Dictionary<string, List<string>>();

        foreach (var kv in nodes)
        {
            incoming[kv.Key] = 0;
            neighbors[kv.Key] = new List<string>();
        }

        void AddEdge(string fromGuid, string toGuid)
        {
            if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid))
                return;
            if (!neighbors.ContainsKey(fromGuid) || !incoming.ContainsKey(toGuid))
                return;

            neighbors[fromGuid].Add(toGuid);
            incoming[toGuid] += 1;
        }

        foreach (var st in scenario.steps)
        {
            if (st == null || string.IsNullOrEmpty(st.guid)) continue;

            string from = st.guid;

            if (st is TimelineStep tl && !string.IsNullOrEmpty(tl.nextGuid))
                AddEdge(from, tl.nextGuid);
            else if (st is CueCardsStep cc && !string.IsNullOrEmpty(cc.nextGuid))
                AddEdge(from, cc.nextGuid);
            else if (st is InsertStep ins && !string.IsNullOrEmpty(ins.nextGuid))
                AddEdge(from, ins.nextGuid);
            else if (st is EventStep ev && !string.IsNullOrEmpty(ev.nextGuid))
                AddEdge(from, ev.nextGuid);
            else if (st is GroupStep g && !string.IsNullOrEmpty(g.nextGuid))
                AddEdge(from, g.nextGuid);

            if (st is QuestionStep q && q.choices != null)
            {
                foreach (var ch in q.choices)
                    if (ch != null && !string.IsNullOrEmpty(ch.nextGuid))
                        AddEdge(from, ch.nextGuid);
            }

            if (st is SelectionStep sel)
            {
                if (!string.IsNullOrEmpty(sel.correctNextGuid))
                    AddEdge(from, sel.correctNextGuid);
                if (!string.IsNullOrEmpty(sel.wrongNextGuid))
                    AddEdge(from, sel.wrongNextGuid);
            }
        }

        // Roots = no incoming. If none, start from first step
        var roots = incoming.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList();
        if (roots.Count == 0 && scenario.steps.Count > 0 && !string.IsNullOrEmpty(scenario.steps[0].guid))
            roots.Add(scenario.steps[0].guid);

        var level = new Dictionary<string, int>();
        var queue = new Queue<string>();

        foreach (var r in roots)
        {
            level[r] = 0;
            queue.Enqueue(r);
        }

        // BFS: assign each node a level ONCE → no infinite loop in cycles
        while (queue.Count > 0)
        {
            var g = queue.Dequeue();
            int l = level[g];

            if (!neighbors.TryGetValue(g, out var neighList)) continue;

            foreach (var n in neighList)
            {
                if (level.ContainsKey(n))   // already visited, do not enqueue again
                    continue;

                level[n] = l + 1;
                queue.Enqueue(n);
            }
        }

        // Nodes not reached from any root (disconnected or pure cycles)
        int maxLevel = level.Count > 0 ? level.Values.Max() : 0;
        foreach (var kv in nodes)
        {
            if (!level.ContainsKey(kv.Key))
                level[kv.Key] = maxLevel + 1;
        }

        // Group by level
        var levels = level
            .GroupBy(kv => kv.Value)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Select(k => k.Key).ToList());

        // Layout constants (dynamic spacing based on node sizes)
        const float xStart = 80f;
        const float yStart = 120f;
        const float xGap = 70f;
        const float yGap = 70f;

        // Precompute desired size per node (Group nodes are wider)
        var sizeByGuid = new Dictionary<string, Vector2>();
        foreach (var kv in nodes)
        {
            var guid = kv.Key;
            var node = kv.Value;
            if (node == null || node.step == null) continue;

            float w = StepNodeWidth;
            float h = node.GetHeight();

            if (node.step is GroupStep gs)
            {
                int c = gs.steps != null ? gs.steps.Count : 0;
                w = GetGroupPreferredWidth(c);
                // ensure the rect accounts for the UI tiles/settings state
                FitGroupToChildren(gs, node);
                h = Mathf.Max(h, node.GetPosition().height);
            }

            sizeByGuid[guid] = new Vector2(w, h);
        }

        // Compute x offsets per level based on widest node in each column
        var levelKeys = levels.Keys.OrderBy(k => k).ToList();
        var xByLevel = new Dictionary<int, float>();
        float curX = xStart;
        foreach (var lvKey in levelKeys)
        {
            xByLevel[lvKey] = curX;
            float colW = 360f;
            foreach (var guid in levels[lvKey])
                if (sizeByGuid.TryGetValue(guid, out var sz))
                    colW = Mathf.Max(colW, sz.x);
            curX += colW + xGap;
        }

        // Apply positions (stack within each level using actual heights)
        _isLoading = true;
        Dirty(scenario, "Rearrange Graph");

        foreach (var lv in levelKeys)
        {
            var guidsAtLevel = levels[lv]
                .OrderBy(guid => scenario.steps.FindIndex(s => s != null && s.guid == guid))
                .ToList();

            float y = yStart;
            float x = xByLevel[lv];

            foreach (var guid in guidsAtLevel)
            {
                if (!nodes.TryGetValue(guid, out var node) || node == null) continue;
                if (!sizeByGuid.TryGetValue(guid, out var sz)) sz = new Vector2(StepNodeWidth, node.GetHeight());

                node.SetPositionSilent(new Rect(new Vector2(x, y), sz));
                node.step.graphPos = new Vector2(x, y);

                y += sz.y + yGap;
            }
        }

        _isLoading = false;
        EditorUtility.SetDirty(scenario);
        EditorSceneManager.MarkSceneDirty(scenario.gameObject.scene);

        view?.FrameAll();
    }


    void OnSkipRequested(Step step, int branchIndex)
    {
        if (!Application.isPlaying) return;

        var mgr = UnityEngine.Object
            .FindObjectsOfType<Pitech.XR.Scenario.SceneManager>()
            .FirstOrDefault(m => m && m.scenario == scenario)
            ?? UnityEngine.Object.FindObjectsOfType<Pitech.XR.Scenario.SceneManager>().FirstOrDefault();

        if (!mgr) return;

        mgr.EditorSkipFromGraph(step.guid, branchIndex);
    }



    // Back-compat hook: some node UI actions request a full rebuild (e.g. recreating ports).
    // We now defer and do a safe full sync.
    void RebuildLinksFromGraph() => ScheduleFullRouteSync();

    void DeleteStep(Step step)
    {
        if (!scenario || scenario.steps == null || step == null)
            return;

        // Find the exact object in the list (same instance we built the node from)
        int index = scenario.steps.IndexOf(step);
        if (index < 0)
        {
            Debug.LogWarning($"[ScenarioGraph] DeleteStep: step not found in list (guid={step.guid})");
            return;
        }

        var s = scenario.steps[index];

        if (!EditorUtility.DisplayDialog(
                "Delete Step",
                $"Delete “{s.Kind}” step ({index:00})?",
                "Delete",
                "Cancel"))
            return;

        // This is the ONLY place we mutate the list: same pattern as CreateStep.
        Undo.RecordObject(scenario, "Delete Step");

        scenario.steps.RemoveAt(index);

        // Optional: clear any dangling routing that pointed to this guid
        string removedGuid = s.guid;
        foreach (var st in scenario.steps)
        {
            if (st is TimelineStep tl && tl.nextGuid == removedGuid) tl.nextGuid = "";
            else if (st is CueCardsStep cc && cc.nextGuid == removedGuid) cc.nextGuid = "";
            else if (st is InsertStep ins && ins.nextGuid == removedGuid) ins.nextGuid = "";
            else if (st is EventStep ev && ev.nextGuid == removedGuid) ev.nextGuid = "";
            else if (st is QuestionStep q && q.choices != null)
            {
                foreach (var ch in q.choices)
                    if (ch != null && ch.nextGuid == removedGuid)
                        ch.nextGuid = "";
            }
            else if (st is SelectionStep sel)
            {
                if (sel.correctNextGuid == removedGuid) sel.correctNextGuid = "";
                if (sel.wrongNextGuid == removedGuid) sel.wrongNextGuid = "";
            }
        }

        EditorUtility.SetDirty(scenario);
        EditorSceneManager.MarkSceneDirty(scenario.gameObject.scene);

        // Rebuild graph from *actual* serialized state
        Load(scenario);
    }

    void Connect(Port src, string dstGuid)
    {
        if (src == null) return;
        if (!nodes.TryGetValue(dstGuid, out var dstNode)) return;
        if (dstNode == null) return;
        if (dstNode.inPort == null) return; // nested steps do not accept routing

        var edge = new FlowEdge
        {
            output = src,
            input = dstNode.inPort
        };

        if (edge.output == null || edge.input == null) return;
        edge.output.Connect(edge);
        edge.input.Connect(edge);

        view.AddElement(edge);
    }



    void OnEditorUpdate()
    {
        if (_suppressGraphPosWritesFrames > 0) _suppressGraphPosWritesFrames--;
        CommitPendingNoteEdits();
        SyncNoteContentsFallback();
        // If we lost the object reference across reloads, try to resolve authoring scenario.
        if (!scenario) TryResolveAuthoringScenario();

        // detect transitions
        if (Application.isPlaying && !_wasPlaying)
        {
            // just entered Play
            _wasPlaying = true;
        }
        else if (!Application.isPlaying && _wasPlaying)
        {
            // just exited Play
            _wasPlaying = false;
        }

        // --- your existing code below ---
        // Only highlight during play
        if (!Application.isPlaying)
        {
            UpdateNodeHighlights(null, null);
            return;
        }

        // Find any SceneManager in the scene
        var managers = UnityEngine.Object.FindObjectsOfType<Pitech.XR.Scenario.SceneManager>();
        if (managers == null || managers.Length == 0)
        {
            UpdateNodeHighlights(null, null);
            return;
        }

        // Prefer one whose scenario matches ours, otherwise just take the first
        var mgr = managers.FirstOrDefault(m => m && m.scenario == scenario)
                  ?? managers.FirstOrDefault(m => m && m.scenario != null);

        if (!mgr || mgr.scenario == null || mgr.scenario.steps == null)
        {
            UpdateNodeHighlights(null, null);
            return;
        }

        // IMPORTANT: do NOT swap the window to runtime scenario during Play.
        // We highlight by GUID, using the authoring graph as the visual source of truth.
        var sc = mgr.scenario;

        int idx = mgr.StepIndex;
        if (idx < 0 || idx >= sc.steps.Count)
        {
            UpdateNodeHighlights(null, null);
            return;
        }

        var curStep = sc.steps[idx];
        if (curStep == null || string.IsNullOrEmpty(curStep.guid))
        {
            UpdateNodeHighlights(null, null);
            return;
        }

        string curGuid = curStep.guid;

        // Drive the active / previous flow
        if (curGuid != _activeGuid)
        {
            _prevGuid = _activeGuid;
            _activeGuid = curGuid;
            UpdateNodeHighlights(_activeGuid, _prevGuid);
        }
    }

    static bool IsValidGraphPos(Vector2 p)
    {
        return !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y));
    }

    void CommitPendingNoteEdits()
    {
        if (scenario == null || scenario.GraphNotes == null || _pendingNoteEdits.Count == 0) return;

        double now = EditorApplication.timeSinceStartup;
        List<string> ready = null;
        foreach (var kv in _pendingNoteEdits)
        {
            if (kv.Value.dueTime <= now)
            {
                ready ??= new List<string>();
                ready.Add(kv.Key);
            }
        }

        if (ready == null) return;

        foreach (var guid in ready)
        {
            if (!_pendingNoteEdits.TryGetValue(guid, out var pending)) continue;
            _pendingNoteEdits.Remove(guid);

            var data = scenario.GraphNotes.FirstOrDefault(x => x != null && x.guid == guid);
            if (data == null) continue;

            if (data.text != pending.text)
            {
                data.text = pending.text;
                Dirty(scenario, "Edit Note");
            }
        }
    }

    // Extra safety: some StickyNote edits don't fire FocusOut/ValueChanged reliably in GraphView.
    // Poll the note contents and persist if changed (throttled by the debounce map above).
    void SyncNoteContentsFallback()
    {
        if (_isLoading || scenario == null || scenario.GraphNotes == null) return;
        if (notes == null || notes.Count == 0) return;

        double now = EditorApplication.timeSinceStartup;
        foreach (var kv in notes)
        {
            var guid = kv.Key;
            var note = kv.Value;
            if (note == null || string.IsNullOrEmpty(guid)) continue;

            var data = scenario.GraphNotes.FirstOrDefault(x => x != null && x.guid == guid);
            if (data == null) continue;

            // If the UI shows different text and nothing is queued, queue it.
            string uiText = note.contents ?? "";
            if (data.text != uiText && !_pendingNoteEdits.ContainsKey(guid))
            {
                _pendingNoteEdits[guid] = new PendingNoteEdit
                {
                    text = uiText,
                    dueTime = now + 0.25
                };
            }
        }
    }

    void SaveViewTransform()
    {
        if (view == null) return;
        try
        {
            _savedViewPos = view.contentViewContainer.transform.position;
            _savedViewScale = view.contentViewContainer.transform.scale;
            _hasSavedView = true;
        }
        catch { }
    }

    void RestoreViewTransform()
    {
        if (!_hasSavedView || view == null) return;
        try
        {
            // GraphView provides UpdateViewTransform for correct pan/zoom restoration.
            view.UpdateViewTransform(_savedViewPos, _savedViewScale);
        }
        catch
        {
            // Fallback if UpdateViewTransform is unavailable for some reason
            try
            {
                view.contentViewContainer.transform.position = _savedViewPos;
                view.contentViewContainer.transform.scale = _savedViewScale;
            }
            catch { }
        }
    }

    void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingPlayMode)
        {
            // After Play -> Stop, Unity may restore the scene and invalidate references.
            // Re-resolve authoring scenario and reload the graph.
            TryResolveAuthoringScenario();
            if (scenario != null)
                Load(scenario);
        }
    }

    void TryResolveAuthoringScenario()
    {
        if (scenario) return;

        // 1) Try GlobalObjectId (best case)
        if (!string.IsNullOrEmpty(authoringScenarioGlobalId))
        {
            try
            {
                if (GlobalObjectId.TryParse(authoringScenarioGlobalId, out var gid))
                {
                    var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                    if (obj is Scenario sc)
                    {
                        scenario = sc;
                        return;
                    }
                }
            }
            catch { }
        }

        // 2) Fallback: search loaded scenes for matching name + scene path
        try
        {
            var all = UnityEngine.Object.FindObjectsOfType<Scenario>(true);
            foreach (var sc in all)
            {
                if (!sc) continue;
                if (EditorUtility.IsPersistent(sc)) continue; // skip prefabs/assets
                if (!string.IsNullOrEmpty(authoringScenarioScenePath) && sc.gameObject.scene.path != authoringScenarioScenePath) continue;
                if (!string.IsNullOrEmpty(authoringScenarioGameObjectName) && sc.gameObject.name != authoringScenarioGameObjectName) continue;
                scenario = sc;
                return;
            }
        }
        catch { }
    }


    void UpdateNodeHighlights(string activeGuid, string prevGuid)
    {
        // Nodes
        foreach (var kv in nodes)
        {
            bool isActive = kv.Key == activeGuid;
            kv.Value.SetActiveHighlight(isActive);
        }

        // Edges
        if (view == null) return;

        var edges = view.graphElements.ToList().OfType<Edge>().ToList();
        foreach (var e in edges)
        {
            var fromNode = e.output?.node as StepNode;
            var toNode = e.input?.node as StepNode;

            if (fromNode == null || toNode == null)
                continue;

            bool isTransitionEdge =
                !string.IsNullOrEmpty(activeGuid) &&
                !string.IsNullOrEmpty(prevGuid) &&
                fromNode.step != null && toNode.step != null &&
                fromNode.step.guid == prevGuid &&
                toNode.step.guid == activeGuid;

            var ec = e.edgeControl;
            if (ec == null)
                continue;

            Color activeColor = new Color(0.3f, 0.7f, 1f); // μπλε-κυανό

            if (isTransitionEdge)
            {
                ec.inputColor = activeColor;
                ec.outputColor = activeColor;
                ec.edgeWidth = 3;

                // kick off the moving highlight
                if (e is FlowEdge fe)
                    fe.PlayFlow();
            }
            else
            {
                ec.inputColor = _edgeDefaultColor;
                ec.outputColor = _edgeDefaultColor;
                ec.edgeWidth = _edgeDefaultWidth;
            }

            e.MarkDirtyRepaint();
        }
    }


    // ---------- context menu ----------
    void ShowCreateMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Add/Timeline", _ => CreateStep(typeof(TimelineStep)));
        evt.menu.AppendAction("Add/Cue Cards", _ => CreateStep(typeof(CueCardsStep)));
        evt.menu.AppendAction("Add/Question", _ => CreateStep(typeof(QuestionStep)));
        evt.menu.AppendAction("Add/Selection", _ => CreateStep(typeof(SelectionStep)));
        evt.menu.AppendAction("Add/Insert", _ => CreateStep(typeof(InsertStep)));
        evt.menu.AppendAction("Add/Event", _ => CreateStep(typeof(EventStep)));
        evt.menu.AppendAction("Add/Group", _ => CreateStep(typeof(GroupStep)));
        evt.menu.AppendSeparator();
        evt.menu.AppendAction("Add/Note", _ => CreateNote());
    }

    void CreateNote()
    {
#if UNITY_EDITOR
        if (!scenario) return;

        Dirty(scenario, "Add Note");

        var n = new Scenario.GraphNote
        {
            guid = Guid.NewGuid().ToString(),
            rect = new Rect(mouseWorld, new Vector2(260, 170)),
            text = "Note…"
        };
        scenario.GraphNotes.Add(n);
        AddOrUpdateNoteElement(n);
#endif
    }

#if UNITY_EDITOR
    void AddOrUpdateNoteElement(Scenario.GraphNote n)
    {
        if (n == null || string.IsNullOrEmpty(n.guid) || view == null) return;

        if (!notes.TryGetValue(n.guid, out var note) || note == null)
        {
            note = new StickyNote
            {
                title = "NOTE",
                contents = n.text
            };
            note.SetPosition(n.rect);
            note.userData = n.guid;
            view.AddElement(note);
            notes[n.guid] = note;

            // Styling: smaller contents font + tiny NOTE label top-right.
            try
            {
                var titleLabel = note.Q<Label>("title");
                if (titleLabel != null)
                {
                    titleLabel.style.fontSize = 9;
                    titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    // subtle label, not shouting
                    titleLabel.style.color = new Color(0f, 0f, 0f, 0.35f);
                    titleLabel.style.unityTextAlign = TextAnchor.UpperRight;
                }

            var tf = note.Q<TextField>();
                if (tf != null)
                {
                    tf.style.fontSize = 10;
                    tf.style.color = new Color(0f, 0f, 0f, 0.85f);

                // Save note text reliably (debounced), not only on focus-out.
                tf.RegisterValueChangedCallback(e =>
                {
                    if (_isLoading || scenario == null) return;
                    if (note.userData is not string ng || string.IsNullOrEmpty(ng)) return;
                    _pendingNoteEdits[ng] = new PendingNoteEdit
                    {
                        text = e.newValue ?? "",
                        dueTime = EditorApplication.timeSinceStartup + 0.25
                    };
                });
                }
            }
            catch { /* best-effort UI styling; Unity versions vary */ }

            note.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                // Keep rect in sync
                if (_isLoading) return;
                var guid = note.userData as string;
                var data = scenario.GraphNotes.FirstOrDefault(x => x.guid == guid);
                if (data == null) return;
                data.rect = note.GetPosition();
                Dirty(scenario, "Move Note");
            });

            // Text edit callback (StickyNote doesn't expose a direct event, so poll on focus-out)
            note.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (_isLoading) return;
                var guid = note.userData as string;
                var data = scenario.GraphNotes.FirstOrDefault(x => x.guid == guid);
                if (data == null) return;
                if (data.text != note.contents)
                {
                    data.text = note.contents;
                    Dirty(scenario, "Edit Note");
                }
            });
        }
        else
        {
            note.contents = n.text;
            note.SetPosition(n.rect);
        }
    }

    void DeleteNoteByGuid(string guid)
    {
        if (!scenario || scenario.GraphNotes == null || string.IsNullOrEmpty(guid)) return;

        int idx = scenario.GraphNotes.FindIndex(x => x != null && x.guid == guid);
        if (idx < 0) return;

        Dirty(scenario, "Delete Note");
        scenario.GraphNotes.RemoveAt(idx);

        if (notes.TryGetValue(guid, out var note) && note != null)
            view.RemoveElement(note);
        notes.Remove(guid);
    }
#endif

    void CreateStep(Type t)
    {
        var inst = (Step)Activator.CreateInstance(t);
        inst.guid = Guid.NewGuid().ToString();
        inst.graphPos = mouseWorld;

        Dirty(scenario, "Add Step");
        scenario.steps.Add(inst);
        Load(scenario);
    }

    // ================= GraphView =================
    class ScenarioGraphView : GraphView
    {
        public Action<ContextualMenuPopulateEvent> OnContextAdd;
        public Action<Vector2> OnMouseWorld;
        public Action OnEdgeDropped;
        public Action OnMouseUp;
        public Action OnMouseDown;
        public Action OnViewTransformChanged;

        public ScenarioGraphView()
        {
            style.flexGrow = 1;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground() { name = "grid" });
            this.Q("grid")?.StretchToParentSize();
            RegisterCallback<MouseMoveEvent>(e =>
            {
                var p = contentViewContainer.WorldToLocal(e.mousePosition);
                OnMouseWorld?.Invoke(p);
            });

            // Important: use TrickleDown so we still get the event even if GraphView handles it.
            RegisterCallback<MouseDownEvent>(_ => OnMouseDown?.Invoke(), TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(_ => OnMouseUp?.Invoke(), TrickleDown.TrickleDown);

            // Fire when user pans/zooms so we can persist view transform.
            RegisterCallback<WheelEvent>(_ => OnViewTransformChanged?.Invoke(), TrickleDown.TrickleDown);
            RegisterCallback<MouseMoveEvent>(_ => OnViewTransformChanged?.Invoke(), TrickleDown.TrickleDown);
        }

        public void ClearGraph()
        {
            foreach (var ge in graphElements.ToList())
                RemoveElement(ge);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var results = new List<Port>();
            ports.ForEach(p =>
            {
                if (p == startPort) return;
                if (p.node == startPort.node) return;
                if (p.direction == startPort.direction) return;
                if (p.portType != startPort.portType) return;
                results.Add(p);
            });
            return results;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // Kill ALL default items
            evt.menu.ClearItems();

            // Right-click on a node → let StepNode decide
            if (evt.target is StepNode node)
            {
                node.BuildContextualMenu(evt);
                return;
            }

            // Right-click on a note → allow delete
#if UNITY_EDITOR
            if (evt.target is StickyNote sn && sn.userData is string guid)
            {
                evt.menu.AppendAction("Delete Note", _ =>
                {
                    var win = EditorWindow.focusedWindow as ScenarioGraphWindow;
                    win?.DeleteNoteByGuid(guid);
                });
                return;
            }
#endif

            // Right-click on empty space → creation menu
            if (!(evt.target is GraphElement))
            {
                OnContextAdd?.Invoke(evt);
                return;
            }

            // Ports / edges: nothing special for now
        }
    }


    // ======== metadata kept on ports ========
    sealed class PortMeta
    {
        public Step owner;
        public int choiceIndex; // -1 => "Next"
        public static PortMeta From(Port p) => p?.userData as PortMeta;
        public static void Set(Port p, Step owner, int choiceIndex) => p.userData = new PortMeta { owner = owner, choiceIndex = choiceIndex };
    }

    // ---- Edge with moving "flow" marker ----
    // ---- Edge with moving glowing "light" segment ----
    // ---- Edge with moving glowing "light" segment ----
    class FlowEdge : Edge
    {
        float glowT;
        bool playing;

        const float SegmentLength = 0.18f;   // length of glow along the curve
        const float GlowWidth = 6f;      // thickness of glow

        // we cache whatever drawing the base Edge already had
        readonly Action<MeshGenerationContext> _baseGenerate;

        public FlowEdge()
        {
            // Cache existing generator then replace with our own wrapper
            _baseGenerate = this.generateVisualContent;
            this.generateVisualContent = OnGenerate;
        }

        public void PlayFlow()
        {
            glowT = 0f;
            playing = true;

            // simple ~0.5s animation (~60fps)
            this.schedule.Execute(_ =>
            {
                if (!playing)
                    return;

                glowT += 0.03f;
                if (glowT >= 1f)
                {
                    glowT = 1f;
                    playing = false;
                }

                MarkDirtyRepaint();

            }).Every(16).Until(() => !playing);
        }

        void OnGenerate(MeshGenerationContext ctx)
        {
            // draw normal edge first
            _baseGenerate?.Invoke(ctx);

            if (!playing || edgeControl == null)
                return;

            var cps = edgeControl.controlPoints;   // IList<Vector2> in GraphView
            if (cps == null || cps.Length < 4)
                return;

            DrawGlow(ctx, cps, glowT);
        }

        void DrawGlow(MeshGenerationContext ctx, System.Collections.Generic.IList<Vector2> cps, float tCenter)
        {
            if (cps == null || cps.Count < 4)
                return;

            float t0 = Mathf.Clamp01(tCenter - SegmentLength * 0.5f);
            float t1 = Mathf.Clamp01(tCenter + SegmentLength * 0.5f);
            if (t1 <= t0)
                return;

            const int segments = 8;
            int vertCount = segments * 4;
            int idxCount = segments * 6;

            var mesh = ctx.Allocate(vertCount, idxCount);
            var color = new Color(0.3f, 0.8f, 1f, 0.85f);   // cyan-ish glow

            for (int i = 0; i < segments; i++)
            {
                float tt0 = Mathf.Lerp(t0, t1, (float)i / segments);
                float tt1 = Mathf.Lerp(t0, t1, (float)(i + 1) / segments);

                Vector2 p0 = CubicBezier(cps[0], cps[1], cps[2], cps[3], tt0);
                Vector2 p1 = CubicBezier(cps[0], cps[1], cps[2], cps[3], tt1);

                Vector2 dir = (p1 - p0);
                if (dir.sqrMagnitude < 1e-6f)
                    continue;
                dir.Normalize();

                Vector2 n = new Vector2(-dir.y, dir.x);
                float halfW = GlowWidth * 0.5f;

                float fade = 1f - (float)i / segments;
                Color c = color;
                c.a *= fade;

                // 4 verts per segment
                mesh.SetNextVertex(new Vertex { position = p0 + n * halfW, tint = c });
                mesh.SetNextVertex(new Vertex { position = p0 - n * halfW, tint = c });
                mesh.SetNextVertex(new Vertex { position = p1 - n * halfW, tint = c });
                mesh.SetNextVertex(new Vertex { position = p1 + n * halfW, tint = c });

                int baseIndex = i * 4;
                mesh.SetNextIndex((ushort)(baseIndex + 0));
                mesh.SetNextIndex((ushort)(baseIndex + 1));
                mesh.SetNextIndex((ushort)(baseIndex + 2));
                mesh.SetNextIndex((ushort)(baseIndex + 0));
                mesh.SetNextIndex((ushort)(baseIndex + 2));
                mesh.SetNextIndex((ushort)(baseIndex + 3));
            }
        }

        static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0;
            p += 3f * uu * t * p1;
            p += 3f * u * tt * p2;
            p += ttt * p3;
            return p;
        }
    }




    // ======== Node (with “Edit…” button & working edge connectors) ========
    class StepNode : Node
    {
        readonly ScenarioGraphWindow owner;
        public readonly Scenario scenario;
        public readonly Step step;
        public readonly int index;

        public Port inPort;
        public Port outNext;
        public List<Port> outChoices;
        public Port outCorrect;
        public Port outWrong;


        readonly ScenarioGraphView graph;
        readonly Action rebuild;
        readonly Action<Step, int> skipRequest;
        readonly Action<Step> deleteRequest;

        bool _isActive;
        public bool IsNested { get; }
        public string ParentGroupGuid { get; }
        Foldout _foldout;
        public bool GroupSettingsExpanded => _foldout == null ? true : _foldout.value;
        float _expandedHeightCache;
        bool _resizeQueued;

        public StepNode(ScenarioGraphWindow ownerWindow, Scenario sc, Step s, int idx, ScenarioGraphView gv, Action rebuildLinks, Action<Step, int> onSkipRequest, Action<Step> onDeleteRequest, bool isNested = false, string parentGroupGuid = null)
        {
            owner = ownerWindow;
            scenario = sc; step = s; index = idx; graph = gv; rebuild = rebuildLinks; skipRequest = onSkipRequest; deleteRequest = onDeleteRequest;
            IsNested = isNested;
            ParentGroupGuid = parentGroupGuid;

            // Force compact width for non-group steps (GraphView can impose a larger min-width by default).
            if (s is not GroupStep)
            {
                style.minWidth = StepNodeWidth;
                style.maxWidth = StepNodeWidth;
                style.width = StepNodeWidth;
            }

            title = $"{idx:00}. {s.Kind}";
            var tbox = this.Q("title");
            var titleLabel = tbox?.Q<Label>();

            if (s is TimelineStep) tbox.style.backgroundColor = new Color(0.20f, 0.42f, 0.85f);
            if (s is CueCardsStep) tbox.style.backgroundColor = new Color(0.32f, 0.62f, 0.32f);
            if (s is QuestionStep) tbox.style.backgroundColor = new Color(0.76f, 0.45f, 0.22f);
            if (s is SelectionStep) tbox.style.backgroundColor = new Color(0.58f, 0.38f, 0.78f);
            if (s is InsertStep)
            {
                tbox.style.backgroundColor = new Color(0.90f, 0.75f, 0.25f);
                if (titleLabel != null)
                    titleLabel.style.color = Color.black;   // μαύρο text για το κίτρινο
            }
            if (s is EventStep)
            {
                tbox.style.backgroundColor = new Color(0.25f, 0.70f, 0.70f);
                if (titleLabel != null)
                    titleLabel.style.color = Color.black;   // μαύρο text για το κίτρινο
            }
            if (s is GroupStep)
            {
                tbox.style.backgroundColor = new Color(0.55f, 0.55f, 0.60f);
            }

            // In (nested steps do not participate in routing inside the main graph)
            if (!IsNested)
            {
                inPort = MakePort(Direction.Input, Port.Capacity.Multi, "In", -1);
                inputContainer.Add(inPort);
            }

            // top-right small “Edit…” button
            var headerButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            void OpenEditor()
            {
                if (step is TimelineStep tl) StepEditWindow.OpenTimeline(scenario, tl);
                else if (step is CueCardsStep cc) StepEditWindow.OpenCueCards(scenario, cc);
                else if (step is QuestionStep q) StepEditWindow.OpenQuestion(scenario, q, rebuild);
                else if (step is SelectionStep se) StepEditWindow.OpenSelection(scenario, se);
                else if (step is InsertStep ins) StepEditWindow.OpenInsert(scenario, ins);
                else if (step is EventStep ev) StepEditWindow.OpenEvent(scenario, ev);
                else if (step is GroupStep g) StepEditWindow.OpenGroup(scenario, g);
            }

            var editBtn = new UIEButton(OpenEditor) { text = "Edit…" };

            editBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            editBtn.style.marginLeft = 6;
            titleContainer.Add(editBtn);

            // quick inline fields foldout (kept for speed)
            var fold = new Foldout { text = "Settings", value = false };
            _foldout = fold;
            mainContainer.Add(fold);

            // When Details is toggled, resize the node so inline editing doesn't get clipped.
            fold.RegisterValueChangedCallback(_ =>
            {
                owner?.ScheduleResizeGroup(step is GroupStep gg ? gg.guid : null);

                // Non-group nodes: resize immediately based on deterministic height calc.
                if (step is not GroupStep)
                {
                    if (fold.value)
                    {
                        // Expand width a bit for editing comfort.
                        style.minWidth = StepNodeWidthExpanded;
                        style.maxWidth = StepNodeWidthExpanded;
                        style.width = StepNodeWidthExpanded;
                        ResizeToFitDetails();
                    }
                    else
                    {
                        // Collapse back to the compact height (don't keep expanded height).
                        _expandedHeightCache = 0f;
                        var r = GetPosition();
                        // Restore compact width + height.
                        style.minWidth = StepNodeWidth;
                        style.maxWidth = StepNodeWidth;
                        style.width = StepNodeWidth;
                        SetPositionSilent(new Rect(r.position, new Vector2(StepNodeWidth, GetCollapsedHeight())));
                    }
                }
            });

            // When inline IMGUI content expands/collapses while the foldout is open, keep sizing in sync.
            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (step is GroupStep) return;
                if (_foldout == null || !_foldout.value) return;
                if (_resizeQueued) return;
                _resizeQueued = true;
                EditorApplication.delayCall += () =>
                {
                    _resizeQueued = false;
                    if (this == null) return;
                    ResizeToFitDetails();
                };
            });

            // For Groups, Unity's default collapse toggle can become unclickable depending on layout.
            // Provide our own always-visible toggle in the title bar.
            if (s is GroupStep)
            {
                var builtinCollapse = this.Q("collapse-button");
                if (builtinCollapse != null)
                    builtinCollapse.style.display = DisplayStyle.None;

                var expBtn = new UIEButton() { text = "▾" };
                expBtn.clicked += () =>
                {
                    // Toggle foldout content, never hide the header (so you can always reopen).
                    fold.value = !fold.value;
                    expBtn.text = fold.value ? "▾" : "▸";
                    if (step is GroupStep gg)
                        owner?.ScheduleResizeGroup(gg.guid);
                };
                expBtn.style.width = 22;
                expBtn.style.height = 18;
                expBtn.style.marginLeft = 6;
                expBtn.tooltip = "Collapse/Expand Group";
                titleContainer.Insert(0, expBtn);

                // Keep node expanded; only toggle fold visibility.
                fold.value = true;
                expBtn.text = "▾";
            }

            // Double-click to edit (especially important for nested tiles).
            RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button == 0 && e.clickCount == 2)
                {
                    OpenEditor();
                    e.StopPropagation();
                }
            });

            if (IsNested)
            {
                // Nested nodes stay compact to avoid overlap; edit via the "Edit…" button (modal StepEditWindow).
                fold.visible = false;
                extensionContainer.style.display = DisplayStyle.None;
                RefreshExpandedState();
                RefreshPorts();
                return;
            }

            if (s is TimelineStep tl)
            {
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    EditorGUI.BeginChangeCheck();
                    var newDir = (PlayableDirector)EditorGUILayout.ObjectField("Director", tl.director, typeof(PlayableDirector), true);
                    tl.rewindOnEnter = EditorGUILayout.Toggle("Rewind On Enter", tl.rewindOnEnter);
                    tl.waitForEnd = EditorGUILayout.Toggle("Wait For End", tl.waitForEnd);
                    if (EditorGUI.EndChangeCheck()) { Dirty(scenario, "Edit Timeline"); tl.director = newDir; }
                }));

                outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                outputContainer.Add(outNext);
            }
            else if (s is CueCardsStep cc)
            {
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    EditorGUI.BeginChangeCheck();
                    cc.director = (PlayableDirector)EditorGUILayout.ObjectField("Clock (opt)", cc.director, typeof(PlayableDirector), true);
                    cc.autoShowFirst = EditorGUILayout.Toggle("Auto Show First", cc.autoShowFirst);
                    cc.tapHint = (GameObject)EditorGUILayout.ObjectField("Tap Hint", cc.tapHint, typeof(GameObject), true);
                    cc.extraObject = (GameObject)EditorGUILayout.ObjectField("Extra Object", cc.extraObject, typeof(GameObject), true);
                    cc.extraShowAtIndex = EditorGUILayout.IntField("Extra Show At Index", cc.extraShowAtIndex);
                    cc.hideExtraWithFinalTap = EditorGUILayout.Toggle("Hide Extra With Final Tap", cc.hideExtraWithFinalTap);
                    cc.useRenderersForExtra = EditorGUILayout.Toggle("Use Renderers For Extra", cc.useRenderersForExtra);
                    cc.fadeDuration = EditorGUILayout.FloatField("Fade Duration", cc.fadeDuration);
                    cc.popScale = EditorGUILayout.FloatField("Pop Scale", cc.popScale);
                    cc.popDuration = EditorGUILayout.FloatField("Pop Duration", cc.popDuration);
                    EditorGUILayout.HelpBox("Open “Edit…” for full Cards & Cue Times editing.", MessageType.None);
                    if (EditorGUI.EndChangeCheck()) Dirty(scenario, "Edit Cue Cards");
                }));

                outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                outputContainer.Add(outNext);
            }
            else if (s is QuestionStep q)
            {
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    EditorGUI.BeginChangeCheck();
                    q.panelRoot = (RectTransform)EditorGUILayout.ObjectField("Panel Root", q.panelRoot, typeof(RectTransform), true);
                    q.panelAnimator = (Animator)EditorGUILayout.ObjectField("Animator", q.panelAnimator, typeof(Animator), true);
                    q.showTrigger = EditorGUILayout.TextField("Show Trigger", q.showTrigger);
                    q.hideTrigger = EditorGUILayout.TextField("Hide Trigger", q.hideTrigger);
                    q.fallbackHideSeconds = EditorGUILayout.FloatField("Fallback Hide (s)", q.fallbackHideSeconds);
                    EditorGUILayout.HelpBox("Use “Edit…” to manage Choices & Effects in detail.", MessageType.None);
                    if (EditorGUI.EndChangeCheck()) Dirty(scenario, "Edit Question");
                }));

                RecreateChoicePorts();
            }
            else if (s is SelectionStep sel)
            {
                // Inline quick fields
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    EditorGUI.BeginChangeCheck();
                    sel.lists = (SelectionLists)EditorGUILayout.ObjectField("Lists", sel.lists, typeof(SelectionLists), true);
                    sel.listKey = EditorGUILayout.TextField("List Name", sel.listKey);
                    sel.listIndex = EditorGUILayout.IntField("(or) List Index", sel.listIndex);
                    sel.resetOnEnter = EditorGUILayout.Toggle("Reset On Enter", sel.resetOnEnter);
                    sel.completion = (SelectionStep.CompleteMode)EditorGUILayout.EnumPopup("Completion", sel.completion);
                    if (sel.completion == SelectionStep.CompleteMode.OnSubmitButton)
                        sel.submitButton = (UGUIButton)EditorGUILayout.ObjectField("Submit Button", sel.submitButton, typeof(UGUIButton), true);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Requirement", EditorStyles.boldLabel);
                    sel.requiredSelections = EditorGUILayout.IntField("Required Selections", sel.requiredSelections);
                    sel.requireExactCount = EditorGUILayout.Toggle("Require Exact Count", sel.requireExactCount);
                    sel.allowedWrong = EditorGUILayout.IntField("Allowed Wrong", sel.allowedWrong);
                    sel.timeoutSeconds = EditorGUILayout.FloatField("Timeout (s)", sel.timeoutSeconds);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("UI (optional)", EditorStyles.boldLabel);
                    sel.panelRoot = (RectTransform)EditorGUILayout.ObjectField("Panel Root", sel.panelRoot, typeof(RectTransform), true);
                    sel.panelAnimator = (Animator)EditorGUILayout.ObjectField("Animator", sel.panelAnimator, typeof(Animator), true);
                    sel.showTrigger = EditorGUILayout.TextField("Show Trigger", sel.showTrigger);
                    sel.hideTrigger = EditorGUILayout.TextField("Hide Trigger", sel.hideTrigger);
                    sel.hint = (GameObject)EditorGUILayout.ObjectField("Hint", sel.hint, typeof(GameObject), true);

                    if (EditorGUI.EndChangeCheck()) Dirty(scenario, "Edit Selection");
                }));



                // Two outputs
                outCorrect = MakePort(Direction.Output, Port.Capacity.Single, "Correct", -2);
                outWrong = MakePort(Direction.Output, Port.Capacity.Single, "Wrong", -3);
                outputContainer.Add(outCorrect);
                outputContainer.Add(outWrong);
            }

            else if (s is InsertStep ins)
            {
                // Inline mini-inspector για γρήγορο authoring
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    EditorGUI.BeginChangeCheck();

                    ins.item = (Transform)EditorGUILayout.ObjectField("Item", ins.item, typeof(Transform), true);
                    ins.targetTrigger = (Collider)EditorGUILayout.ObjectField("Target Trigger", ins.targetTrigger, typeof(Collider), true);
                    ins.attachTransform = (Transform)EditorGUILayout.ObjectField("Attach Transform", ins.attachTransform, typeof(Transform), true);

                    EditorGUILayout.Space(4);
                    ins.smoothAttach = EditorGUILayout.Toggle("Smooth Attach", ins.smoothAttach);
                    ins.parentToAttach = EditorGUILayout.Toggle("Parent To Attach", ins.parentToAttach);
                    ins.moveSpeed = EditorGUILayout.FloatField("Move Speed", ins.moveSpeed);
                    ins.rotateSpeed = EditorGUILayout.FloatField("Rotate Speed", ins.rotateSpeed);

                    EditorGUILayout.Space(4);
                    ins.positionTolerance = EditorGUILayout.FloatField("Position Tolerance", ins.positionTolerance);
                    ins.angleTolerance = EditorGUILayout.FloatField("Angle Tolerance", ins.angleTolerance);

                    if (EditorGUI.EndChangeCheck()) Dirty(scenario, "Edit Insert");
                }));

                outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                outputContainer.Add(outNext);
            }
            else if (s is EventStep ev)
            {
                // Cache SerializedObject + this step's SerializedProperty once
                var so = new SerializedObject(scenario);
                var stepsProp = so.FindProperty("steps");
                SerializedProperty stepProp = null;

                if (stepsProp != null)
                {
                    for (int i = 0; i < stepsProp.arraySize; i++)
                    {
                        var el = stepsProp.GetArrayElementAtIndex(i);
                        var g = el.FindPropertyRelative("guid");
                        if (g != null && g.stringValue == ev.guid)
                        {
                            stepProp = el;
                            break;
                        }
                    }
                }

                // IMGUIContainer that actually draws the fields
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    if (stepProp == null) return; // nothing to draw

                    so.Update();
                    EditorGUI.BeginChangeCheck();

                    var onEnterProp = stepProp.FindPropertyRelative("onEnter");
                    var waitProp = stepProp.FindPropertyRelative("waitSeconds");

                    if (onEnterProp != null)
                        EditorGUILayout.PropertyField(onEnterProp, new GUIContent("On Enter Events"));

                    if (waitProp != null)
                        EditorGUILayout.PropertyField(waitProp, new GUIContent("Wait Seconds Before Next"));

                    if (EditorGUI.EndChangeCheck())
                    {
                        so.ApplyModifiedProperties();
                        Dirty(scenario, "Edit Event");
                    }
                }));

                // Normal Next output like the other linear steps
                outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                outputContainer.Add(outNext);
            }
            else if (s is GroupStep g)
            {
                // Make it feel like a container: settings on top + a visible drop zone area below.
                fold.text = "Group Settings";
                fold.value = true;
                fold.RegisterValueChangedCallback(_ =>
                {
                    owner?.ScheduleResizeGroup(g.guid);
                });

                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    EditorGUI.BeginChangeCheck();
                    g.completeWhen = (GroupStep.CompleteWhen)EditorGUILayout.EnumPopup("Complete When", g.completeWhen);
                    if (g.completeWhen == GroupStep.CompleteWhen.AfterSeconds)
                        g.afterSeconds = EditorGUILayout.FloatField("After Seconds", g.afterSeconds);
                    else if (g.completeWhen == GroupStep.CompleteWhen.WhenSpecificStepCompletes)
                    {
                        // Pick from nested steps (numbered) instead of typing GUID
                        if (g.steps != null && g.steps.Count > 0)
                        {
                            var options = new List<string> { "None" };
                            var guids = new List<string> { "" };
                            for (int i = 0; i < g.steps.Count; i++)
                            {
                                var st = g.steps[i];
                                if (st == null || string.IsNullOrEmpty(st.guid)) continue;
                                options.Add($"{i + 1}. {st.Kind}");
                                guids.Add(st.guid);
                            }

                            int cur = 0;
                            if (!string.IsNullOrEmpty(g.specificStepGuid))
                            {
                                int idx = guids.IndexOf(g.specificStepGuid);
                                if (idx >= 0) cur = idx;
                            }

                            int next = EditorGUILayout.Popup("Specific Step", cur, options.ToArray());
                            g.specificStepGuid = guids[Mathf.Clamp(next, 0, guids.Count - 1)];
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("Add nested steps to the Group to select a specific completion step.", MessageType.Info);
                            g.specificStepGuid = EditorGUILayout.TextField("Specific Step Guid", g.specificStepGuid);
                        }
                    }
                    g.stopOthersOnComplete = EditorGUILayout.Toggle("Stop Others On Complete", g.stopOthersOnComplete);

                    int count = g.steps != null ? g.steps.Count : 0;
                    EditorGUILayout.LabelField("Nested Steps", $"{count} step(s)");
                    if (count > 0)
                    {
                        for (int i = 0; i < Mathf.Min(count, 8); i++)
                        {
                            var st = g.steps[i];
                            EditorGUILayout.LabelField($"• {(st != null ? st.Kind : "<null>")}");
                        }
                        if (count > 8)
                            EditorGUILayout.LabelField($"… +{count - 8} more");
                    }
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Tip: Drop steps into the big box above these settings.", EditorStyles.miniLabel);

                    if (EditorGUI.EndChangeCheck()) Dirty(scenario, "Edit Group");
                }));

                // Visual drop zone (container body; includes nested tiles)
                var dropZone = new VisualElement();
                dropZone.name = "group-drop-zone";
                // Do not steal clicks/drags from nested nodes.
                dropZone.pickingMode = PickingMode.Ignore;
                dropZone.style.marginTop = 6;
                dropZone.style.paddingLeft = 10;
                dropZone.style.paddingRight = 10;
                dropZone.style.paddingTop = 8;
                dropZone.style.paddingBottom = 8;
                dropZone.style.minHeight = 0;
                dropZone.style.backgroundColor = new Color(1f, 1f, 1f, 0.04f);
                dropZone.style.borderTopWidth = 1;
                dropZone.style.borderBottomWidth = 1;
                dropZone.style.borderLeftWidth = 1;
                dropZone.style.borderRightWidth = 1;
                dropZone.style.borderTopColor = new Color(1f, 1f, 1f, 0.12f);
                dropZone.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
                dropZone.style.borderLeftColor = new Color(1f, 1f, 1f, 0.12f);
                dropZone.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);

                var dzTitle = new Label("DROP AREA");
                dzTitle.pickingMode = PickingMode.Ignore;
                dzTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                dzTitle.style.fontSize = 10;
                dzTitle.style.color = new Color(1f, 1f, 1f, 0.55f);
                dzTitle.style.marginBottom = 4;
                dropZone.Add(dzTitle);

                var dzHint = new Label("Drag existing steps here to add them to the group.\nDrag a step out to remove it.");
                dzHint.pickingMode = PickingMode.Ignore;
                dzHint.style.fontSize = 10;
                dzHint.style.color = new Color(1f, 1f, 1f, 0.75f);
                dropZone.Add(dzHint);

                // If we already have nested steps, hide the instructional text so tiles don't overlap text.
                if (g.steps != null && g.steps.Count > 0)
                {
                    dzTitle.style.display = DisplayStyle.None;
                    dzHint.style.display = DisplayStyle.None;
                }

                // --- Nested tiles (REAL container UX; not GraphView nodes) ---
                var tiles = new VisualElement();
                tiles.style.flexDirection = FlexDirection.Row;
                tiles.style.flexWrap = Wrap.Wrap;
                tiles.style.alignContent = Align.FlexStart;
                // Critical: stretch to the full container width so Wrap can use all available space.
                // Without this, UIElements may size the container to its content and you get "2 columns + huge empty gap".
                tiles.style.flexGrow = 1;
                tiles.style.alignSelf = Align.Stretch;
                tiles.style.width = Length.Percent(100);
                tiles.style.marginTop = 6;
                dropZone.Add(tiles);

                if (g.steps != null)
                {
                    for (int i = 0; i < g.steps.Count; i++)
                    {
                        var sub = g.steps[i];
                        if (sub == null) continue;

                        var tile = BuildNestedTile(g, sub, i);
                        tiles.Add(tile);
                    }
                }

                // Put the drop zone into the extension area so it appears as part of the node body
                extensionContainer.Add(dropZone);

                outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                outputContainer.Add(outNext);
            }


            RefreshExpandedState();
            RefreshPorts();
        }

        public void SetActiveHighlight(bool active)
        {
            if (_isActive == active) return;
            _isActive = active;

            var mc = this.mainContainer;

            // borders (as you already do)
            if (active)
            {
                mc.style.borderTopWidth = 3;
                mc.style.borderBottomWidth = 3;
                mc.style.borderLeftWidth = 3;
                mc.style.borderRightWidth = 3;
                var c = new Color(0.3f, 0.7f, 1f);
                mc.style.borderTopColor = c;
                mc.style.borderBottomColor = c;
                mc.style.borderLeftColor = c;
                mc.style.borderRightColor = c;

                if (Application.isPlaying)
                    EnsureSkipButtons();
            }
            else
            {
                mc.style.borderTopWidth = 0;
                mc.style.borderBottomWidth = 0;
                mc.style.borderLeftWidth = 0;
                mc.style.borderRightWidth = 0;
                RemoveSkipButtons();
            }

            this.MarkDirtyRepaint();
        }
        VisualElement skipRow;
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // Kill Unity’s default items (Delete, Cut, etc)
            evt.menu.ClearItems();

            // Our single source of truth for deletion
            evt.menu.AppendAction("Delete Step", _ =>
            {
                deleteRequest?.Invoke(step);
            });

            // If you want, you can also add Disconnect all etc here
            // evt.menu.AppendAction("Disconnect all", _ => { ... });
        }

        void EnsureSkipButtons()
        {
            RemoveSkipButtons();

            skipRow = new VisualElement();
            skipRow.style.flexDirection = FlexDirection.Row;
            skipRow.style.marginTop = 4;
            skipRow.style.marginBottom = 2;

            if (step is TimelineStep || step is CueCardsStep || step is InsertStep || step is EventStep)
            {
                var btn = new UIEButton(() => skipRequest?.Invoke(step, -1))
                {
                    text = "Skip ▶"
                };
                skipRow.Add(btn);
            }
            else if (step is GroupStep)
            {
                var btn = new UIEButton(() => skipRequest?.Invoke(step, -1)) { text = "Skip ▶" };
                skipRow.Add(btn);
            }
            else if (step is SelectionStep)
            {
                var bCorrect = new UIEButton(() => skipRequest?.Invoke(step, -2)) { text = "Correct ▶" };
                var bWrong = new UIEButton(() => skipRequest?.Invoke(step, -3)) { text = "Wrong ▶" };

                bWrong.style.marginLeft = 4;

                skipRow.Add(bCorrect);
                skipRow.Add(bWrong);
            }
            else if (step is QuestionStep q)
            {
                int count = q.choices != null ? q.choices.Count : 0;
                for (int i = 0; i < count; i++)
                {
                    int idx = i;
                    var b = new UIEButton(() => skipRequest?.Invoke(step, idx))
                    {
                        text = $"Choice {idx} ▶"
                    };
                    if (i > 0) b.style.marginLeft = 2;
                    skipRow.Add(b);
                }
            }

            if (skipRow.childCount > 0)
                mainContainer.Add(skipRow);
        }

        void RemoveSkipButtons()
        {
            if (skipRow != null && skipRow.parent == mainContainer)
                mainContainer.Remove(skipRow);
            skipRow = null;
        }


        Port MakePort(Direction dir, Port.Capacity cap, string label, int choiceIndex)
        {
            var p = InstantiatePort(Orientation.Horizontal, dir, cap, typeof(bool));
            p.portName = label;
            PortMeta.Set(p, step, choiceIndex);

            // Create FlowEdge when user drags connections
            var connector = new EdgeConnector<FlowEdge>(new ECListener());
            p.AddManipulator(connector);

            return p;
        }

        void RecreateChoicePorts()
        {
            if (step is not QuestionStep q) return;

            if (outChoices != null)
                foreach (var p in outChoices) outputContainer.Remove(p);

            outChoices = new List<Port>();
            int count = q.choices != null ? q.choices.Count : 0;

            if (count == 0)
            {
                outputContainer.Add(new Label("No choices (edit in “Edit…”)"));
            }
            else
            {
                for (int c = 0; c < count; c++)
                {
                    var p = MakePort(Direction.Output, Port.Capacity.Single, $"Choice {c}", c);
                    outChoices.Add(p);
                    outputContainer.Add(p);
                }
            }

            RefreshPorts();
            rebuild?.Invoke(); // will be ignored during Load thanks to guard
        }

        VisualElement BuildNestedTile(GroupStep group, Step sub, int ordinalIndex)
        {
            var tile = new VisualElement();
            tile.style.width = GroupTileW;
            tile.style.height = GroupTileH;
            tile.style.marginRight = 10;
            tile.style.marginBottom = 10;
            tile.style.paddingLeft = 10;
            tile.style.paddingRight = 10;
            tile.style.paddingTop = 8;
            tile.style.paddingBottom = 8;
            tile.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            tile.style.borderTopLeftRadius = 6;
            tile.style.borderTopRightRadius = 6;
            tile.style.borderBottomLeftRadius = 6;
            tile.style.borderBottomRightRadius = 6;
            tile.style.borderTopWidth = 1;
            tile.style.borderBottomWidth = 1;
            tile.style.borderLeftWidth = 1;
            tile.style.borderRightWidth = 1;
            tile.style.borderTopColor = new Color(1f, 1f, 1f, 0.12f);
            tile.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
            tile.style.borderLeftColor = new Color(1f, 1f, 1f, 0.12f);
            tile.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);

            // color accent (matches step color in graph)
            Color accent = GetStepAccent(sub);
            tile.style.borderLeftWidth = 4;
            tile.style.borderLeftColor = accent;

            // header row
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            tile.Add(row);

            var name = new Label($"{ordinalIndex + 1}. {sub.Kind}");
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = Color.white;
            name.style.fontSize = 12;
            name.style.flexGrow = 1;
            row.Add(name);

            var edit = new UIEButton(() =>
            {
                // Edit nested step in focused modal
                if (sub is TimelineStep tl) StepEditWindow.OpenTimeline(scenario, tl);
                else if (sub is CueCardsStep cc) StepEditWindow.OpenCueCards(scenario, cc);
                else if (sub is QuestionStep q) StepEditWindow.OpenQuestion(scenario, q, rebuild);
                else if (sub is SelectionStep se) StepEditWindow.OpenSelection(scenario, se);
                else if (sub is InsertStep ins) StepEditWindow.OpenInsert(scenario, ins);
                else if (sub is EventStep ev) StepEditWindow.OpenEvent(scenario, ev);
                else if (sub is GroupStep g) StepEditWindow.OpenGroup(scenario, g);
            })
            { text = "Edit…" };
            edit.style.marginLeft = 6;
            row.Add(edit);

            var remove = new UIEButton(() =>
            {
                if (group?.steps == null) return;
                int idx = group.steps.IndexOf(sub);
                if (idx < 0) return;

                Dirty(scenario, "Ungroup Step");
                group.steps.RemoveAt(idx);

                int groupTopIndex = scenario.steps.IndexOf(group);
                int insertAt = Mathf.Clamp(groupTopIndex + 1, 0, scenario.steps.Count);

                // place next to the group
                var gp = group.graphPos;
                sub.graphPos = new Vector2(gp.x + 760f, gp.y + 40f + 180f * idx);

                scenario.steps.Insert(insertAt, sub);

                // rebuild window cleanly next tick
                EditorApplication.delayCall += () =>
                {
                    if (owner != null && owner.scenario != null)
                        owner.Load(owner.scenario);
                };
            })
            { text = "↗" };
            remove.tooltip = "Remove from group";
            remove.style.marginLeft = 6;
            row.Add(remove);

            // small subtext
            var hint = new Label("Nested step (runs in group)");
            hint.style.marginTop = 6;
            hint.style.fontSize = 10;
            hint.style.color = new Color(1f, 1f, 1f, 0.55f);
            tile.Add(hint);

            return tile;
        }

        static Color GetStepAccent(Step s)
        {
            if (s is TimelineStep) return new Color(0.20f, 0.42f, 0.85f);
            if (s is CueCardsStep) return new Color(0.32f, 0.62f, 0.32f);
            if (s is QuestionStep) return new Color(0.76f, 0.45f, 0.22f);
            if (s is SelectionStep) return new Color(0.58f, 0.38f, 0.78f);
            if (s is InsertStep) return new Color(0.90f, 0.75f, 0.25f);
            if (s is EventStep) return new Color(0.25f, 0.70f, 0.70f);
            if (s is GroupStep) return new Color(0.55f, 0.55f, 0.60f);
            return new Color(0.6f, 0.6f, 0.6f);
        }

        public override void SetPosition(Rect newPos)
        {
            // Persist + relative handling is centralized in graphViewChanged to avoid double-Undo and ordering bugs.
            base.SetPosition(newPos);
        }

        public void SetPositionSilent(Rect newPos)
        {
            base.SetPosition(newPos);
            // Hard-enforce size so user-resizes / layout quirks can't leave a permanent right-side gap.
            style.width = newPos.width;
            style.height = newPos.height;
        }

        public float GetHeight()
        {
            if (IsNested) return 110f; // compact tile in container
            bool expandedDetails = _foldout != null && _foldout.value;
            float collapsed = GetCollapsedHeight();
            if (!expandedDetails) return collapsed;
            return Mathf.Max(collapsed, _expandedHeightCache > 0 ? _expandedHeightCache : collapsed);

            if (step is GroupStep g)
            {
                int count = g.steps?.Count ?? 0;
                int rows = Mathf.CeilToInt(count / (float)GroupTileColumns);
                float tilesH = count == 0 ? 72f : rows * GroupTileH + Mathf.Max(0, rows - 1) * 10f + 18f;
                return Mathf.Max(260f, 54f + tilesH + 160f);
            }
            return collapsed;
        }

        float GetCollapsedHeight()
        {
            // Compact collapsed sizes (match "small nodes" UX). Expanded sizes are handled by the Details sizing logic.
            const float small = 120f;     // header + ports + a little breathing room
            const float medium = 140f;    // steps with extra ports (selection) need a bit more

            if (step is TimelineStep) return small;
            if (step is CueCardsStep) return small;
            if (step is InsertStep) return small;
            if (step is EventStep) return small;

            if (step is SelectionStep) return medium;

            if (step is QuestionStep q)
            {
                int count = q.choices?.Count ?? 0;
                // Keep enough vertical space to show choice ports without becoming huge.
                return Mathf.Max(medium, 130f + 18f * Mathf.Clamp(count, 0, 8));
            }

            return small;
        }

        void ResizeToFitDetails()
        {
            if (_foldout == null || !_foldout.value) return;

            float width = GetPosition().width;
            if (width <= 0) width = StepNodeWidthExpanded;

            _expandedHeightCache = ComputeExpandedHeight(width);
            var r = GetPosition();
            SetPositionSilent(new Rect(r.position, new Vector2(r.width, Mathf.Max(GetCollapsedHeight(), _expandedHeightCache))));
        }

        float ComputeExpandedHeight(float nodeWidth)
        {
            // Deterministic IMGUI-style height: count the controls we draw in each Details section.
            // This is much more stable than trying to measure IMGUIContainer via resolvedStyle.
            float line = EditorGUIUtility.singleLineHeight;
            float v = EditorGUIUtility.standardVerticalSpacing;
            float h = 0f;

            // Ports/header area is handled by the node chrome; we size only for the Details content area.
            // Provide a base padding so controls don't clip.
            h += 110f; // chrome + foldout header baseline

            if (step is TimelineStep)
            {
                // Director + 2 toggles
                h += (3 * (line + v)) + 24f;
            }
            else if (step is CueCardsStep)
            {
                // Clock + ~9 fields + helpbox
                h += (10 * (line + v)) + 60f;
            }
            else if (step is QuestionStep)
            {
                // ~5 fields + helpbox
                h += (5 * (line + v)) + 60f;
            }
            else if (step is SelectionStep sel)
            {
                // This matches the inline IMGUI we draw for SelectionStep
                int lines = 5; // lists, listKey, listIndex, reset, completion
                if (sel != null && sel.completion == SelectionStep.CompleteMode.OnSubmitButton) lines += 1;
                lines += 1; // "Requirement" label
                lines += 4; // requiredSelections, exactCount, allowedWrong, timeout
                lines += 1; // "UI (optional)" label
                lines += 5; // panelRoot, panelAnimator, show, hide, hint
                h += (lines * (line + v)) + 80f; // spaces + margins
            }
            else if (step is InsertStep)
            {
                // item, trigger, attach + 4 fields + 2 fields
                h += (9 * (line + v)) + 80f;
            }
            else if (step is EventStep ev)
            {
                // Use SerializedProperty height for the UnityEvent list if possible (varies by expansion)
                float eventH = 0f;
                try
                {
                    var so = new SerializedObject(scenario);
                    var p = StepEditWindow.FindStepPropertyRecursive(so, ev.guid);
                    var onEnter = p?.FindPropertyRelative("onEnter");
                    if (onEnter != null) eventH = EditorGUI.GetPropertyHeight(onEnter, true);
                }
                catch { }
                h += (2 * (line + v)) + Mathf.Max(60f, eventH) + 40f;
            }

            return Mathf.Ceil(h);
        }


        sealed class ECListener : IEdgeConnectorListener
        {
            public void OnDropOutsidePort(Edge edge, Vector2 pos) { }
            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (edge?.input == null || edge.output == null) return;
                graphView.AddElement(edge);

                // IMPORTANT: some GraphView flows do not populate graphViewChanged.edgesToCreate.
                // Ensure we persist routes by scheduling a full sync after an edge drop.
                if (graphView is ScenarioGraphView gv)
                    gv.OnEdgeDropped?.Invoke();
            }
        }
        // ---- Edge with moving "flow" marker ----

    }
}

// =================== STEP EDIT WINDOWS (UI shown when you click “Edit…”) ===================

sealed class StepEditWindow : EditorWindow
{
    Scenario scenario;
    string scenarioGlobalId;
    string stepGuid;
    SerializedObject so;
    SerializedProperty stepProp;
    Vector2 scroll;

    Action onAfterApply; // optional (ex: rebuild choice ports)

    // --------- open helpers ----------
    public static void OpenTimeline(Scenario sc, TimelineStep tl)
        => Open(sc, tl.guid, "Timeline", w => w.DrawTimeline());

    public static void OpenCueCards(Scenario sc, CueCardsStep cc)
        => Open(sc, cc.guid, "Cue Cards", w => w.DrawCueCards());

    public static void OpenQuestion(Scenario sc, QuestionStep q, Action afterApply = null)
        => Open(sc, q.guid, "Question", w => w.DrawQuestion(), afterApply);
    public static void OpenSelection(Scenario sc, SelectionStep sel)
    => Open(sc, sel.guid, "Selection", w => w.DrawSelection());

    public static void OpenInsert(Scenario sc, InsertStep ins)
    => Open(sc, ins.guid, "Insert", w => w.DrawInsert());

    public static void OpenEvent(Scenario sc, EventStep ev)
    => Open(sc, ev.guid, "Event", w => w.DrawEvent());

    public static void OpenGroup(Scenario sc, GroupStep g)
    => Open(sc, g.guid, "Group", w => w.DrawGroup());


    static void Open(Scenario sc, string guid, string title, Action<StepEditWindow> draw, Action afterApply = null)
    {
        var w = CreateInstance<StepEditWindow>();
        w.scenario = sc;
        try
        {
            if (sc)
                w.scenarioGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(sc).ToString();
        }
        catch { /* best-effort; GlobalObjectId can fail in some editor contexts */ }
        w.stepGuid = guid;
        w.minSize = new Vector2(460, 360);
        w.titleContent = new GUIContent($"{title} • Step");
        w.onAfterApply = afterApply;
        w.ShowUtility();
        // Event.current can be null when opened from UIElements callbacks; fall back to a sensible position.
        var mp = Event.current != null ? Event.current.mousePosition : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        w.position = new Rect(GUIUtility.GUIToScreenPoint(mp) + new Vector2(8, 8), w.minSize);
        w.Init(draw);
    }

    Action<StepEditWindow> drawer;

    void Init(Action<StepEditWindow> d)
    {
        drawer = d;
        TryResolveScenario();
        if (!scenario) return;
        so = new SerializedObject(scenario);
        stepProp = FindStepPropertyRecursive(so, stepGuid);
    }

    internal static SerializedProperty FindStepPropertyRecursive(SerializedObject so, string guid)
    {
        if (so == null || string.IsNullOrEmpty(guid)) return null;
        var steps = so.FindProperty("steps");
        return FindInStepsList(steps, guid);
    }

    static SerializedProperty FindInStepsList(SerializedProperty stepsList, string guid)
    {
        if (stepsList == null || !stepsList.isArray) return null;

        for (int i = 0; i < stepsList.arraySize; i++)
        {
            var el = stepsList.GetArrayElementAtIndex(i);
            if (el == null) continue;

            var g = el.FindPropertyRelative("guid");
            if (g != null && g.stringValue == guid)
                return el;

            // If this element is a GroupStep, it has a nested SerializeReference list called "steps".
            var nested = el.FindPropertyRelative("steps");
            if (nested != null && nested.isArray)
            {
                var found = FindInStepsList(nested, guid);
                if (found != null) return found;
            }
        }

        return null;
    }

    void OnGUI()
    {
        if (!scenario)
        {
            TryResolveScenario();
        }

        if (!scenario)
        {
            EditorGUILayout.HelpBox("Scenario not found.", MessageType.Warning);
            if (GUILayout.Button("Close")) Close();
            return;
        }

        if (so == null)
            so = new SerializedObject(scenario);

        // Step may be nested inside a GroupStep; always re-resolve each frame to avoid stale references after reflow/ungroup.
        if (stepProp == null)
            stepProp = FindStepPropertyRecursive(so, stepGuid);

        if (so == null || stepProp == null)
        {
            EditorGUILayout.HelpBox("Step not found (maybe removed).", MessageType.Warning);
            if (GUILayout.Button("Close")) Close();
            return;
        }

        so.Update();

        EditorGUI.BeginChangeCheck();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(2);
        drawer?.Invoke(this);
        EditorGUILayout.EndScrollView();

        // Apply immediately on change so the window doesn't feel "read-only".
        // (If we only apply on the Apply button, so.Update() will reload from the target every frame and discard edits.)
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(scenario, "Edit Step");
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(scenario);
            EditorSceneManager.MarkSceneDirty(scenario.gameObject.scene);
            onAfterApply?.Invoke();
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Apply", GUILayout.Width(90)))
            {
                Undo.RecordObject(scenario, "Edit Step");
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(scenario);
                EditorSceneManager.MarkSceneDirty(scenario.gameObject.scene);
                onAfterApply?.Invoke();
            }
            if (GUILayout.Button("Close", GUILayout.Width(90))) Close();
        }
    }

    void TryResolveScenario()
    {
        if (scenario) return;
        if (string.IsNullOrEmpty(scenarioGlobalId)) return;

        try
        {
            if (GlobalObjectId.TryParse(scenarioGlobalId, out var gid))
            {
                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                if (obj is Scenario sc)
                    scenario = sc;
            }
        }
        catch { }
    }

    // ----------- specific drawers -----------
    void DrawTimeline()
    {
        EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("director"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("rewindOnEnter"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("waitForEnd"));
        DrawNextGuid();
    }

    void DrawCueCards()
    {
        EditorGUILayout.LabelField("Cue Cards", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("director"), new GUIContent("Clock Director (opt)"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cards in order", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("cards"), true);

        EditorGUILayout.Space(2);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("cueTimes"), new GUIContent("Cue Times (sec)"), true);
        EditorGUILayout.HelpBox("Cue Time = max seconds to keep the card if the player doesn’t tap.\nLeave empty for tap-only. If only one value is provided it applies to all cards.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("autoShowFirst"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("tapHint"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Optional extra object", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("extraObject"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("extraShowAtIndex"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hideExtraWithFinalTap"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("useRenderersForExtra"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Transitions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("fadeDuration"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("popScale"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("popDuration"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("fadeCurve"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("scaleCurve"));

        DrawNextGuid();
    }

    void DrawQuestion()
    {
        EditorGUILayout.LabelField("Question", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelRoot"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelAnimator"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("showTrigger"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hideTrigger"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("fallbackHideSeconds"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Choices", EditorStyles.boldLabel);
        var choices = stepProp.FindPropertyRelative("choices");
        if (choices != null)
        {
            // Unity will render button + nested effects when we draw the element with 'true'
            EditorGUILayout.PropertyField(choices, includeChildren: true);
            EditorGUILayout.HelpBox("Each Choice → assign the Button and edit its Effects. Effects list supports Add/Remove/Reorder.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Choices list not found.", MessageType.Warning);
        }
    }
    void DrawSelection()
    {
        EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("lists"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("listKey"), new GUIContent("List Name"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("listIndex"), new GUIContent("(or) List Index"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("resetOnEnter"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("completion"));
        var comp = stepProp.FindPropertyRelative("completion").enumValueIndex;
        if (comp == (int)SelectionStep.CompleteMode.OnSubmitButton)
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("submitButton"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Requirement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("requiredSelections"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("requireExactCount"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("allowedWrong"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("timeoutSeconds"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("UI (optional)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelRoot"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelAnimator"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("showTrigger"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hideTrigger"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hint"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stat Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("onCorrectEffects"), true);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("onWrongEffects"), true);

        DrawCorrectWrongGuids();
    }

    void DrawInsert()
    {
        EditorGUILayout.LabelField("Insert", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("item"), new GUIContent("Item"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("targetTrigger"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("attachTransform"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Attach Behaviour", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("smoothAttach"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("parentToAttach"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("moveSpeed"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("rotateSpeed"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Detection", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("positionTolerance"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("angleTolerance"));

        DrawNextGuid();
    }

    void DrawEvent()
    {
        EditorGUILayout.LabelField("Event", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            stepProp.FindPropertyRelative("onEnter"),
            new GUIContent("On Enter Events")
        );
        EditorGUILayout.PropertyField(
            stepProp.FindPropertyRelative("waitSeconds"),
            new GUIContent("Wait Seconds Before Next")
        );

        DrawNextGuid();
    }

    void DrawGroup()
    {
        EditorGUILayout.LabelField("Group", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Group runs all nested steps together.\n" +
            "Nested routing (nextGuid / branch guids) is ignored; only the Group's Next is used.\n" +
            "Tip: Avoid multiple click-driven steps (Cue Cards / Question) in the same group.",
            MessageType.Info);

        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("completeWhen"));
        var modeProp = stepProp.FindPropertyRelative("completeWhen");
        var mode = modeProp != null ? modeProp.enumValueIndex : 0;
        if (mode == (int)GroupStep.CompleteWhen.AfterSeconds)
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("afterSeconds"));
        else if (mode == (int)GroupStep.CompleteWhen.WhenSpecificStepCompletes)
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("specificStepGuid"));

        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("stopOthersOnComplete"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Nested Steps", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("steps"), includeChildren: true);

        DrawNextGuid();
    }


    void DrawCorrectWrongGuids()
    {
        var corr = stepProp.FindPropertyRelative("correctNextGuid");
        var wrong = stepProp.FindPropertyRelative("wrongNextGuid");
        if (corr == null || wrong == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Branches", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Correct →", GUILayout.Width(70));
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(corr.stringValue) ? "(next in list)" : corr.stringValue, GUILayout.Height(18));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) corr.stringValue = "";
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Wrong →", GUILayout.Width(70));
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(wrong.stringValue) ? "(next in list)" : wrong.stringValue, GUILayout.Height(18));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) wrong.stringValue = "";
        }
    }


    void DrawNextGuid()
    {
        // Render “Next Guid” preview/readout so authors can confirm wiring without leaving the popup
        var ng = stepProp.FindPropertyRelative("nextGuid");
        if (ng == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Next", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(ng.stringValue) ? "(next in list)" : ng.stringValue, GUILayout.Height(18));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) ng.stringValue = "";
        }
    }
}
#endif
