using UnityEngine;

public class CameraMove : MonoBehaviour
{
    public GameObject viewPoint;

    private void Update()
    {
        transform.position = viewPoint.transform.position;
        transform.rotation = viewPoint.transform.rotation;
    }
}
