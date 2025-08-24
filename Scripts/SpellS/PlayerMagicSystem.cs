using UnityEngine;
using Mirror;

public class PlayerMagicSystem : NetworkBehaviour
{
    [Header("Spell Settings")]
    [SerializeField] private Spell spellToCast;
    [SerializeField] private float timeBetweenCasts = 0.25f;
    private float currentCastTimer;

    [Header("Cast Point Settings")]
    [SerializeField] private Transform castPoint;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float minCastDistance = 1.5f;
    [SerializeField] private float maxCastCheckDistance = 2.5f;
    [SerializeField] private float castPointRadius = 0.3f;
    [SerializeField] private LayerMask obstacleCheckLayers = -1;

    private bool castingMagic = false;
    private PlayerControls playerControls;

    private void Awake()
    {
        playerControls = new PlayerControls();

        if (playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                playerCamera = cam.transform;
        }
    }

    private void OnEnable()
    {
        playerControls.Enable();
    }

    private void OnDisable()
    {
        playerControls.Disable();
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        bool isSpellCastHeldDown = playerControls.Controls.SpellCast.ReadValue<float>() > 0.1f;

        if (!castingMagic && isSpellCastHeldDown)
        {
            castingMagic = true;
            currentCastTimer = 0;

            Vector3 safeSpawnPoint;
            Vector3 castDirection;

            CalculateSafeSpawnPoint(out safeSpawnPoint, out castDirection);
            CmdCastSpell(safeSpawnPoint, castDirection);
        }

        if (castingMagic)
        {
            currentCastTimer += Time.deltaTime;
            if (currentCastTimer > timeBetweenCasts)
            {
                castingMagic = false;
            }
        }
    }

    private bool CalculateSafeSpawnPoint(out Vector3 spawnPoint, out Vector3 direction)
    {
        Transform aimTransform = playerCamera != null ? playerCamera : castPoint;

        Vector3 origin = aimTransform.position;
        Vector3 forward = aimTransform.forward;
        direction = forward;

        int layerMaskWithoutPlayer = obstacleCheckLayers & ~(1 << gameObject.layer);

        RaycastHit rayHit;
        bool rayHitWall = Physics.Raycast(origin, forward, out rayHit, minCastDistance, layerMaskWithoutPlayer);

        if (!rayHitWall)
        {
            Vector3 idealPoint = origin + forward * minCastDistance;
            if (!Physics.CheckSphere(idealPoint, castPointRadius, layerMaskWithoutPlayer))
            {
                spawnPoint = idealPoint;
                return true;
            }
        }

        if (rayHitWall && rayHit.distance > castPointRadius * 2)
        {
            spawnPoint = origin + forward * (rayHit.distance - castPointRadius);
            return true;
        }

        Vector3 backwardPoint = origin - forward * (castPointRadius * 3);
        if (!Physics.CheckSphere(backwardPoint, castPointRadius * 0.8f, layerMaskWithoutPlayer))
        {
            spawnPoint = backwardPoint;
            return true;
        }

        Vector3[] sideDirections = {
            transform.right,
            -transform.right,
            transform.right + transform.up * 0.5f,
            -transform.right + transform.up * 0.5f
        };

        foreach (Vector3 sideDir in sideDirections)
        {
            Vector3 sidePoint = origin + sideDir.normalized * (castPointRadius * 3);
            if (!Physics.CheckSphere(sidePoint, castPointRadius * 0.8f, layerMaskWithoutPlayer))
            {
                spawnPoint = sidePoint;
                return true;
            }
        }

        Vector3 abovePoint = origin + Vector3.up * 1.5f;
        if (!Physics.CheckSphere(abovePoint, castPointRadius * 0.8f, layerMaskWithoutPlayer))
        {
            spawnPoint = abovePoint;
            return true;
        }

        spawnPoint = origin + forward * castPointRadius;
        return true;
    }

    public void CastSpellByVoice(string command)
    {
        if (!isLocalPlayer) return;
        if (command == "Fireball" && !castingMagic)
        {
            castingMagic = true;
            currentCastTimer = 0;

            Vector3 safeSpawnPoint;
            Vector3 castDirection;

            CalculateSafeSpawnPoint(out safeSpawnPoint, out castDirection);
            CmdCastSpell(safeSpawnPoint, castDirection);
        }
    }

    [Command]
    private void CmdCastSpell(Vector3 spawnPos, Vector3 direction)
    {
        float maxAllowedDistance = maxCastCheckDistance + 2f;
        float distanceFromPlayer = Vector3.Distance(transform.position, spawnPos);

        if (distanceFromPlayer > maxAllowedDistance)
        {
            spawnPos = transform.position + transform.forward * castPointRadius * 2;
        }

        GameObject spellInstance = Instantiate(spellToCast.gameObject, spawnPos, Quaternion.LookRotation(direction));

        Spell spellComponent = spellInstance.GetComponent<Spell>();
        if (spellComponent != null)
        {
            spellComponent.casterId = netId;

            Rigidbody rb = spellInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = direction * 2f;
            }
        }

        NetworkServer.Spawn(spellInstance);
    }
}