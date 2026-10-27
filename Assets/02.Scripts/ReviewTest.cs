using UnityEngine;

public class ReviewTest : MonoBehaviour
{
    private void Update()
    {
        GameObject player = GameObject.Find("Player");
        Rigidbody playerRigidbody = player.GetComponent<Rigidbody>();
        playerRigidbody.AddForce(Vector3.up * 10f);
    }
}