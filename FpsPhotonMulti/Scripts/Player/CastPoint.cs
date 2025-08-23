using UnityEngine;

public class CastPoint : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform; // Камера игрока
    [SerializeField] private float distanceFromCamera = 1.5f; // Расстояние вперед

    void Update()
    {
        if (cameraTransform == null) return;

        // Ставим точку перед камерой
        transform.position = cameraTransform.position + cameraTransform.forward * distanceFromCamera;

        // Поворачиваем точку так же, как камеру
        transform.rotation = cameraTransform.rotation;
    }
}
