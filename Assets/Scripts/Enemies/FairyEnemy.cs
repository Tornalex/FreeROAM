using UnityEngine;

public class FairyEnemy : BaseEnemy, IDamageable
{
        public override bool TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
    {

        if (enemyData != null)
        {
            knockback?.ApplyKnockback(damageSourcePosition, enemyData.knockbackForce, enemyData.knockbackDuration);
        }

        SFXManager.Instance?.PlaySFX(SFXType.EnemyPierced);
        VFXManager.Instance?.PlayVFX(VFXType.HitDash, hitPoint, (hitPoint - (Vector2)transform.position).normalized);

        if (animator != null) animator.SetTrigger("TookHit");

        if (hitFlash != null) hitFlash.Flash();

        if (currentHealth <= 0) Die();

        return damage > 0;
    }
}
