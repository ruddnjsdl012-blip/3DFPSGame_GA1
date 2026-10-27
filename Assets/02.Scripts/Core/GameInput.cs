using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
#endif

// Keep the existing game playable after an Asset Store import switches input backends.
// Prefer the original Input Manager when Both is enabled, preserving its axis settings.
public static class GameInput
{
    public static float GetAxis(string name)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Mouse mouse = Mouse.current;
        if (name == "Mouse X") return mouse != null ? mouse.delta.ReadValue().x * 0.1f : 0f;
        if (name == "Mouse Y") return mouse != null ? mouse.delta.ReadValue().y * 0.1f : 0f;
        return GetAxisRaw(name);
#else
        return UnityEngine.Input.GetAxis(name);
#endif
    }

    public static float GetAxisRaw(string name)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard keyboard = Keyboard.current;
        float value = 0f;
        if (name == "Horizontal")
        {
            if (keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)) value += 1f;
            if (keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)) value -= 1f;
            return value;
        }
        if (name == "Vertical")
        {
            if (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)) value += 1f;
            if (keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)) value -= 1f;
            return value;
        }
        if (name == "Mouse X" || name == "Mouse Y") return GetAxis(name);
        return 0f;
#else
        return UnityEngine.Input.GetAxisRaw(name);
#endif
    }

    public static bool GetKey(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        ButtonControl control = FindButton(key);
        return control != null && control.isPressed;
#else
        return UnityEngine.Input.GetKey(key);
#endif
    }

    public static bool GetKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        ButtonControl control = FindButton(key);
        return control != null && control.wasPressedThisFrame;
#else
        return UnityEngine.Input.GetKeyDown(key);
#endif
    }

    public static bool GetMouseButton(int button)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        ButtonControl control = FindMouseButton(button);
        return control != null && control.isPressed;
#else
        return UnityEngine.Input.GetMouseButton(button);
#endif
    }

    public static bool GetButton(string name)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (name == "Fire1") return GetMouseButton(0) || GetKey(KeyCode.LeftControl);
        if (name == "Fire2") return GetMouseButton(1) || GetKey(KeyCode.LeftAlt);
        if (name == "Jump") return GetKey(KeyCode.Space);
        return false;
#else
        return UnityEngine.Input.GetButton(name);
#endif
    }

    public static bool GetButtonDown(string name)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (name == "Fire1") return GetKeyDown(KeyCode.Mouse0) || GetKeyDown(KeyCode.LeftControl);
        if (name == "Fire2") return GetKeyDown(KeyCode.Mouse1) || GetKeyDown(KeyCode.LeftAlt);
        if (name == "Jump") return GetKeyDown(KeyCode.Space);
        return false;
#else
        return UnityEngine.Input.GetButtonDown(name);
#endif
    }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    private static ButtonControl FindMouseButton(int button)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return null;
        switch (button)
        {
            case 0: return mouse.leftButton;
            case 1: return mouse.rightButton;
            case 2: return mouse.middleButton;
            case 3: return mouse.backButton;
            case 4: return mouse.forwardButton;
            default: return null;
        }
    }

    private static ButtonControl FindButton(KeyCode key)
    {
        if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse4)
            return FindMouseButton((int)key - (int)KeyCode.Mouse0);
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || key == KeyCode.None) return null;
        string name = key.ToString();
        switch (key)
        {
            case KeyCode.Return: name = "Enter"; break;
            case KeyCode.KeypadEnter: name = "NumpadEnter"; break;
            case KeyCode.LeftControl: name = "LeftCtrl"; break;
            case KeyCode.RightControl: name = "RightCtrl"; break;
            case KeyCode.LeftCommand: name = "LeftMeta"; break;
            case KeyCode.RightCommand: name = "RightMeta"; break;
            case KeyCode.LeftWindows: name = "LeftMeta"; break;
            case KeyCode.RightWindows: name = "RightMeta"; break;
            case KeyCode.UpArrow: name = "UpArrow"; break;
            case KeyCode.DownArrow: name = "DownArrow"; break;
            case KeyCode.LeftArrow: name = "LeftArrow"; break;
            case KeyCode.RightArrow: name = "RightArrow"; break;
        }
        if (name.StartsWith("Alpha", StringComparison.Ordinal)) name = "Digit" + name.Substring(5);
        else if (name.StartsWith("Keypad", StringComparison.Ordinal)) name = "Numpad" + name.Substring(6);
        return Enum.TryParse(name, true, out Key mapped) ? keyboard[mapped] : null;
    }
#endif

    // A start menu using StandaloneInputModule also stops responding in New-only mode.
    public static void ConfigureUI(EventSystem eventSystem)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (eventSystem == null) return;
        foreach (StandaloneInputModule legacy in eventSystem.GetComponents<StandaloneInputModule>())
            legacy.enabled = false;
        InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        if (module.actionsAsset == null) module.AssignDefaultActions();
        module.enabled = true;
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConfigureSceneUI()
    {
        foreach (EventSystem eventSystem in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            ConfigureUI(eventSystem);
    }
}
