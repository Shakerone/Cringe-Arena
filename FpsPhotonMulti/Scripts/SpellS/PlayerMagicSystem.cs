using UnityEngine;
using Mirror;

public class PlayerMagicSystem : NetworkBehaviour
{
    [Header("Spell Settings")]
    [SerializeField] private Spell[] spellsToCast;
    [SerializeField] private Spell spellToCast;
    [SerializeField] private float timeBetweenCasts = 0.25f;
    private float currentCastTimer;

    [Header("Cast Point Settings")]
    [SerializeField] private Transform castPoint;
    [SerializeField] private Transform playerCamera; // ������ ������ ��� ����� ������� �����������
    [SerializeField] private float minCastDistance = 1.5f; // ����������� ��������� �� ������
    [SerializeField] private float maxCastCheckDistance = 2.5f; // ������������ �������� ������
    [SerializeField] private float castPointRadius = 0.3f; // ������ ��� �������� ��������
    [SerializeField] private LayerMask obstacleCheckLayers = -1; // ���� ��� �������� �����������

    [Header("Debug")]
    [SerializeField] private bool showDebugVisuals = false;

    private bool castingMagic = false;
    private PlayerControls playerControls;

    private void Awake()
    {
        playerControls = new PlayerControls();

        // ���� ������ �� ���������, ��������� �����
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

            // ��������� ���������� ����� ������ � �����������
            Vector3 safeSpawnPoint;
            Vector3 castDirection;

            // ������ ������ ��������� �������
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
        // ���������� ����������� ������ ���� ����, ����� ����������� ���������
        Transform aimTransform = playerCamera != null ? playerCamera : castPoint;

        // �����: �������� �� ������ ������/������
        Vector3 origin = aimTransform.position;
        Vector3 forward = aimTransform.forward;
        direction = forward; // ����������� ������ ������, ���������� �� ����� ������

        // ��������� ���� ������ �� ��������
        int layerMaskWithoutPlayer = obstacleCheckLayers & ~(1 << gameObject.layer);

        // ��� 1: ��������� ��������� ����� �������
        RaycastHit rayHit;
        bool rayHitWall = Physics.Raycast(origin, forward, out rayHit, minCastDistance, layerMaskWithoutPlayer);

        // ���� ������� �������� - ������� ��� ������
        if (!rayHitWall)
        {
            Vector3 idealPoint = origin + forward * minCastDistance;
            if (!Physics.CheckSphere(idealPoint, castPointRadius, layerMaskWithoutPlayer))
            {
                spawnPoint = idealPoint;
                return true;
            }
        }

        // ��� 2: ���� ������� �����, �� ���� ������� �����
        if (rayHitWall && rayHit.distance > castPointRadius * 2)
        {
            spawnPoint = origin + forward * (rayHit.distance - castPointRadius);
            return true;
        }

        // ��� 3: ����� ����� ������ - ���� �������������� �����

        // ������� ������ ������
        Vector3 backwardPoint = origin - forward * (castPointRadius * 3);
        if (!Physics.CheckSphere(backwardPoint, castPointRadius * 0.8f, layerMaskWithoutPlayer))
        {
            spawnPoint = backwardPoint;
            Debug.Log("Spawning fireball behind player - wall too close!");
            return true;
        }

        // ������� �����
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
                Debug.Log("Spawning fireball to the side - wall too close!");
                return true;
            }
        }

        // ������� ��� �������
        Vector3 abovePoint = origin + Vector3.up * 1.5f;
        if (!Physics.CheckSphere(abovePoint, castPointRadius * 0.8f, layerMaskWithoutPlayer))
        {
            spawnPoint = abovePoint;
            Debug.Log("Spawning fireball above player - wall too close!");
            return true;
        }

        // ��� 4: ��������� ������� - ������� ����� � ������ � ����������� ���������
        // ������� ����� ��������� �� �����, �� ����� ���� �� ����� ����������
        spawnPoint = origin + forward * castPointRadius;
        Debug.LogWarning("Spawning fireball very close - will likely explode immediately!");
        return true; // ������ ���������� true - ������ ���� ����������
    }

    public void CastSpellByVoice(string command)
    {
        if (!isLocalPlayer) return;
        if (command == "Fireball" && !castingMagic)
        {
            spellToCast = spellsToCast[0];
            castingMagic = true;
            currentCastTimer = 0;

            Vector3 safeSpawnPoint;
            Vector3 castDirection;

            // ������ ������ ���������� true
            CalculateSafeSpawnPoint(out safeSpawnPoint, out castDirection);
            CmdCastSpell(safeSpawnPoint, castDirection);
        }
        if (command == "Freeze" && !castingMagic)
        {
            spellToCast = spellsToCast[1];
            castingMagic = true;
            currentCastTimer = 0;

            Vector3 safeSpawnPoint;
            Vector3 castDirection;

            // ������ ������ ���������� true
            CalculateSafeSpawnPoint(out safeSpawnPoint, out castDirection);
            CmdCastSpell(safeSpawnPoint, castDirection);
        }




    }

    [Command]
    private void CmdCastSpell(Vector3 spawnPos, Vector3 direction)
    {
        // �������������� �������� �� ������� ��� ������ �� �����
        float maxAllowedDistance = maxCastCheckDistance + 2f; // ��������� ������ ��� �������������� �����
        float distanceFromPlayer = Vector3.Distance(transform.position, spawnPos);

        if (distanceFromPlayer > maxAllowedDistance)
        {
            Debug.LogWarning($"Suspicious cast position from {netId}, distance: {distanceFromPlayer}");
            // �� �� ����� ������� ����� � �������
            spawnPos = transform.position + transform.forward * castPointRadius * 2;
        }

        GameObject spellInstance = Instantiate(spellToCast.gameObject, spawnPos, Quaternion.LookRotation(direction));

        // ������������� ID ������� � ����������
        Spell spellComponent = spellInstance.GetComponent<Spell>();
        if (spellComponent != null)
        {
            spellComponent.casterId = netId;

            // ��������� ��������� ��������� �������� ����� ������� ������� ������� �� ������
            Rigidbody rb = spellInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = direction * 2f; // ��������� ��������� �������
            }
        }

        NetworkServer.Spawn(spellInstance);
    }

    // ������������ ��� �������
    private void OnDrawGizmos()
    {
        if (!showDebugVisuals || !isLocalPlayer) return;

        Transform aimTransform = playerCamera != null ? playerCamera : castPoint;
        if (aimTransform == null) return;

        Vector3 origin = aimTransform.position;
        Vector3 forward = aimTransform.forward;

        // ���������� ��� ��������
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, forward * maxCastCheckDistance);

        // ���������� ��������� ����� ������
        Vector3 idealPoint = origin + forward * minCastDistance;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(idealPoint, castPointRadius);

        // ��������� �����
        int layerMaskWithoutPlayer = obstacleCheckLayers & ~(1 << gameObject.layer);
        RaycastHit hit;
        if (Physics.Raycast(origin, forward, out hit, maxCastCheckDistance, layerMaskWithoutPlayer))
        {
            // ���������� ����� ������������ �� ������
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hit.point, 0.1f);
            Gizmos.DrawLine(origin, hit.point);

            // ���� ����� ����� ������, ���������� �������������� �����
            if (hit.distance < castPointRadius * 2)
            {
                // ����� ������
                Gizmos.color = Color.magenta;
                Vector3 backPoint = origin - forward * (castPointRadius * 3);
                Gizmos.DrawWireSphere(backPoint, castPointRadius * 0.8f);

                // ����� �� �����
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(origin + transform.right * (castPointRadius * 3), castPointRadius * 0.8f);
                Gizmos.DrawWireSphere(origin - transform.right * (castPointRadius * 3), castPointRadius * 0.8f);
            }
        }

        // ���������� ������� ������
        Gizmos.color = new Color(0, 0, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
    }
}