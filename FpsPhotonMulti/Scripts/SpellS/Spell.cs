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

    private bool hasExploded = false; // ���� ��� �������������� ��������� �������
    private float ignoreTimeAfterCast = 0.1f; // ����� ������������� ������� ����� �������
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

        // �����������: ���������� �������� � �������� �� ������
        if (NetworkServer.spawned.TryGetValue(casterId, out NetworkIdentity casterIdentity))
        {
            Collider casterCollider = casterIdentity.GetComponent<Collider>();
            if (casterCollider != null && myCollider != null)
            {
                Physics.IgnoreCollision(myCollider, casterCollider, true);
                // �������� �������� ������� ����� �������� �����
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
        // ���� ��� ����������, �� ������������ ��������
        if (hasExploded) return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

        // ���� ������ � ������
        if (playerHealth != null)
        {
            // �������������� ��������: ���������� ������� � ������ �������
            if (playerHealth.netId == casterId)
            {
                // ���� ������ ���� ������� � ������� ����� - ����������
                if (Time.time - spawnTime < ignoreTimeAfterCast)
                {
                    return;
                }
                // ����� ��������� ������� ������������� - ����� ���������� �� ����
            }


            if (this.gameObject.GetComponent<FrostMissleCollision>() != null)
            {
                GameObject freeze = Instantiate(freezePrefab, other.transform.position, Quaternion.identity);
            }

        }

        // ���������� ��� ������������ � ����� ��������
        ExplodeSpell();
    }

    [Server]
    private void ExplodeSpell()
    {
        if (hasExploded) return; // ������������� ��������� ������
        hasExploded = true;

        // ������� ������ ������
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            NetworkServer.Spawn(explosion);

            // ���������� ������ ������ ����� ��������� �����
            StartCoroutine(DestroyExplosionAfterTime(explosion, 3f)); // 3 �������
        }


        if (this.name == "FireMissle")
        {
            // ������� ���� � knockback ���� ������� � ������� ������
            DamageAndKnockbackPlayersInRadius();
        }



        // ���������� ��� ��������
        DestroySelf();
    }

    [Server]
    private void DamageAndKnockbackPlayersInRadius()
    {
        // ������� ���� ������� � ������� ������
        Collider[] playersInRange = Physics.OverlapSphere(transform.position, SpellToCast.ExplosionRadius);

        foreach (Collider playerCollider in playersInRange)
        {
            PlayerHealth playerHealth = playerCollider.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                // �� ������� ������ ���� (�� knockback ����� ���������)
                bool isSelfDamage = playerHealth.netId == casterId;

                // ���������, ���� �� ����������� ����� ������� ������ � �������
                Vector3 explosionCheckPoint = transform.position + Vector3.up * 0.5f; // ��������� ����� ������ ������� �����
                Vector3 playerCenter = playerHealth.transform.position + Vector3.up; // ����� ������
                Vector3 directionToPlayer = (playerCenter - explosionCheckPoint).normalized;
                float distanceToPlayer = Vector3.Distance(transform.position, playerHealth.transform.position); // ���������� �������� ��������� �� ������

                // ���������� SphereCast ������ Raycast ��� ������� �����������
                RaycastHit hit;
                float sphereRadius = 0.3f; // ������ ��� SphereCast
                bool hasObstacle = Physics.SphereCast(explosionCheckPoint, sphereRadius, directionToPlayer,
                    out hit, Vector3.Distance(explosionCheckPoint, playerCenter),
                    ~LayerMask.GetMask("Player", "Spell")); // ���������� ���� Player � Spell

                // ���������, ��� ����������� - ��� �� ��� ����� � �� ����� ��� ��������� �����
                bool canAffectPlayer = !hasObstacle ||
                                       hit.collider == playerCollider ||
                                       hit.collider.CompareTag("Player") ||
                                       (hit.normal.y > 0.7f); // ���� ��� ���/����� (������� �����), �� ���������

                // Debug ����������
                if (SpellToCast.ShowDebugVisuals)
                {
                    string blockReason = "";
                    if (hasObstacle && hit.collider != null)
                    {
                        blockReason = $"Blocked by: {hit.collider.name}, Normal Y: {hit.normal.y:F2}";
                    }
                    DebugKnockback(playerHealth.name, distanceToPlayer, canAffectPlayer, blockReason);
                }

                // ���� ����� �������������� �� ������
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
                                // ��������� �������� �� �����
                                float healthBeforeDamage = playerHealth.GetCurrentHealth();

                                // ������� ���� ������ ���� ���� ��� ����
                                if (healthBeforeDamage > 0)
                                {
                                    playerHealth.TakeDamage(SpellToCast.DamageAmount);

                                    // ���������� ���� ����������
                                    attackerHealth.TargetShowDamage(attackerIdentity.connectionToClient, SpellToCast.DamageAmount, playerHealth.transform.position);

                                    // ��������� �������� ����� �����
                                    float healthAfterDamage = playerHealth.GetCurrentHealth();

                                    // ���� ���� ������ �� ����� �����, ���������� ������ ����������
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

        // Calculate direction - ���������� ������ �������������� ��������� ��� ��������
        Vector3 knockbackDirection = (playerPos - explosionPos);
        Vector3 horizontalDirection = new Vector3(
            playerPos.x - explosionPos.x,
            0,
            playerPos.z - explosionPos.z
        );

        // �����: ��������� �������������� ��������, � �� �����
        if (horizontalDirection.magnitude < 0.5f) // ����� ������ �� �����������
        {
            // ���������� ����������� "�����" �� ������� ������
            Transform playerTransform = playerHealth.transform;

            // ����� ����� �������������� ����������� �����
            Vector3 backDirection = -playerTransform.forward;
            backDirection.y = 0; // ������� ������������ ������������
            backDirection = backDirection.normalized;

            // ����������� � ��������� ��������
            knockbackDirection = backDirection + Vector3.up * 0.2f;

            // ��������� ��������� ����������� ������ �� �����������
            knockbackDirection.x += UnityEngine.Random.Range(-0.1f, 0.1f);
            knockbackDirection.z += UnityEngine.Random.Range(-0.1f, 0.1f);

            Debug.Log($"Close explosion - using backward direction. Horizontal dist: {horizontalDirection.magnitude}");
        }
        else
        {
            // ���� ���� �������������� ���������, �� ��� ��������� - ���������
            if (horizontalDirection.magnitude < 1f && horizontalDirection.magnitude > 0.1f)
            {
                // ��������� �������������� ������������
                Vector3 boostedHorizontal = horizontalDirection.normalized * 0.7f;
                knockbackDirection = new Vector3(
                    boostedHorizontal.x,
                    knockbackDirection.y * 0.3f, // ��������� ������������ ������������
                    boostedHorizontal.z
                );
                Debug.Log("Boosting horizontal knockback for near-wall explosion");
            }
        }

        knockbackDirection = knockbackDirection.normalized;

        // Calculate force falloff based on distance (closer = stronger)
        float distanceFalloff = 1f - (distanceToPlayer / SpellToCast.ExplosionRadius);
        distanceFalloff = Mathf.Clamp01(distanceFalloff);

        // ��� ����� ������� ������� ���������� ������������ ����
        if (distanceToPlayer < 0.5f)
        {
            distanceFalloff = 1f; // ������������ ���� ��� ������� "� ����"
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
                knockbackDirection.x * horizontalForce,  // ��������� �������������� ����
                baseForce * upwardForce,                  // ������������ ���� ��� ��������
                knockbackDirection.z * horizontalForce    // ��������� �������������� ����
            );

            // For very close explosions at feet level, boost vertical component
            if (distanceToPlayer < 2f && explosionBelow)
            {
                knockbackForce.y = baseForce * Mathf.Max(upwardForce, 0.8f);
            }

            // ����������� ����������� �������������� knockback ��� ������� ��������
            if (distanceToPlayer < 1f)
            {
                float minHorizontalForce = SpellToCast.KnockbackForce * 0.8f; // ��������� � 0.7 �� 0.8

                // ��������� ��� �������������� ���������� ���������� �������
                Vector2 currentHorizontalForce = new Vector2(knockbackForce.x, knockbackForce.z);
                if (currentHorizontalForce.magnitude < minHorizontalForce)
                {
                    // ��������� �������������� ������������
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