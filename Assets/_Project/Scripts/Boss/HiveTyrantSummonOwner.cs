using UnityEngine;

// A returned/reused pooled instance no longer belongs to the old encounter.
public sealed class HiveTyrantSummonOwner : MonoBehaviour
{
    public HiveTyrantCombat Owner { get; private set; }
    public void Claim(HiveTyrantCombat owner) { Owner = owner; }
    private void OnDisable() { Owner = null; }
}
