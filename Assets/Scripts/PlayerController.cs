using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed;
    public float axisSensitivity = 100f;
    private void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(Vector3.forward * Time.deltaTime * speed);
        }
        else if (Input.GetKey(KeyCode.S))
        {
            transform.Translate(Vector3.back * Time.deltaTime * speed);
        }
        else if (Input.GetKey(KeyCode.A))
        {
            transform.Translate(Vector3.left * Time.deltaTime * speed);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            transform.Translate(Vector3.right * Time.deltaTime * speed);
        }
        float cameraHorizontal = Input.GetAxis("Mouse X") * axisSensitivity * Time.fixedDeltaTime;
        transform.Rotate(Vector3.up, cameraHorizontal);
        float cameraVetical = Input.GetAxis("Mouse Y") * axisSensitivity * Time.fixedDeltaTime;
        Camera.main.transform.localEulerAngles = new Vector3(Camera.main.transform.eulerAngles.x - cameraVetical, 0, 0);
    }
}
