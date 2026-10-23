using UnityEngine;
using UnityEngine.UI;

public class ZombieHealthBar : MonoBehaviour
{
    [SerializeField] Slider _slider;
    private Zombie _zombie;

    private void Awake()
    {
        _zombie = GetComponent<Zombie>();
    }


    private void LateUpdate()
    {
        // 0 ~100 -> 0 ~ 1
        //  _Slider.value = 좀비의 현재 체력 / 
        _slider.value = _zombie.Health / _zombie.MAxHealth;

        // 빌보드 : 체력바가 카메라를 쳐다보게 한다.

        transform.forward = Camera.main.transform.forward;
    }
}