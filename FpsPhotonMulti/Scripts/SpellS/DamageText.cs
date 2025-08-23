using UnityEngine;
using TMPro;

public class DamageText : MonoBehaviour
{
    GameObject cameraGo;

    void DestroyText()
    {
        Destroy(gameObject);
    }

    public void GetCalled(float damage_, GameObject camera_)
    {
        TMP_Text textComponent = GetComponent<TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = damage_.ToString();
        }

        cameraGo = camera_;

        Invoke(nameof(DestroyText), 3f);
    }

    private void LateUpdate()
    {
        if (cameraGo != null)
        {
            transform.LookAt(transform.position + cameraGo.transform.rotation * Vector3.forward,
                           cameraGo.transform.rotation * Vector3.up);
        }
    }
}