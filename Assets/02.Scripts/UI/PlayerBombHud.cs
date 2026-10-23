using UnityEngine;

[RequireComponent(typeof(PlayerBombFire))]
public class PlayerBombHud : MonoBehaviour
{
    private PlayerBombFire _weapon;

    private GUIStyle _titleStyle;
    private GUIStyle _hintStyle;

    private void Awake()
    {
        _weapon = GetComponent<PlayerBombFire>();
    }

    private void OnGUI()
    {
        if (_weapon == null || _weapon.enabled == false)
        {
            return;
        }

        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = 20;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.normal.textColor = Color.white;

            _hintStyle = new GUIStyle(GUI.skin.label);
            _hintStyle.fontSize = 11;
            _hintStyle.normal.textColor =
                new Color(0.75f, 0.8f, 0.85f);
        }

        float x = Mathf.Max(12f, Screen.width - 292f);
        float y = Mathf.Max(12f, Screen.height - 112f);

        Color original = GUI.color;

        GUI.color = new Color(0.035f, 0.05f, 0.07f, 0.85f);

        GUI.DrawTexture(
            new Rect(x, y, 272f, 92f),
            Texture2D.whiteTexture);

        GUI.color = Color.white;

        GUI.Label(
            new Rect(x + 16f, y + 9f, 240f, 27f),
            "BOMBS",
            _titleStyle);

        GUI.Label(
            new Rect(x + 16f, y + 40f, 245f, 20f),
            "RMB  /  THROW",
            _hintStyle);

        GUI.color = original;
    }
}