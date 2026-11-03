using UnityEngine;

public class PlayerRotate : MonoBehaviour
{
    [SerializeField] private float _rotationSpeed = 200f;

    private float _accumulationX = 0f; // 좌우   회전 각도

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;
        if (Time.timeScale <= 0f) return; // 일시정지 중에는 회전하지 않는다

        float mouseX = Input.GetAxis("Mouse X");
        _accumulationX += mouseX * _rotationSpeed * Time.deltaTime;

        transform.eulerAngles = new Vector3(0f, _accumulationX, 0f);
    }

    // 총 반동: 좌우로 몸을 돌린다. (CameraRotate.AddRecoil과 같은 yaw를 넣는다)
    public void AddRecoil(float yaw)
    {
        _accumulationX += yaw;
    }
}