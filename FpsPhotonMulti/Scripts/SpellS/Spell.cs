using UnityEngine;
using Mirror;
using System.Collections;
using Unity.VisualScripting;

[RequireComponent(typeof(SphereCollider))]
public class Spell : NetworkBehaviour
{
    public SpellScriptableObject SpellToCast;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private GameObject freezePrefab;
    private SphereCollider myCollider;
    [SyncVar] public uint casterId;

    private bool hasExploded = false; // Флаг для предотвращения повторных взрывов
    private float ignoreTimeAfterCast = 0.1f; // Время игнорирования кастера после выпуска
    private float spawnTime;

    private void Awake()
    {
        myCollider = GetComponent<SphereCollider>();
        myCollider.isTrigger = true;
        myCollider.radius = SpellToCast.SpellRadius;
    }

    public override void OnStartServer()
    {
        spawnTime = Time.time;
        Invoke(nameof(ExplodeSpell), SpellToCast.Lifetime);

        // Опционально: Игнорируем коллизии с кастером на старте
        if (NetworkServer.spawned.TryGetValue(casterId, out NetworkIdentity casterIdentity))
        {
            Collider casterCollider = casterIdentity.GetComponent<Collider>();
            if (casterCollider != null && myCollider != null)
            {
                Physics.IgnoreCollision(myCollider, casterCollider, true);
                // Включаем коллизии обратно через короткое время
                Invoke(nameof(EnableCasterCollision), 0.2f);
            }
        }
    }

    private void EnableCasterCollision()
    {
        if (NetworkServer.spawned.TryGetValue(casterId, out NetworkIdentity casterIdentity))
        {
            Collider casterCollider = casterIdentity.GetComponent<Collider>();
            if (casterCollider != null && myCollider != null)
            {
                Physics.IgnoreCollision(myCollider, casterCollider, false);
            }
        }
    }

    [ServerCallback]
    private void Update()
    {
        if (SpellToCast.Speed > 0 && !hasExploded)
            transform.position += transform.forward * SpellToCast.Speed * Time.deltaTime;
    }

    [ServerCallback]
    private void OnTriggerEnter(Collider other)
    {
        // Если уже взорвались, не обрабатываем коллизии
        if (hasExploded) return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

        // Если попали в игрока
        if (playerHealth != null)
        {
            // Дополнительная проверка: игнорируем кастера в первые моменты
            if (playerHealth.netId == casterId)
            {
                // Если прошло мало времени с момента каста - игнорируем
                if (Time.time - spawnTime < ignoreTimeAfterCast)
                {
                    return;
                }
                // После истечения времени игнорирования - можем взрываться от себя
            }


            if (this.gameObject.GetComponent<FrostMissleCollision>() != null)
            {
                GameObject freeze = Instantiate(freezePrefab, other.transform.position, Quaternion.identity);
            }

        }

        // Взрываемся при столкновении с любым объектом
        ExplodeSpell();
    }

    [Server]
    private void ExplodeSpell()
    {
        if (hasExploded) return; // Предотвращаем повторные взрывы
        hasExploded = true;

        // Создаем эффект взрыва
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            NetworkServer.Spawn(explosion);

            // Уничтожаем эффект взрыва через некоторое время
            StartCoroutine(DestroyExplosionAfterTime(explosion, 3f)); // 3 секунды
        }


        if (this.name == "FireMissle")
        {
            // Наносим урон и knockback всем игрокам в радиусе взрыва
            DamageAndKnockbackPlayersInRadius();
        }



        // Уничтожаем сам файербол
        DestroySelf();
    }

    [Server]
    private void DamageAndKnockbackPlayersInRadius()
    {
        // Находим всех игроков в радиусе взрыва
        Collider[] playersInRange = Physics.OverlapSphere(transform.position, SpellToCast.ExplosionRadius);

        foreach (Collider playerCollider in playersInRange)
        {
            PlayerHealth playerHealth = playerCollider.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                // Не атакуем самого себя (но knockback можем применить)
                bool isSelfDamage = playerHealth.netId == casterId;

                // Проверяем, есть ли препятствия между центром взрыва и игроком
                Vector3 explosionCheckPoint = transform.position + Vector3.up * 0.5f; // Поднимаем точку взрыва немного вверх
                Vector3 playerCenter = playerHealth.transform.position + Vector3.up; // Центр игрока
                Vector3 directionToPlayer = (playerCenter - explosionCheckPoint).normalized;
                float distanceToPlayer = Vector3.Distance(transform.position, playerHealth.transform.position); // Используем реальную дистанцию от взрыва

                // Используем SphereCast вместо Raycast для лучшего обнаружения
                RaycastHit hit;
                float sphereRadius = 0.3f; // Радиус для SphereCast
                bool hasObstacle = Physics.SphereCast(explosionCheckPoint, sphereRadius, directionToPlayer,
                    out hit, Vector3.Distance(explosionCheckPoint, playerCenter),
                    ~LayerMask.GetMask("Player", "Spell")); // Игнорируем слои Player и Spell

                // Проверяем, что препятствие - это не сам игрок и не земля под небольшим углом
                bool canAffectPlayer = !hasObstacle ||
                                       hit.collider == playerCollider ||
                                       hit.collider.CompareTag("Player") ||
                                       (hit.normal.y > 0.7f); // Если это пол/земля (нормаль вверх), не блокируем

                // Debug информация
                if (SpellToCast.ShowDebugVisuals)
                {
                    string blockReason = "";
                    if (hasObstacle && hit.collider != null)
                    {
                        blockReason = $"Blocked by: {hit.collider.name}, Normal Y: {hit.normal.y:F2}";
                    }
                    DebugKnockback(playerHealth.name, distanceToPlayer, canAffectPlayer, blockReason);
                }

                // Если можем воздействовать на игрока
                if (canAffectPlayer)
                {
                    // Apply knockback if enabled
                    if (SpellToCast.EnableKnockback)
                    {
                        ApplyKnockbackToPlayer(playerHealth, distanceToPlayer);
                    }

                    // Only apply damage if it's not self-damage
                    if (!isSelfDamage)
                    {
                        // Apply damage
                        if (NetworkServer.spawned.TryGetValue(casterId, out NetworkIdentity attackerIdentity))
                        {
                            PlayerHealth attackerHealth = attackerIdentity.GetComponent<PlayerHealth>();
                            if (attackerIdentity.connectionToClient != null && attackerHealth != null)
                            {
                                // Сохраняем здоровье до урона
                                float healthBeforeDamage = playerHealth.GetCurrentHealth();

                                // Наносим урон только если цель еще жива
                                if (healthBeforeDamage > 0)
                                {
                                    playerHealth.TakeDamage(SpellToCast.DamageAmount);

                                    // Показываем урон атакующему
                                    attackerHealth.TargetShowDamage(attackerIdentity.connectionToClient, SpellToCast.DamageAmount, playerHealth.transform.position);

                                    // Проверяем здоровье после урона
                                    float healthAfterDamage = playerHealth.GetCurrentHealth();

                                    // Если цель умерла от этого урона, показываем победу атакующему
                                    if (healthAfterDamage <= 0)
                                    {
                                        attackerHealth.TargetShowVictory(attackerIdentity.connectionToClient);
                                    }
                                }
                            }
                            else
                            {
                                playerHealth.TakeDamage(SpellToCast.DamageAmount);
                            }
                        }
                        else
                        {
                            playerHealth.TakeDamage(SpellToCast.DamageAmount);
                        }
                    }
                }
            }
        }
    }

    [Server]
    private void ApplyKnockbackToPlayer(PlayerHealth playerHealth, float distanceToPlayer)
    {
        PlayerMovement playerMovement = playerHealth.GetComponent<PlayerMovement>();
        if (playerMovement == null) return;

        // Calculate knockback direction from explosion center to player
        Vector3 explosionPos = transform.position;
        Vector3 playerPos = playerHealth.transform.position;

        // Calculate direction - используем только горизонтальную дистанцию для проверки
        Vector3 knockbackDirection = (playerPos - explosionPos);
        Vector3 horizontalDirection = new Vector3(
            playerPos.x - explosionPos.x,
            0,
            playerPos.z - explosionPos.z
        );

        // ВАЖНО: Проверяем горизонтальную близость, а не общую
        if (horizontalDirection.magnitude < 0.5f) // Взрыв близко по горизонтали
        {
            // Используем направление "назад" от взгляда игрока
            Transform playerTransform = playerHealth.transform;

            // Берем чисто горизонтальное направление назад
            Vector3 backDirection = -playerTransform.forward;
            backDirection.y = 0; // Убираем вертикальную составляющую
            backDirection = backDirection.normalized;

            // Комбинируем с небольшим подъемом
            knockbackDirection = backDirection + Vector3.up * 0.2f;

            // Добавляем небольшую случайность только по горизонтали
            knockbackDirection.x += UnityEngine.Random.Range(-0.1f, 0.1f);
            knockbackDirection.z += UnityEngine.Random.Range(-0.1f, 0.1f);

            Debug.Log($"Close explosion - using backward direction. Horizontal dist: {horizontalDirection.magnitude}");
        }
        else
        {
            // Если есть горизонтальная дистанция, но она маленькая - усиливаем
            if (horizontalDirection.magnitude < 1f && horizontalDirection.magnitude > 0.1f)
            {
                // Усиливаем горизонтальную составляющую
                Vector3 boostedHorizontal = horizontalDirection.normalized * 0.7f;
                knockbackDirection = new Vector3(
                    boostedHorizontal.x,
                    knockbackDirection.y * 0.3f, // Уменьшаем вертикальную составляющую
                    boostedHorizontal.z
                );
                Debug.Log("Boosting horizontal knockback for near-wall explosion");
            }
        }

        knockbackDirection = knockbackDirection.normalized;

        // Calculate force falloff based on distance (closer = stronger)
        float distanceFalloff = 1f - (distanceToPlayer / SpellToCast.ExplosionRadius);
        distanceFalloff = Mathf.Clamp01(distanceFalloff);

        // Для очень близких взрывов используем максимальную силу
        if (distanceToPlayer < 0.5f)
        {
            distanceFalloff = 1f; // Максимальная сила для взрывов "в упор"
        }

        // Apply more force at close range
        float baseForce = SpellToCast.KnockbackForce * distanceFalloff;

        // Apply horizontal multiplier for stronger horizontal push
        float horizontalForce = baseForce * SpellToCast.HorizontalForceMultiplier;

        // If explosion is below player (like casting at your feet), emphasize upward force
        float verticalDifference = playerPos.y - explosionPos.y;
        bool explosionBelow = verticalDifference > -0.5f; // Explosion is at or below player level

        Vector3 knockbackForce;

        if (SpellToCast.OnlyHorizontalKnockback)
        {
            // Only horizontal knockback with multiplier
            knockbackForce = new Vector3(
                knockbackDirection.x * horizontalForce,
                0,
                knockbackDirection.z * horizontalForce
            );
        }
        else
        {
            // Calculate knockback with vertical component
            float upwardForce = SpellToCast.UpwardForce;

            // If explosion is below the player, increase upward force
            if (explosionBelow && distanceFalloff > 0.5f) // Close range explosion from below
            {
                upwardForce = Mathf.Max(upwardForce, 0.7f); // Ensure minimum upward force
            }

            // Create knockback force with proper vertical component
            knockbackForce = new Vector3(
                knockbackDirection.x * horizontalForce,  // Усиленная горизонтальная сила
                baseForce * upwardForce,                  // Вертикальная сила без усиления
                knockbackDirection.z * horizontalForce    // Усиленная горизонтальная сила
            );

            // For very close explosions at feet level, boost vertical component
            if (distanceToPlayer < 2f && explosionBelow)
            {
                knockbackForce.y = baseForce * Mathf.Max(upwardForce, 0.8f);
            }

            // Гарантируем минимальный горизонтальный knockback для взрывов вплотную
            if (distanceToPlayer < 1f)
            {
                float minHorizontalForce = SpellToCast.KnockbackForce * 0.8f; // Увеличили с 0.7 до 0.8

                // Проверяем что горизонтальные компоненты достаточно сильные
                Vector2 currentHorizontalForce = new Vector2(knockbackForce.x, knockbackForce.z);
                if (currentHorizontalForce.magnitude < minHorizontalForce)
                {
                    // Усиливаем горизонтальную составляющую
                    float boostFactor = minHorizontalForce / Mathf.Max(currentHorizontalForce.magnitude, 0.1f);
                    knockbackForce.x *= boostFactor;
                    knockbackForce.z *= boostFactor;
                    Debug.Log($"Boosted horizontal knockback by factor: {boostFactor}");
                }
            }
        }

        // Apply knockback via ClientRpc
        playerMovement.RpcApplyKnockback(knockbackForce, SpellToCast.UpwardForce, SpellToCast.OnlyHorizontalKnockback);
    }

    [Server]
    private IEnumerator DestroyExplosionAfterTime(GameObject explosion, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (explosion != null)
        {
            NetworkServer.Destroy(explosion);
        }
    }

    [Server]
    private void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }

    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (SpellToCast != null)
        {
            // Draw explosion radius
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, SpellToCast.ExplosionRadius);

            // Draw spell collision radius
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, SpellToCast.SpellRadius);

            if (SpellToCast.ShowDebugVisuals)
            {
                // Draw raised explosion check point
                Vector3 explosionCheckPoint = transform.position + Vector3.up * 0.5f;
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(explosionCheckPoint, 0.3f);

                // Draw line from explosion to check point
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, explosionCheckPoint);
            }
        }
    }

    // Additional debug method for runtime debugging
    [Server]
    private void DebugKnockback(string playerName, float distance, bool canAffect, string reason = "")
    {
        if (SpellToCast.ShowDebugVisuals)
        {
            Debug.Log($"[Knockback Debug] Player: {playerName}, Distance: {distance:F2}, CanAffect: {canAffect}, Reason: {reason}");
        }
    }
}