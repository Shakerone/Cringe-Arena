using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;
using System.Collections;

public class PlayerHealth : NetworkBehaviour
{
    [SyncVar(hook = nameof(HealthValueChanged))] private float HealthValue = 100f;
    [SerializeField] private TMP_Text Health_txt = null;
    [SerializeField] private Slider HealthBar = null;
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Animator TpAnimator = null;
    [SerializeField] private GameObject mainFpsCamera = null, AfterDeathCamera = null, TpModelMesh = null;
    [SerializeField] private PlayerMovement movementScript = null;
    [SerializeField] private CharacterController controller = null;
    [SerializeField] private GameObject RoundOverPanel = null;
    [SerializeField] private TMP_Text WinnerTxt = null;
    [SerializeField] private TMP_Text CountdownTxt = null;

    [Header("Respawn Settings")]
    [SerializeField] private float respawnDelay = 5f;

    private bool isDead = false;
    private bool roundEnded = false;

    private void Start()
    {
        if (!isLocalPlayer) { return; }
        Health_txt.text = HealthValue.ToString();
        HealthBar.value = HealthValue;
    }

    [Server]
    public void TakeDamage(float damage_)
    {
        if (isDead || roundEnded) return;

        HealthValue = Mathf.Max(0, HealthValue - damage_);

        if (HealthValue <= 0)
        {
            isDead = true;
        }
    }

    public float GetCurrentHealth()
    {
        return HealthValue;
    }

    void HealthValueChanged(float oldHealth, float newHealth)
    {
        if (!isLocalPlayer)
        {
            return;
        }

        // Обновляем UI здоровья
        Health_txt.text = newHealth.ToString();
        HealthBar.value = newHealth;

        // Показываем экран поражения только если этот игрок умер
        if (newHealth <= 0 && !roundEnded)
        {
            ShowGameOverScreen("You lost!");
        }
    }

    // Метод для показа победы (вызывается только для победителя)
    [TargetRpc]
    public void TargetShowVictory(NetworkConnection target)
    {
        if (roundEnded) return;
        ShowGameOverScreen("You Won!");
    }

    // Универсальный метод для показа экрана окончания раунда
    private void ShowGameOverScreen(string message)
    {
        if (roundEnded) return;
        roundEnded = true;

        RoundOverPanel.SetActive(true);
        WinnerTxt.text = message;

        // Анимация смерти и отключение камеры только для проигравшего
        if (message.Contains("lost"))
        {
            AfterDeathCamera.SetActive(true);
            mainFpsCamera.SetActive(false);
            TpModelMesh.SetActive(true);
            TpAnimator.SetBool("die", true);
        }

        // Отключаем движение для всех
        movementScript.enabled = false;
        controller.enabled = false;
        TpAnimator.SetBool("walking", false);

        // Запускаем обратный отсчет
        StartCoroutine(CountdownAndRespawn());
    }

    private IEnumerator CountdownAndRespawn()
    {
        // Обратный отсчет
        for (int i = (int)respawnDelay; i > 0; i--)
        {
            if (CountdownTxt != null)
            {
                CountdownTxt.text = $"New Round in {i}";
            }
            yield return new WaitForSeconds(1f);
        }

        // Начинаем новый раунд
        CmdBeginNewRound();
    }

    // Команда для сервера чтобы начать новый раунд для всех
    [Command]
    private void CmdBeginNewRound()
    {
        // Сбрасываем состояние для всех игроков
        PlayerHealth[] allPlayers = FindObjectsOfType<PlayerHealth>();
        foreach (var player in allPlayers)
        {
            player.ServerResetPlayer();
        }

        // Респавним всех игроков
        RespawnAllPlayers();

        // Уведомляем всех клиентов о начале нового раунда
        RpcBeginNewRound();
    }

    [Server]
    public void ServerResetPlayer()
    {
        // Восстанавливаем здоровье на сервере
        HealthValue = 100f;
        isDead = false;
        roundEnded = false;
    }

    // Респавн всех игроков используя Network Start Positions
    [Server]
    private void RespawnAllPlayers()
    {
        PlayerHealth[] allPlayers = FindObjectsOfType<PlayerHealth>();
        NetworkStartPosition[] startPositions = FindObjectsOfType<NetworkStartPosition>();

        for (int i = 0; i < allPlayers.Length; i++)
        {
            if (startPositions.Length > 0)
            {
                // Получаем позицию спавна (циклично если игроков больше чем точек)
                NetworkStartPosition spawnPoint = startPositions[i % startPositions.Length];
                Vector3 spawnPosition = spawnPoint.transform.position;
                Quaternion spawnRotation = spawnPoint.transform.rotation;

                // Телепортируем игрока
                allPlayers[i].RpcTeleportToStart(spawnPosition, spawnRotation);
            }
        }
    }

    // Принудительная телепортация на клиенте
    [ClientRpc]
    private void RpcTeleportToStart(Vector3 targetPosition, Quaternion targetRotation)
    {
        // Отключаем CharacterController перед телепортацией
        if (controller != null)
        {
            controller.enabled = false;
        }

        // Телепортируем
        transform.position = targetPosition;
        transform.rotation = targetRotation;

        // Включаем обратно CharacterController
        if (controller != null)
        {
            controller.enabled = true;
        }

        // Сбрасываем скорость
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    [ClientRpc]
    private void RpcBeginNewRound()
    {
        BeginNewRound();
    }

    // Метод для клиентского сброса
    public void BeginNewRound()
    {
        roundEnded = false;
        isDead = false;

        // Скрываем панель окончания раунда
        RoundOverPanel.SetActive(false);

        // Включаем управление
        movementScript.enabled = true;
        controller.enabled = true;

        // Восстанавливаем камеры и модели для всех
        if (isLocalPlayer)
        {
            mainFpsCamera.SetActive(true);
            AfterDeathCamera.SetActive(false);
            TpModelMesh.SetActive(false);
        }
        else
        {
            TpModelMesh.SetActive(true);
        }

        // Сбрасываем анимации
        TpAnimator.SetBool("walking", false);
        TpAnimator.SetBool("die", false);

        // Принудительно обновляем UI здоровья
        if (isLocalPlayer)
        {
            Health_txt.text = "100";
            HealthBar.value = 100f;
        }

        // Очищаем текст обратного отсчета
        if (CountdownTxt != null)
        {
            CountdownTxt.text = "";
        }
    }

    [TargetRpc]
    public void TargetShowDamage(NetworkConnection target, float damage, Vector3 damagePosition)
    {
        ShowDamageLocal(damage, damagePosition);
    }

    private void ShowDamageLocal(float damage, Vector3 damagePosition)
    {
        Vector3 spawnPosition = damagePosition + new Vector3(0f, 2f, 0f);
        Camera playerCamera = GetComponentInChildren<Camera>();
        if (damageTextPrefab != null)
        {
            GameObject dmgTxt = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity);
            DamageText damageTextComponent = dmgTxt.GetComponentInChildren<DamageText>();
            if (damageTextComponent != null)
            {
                damageTextComponent.GetCalled(damage, playerCamera != null ? playerCamera.gameObject : null);
            }
        }
    }
}