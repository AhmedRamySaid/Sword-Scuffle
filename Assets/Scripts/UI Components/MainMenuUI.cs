using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI_Components
{
    public class MainMenuGenerator : MonoBehaviour
    {
        public Canvas canvas;
        private GameObject usernameField;
        private GameObject hostButton;
        private GameObject joinButton;

        void Start()
        {
            GenerateMainMenu();
        }

        void GenerateMainMenu()
        {
            // Create Canvas
            CreateCanvas();

            // Create Background
            CreateBackground();

            // Create Animated Particles
            CreateFloatingParticles();

            // Create Title
            CreateTitle();

            // Create Menu Panel
            CreateMenuPanel();

            // Create Username Input Field (Top-Left)
            CreateUsernameField();

            // Create Centered Buttons
            CreateHostButton();
            CreateJoinButton();

            // Create Footer Text
            CreateFooter();
        }

        void CreateCanvas()
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        void CreateBackground()
        {
            // Main background with gradient effect
            GameObject bgObj = new GameObject("Background");
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.SetParent(canvas.transform, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.08f, 0.12f, 0.18f, 1f); // Dark blue-gray

            // Create gradient overlay effect with multiple panels
            CreateGradientOverlay(canvas.transform);

            // Add decorative corner elements
            CreateCornerDecoration(canvas.transform, new Vector2(0, 1), new Vector2(-45, 45)); // Top-left
            CreateCornerDecoration(canvas.transform, new Vector2(1, 1), new Vector2(45, 45)); // Top-right
            CreateCornerDecoration(canvas.transform, new Vector2(0, 0), new Vector2(-45, -45)); // Bottom-left
            CreateCornerDecoration(canvas.transform, new Vector2(1, 0), new Vector2(45, -45)); // Bottom-right
        }

        void CreateGradientOverlay(Transform parent)
        {
            // Top gradient
            GameObject topGradient = new GameObject("TopGradient");
            RectTransform topRect = topGradient.AddComponent<RectTransform>();
            topRect.SetParent(parent, false);
            topRect.anchorMin = new Vector2(0, 0.7f);
            topRect.anchorMax = new Vector2(1, 1);
            topRect.offsetMin = Vector2.zero;
            topRect.offsetMax = Vector2.zero;

            Image topImage = topGradient.AddComponent<Image>();
            topImage.color = new Color(0.2f, 0.3f, 0.5f, 0.3f);

            // Bottom gradient
            GameObject bottomGradient = new GameObject("BottomGradient");
            RectTransform bottomRect = bottomGradient.AddComponent<RectTransform>();
            bottomRect.SetParent(parent, false);
            bottomRect.anchorMin = new Vector2(0, 0);
            bottomRect.anchorMax = new Vector2(1, 0.3f);
            bottomRect.offsetMin = Vector2.zero;
            bottomRect.offsetMax = Vector2.zero;

            Image bottomImage = bottomGradient.AddComponent<Image>();
            bottomImage.color = new Color(0.15f, 0.1f, 0.25f, 0.4f);
        }

        void CreateCornerDecoration(Transform parent, Vector2 anchor, Vector2 rotation)
        {
            GameObject corner = new GameObject("CornerDecoration");
            RectTransform cornerRect = corner.AddComponent<RectTransform>();
            cornerRect.SetParent(parent, false);
            cornerRect.anchorMin = anchor;
            cornerRect.anchorMax = anchor;
            cornerRect.pivot = anchor;
            cornerRect.anchoredPosition = Vector2.zero;
            cornerRect.sizeDelta = new Vector2(200, 200);
            cornerRect.rotation = Quaternion.Euler(0, 0, rotation.x);

            Image cornerImage = corner.AddComponent<Image>();
            cornerImage.color = new Color(0.3f, 0.5f, 0.8f, 0.15f);
        }

        void CreateFloatingParticles()
        {
            GameObject particleContainer = new GameObject("FloatingParticles");
            RectTransform containerRect = particleContainer.AddComponent<RectTransform>();
            containerRect.SetParent(canvas.transform, false);
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            // Create multiple floating particles
            for (int i = 0; i < 15; i++)
            {
                GameObject particle = new GameObject($"Particle_{i}");
                RectTransform particleRect = particle.AddComponent<RectTransform>();
                particleRect.SetParent(containerRect, false);

                // Random position
                particleRect.anchorMin = new Vector2(0.5f, 0.5f);
                particleRect.anchorMax = new Vector2(0.5f, 0.5f);
                particleRect.pivot = new Vector2(0.5f, 0.5f);
                particleRect.anchoredPosition = new Vector2(
                    Random.Range(-800f, 800f),
                    Random.Range(-400f, 400f)
                );

                // Random size
                float size = Random.Range(3f, 8f);
                particleRect.sizeDelta = new Vector2(size, size);

                Image particleImage = particle.AddComponent<Image>();
                particleImage.color = new Color(0.5f, 0.7f, 1f, Random.Range(0.2f, 0.5f));

                // Add floating animation
                FloatingAnimation floater = particle.AddComponent<FloatingAnimation>();
                floater.speed = Random.Range(10f, 30f);
                floater.amplitude = Random.Range(50f, 150f);
                floater.offset = Random.Range(0f, 2f * Mathf.PI);
            }
        }

        void CreateTitle()
        {
            GameObject titleObj = new GameObject("GameTitle");
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.SetParent(canvas.transform, false);
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 250);
            titleRect.sizeDelta = new Vector2(800, 100);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Sword Scuffle";
            titleText.fontSize = 60;
            titleText.color = new Color(0.9f, 0.95f, 1f, 1f);
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;

            // Add outline
            titleText.outlineWidth = 0.2f;
            titleText.outlineColor = new Color(0.2f, 0.4f, 0.7f, 1f);

            // Add glow animation
            GlowAnimation glow = titleObj.AddComponent<GlowAnimation>();
        }

        void CreateMenuPanel()
        {
            GameObject panel = new GameObject("MenuPanel");
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.SetParent(canvas.transform, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0, -30);
            panelRect.sizeDelta = new Vector2(450, 350);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.15f, 0.2f, 0.85f);

            // Add subtle shadow/border
            GameObject shadow = new GameObject("PanelShadow");
            RectTransform shadowRect = shadow.AddComponent<RectTransform>();
            shadowRect.SetParent(panelRect, false);
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.offsetMin = new Vector2(-5, -5);
            shadowRect.offsetMax = new Vector2(5, 5);
            shadowRect.SetAsFirstSibling();

            Image shadowImage = shadow.AddComponent<Image>();
            shadowImage.color = new Color(0, 0, 0, 0.5f);
        }

        void CreateUsernameField()
        {
            // Parent container for the input field
            usernameField = new GameObject("UsernameField");
            RectTransform fieldRect = usernameField.AddComponent<RectTransform>();
            fieldRect.SetParent(canvas.transform, false);

            // Position in top-left corner
            fieldRect.anchorMin = new Vector2(0, 1);
            fieldRect.anchorMax = new Vector2(0, 1);
            fieldRect.pivot = new Vector2(0, 1);
            fieldRect.anchoredPosition = new Vector2(30, -30);
            fieldRect.sizeDelta = new Vector2(300, 50);

            // Background image with rounded look
            Image fieldImage = usernameField.AddComponent<Image>();
            fieldImage.color = new Color(0.27f, 0.23f, 0.45f, 1f);
            fieldImage.raycastTarget = true;

            // Add border
            GameObject border = new GameObject("Border");
            RectTransform borderRect = border.AddComponent<RectTransform>();
            borderRect.SetParent(fieldRect, false);
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2, -2);
            borderRect.offsetMax = new Vector2(2, 2);
            borderRect.SetAsFirstSibling();

            Image borderImage = border.AddComponent<Image>();
            borderImage.color = new Color(0.4f, 0.6f, 0.9f, 0.5f);

            // Add TMP_InputField component
            TMP_InputField inputField = usernameField.AddComponent<TMP_InputField>();

            // Create Text Area (child object for text viewport)
            GameObject textArea = new GameObject("Text Area");
            RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.SetParent(fieldRect, false);
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 6);
            textAreaRect.offsetMax = new Vector2(-10, -6);

            // Add mask to text area
            textArea.AddComponent<RectMask2D>();

            // Create Placeholder Text
            GameObject placeholder = new GameObject("Placeholder");
            RectTransform placeholderRect = placeholder.AddComponent<RectTransform>();
            placeholderRect.SetParent(textAreaRect, false);
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            TextMeshProUGUI placeholderText = placeholder.AddComponent<TextMeshProUGUI>();
            placeholderText.text = "Enter Username...";
            placeholderText.fontSize = 18;
            placeholderText.color = new Color(1f, 1f, 1f, 0.8f);
            placeholderText.alignment = TextAlignmentOptions.Left;
            placeholderText.fontStyle = FontStyles.Italic;

            // Create Input Text
            GameObject inputText = new GameObject("Text");
            RectTransform inputTextRect = inputText.AddComponent<RectTransform>();
            inputTextRect.SetParent(textAreaRect, false);
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmpInputText = inputText.AddComponent<TextMeshProUGUI>();
            tmpInputText.text = "";
            tmpInputText.fontSize = 18;
            tmpInputText.color = new Color(0.95f, 0.95f, 1f, 1f);
            tmpInputText.alignment = TextAlignmentOptions.Left;

            // Assign references to InputField
            inputField.textViewport = textAreaRect;
            inputField.textComponent = tmpInputText;
            inputField.placeholder = placeholderText;
            inputField.characterLimit = 20;
        }

        void CreateHostButton()
        {
            hostButton = CreateButton("HostButton", "Host Game", new Vector2(0, 20),
                new Color(0.3f, 0.6f, 0.95f, 1f), new Color(0.4f, 0.7f, 1f, 1f));

            Button btn = hostButton.GetComponent<Button>();
            btn.onClick.AddListener(OnHostGameClicked);
        }

        void CreateJoinButton()
        {
            joinButton = CreateButton("JoinButton", "Join Game", new Vector2(0, -80),
                new Color(0.35f, 0.85f, 0.55f, 1f), new Color(0.45f, 0.95f, 0.65f, 1f));

            Button btn = joinButton.GetComponent<Button>();
            btn.onClick.AddListener(OnJoinGameClicked);
        }

        GameObject CreateButton(string btnName, string label, Vector2 position, Color normalColor, Color hoverColor)
        {
            // Create button GameObject
            GameObject buttonObj = new GameObject(btnName);
            RectTransform btnRect = buttonObj.AddComponent<RectTransform>();
            btnRect.SetParent(canvas.transform, false);

            // Center the button
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = position;
            btnRect.sizeDelta = new Vector2(350, 65);

            // Button shadow
            GameObject shadow = new GameObject("Shadow");
            RectTransform shadowRect = shadow.AddComponent<RectTransform>();
            shadowRect.SetParent(btnRect, false);
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.offsetMin = new Vector2(-3, -3);
            shadowRect.offsetMax = new Vector2(3, 3);
            shadowRect.SetAsFirstSibling();

            Image shadowImage = shadow.AddComponent<Image>();
            shadowImage.color = new Color(0, 0, 0, 0.4f);

            // Add Image component
            Image btnImage = buttonObj.AddComponent<Image>();
            btnImage.color = normalColor;

            // Add Button component
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = btnImage;

            // Set button colors for hover effect
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = hoverColor;
            colors.pressedColor = new Color(normalColor.r * 0.7f, normalColor.g * 0.7f, normalColor.b * 0.7f, 1f);
            colors.selectedColor = normalColor;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.15f;
            button.colors = colors;

            // Create button text
            GameObject textObj = new GameObject("Text");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(btnRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = label;
            buttonText.fontSize = 26;
            buttonText.color = Color.white;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.fontStyle = FontStyles.Bold;
            buttonText.outlineWidth = 0.15f;
            buttonText.outlineColor = new Color(0, 0, 0, 0.5f);

            return buttonObj;
        }

        void CreateFooter()
        {
            GameObject footerObj = new GameObject("Footer");
            RectTransform footerRect = footerObj.AddComponent<RectTransform>();
            footerRect.SetParent(canvas.transform, false);
            footerRect.anchorMin = new Vector2(0.5f, 0);
            footerRect.anchorMax = new Vector2(0.5f, 0);
            footerRect.pivot = new Vector2(0.5f, 0);
            footerRect.anchoredPosition = new Vector2(0, 20);
            footerRect.sizeDelta = new Vector2(400, 30);

            TextMeshProUGUI footerText = footerObj.AddComponent<TextMeshProUGUI>();
            footerText.text = "v1.0.0 | Ready to Play";
            footerText.fontSize = 16;
            footerText.color = new Color(0.6f, 0.7f, 0.8f, 0.7f);
            footerText.alignment = TextAlignmentOptions.Center;
        }

        // Placeholder button functions
        void OnHostGameClicked()
        {
            string username = GetUsername();
            Debug.Log($"Host Game clicked! Username: {username}");

            // TODO: Implement host game logic here
            // Example: NetworkManager.Singleton.StartHost();
        }

        void OnJoinGameClicked()
        {
            string username = GetUsername();
            Debug.Log($"Join Game clicked! Username: {username}");

            // TODO: Implement join game logic here
            // Example: NetworkManager.Singleton.StartClient();
        }

        string GetUsername()
        {
            if (usernameField != null)
            {
                TMP_InputField inputField = usernameField.GetComponent<TMP_InputField>();
                if (inputField != null && !string.IsNullOrEmpty(inputField.text))
                {
                    return inputField.text;
                }
            }

            return "Player";
        }
    }
}