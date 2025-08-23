using Mirror;
using UnityEngine;

public class NetworkStuff : NetworkBehaviour
{
    [Header("Player Components")]
    [SerializeField] private GameObject FpsCamera = null;
    [SerializeField] private GameObject TpMesh = null;
    [SerializeField] private AudioListener audioListener = null;

    [Header("Optional Components")]
    [SerializeField] private Canvas playerUI = null;
    [SerializeField] private GameObject playerNameTag = null;

    void Start()
    {
        SetupPlayerComponents();
    }

    private void SetupPlayerComponents()
    {
        if (isLocalPlayer)
        {
            // ��������� ����� - �������� FPS ������ � UI
            if (FpsCamera != null)
                FpsCamera.SetActive(true);

            if (TpMesh != null)
                TpMesh.SetActive(false);

            if (audioListener != null)
                audioListener.enabled = true;

            if (playerUI != null)
                playerUI.gameObject.SetActive(true);

            if (playerNameTag != null)
                playerNameTag.SetActive(false); // ���� ��� �� ����������
        }
        else
        {
            // ��������� ����� - ���������� ������ ������������ ������
            if (FpsCamera != null)
                FpsCamera.SetActive(false);

            if (TpMesh != null)
                TpMesh.SetActive(true);

            if (audioListener != null)
                audioListener.enabled = false;

            if (playerUI != null)
                playerUI.gameObject.SetActive(false);

            if (playerNameTag != null)
                playerNameTag.SetActive(true); // ���������� ���� ������ �������
        }
    }

    // ����� ��� ������������ ��������� ����������� �� ����� ���� (���� �����)
    [Client]
    public void ToggleFirstPersonView(bool enableFPS)
    {
        if (!isLocalPlayer) return;

        if (FpsCamera != null)
            FpsCamera.SetActive(enableFPS);

        if (TpMesh != null)
            TpMesh.SetActive(!enableFPS);
    }

    /*
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.U)) {
            if (!isLocalPlayer) return;
            LeaveGame();
        }
    }

    */

/*
    public void LeaveGame()
    {
        if (isServer)
        {
            NetworkManager.singleton.StopHost();
            return;
        }
        else
        {
            NetworkManager.singleton.StopClient();
        }

    }
*/





}