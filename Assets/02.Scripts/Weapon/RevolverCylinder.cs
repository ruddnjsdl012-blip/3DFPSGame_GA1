using UnityEngine;

// 리볼버 쏘는 애니메이션(ReichsrevolverM1879_Shooting1/2)에 들어 있는 'RotateCyl' 이벤트를 받는다.
// 원래 에셋의 RevolverController와 같은 일: 한 발 쏠 때마다 탄창(실린더)을 60도 돌린다.
// (이 스크립트가 없으면 "AnimationEvent 'RotateCyl' ... has no receiver" 오류가 계속 뜬다)
public class RevolverCylinder : MonoBehaviour
{
    [SerializeField] private float _turnSpeed = 600f;  // 도/초

    private Transform _cylinder;
    private Quaternion _baseRotation;
    private float _angle;
    private float _target;

    public static void Attach(GameObject animatorObject, Transform cylinder)
    {
        RevolverCylinder receiver = animatorObject.GetComponent<RevolverCylinder>();
        if (receiver == null)
        {
            receiver = animatorObject.AddComponent<RevolverCylinder>();
        }

        receiver._cylinder = cylinder;
        if (cylinder != null)
        {
            receiver._baseRotation = cylinder.localRotation;
        }
    }

    // 애니메이션 이벤트
    public void RotateCyl()
    {
        _target += 60f;
    }

    private void LateUpdate()
    {
        if (_cylinder == null || Mathf.Approximately(_angle, _target))
        {
            return;
        }

        _angle = Mathf.MoveTowards(_angle, _target, _turnSpeed * Time.deltaTime);
        if (_angle >= 360f && _target >= 360f)
        {
            _angle -= 360f;
            _target -= 360f;
        }

        _cylinder.localRotation = _baseRotation * Quaternion.Euler(0f, _angle, 0f);
    }
}
