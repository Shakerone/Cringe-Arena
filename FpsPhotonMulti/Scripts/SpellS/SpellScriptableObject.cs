using UnityEngine;

[CreateAssetMenu(fileName = "New Spell", menuName = "Spells")]
public class SpellScriptableObject : ScriptableObject
{
    [Header("Basic Settings")]
    public float DamageAmount = 10f;
    public float Lifetime = 2f;
    public float Speed = 15f;
    public float SpellRadius = 0.5f;
    public float ExplosionRadius = 10f;

    [Header("Knockback System")]
    [Tooltip("Enable/Disable knockback for testing purposes")]
    public bool EnableKnockback = true;         // Главный флаг для включения/выключения

    [Space]
    [Tooltip("Base knockback force strength")]
    public float KnockbackForce = 12f;          // Базовая сила отбрасывания

    [Tooltip("Horizontal force multiplier. Values > 1 boost horizontal knockback")]
    [Range(0.5f, 3f)]
    public float HorizontalForceMultiplier = 1.5f;  // Множитель горизонтальной силы

    [Tooltip("Upward force multiplier (0-1). Higher values = more vertical knockback")]
    [Range(0f, 1f)]
    public float UpwardForce = 0.5f;            // Сила подъема (0-1)

    [Tooltip("If true, only horizontal knockback will be applied")]
    public bool OnlyHorizontalKnockback = false; // Только горизонтальное отбрасывание

    [Header("Debug")]
    [Tooltip("Show explosion and knockback debug visuals in Scene view")]
    public bool ShowDebugVisuals = false;
}