using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienPurge.Level01.RegionLoop
{
    /// <summary>Presentation only; values and progression still belong to the native HUDManager.</summary>
    public sealed class PlayerStatusPanel : MonoBehaviour
    {
        public Slider health;
        public Image healthFill;
        public TMP_Text healthLabel;
        public Color normal = new Color(.68f,.29f,.21f), low = new Color(1,.39f,.25f);
        float previous=-1,flashUntil;
        void LateUpdate()
        {
            if(health==null||healthFill==null)return;
            if(previous>=0&&health.value<previous)flashUntil=Time.time+.16f;
            previous=health.value;
            bool danger=health.normalizedValue<=.3f;
            healthFill.color=Time.time<flashUntil?new Color(1,.83f,.62f):danger?low:normal;
            if(healthLabel!=null)healthLabel.color=danger?low:new Color(.9f,.91f,.85f);
        }
    }
}
