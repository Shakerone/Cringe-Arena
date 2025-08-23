using UnityEngine;

public class CastPoint : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform; // ������ ������
    [SerializeField] private float distanceFromCamera = 1.5f; // ���������� ������

    void Update()
    {
        if (cameraTransform == null) return;

        // ������ ����� ����� �������
        transform.position = cameraTransform.position + cameraTransform.forward * distanceFromCamera;

        // ������������ ����� ��� ��, ��� ������
        transform.rotation = cameraTransform.rotation;
    }
}
