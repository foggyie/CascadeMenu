using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CascadeMenu.UI
{
    /// <summary>
    /// Virtual QWERTY keyboard for VR and desktop text input.
    /// Provides a full keyboard with shift, backspace, and enter keys.
    /// </summary>
    public class VirtualKeyboard : MonoBehaviour
    {
        private static VirtualKeyboard _instance;
        
        private Canvas _canvas;
        private GameObject _root;
        private TextMeshProUGUI _displayText;
        private string _currentText = "";
        private bool _shiftPressed;
        private Action<string> _onSubmit;
        private Action _onCancel;

        private readonly string[] _keyRows = new string[]
        {
            "1234567890",
            "QWERTYUIOP",
            "ASDFGHJKL",
            "ZXCVBNM"
        };

        public static VirtualKeyboard Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject obj = new GameObject("VirtualKeyboard");
                    DontDestroyOnLoad(obj);
                    _instance = obj.AddComponent<VirtualKeyboard>();
                    _instance.BuildKeyboard();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Shows the keyboard and calls onSubmit when user presses Enter.
        /// </summary>
        public static void Show(string initialText, string prompt, Action<string> onSubmit, Action onCancel = null)
        {
            Instance._currentText = initialText ?? "";
            Instance._onSubmit = onSubmit;
            Instance._onCancel = onCancel;
            Instance.UpdateDisplay();
            Instance._root.SetActive(true);
        }

        public static void Hide()
        {
            if (_instance != null && _instance._root != null)
            {
                _instance._root.SetActive(false);
            }
        }

        private void BuildKeyboard()
        {
            // Create canvas
            _root = new GameObject("KeyboardRoot");
            _root.transform.SetParent(transform);
            
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 1000;

            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            GraphicRaycaster raycaster = _root.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = _root.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(800, 400);
            canvasRect.localScale = Vector3.one * 0.001f;

            // Background panel
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(_root.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            // Display text
            GameObject displayObj = new GameObject("Display");
            displayObj.transform.SetParent(_root.transform, false);
            RectTransform displayRect = displayObj.AddComponent<RectTransform>();
            displayRect.anchorMin = new Vector2(0.05f, 0.8f);
            displayRect.anchorMax = new Vector2(0.95f, 0.95f);
            displayRect.sizeDelta = Vector2.zero;

            Image displayBg = displayObj.AddComponent<Image>();
            displayBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(displayObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-20, -10);

            _displayText = textObj.AddComponent<TextMeshProUGUI>();
            _displayText.fontSize = 24;
            _displayText.color = Color.white;
            _displayText.alignment = TextAlignmentOptions.MidlineLeft;
            _displayText.text = "";

            // Build key rows
            float rowHeight = 60f;
            float startY = -100f;

            for (int row = 0; row < _keyRows.Length; row++)
            {
                BuildKeyRow(_keyRows[row], row, startY - (row * rowHeight));
            }

            // Bottom row: Space, Backspace, Clear, Enter, Cancel
            BuildBottomRow(startY - (_keyRows.Length * rowHeight));

            _root.SetActive(false);
        }

        private void BuildKeyRow(string keys, int rowIndex, float yPos)
        {
            float keyWidth = 70f;
            float keyGap = 5f;
            float totalWidth = keys.Length * keyWidth + (keys.Length - 1) * keyGap;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < keys.Length; i++)
            {
                char key = keys[i];
                float xPos = startX + i * (keyWidth + keyGap);
                CreateKey(key.ToString(), xPos, yPos, keyWidth, 50f, () => TypeKey(key));
            }
        }

        private void BuildBottomRow(float yPos)
        {
            CreateKey("Space", -200f, yPos, 150f, 50f, () => TypeKey(' '));
            CreateKey("←", -30f, yPos, 70f, 50f, () => Backspace());
            CreateKey("Clear", 60f, yPos, 80f, 50f, () => Clear());
            CreateKey("✓ Enter", 160f, yPos, 100f, 50f, () => Submit(), new Color(0.2f, 0.6f, 0.2f));
            CreateKey("✗ Cancel", 280f, yPos, 100f, 50f, () => Cancel(), new Color(0.6f, 0.2f, 0.2f));
        }

        private void CreateKey(string label, float x, float y, float width, float height, Action onClick, Color? color = null)
        {
            GameObject keyObj = new GameObject($"Key_{label}");
            keyObj.transform.SetParent(_root.transform, false);

            RectTransform keyRect = keyObj.AddComponent<RectTransform>();
            keyRect.anchoredPosition = new Vector2(x, y);
            keyRect.sizeDelta = new Vector2(width, height);

            Image keyImage = keyObj.AddComponent<Image>();
            keyImage.color = color ?? new Color(0.3f, 0.3f, 0.3f, 1f);

            Button button = keyObj.AddComponent<Button>();
            button.targetGraphic = keyImage;
            
            ColorBlock colors = button.colors;
            colors.normalColor = color ?? new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.highlightedColor = color != null ? color.Value * 1.2f : new Color(0.4f, 0.4f, 0.4f, 1f);
            colors.pressedColor = color != null ? color.Value * 0.8f : new Color(0.2f, 0.2f, 0.2f, 1f);
            button.colors = colors;

            button.onClick.AddListener(() => onClick?.Invoke());

            // Text label
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(keyObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
        }

        private void TypeKey(char key)
        {
            if (_shiftPressed && char.IsLetter(key))
            {
                _currentText += key;
                _shiftPressed = false;
            }
            else
            {
                _currentText += char.ToLower(key);
            }
            UpdateDisplay();
        }

        private void Backspace()
        {
            if (_currentText.Length > 0)
            {
                _currentText = _currentText.Substring(0, _currentText.Length - 1);
                UpdateDisplay();
            }
        }

        private void Clear()
        {
            _currentText = "";
            UpdateDisplay();
        }

        private void Submit()
        {
            _onSubmit?.Invoke(_currentText);
            Hide();
        }

        private void Cancel()
        {
            _onCancel?.Invoke();
            Hide();
        }

        private void UpdateDisplay()
        {
            if (_displayText != null)
            {
                _displayText.text = _currentText + "|";
            }
        }

        private void Update()
        {
            if (!_root.activeSelf) return;

            // Position in front of camera
            Camera cam = Camera.main;
            if (cam != null)
            {
                Transform camTrans = cam.transform;
                _root.transform.position = camTrans.position + camTrans.forward * 1f;
                _root.transform.rotation = Quaternion.LookRotation(_root.transform.position - camTrans.position);
            }

            // Desktop keyboard input
            if (Input.anyKeyDown)
            {
                foreach (char c in Input.inputString)
                {
                    if (c == '\b') // Backspace
                    {
                        Backspace();
                    }
                    else if (c == '\n' || c == '\r') // Enter
                    {
                        Submit();
                    }
                    else if (c >= 32 && c <= 126) // Printable characters
                    {
                        _currentText += c;
                        UpdateDisplay();
                    }
                }
            }
        }
    }
}
