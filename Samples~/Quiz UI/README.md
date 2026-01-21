# Quiz UI (TextMeshPro) Sample

This sample is intentionally lightweight: it guides you to install the default Quiz UI into **your current scene** and wire it correctly.

## Install in your scene (recommended)

1. Open **`Pi tech/DevKit`**
2. Go to **Guided Setup**
3. In **Quiz (optional)** click **Install Quiz UI + Wire Panels**

That will:
- Instantiate the **default Quiz UI prefabs shipped inside the DevKit package** into your scene under `--- UI ---`
- Wire them into your `Pitech.XR.Scenario.SceneManager`:
  - `quizPanel`
  - `quizResultsPanel`

Note: Guided Setup does **not** assign `defaultQuiz` automatically (each scene can use different quiz assets). Create a `QuizAsset` and assign it in your SceneManager, or override per `QuizStep` / `QuizResultsStep`.

## Manual alternative (if you want full control)

1. Locate the prefabs in the package:
   - `Packages/com.pitech.xr.devkit/Editor/Quiz.Editor/DefaultUIPrefabs/QuizPanel.prefab`
   - `Packages/com.pitech.xr.devkit/Editor/Quiz.Editor/DefaultUIPrefabs/QuizResultsPanel.prefab`
2. Drag them into your Canvas
3. Assign them to `SceneManager.quizPanel` / `SceneManager.quizResultsPanel`


