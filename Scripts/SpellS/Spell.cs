using UnityEngine;
using Mirror;
using System.Collections;

[RequireComponent(typeof(SphereCollider))]
public class Spell : NetworkBehaviour
{
    public SpellScriptableObject SpellToCast;
    [SerializeField] private GameObject explosionPrefab;
    private SphereCollider myCollider;
    [SyncVar] public uint casterId;

    private bool hasExploded = false;

    private void Awake()
    {
        myCollider = GetComponent<SphereCollider>();
        myCollider.isTrigger = true;
        myCollider.radius = SpellToCast.SpellRadius;
    }

    public override void OnStartServer()
    {
        Invoke(nameof(ExplodeSpell), SpellToCast.Lifetime);
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
        if (hasExploded) return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

        if (playerHealth != null)
        {
            if (playerHealth.netId == casterId)
            {
                return;
            }
        }

        ExplodeSpell();
    }

    [Server]
    private void ExplodeSpell()
    {
        if (hasExploded) return;
        hasExploded = true;

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            NetworkServer.Spawn(explosion);
            StartCoroutine(DestroyExplosionAfterTime(explosion, 3f));
        }

        DamageAndKnockbackPlayersInRadius();
        DestroySelf();
    }

    [Server]
    private void DamageAndKnockbackPlayersInRadius()
    {
        Collider[] playersInRange = Physics.OverlapSphere(transform.position, SpellToCast.ExplosionRadius);

        foreach (Collider playerCollider in playersInRange)
        {
            PlayerHealth playerHealth = playerCollider.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                bool isSelfDamage = playerHealth.netId == casterId;

                // чтобы raycast были повыше и ему не мешали маленькие предметы на полу
                Vector3 explosionCheckPoint = transform.position + Vector3.up * 0.5f;
                Vector3 playerCenter = playerHealth.transform.position + Vector3.up;
                Vector3 directionToPlayer = (playerCenter - explosionCheckPoint).normalized;

               float distanceToPlayer = Vector3.Distance(transform.position, playerHealth.transform.position);

                RaycastHit hit;

                float sphereRadius = 0.3f;

                // С инверсией (~): Проверять ВСЁ КРОМЕ Player и Spell
                bool hasObstacle = Physics.SphereCast(explosionCheckPoint, sphereRadius, directionToPlayer,
                    out hit, Vector3.Distance(explosionCheckPoint, playerCenter),
                    ~LayerMask.GetMask("Player", "Spell"));

                bool canAffectPlayer = !hasObstacle ||
                                       hit.collider == playerCollider ||
                                       hit.collider.CompareTag("Player") ||
                                       (hit.normal.y > 0.7f);

                if (canAffectPlayer)
                {
                    if (SpellToCast.EnableKnockback)
                    {
                        ApplyKnockbackToPlayer(playerHealth, distanceToPlayer);
                    }

                    if (!isSelfDamage)
                    {
                        if (NetworkServer.spawned.TryGetValue(casterId, out NetworkIdentity attackerIdentity))
                        {
                            PlayerHealth attackerHealth = attackerIdentity.GetComponent<PlayerHealth>();
                            if (attackerIdentity.connectionToClient != null && attackerHealth != null)
                            {
                                float healthBeforeDamage = playerHealth.GetCurrentHealth();

                                if (healthBeforeDamage > 0)
                                {
                                    playerHealth.TakeDamage(SpellToCast.DamageAmount);

                                    attackerHealth.TargetShowDamage(attackerIdentity.connectionToClient, SpellToCast.DamageAmount, playerHealth.transform.position);

                                    float healthAfterDamage = playerHealth.GetCurrentHealth();

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

        Vector3 explosionPos = transform.position;
        Vector3 playerPos = playerHealth.transform.position;

        Vector3 knockbackDirection = (playerPos - explosionPos).normalized;

        float distanceFalloff = 1f - (distanceToPlayer / SpellToCast.ExplosionRadius);
        distanceFalloff = Mathf.Clamp01(distanceFalloff);

        float baseForce = SpellToCast.KnockbackForce * distanceFalloff;
        float horizontalForce = baseForce * SpellToCast.HorizontalForceMultiplier;

        float verticalDifference = playerPos.y - explosionPos.y;
        bool explosionBelow = verticalDifference > -0.5f;

        Vector3 knockbackForce;

        if (SpellToCast.OnlyHorizontalKnockback)
        {
            knockbackForce = new Vector3(
                knockbackDirection.x * horizontalForce,
                0,
                knockbackDirection.z * horizontalForce
            );
        }
        else
        {
            float upwardForce = SpellToCast.UpwardForce;

            if (explosionBelow && distanceFalloff > 0.5f)
            {
                upwardForce = Mathf.Max(upwardForce, 0.7f);
            }

            knockbackForce = new Vector3(
                knockbackDirection.x * horizontalForce,
                baseForce * upwardForce,
                knockbackDirection.z * horizontalForce
            );

            if (distanceToPlayer < 2f && explosionBelow)
            {
                knockbackForce.y = baseForce * Mathf.Max(upwardForce, 0.8f);
            }
        }

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
}