using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pitech.XR.ContentDelivery
{
    /// <summary>
    /// Runtime content-delivery UI presenter.
    /// Bind this to your own Canvas/UI objects (TMP texts, buttons, slider/images).
    /// No UI is drawn from code.
    /// </summary>
    [AddComponentMenu("Pi tech XR/Content Delivery/Content Delivery Status Overlay")]
    public sealed class ContentDeliveryStatusOverlay : MonoBehaviour
    {
        [Serializable]
        public sealed class RuntimeUiOverride
        {
            [Header("Status Titles")]
            public string checkingTitle = "Checking Content";
            public string newContentTitle = "New Content Found";
            public string loadingTitle = "Loading Lab";
            public string downloadingTitle = "Downloading Content";
            public string issueTitle = "Content Delivery Issue";
            public string launchFailedTitle = "Launch Failed";
            public string contentReadyTitle = "Content Ready";

            [Header("Status Messages")]
            [Tooltip("Supports {labId}, {resolvedVersionId}, {size}. Used when resolvedVersionId exists.")]
            [TextArea] public string updateAvailableWithVersionMessageTemplate =
                "New content is available for {labId} (version {resolvedVersionId}).\nDownload {size} now?";

            [Tooltip("Supports {labId}, {size}. Used when resolvedVersionId is missing.")]
            [TextArea] public string updateAvailableMessageTemplate =
                "New content is available for {labId}.\nDownload {size} now?";

            [TextArea] public string checkingMessage = "Resolving remote catalog...";
            [TextArea] public string loadingMessage = "Preparing immersive content...";
            [TextArea] public string downloadingMessage = "Downloading required lab data...";
            [TextArea] public string launchFailedMessage = "No content is available for this lab.";
            [TextArea] public string contentReadyMessage = "Launching experience...";
            [TextArea] public string networkRequiredMessage = "Network connection is required to download this lab content.";

            [Header("Error Templates")]
            [Tooltip("Supports {error}.")]
            [TextArea] public string remoteCatalogErrorTemplate = "Could not load remote catalog.\n{error}";

            [Tooltip("Supports {error}.")]
            [TextArea] public string updateCheckErrorTemplate = "Could not check content updates.\n{error}";

            [Tooltip("Supports {error}.")]
            [TextArea] public string downloadFailedErrorTemplate = "Download failed.\n{error}";

            [Tooltip("Supports {addressKey}, {error}.")]
            [TextArea] public string loadFailedErrorTemplate = "Could not load lab content key '{addressKey}'.\n{error}";

            [Header("Buttons")]
            public string downloadButton = "Download";
            public string retryButton = "Retry";
            public string useCachedButton = "Use Cached";
            public string useLocalButton = "Use Local";
            public string cancelButton = "Cancel";
        }

        [Header("Runtime UI Override (optional)")]
        [Tooltip("Customize runtime dialog titles, messages, and button labels. Leave fields empty to keep defaults.")]
        public RuntimeUiOverride runtimeUiOverride = new RuntimeUiOverride();

        [Header("Root")]
        [Tooltip("Root object for this panel. If empty, current GameObject is used.")]
        public GameObject root;

        [Header("Behavior")]
        public bool visibleOnStart;
        public bool hideRootWhenHidden = true;
        public bool clearTextsOnHide;
        public bool warnIfBindingsMissing = true;

        [Header("Text")]
        public TMP_Text titleText;
        public TMP_Text messageText;
        public TMP_Text progressText;

        [Header("Containers (optional)")]
        [Tooltip("Shown when progress is visible.")]
        public GameObject progressContainer;

        [Tooltip("Shown when prompt buttons are visible.")]
        public GameObject promptContainer;

        [Header("Progress")]
        [Tooltip("Optional progress slider (0..1).")]
        public Slider progressSlider;

        [Tooltip("Optional progress image fillAmount (0..1).")]
        public Image progressFillImage;

        [Header("Prompt Buttons")]
        public Button primaryButton;
        public Button secondaryButton;
        public TMP_Text primaryButtonLabelText;
        public TMP_Text secondaryButtonLabelText;

        private Action primaryAction;
        private Action secondaryAction;
        private bool listenersBound;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            BindButtonListeners();
            ApplyVisible(visibleOnStart);
            if (!visibleOnStart)
            {
                SetPromptVisible(false);
                SetProgressVisible(false);
            }
        }

        private void OnEnable()
        {
            BindButtonListeners();
        }

        private void OnDisable()
        {
            UnbindButtonListeners();
        }

        private void OnDestroy()
        {
            UnbindButtonListeners();
            primaryAction = null;
            secondaryAction = null;
        }

        public bool IsVisible
        {
            get
            {
                GameObject target = root != null ? root : gameObject;
                return target != null && target.activeSelf;
            }
        }

        public void ShowStatus(string newTitle, string newMessage)
        {
            ApplyVisible(true);
            SetTitle(newTitle);
            SetMessage(newMessage);
            SetProgressText(string.Empty);
            SetProgressVisible(false);
            SetPromptVisible(false);
        }

        public void ShowProgress(string newTitle, string newMessage, float progress, string newProgressText)
        {
            ApplyVisible(true);
            SetTitle(newTitle);
            SetMessage(newMessage);
            SetProgressValue(progress);
            SetProgressText(newProgressText ?? string.Empty);
            SetProgressVisible(true);
            SetPromptVisible(false);
        }

        public IEnumerator WaitForChoice(
            string newTitle,
            string newMessage,
            string primaryLabel,
            string secondaryLabel,
            Action<int> onChoice)
        {
            int selected = 0;

            ShowStatus(newTitle, newMessage);
            SetProgressVisible(false);
            SetPromptVisible(true);
            SetButtonLabel(primaryButtonLabelText, primaryButton, primaryLabel, "OK");
            SetButtonLabel(secondaryButtonLabelText, secondaryButton, secondaryLabel, "Cancel");
            primaryAction = () => selected = 1;
            secondaryAction = () => selected = 2;

            bool hasInteractiveButton = (primaryButton != null && primaryButton.gameObject.activeInHierarchy) ||
                                        (secondaryButton != null && secondaryButton.gameObject.activeInHierarchy);
            if (!hasInteractiveButton)
            {
                if (warnIfBindingsMissing)
                {
                    Debug.LogWarning(
                        "[ContentDelivery] WaitForChoice called but prompt buttons are not bound. Defaulting to primary choice.",
                        this);
                }
                selected = 1;
            }

            while (selected == 0)
            {
                yield return null;
            }

            SetPromptVisible(false);
            primaryAction = null;
            secondaryAction = null;
            onChoice?.Invoke(selected);
        }

        public void Hide()
        {
            SetPromptVisible(false);
            SetProgressVisible(false);
            SetProgressText(string.Empty);
            primaryAction = null;
            secondaryAction = null;
            if (clearTextsOnHide)
            {
                SetTitle(string.Empty);
                SetMessage(string.Empty);
            }
            ApplyVisible(false);
        }

        private void BindButtonListeners()
        {
            if (listenersBound)
            {
                return;
            }

            if (primaryButton != null)
            {
                primaryButton.onClick.AddListener(OnPrimaryButtonClicked);
            }

            if (secondaryButton != null)
            {
                secondaryButton.onClick.AddListener(OnSecondaryButtonClicked);
            }

            listenersBound = true;
        }

        private void UnbindButtonListeners()
        {
            if (!listenersBound)
            {
                return;
            }

            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveListener(OnPrimaryButtonClicked);
            }

            if (secondaryButton != null)
            {
                secondaryButton.onClick.RemoveListener(OnSecondaryButtonClicked);
            }

            listenersBound = false;
        }

        private void OnPrimaryButtonClicked()
        {
            primaryAction?.Invoke();
        }

        private void OnSecondaryButtonClicked()
        {
            secondaryAction?.Invoke();
        }

        private void ApplyVisible(bool isVisible)
        {
            GameObject target = root != null ? root : gameObject;
            if (target == null)
            {
                return;
            }

            if (hideRootWhenHidden || isVisible)
            {
                target.SetActive(isVisible);
            }
        }

        private void SetPromptVisible(bool isVisible)
        {
            if (promptContainer != null)
            {
                promptContainer.SetActive(isVisible);
            }
            else
            {
                if (primaryButton != null)
                {
                    primaryButton.gameObject.SetActive(isVisible);
                }

                if (secondaryButton != null)
                {
                    secondaryButton.gameObject.SetActive(isVisible);
                }
            }
        }

        private void SetProgressVisible(bool isVisible)
        {
            if (progressContainer != null)
            {
                progressContainer.SetActive(isVisible);
            }
            else
            {
                if (progressSlider != null)
                {
                    progressSlider.gameObject.SetActive(isVisible);
                }

                if (progressFillImage != null)
                {
                    progressFillImage.gameObject.SetActive(isVisible);
                }

                if (progressText != null)
                {
                    progressText.gameObject.SetActive(isVisible);
                }
            }
        }

        private void SetTitle(string value)
        {
            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            }
        }

        private void SetMessage(string value)
        {
            if (messageText != null)
            {
                messageText.text = value ?? string.Empty;
            }
        }

        private void SetProgressText(string value)
        {
            if (progressText != null)
            {
                progressText.text = value ?? string.Empty;
            }
        }

        private void SetProgressValue(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            if (progressSlider != null)
            {
                progressSlider.normalizedValue = clamped;
            }

            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = clamped;
            }
        }

        private static void SetButtonLabel(TMP_Text labelText, Button button, string value, string fallback)
        {
            string finalText = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

            if (labelText != null)
            {
                labelText.text = finalText;
                return;
            }

            if (button != null)
            {
                TMP_Text nested = button.GetComponentInChildren<TMP_Text>(true);
                if (nested != null)
                {
                    nested.text = finalText;
                }
            }
        }
    }
}
