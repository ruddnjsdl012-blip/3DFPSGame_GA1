using UnityEngine;

public class PlayerRotate : MonoBehaviour
{
    [SerializeField] private float _rotationSpeed = 200f;

    private float _accumulationX = 0f; // 좌우   회전 각도

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;

        float mouseX = Input.GetAxis("Mouse X");
        _accumulationX += mouseX * _rotationSpeed * Time.deltaTime;

        transform.eulerAngles = new Vector3(0f, _accumulationX, 0f);
    }
}