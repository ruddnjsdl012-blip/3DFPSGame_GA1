using UnityEngine;

// 탄약 HUD 위에 수류탄 개수를 표시한다. (남은 개수 / 최대, 칸으로도 표시)
[RequireComponent(typeof(PlayerBombFire))]
public class PlayerBombHud : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.035f, 0.05f, 0.07f, 0.85f);
    private static readonly Color SubColor = new Color(0.75f, 0.8f, 0.85f);
    private static readonly Color PipColor = new Color(1f, 0.72f, 0.25f);
    private static readonly Color EmptyColor = new Color(1f, 0.25f, 0.22f);

    private PlayerBombFire _weapon;

    private GUIStyle _titleStyle;
    private GUIStyle _hintStyle;
    private GUIStyle _countStyle;

    private float _emptyFlash;
    private float _gainFlash;
    private int _lastCount = -1;

    private void Awake()
    {
        _weapon = GetComponent<PlayerBombFire>();
    }

    private void OnEnable()
    {
        if (_weapon == null) _weapon = GetComponent<PlayerBombFire>();
        if (_weapon != null) _weapon.EmptyThrown += HandleEmptyThrown;
    }

    private void OnDisable()
    {
        if (_weapon != null) _weapon.EmptyThrown -= HandleEmptyThrown;
    }

    private void HandleEmptyThrown()
    {
        _emptyFlash = 1f;
    }

    private void Update()
    {
        if (_weapon == null)
        {
            return;
        }

        if (_lastCount >= 0 && _weapon.BombCount > _lastCount)
        {
            _gainFlash = 1f; // 주웠을 때
        }

        _lastCount = _weapon.BombCount;
        _emptyFlash = Mathf.MoveTowards(_emptyFlash, 0f, Time.unscaledDeltaTime * 2.5f);
        _gainFlash = Mathf.MoveTowards(_gainFlash, 0f, Time.unscaledDeltaTime * 3f);
    }

    private void OnGUI()
    {
        // 시작 화면/일시 정지 메뉴가 열려 있으면 그리지 않는다. (IMGUI는 메뉴 위에 그려지므로)
        if (_weapon == null || _weapon.enabled == false || UI_GameMenu.IsOpen)
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
            _hintStyle.normal.textColor = SubColor;

            _countStyle = new GUIStyle(GUI.skin.label);
            _countStyle.fontSize = 34;
            _countStyle.fontStyle = FontStyle.Bold;
            _countStyle.alignment = TextAnchor.MiddleRight;
            _countStyle.richText = true;
        }

        float x = Mathf.Max(12f, Screen.width - 292f);
        // 우측 하단은 탄약 HUD(UI_AmmoHud, 높이 104 + 여백 20)가 쓰므로 그 위에 둔다.
        float y = Mathf.Max(12f, Screen.height - 226f);
        float shake = _emptyFlash > 0f ? Mathf.Sin(Time.unscaledTime * 70f) * 4f * _emptyFlash : 0f;

        Color original = GUI.color;

        GUI.color = PanelColor;
        GUI.DrawTexture(new Rect(x, y, 272f, 92f), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(x + 16f, y + 9f, 240f, 27f), "BOMBS", _titleStyle);
        GUI.Label(new Rect(x + 16f, y + 36f, 245f, 20f), _weapon.ThrowKey + "  /  THROW", _hintStyle);

        // 개수 (예: 2 / 4). 없으면 빨간색, 주우면 잠깐 밝게
        int count = _weapon.BombCount;
        Color countColor = count > 0 ? Color.Lerp(Color.white, PipColor, _gainFlash) : EmptyColor;
        countColor = Color.Lerp(countColor, EmptyColor, _emptyFlash);
        _countStyle.normal.textColor = countColor;
        string sub = ColorUtility.ToHtmlStringRGB(SubColor);
        GUI.Label(new Rect(x + 120f + shake, y + 8f, 136f, 44f),
            count + "<size=18><color=#" + sub + ">  / " + _weapon.MaxBombs + "</color></size>", _countStyle);

        // 칸 표시
        for (int i = 0; i < _weapon.MaxBombs; i++)
        {
            GUI.color = i < count ? PipColor : new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(new Rect(x + 16f + i * 24f + shake, y + 64f, 18f, 12f), Texture2D.whiteTexture);
        }

        GUI.color = original;
    }
}
