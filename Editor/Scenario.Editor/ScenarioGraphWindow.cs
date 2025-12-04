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
    Scenario scenario;
    ScenarioGraphView view;
    readonly Dictionary<string, StepNode> nodes = new();
    Vector2 mouseWorld;

    // NEW: guard to avoid wiping links while rebuilding UI
    bool _isLoading;

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

        var frame = new UIEButton(() => view?.FrameAll()) { text = "Frame All" };
        frame.style.marginLeft = 6;
        bar.Add(frame);

        rootVisualElement.Add(bar);

        view = new ScenarioGraphView();
        view.OnContextAdd += ShowCreateMenu;
        view.OnMouseWorld += p => mouseWorld = p;
        rootVisualElement.Add(view);
    }

    void OnDisable()
    {
        view?.RemoveFromHierarchy();
        nodes.Clear();
    }

    // ---------- load graph from component ----------
    void Load(Scenario sc)
    {
        scenario = sc;
        titleContent = new GUIContent(sc ? $"Scenario Graph • {sc.gameObject.name}" : "Scenario Graph");

        _isLoading = true; // begin guarded load

        view.ClearGraph();
        nodes.Clear();
        if (!scenario || scenario.steps == null)
        {
            _isLoading = false;
            return;
        }

        // ensure guids
        foreach (var s in scenario.steps)
            if (s != null && string.IsNullOrEmpty(s.guid))
                s.guid = Guid.NewGuid().ToString();

        // nodes
        for (int i = 0; i < scenario.steps.Count; i++)
        {
            var s = scenario.steps[i];
            if (s == null) continue;

            var stepRef = s;         // optional, but fine
            int indexCopy = i;       // *** this is the important one ***

            var node = new StepNode(scenario, stepRef, indexCopy, view, () =>
            {
                if (!_isLoading) RebuildLinksFromGraph();
            });

            var pos = s.graphPos == default ? new Vector2(80 + 340 * i, 220) : s.graphPos;
            node.SetPosition(new Rect(pos, new Vector2(360, node.GetHeight())));
            view.AddElement(node);
            nodes[stepRef.guid] = node;

            node.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Delete Step", _ =>
                {
                    DeleteStep(indexCopy);   // <- delete by index
                });
            }));
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
                    var g = q.choices[c]?.nextGuid;
                    if (!string.IsNullOrEmpty(g) && src.outChoices != null && c < src.outChoices.Count)
                        Connect(src.outChoices[c], g);
                }
            }
            if (s is InsertStep ins && !string.IsNullOrEmpty(ins.nextGuid) && nodes.TryGetValue(ins.guid, out var insNode))
                Connect(insNode.outNext, ins.nextGuid);
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


        _isLoading = false; // end guarded load

        view.FrameAll();

        view.graphViewChanged = change =>
        {
            // positions
            if (change.movedElements != null)
                foreach (var el in change.movedElements)
                    if (el is StepNode sn)
                    {
                        sn.step.graphPos = sn.GetPosition().position;
                        Dirty(scenario, "Move Node");
                    }

            // edges created/removed
            if (change.edgesToCreate != null || change.elementsToRemove != null)
                RebuildLinksFromGraph();

            return change;
        };
    }

    void RebuildLinksFromGraph()
    {
        if (!scenario || _isLoading) return;

        // clear all links
        foreach (var st in scenario.steps)
        {
            if (st is TimelineStep tl)
            {
                tl.nextGuid = "";
            }
            else if (st is CueCardsStep cc)
            {
                cc.nextGuid = "";
            }
            else if (st is QuestionStep q && q.choices != null)
            {
                foreach (var ch in q.choices)
                    if (ch != null) ch.nextGuid = "";
            }

            // Selection routes (Correct/Wrong)
            if (st is SelectionStep sl)
            {
                sl.correctNextGuid = "";
                sl.wrongNextGuid = "";
            }

            // NEW: Insert route
            if (st is InsertStep ins)
            {
                ins.nextGuid = "";
            }
        }

        // re-assign from visible edges
        foreach (var e in view.graphElements.ToList().OfType<Edge>())
        {
            var outMeta = PortMeta.From(e.output);
            var inNode = e.input?.node as StepNode;
            if (outMeta == null || inNode == null) continue;

            if (outMeta.owner is TimelineStep otl)
                otl.nextGuid = inNode.step.guid;
            else if (outMeta.owner is CueCardsStep occ)
                occ.nextGuid = inNode.step.guid;
            else if (outMeta.owner is QuestionStep oq && outMeta.choiceIndex >= 0 &&
                     oq.choices != null && outMeta.choiceIndex < oq.choices.Count)
                oq.choices[outMeta.choiceIndex].nextGuid = inNode.step.guid;
            else if (outMeta.owner is SelectionStep osl)
            {
                // -2 => Correct, -3 => Wrong (όπως ήδη έχεις)
                if (outMeta.choiceIndex == -2) osl.correctNextGuid = inNode.step.guid;
                else if (outMeta.choiceIndex == -3) osl.wrongNextGuid = inNode.step.guid;
            }
            // NEW: InsertStep
            else if (outMeta.owner is InsertStep oins)
            {
                oins.nextGuid = inNode.step.guid;
            }
        }

        Dirty(scenario, "Route Change");
    }


    void DeleteStep(int index)
    {
        if (!scenario || scenario.steps == null) return;
        if (index < 0 || index >= scenario.steps.Count) return;

        var s = scenario.steps[index];

        if (!EditorUtility.DisplayDialog(
                "Delete Step",
                $"Delete “{s.Kind}” step ({index:00})?",
                "Delete",
                "Cancel"))
            return;

        Dirty(scenario, "Delete Step");

        scenario.steps.RemoveAt(index);   // <- index based, no reference tricks
        Load(scenario);                   // rebuild graph from the real data
    }


    void Connect(Port src, string dstGuid)
    {
        if (src == null) return;
        if (!nodes.TryGetValue(dstGuid, out var dstNode)) return;
        var edge = src.ConnectTo(dstNode.inPort);
        view.AddElement(edge);
    }

    // ---------- context menu ----------
    void ShowCreateMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Add/Timeline", _ => CreateStep(typeof(TimelineStep)));
        evt.menu.AppendAction("Add/Cue Cards", _ => CreateStep(typeof(CueCardsStep)));
        evt.menu.AppendAction("Add/Question", _ => CreateStep(typeof(QuestionStep)));
        evt.menu.AppendAction("Add/Selection", _ => CreateStep(typeof(SelectionStep)));
        evt.menu.AppendAction("Add/Insert", _ => CreateStep(typeof(InsertStep)));
    }

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

        public ScenarioGraphView()
        {
            style.flexGrow = 1;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground() { name = "grid" });
            this.Q("grid")?.StretchToParentSize();

            this.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if (evt.target is GraphElement) return;
                OnContextAdd?.Invoke(evt);
            }));

            RegisterCallback<MouseMoveEvent>(e =>
            {
                var p = contentViewContainer.WorldToLocal(e.mousePosition);
                OnMouseWorld?.Invoke(p);
            });
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
    }

    // ======== metadata kept on ports ========
    sealed class PortMeta
    {
        public Step owner;
        public int choiceIndex; // -1 => "Next"
        public static PortMeta From(Port p) => p?.userData as PortMeta;
        public static void Set(Port p, Step owner, int choiceIndex) => p.userData = new PortMeta { owner = owner, choiceIndex = choiceIndex };
    }

    // ======== Node (with “Edit…” button & working edge connectors) ========
    class StepNode : Node
    {
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

        static readonly ECListener edgeListener = new ECListener();

        public StepNode(Scenario sc, Step s, int idx, ScenarioGraphView gv, Action rebuildLinks)
        {
            scenario = sc; step = s; index = idx; graph = gv; rebuild = rebuildLinks;

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

            // In
            inPort = MakePort(Direction.Input, Port.Capacity.Multi, "In", -1);
            inputContainer.Add(inPort);

            // top-right small “Edit…” button
            var headerButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var editBtn = new UIEButton(() =>
            {
                if (step is TimelineStep tl) StepEditWindow.OpenTimeline(scenario, tl);
                else if (step is CueCardsStep cc) StepEditWindow.OpenCueCards(scenario, cc);
                else if (step is QuestionStep q) StepEditWindow.OpenQuestion(scenario, q, rebuild);
                else if (step is SelectionStep se) StepEditWindow.OpenSelection(scenario, se);
                else if (step is InsertStep ins) StepEditWindow.OpenInsert(scenario, ins);
            })
            { text = "Edit…" };

            editBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            editBtn.style.marginLeft = 6;
            titleContainer.Add(editBtn);

            // quick inline fields foldout (kept for speed)
            var fold = new Foldout { text = "Details", value = false };
            mainContainer.Add(fold);

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




            RefreshExpandedState();
            RefreshPorts();
        }

        Port MakePort(Direction dir, Port.Capacity cap, string label, int choiceIndex)
        {
            var p = InstantiatePort(Orientation.Horizontal, dir, cap, typeof(bool));
            p.portName = label;
            PortMeta.Set(p, step, choiceIndex);

            // add the edge connector manipulator
            var connector = new EdgeConnector<Edge>(edgeListener);
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

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            step.graphPos = newPos.position;
            Dirty(scenario, "Move Node");
        }

        public float GetHeight()
        {
            if (step is QuestionStep q) return 220 + 22 * Mathf.Max(1, q.choices?.Count ?? 0);
            if (step is SelectionStep) return 220;
            return 170;
        }


        sealed class ECListener : IEdgeConnectorListener
        {
            public void OnDropOutsidePort(Edge edge, Vector2 pos) { }
            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (edge?.input == null || edge.output == null) return;
                graphView.AddElement(edge);
            }
        }
    }
}

// =================== STEP EDIT WINDOWS (UI shown when you click “Edit…”) ===================

sealed class StepEditWindow : EditorWindow
{
    Scenario scenario;
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

    static void Open(Scenario sc, string guid, string title, Action<StepEditWindow> draw, Action afterApply = null)
    {
        var w = CreateInstance<StepEditWindow>();
        w.scenario = sc;
        w.stepGuid = guid;
        w.minSize = new Vector2(460, 360);
        w.titleContent = new GUIContent($"{title} • Step");
        w.onAfterApply = afterApply;
        w.ShowUtility();
        w.position = new Rect(GUIUtility.GUIToScreenPoint(Event.current.mousePosition) + new Vector2(8, 8), w.minSize);
        w.Init(draw);
    }

    Action<StepEditWindow> drawer;

    void Init(Action<StepEditWindow> d)
    {
        drawer = d;
        so = new SerializedObject(scenario);
        stepProp = FindStepProperty(so, stepGuid);
    }

    static SerializedProperty FindStepProperty(SerializedObject so, string guid)
    {
        var steps = so.FindProperty("steps");
        if (steps == null) return null;
        for (int i = 0; i < steps.arraySize; i++)
        {
            var el = steps.GetArrayElementAtIndex(i);
            var g = el.FindPropertyRelative("guid");
            if (g != null && g.stringValue == guid) return el;
        }
        return null;
    }

    void OnGUI()
    {
        if (!scenario || so == null || stepProp == null)
        {
            EditorGUILayout.HelpBox("Step not found (maybe removed).", MessageType.Warning);
            if (GUILayout.Button("Close")) Close();
            return;
        }

        so.Update();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(2);
        drawer?.Invoke(this);
        EditorGUILayout.EndScrollView();

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
