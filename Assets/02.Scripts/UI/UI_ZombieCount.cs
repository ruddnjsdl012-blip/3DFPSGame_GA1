using TMPro;
using UnityEngine;

// 좀비 수 UI: 살아 있는 좀비 수 / 동시 최대 좀비 수를 화면에 보여 준다.
// - 매 프레임 확인(폴링)하지 않고, 스포너의 OnCountChanged 이벤트가 올 때만 갱신한다.
public class UI_ZombieCount : MonoBehaviour
{
    [SerializeField] private ZombieSpawner _spawner;
    [SerializeField] private TextMeshProUGUI _countText;

    private void Start()
    {
        _spawner.OnCountChanged += Refresh;

        // 구독 전의 초기값(0 / 최대)도 보여 준다.
        Refresh();
    }

    private void OnDestroy()
    {
        _spawner.OnCountChanged -= Refresh;
    }

    private void Refresh()
    {
        _countText.text = $"좀비 {_spawner.AliveCount} / {_spawner.MaxCount}";
    }
}
