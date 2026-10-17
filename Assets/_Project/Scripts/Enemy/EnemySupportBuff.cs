using System.Collections.Generic;
using UnityEngine;

// Runtime leases only. Never writes EnemyData or the target's base damage.
[DisallowMultipleComponent]
public sealed class EnemySupportBuff : MonoBehaviour
{
    private readonly List<SupportEnemyAbility> sources = new List<SupportEnemyAbility>(4);
    public uint Generation { get; private set; }
    public int BonusDamage
    {
        get
        {
            int bonus = 0;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                var source = sources[i];
                if (source == null || !source.HasValidLease(this)) sources.RemoveAt(i);
                else bonus = Mathf.Max(bonus, source.Config.bonusDamage);
            }
            return bonus;
        }
    }
    private void OnEnable() { Generation++; sources.Clear(); }
    private void OnDisable() { sources.Clear(); Generation++; }
    public void Grant(SupportEnemyAbility source)
    {
        if (!sources.Contains(source)) sources.Add(source);
    }
    public void Revoke(SupportEnemyAbility source, uint generation)
    {
        if (Generation == generation) sources.Remove(source);
    }
}
