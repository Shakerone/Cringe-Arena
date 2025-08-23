using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Steamworks;

public class FpsNetworkManager : NetworkManager
{
    [SerializeField] private GameObject EnterAddressPanel = null, landingPage = null, lobbyUI = null;
    [SerializeField] private TMP_InputField AddressField = null;
    [SerializeField] private GameObject StartGameButton = null;
    public List<PlayerScript> PlayersList = new List<PlayerScript>();
    [SerializeField] private GameObject PlayerGO = null;

    [SerializeField] private bool UseSteam = true;

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    private void Start()
    {
        if(!UseSteam)
        {
            return;
        }
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);

        PlayerScript PlayerStartPrefab = conn.identity.GetComponent<PlayerScript>();

        PlayersList.Add(PlayerStartPrefab);

        if (PlayersList.Count ==2)
        {
            StartGameButton.SetActive(true);
        }
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);

        PlayerScript PlayerStartPrefab = conn.identity.GetComponent<PlayerScript>();

        PlayersList.Remove(PlayerStartPrefab);

        StartGameButton.SetActive(false);
    }



    public void HostLobby()
    {
        landingPage.SetActive(false);

        if(UseSteam)
        {
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 2);
            return;
        }

        NetworkManager.singleton.StartHost();
    }

    public void JoinButton()
    {
        EnterAddressPanel.SetActive(true);
        landingPage.SetActive(false);

    }

    public void JoinLobby()
    {
        NetworkManager.singleton.networkAddress = AddressField.text;
        NetworkManager.singleton.StartClient();
    }


    public override void OnClientConnect()
    {
        base.OnClientConnect();

        lobbyUI.SetActive(true);
        EnterAddressPanel.SetActive(false);
        landingPage.SetActive(false);

    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();

        landingPage.SetActive(true);
        lobbyUI.SetActive(false);
        EnterAddressPanel.SetActive(false);

    }

    public void LeaveLobby()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopHost();

        }
        else
        {
            NetworkManager.singleton.StopClient();
        }
    }


    public void StartGame()
    {
        ServerChangeScene("FPS");

    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        if (SceneManager.GetActiveScene().name.StartsWith("FPS"))
        {
            foreach(PlayerScript player in PlayersList)
            {
                var connectionTC = player.connectionToClient;
                GameObject PlayerP = Instantiate(PlayerGO, GetStartPosition().transform.position, Quaternion.identity);
                NetworkServer.ReplacePlayerForConnection(connectionTC, PlayerP);
            }
        }

    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            landingPage.SetActive(true);
            return;
        }

        NetworkManager.singleton.StartHost();

        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "HostIP", SteamUser.GetSteamID().ToString());
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        if(NetworkServer.active)
        {
            return;

        }

        string HostIP = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "HostIP");

        NetworkManager.singleton.networkAddress = HostIP;
        NetworkManager.singleton.StartClient();

        landingPage.SetActive(false);
    }


}
