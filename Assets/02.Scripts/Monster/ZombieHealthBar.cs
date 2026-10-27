using UnityEngine;
using UnityEngine.UI;

public class ZombieHealthBar : MonoBehaviour
{
    [SerializeField] private Slider _slider;

    private Zombie _zombie;

    private void Awake()
    {
        _zombie = GetComponent<Zombie>();
    }

    private void LateUpdate()
    {
        // 0 ~ 100% -> 0 ~ 1
        // _slider.value = 좀비의 현재 체력 / 좀비의 최대 체력
        _slider.value = _zombie.Health / _zombie.MaxHealth;

        // 빌보드: 체력바가 카메라를 쳐다보게한다.
        _slider.transform.forward = Camera.main.transform.forward;
    }
}
