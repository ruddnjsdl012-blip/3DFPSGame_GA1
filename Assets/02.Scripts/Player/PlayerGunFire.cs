using UnityEngine;

public class PlayerGunFire : MonoBehaviour
{
    [SerializeField] private float _range = 100f;
    [SerializeField] private float _damage = 10f;

    private void Update()
    {
        // 마우스 왼쪽 버튼을 누르면 발사한다.
        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError(
                "Main Camera의 Tag를 MainCamera로 설정해 주세요.",
                this);
            return;
        }

        // 카메라 위치에서 카메라가 바라보는 방향으로 광선을 만든다.
        Transform cameraTransform = mainCamera.transform;
        Ray ray = new Ray(
            cameraTransform.position,
            cameraTransform.forward);

        // 광선을 한 번 발사하고 명중 정보를 받는다.
        bool isHit = Physics.Raycast(
            ray,
            out RaycastHit hitInfo,
            _range);

        if (isHit == false)
        {
            return;
        }

        // 이 스크립트가 붙은 자기 자신에게 명중하면 처리하지 않는다.
        if (hitInfo.collider.gameObject == gameObject)
        {
            return;
        }

        // 명중한 대상과 설정된 공격력을 확인한다.
        Debug.Log(
            $"명중: {hitInfo.collider.gameObject.name}, 공격력: {_damage}",
            this);
    }
}