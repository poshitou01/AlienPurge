using UnityEngine;


[DisallowMultipleComponent]
public class WeaponUpgradeVisualController : MonoBehaviour
{

    // =========================================================
    // References
    // =========================================================

    [Header("Weapon Visual")]

    [SerializeField]
    private SpriteRenderer weaponRenderer;


    [SerializeField]
    private Sprite[] weaponLevelSprites;



    [Header("Muzzle Flash")]

    [SerializeField]
    private Transform muzzleFlashTransform;



    [SerializeField]
    private float[] muzzleFlashScales;


    [SerializeField]
    private Sprite[] weaponSprites;


  


    // =========================================================
    // Runtime
    // =========================================================


    private int currentLevel = 1;



    // =========================================================
    // Public API
    // =========================================================


    public void ApplyWeaponLevel(
        int level)
    {

        currentLevel = level;


        ApplyWeaponSprite();

        ApplyMuzzleFlashScale();

    }



    // =========================================================
    // Visual
    // =========================================================


    private void ApplyWeaponSprite()
    {

        if (
            weaponRenderer == null ||
            weaponLevelSprites == null ||
            weaponLevelSprites.Length == 0
        )
        {
            return;
        }


        int index =
            Mathf.Clamp(
                currentLevel - 1,
                0,
                weaponLevelSprites.Length - 1
            );


        weaponRenderer.sprite =
            weaponLevelSprites[index];

    }



    private void ApplyMuzzleFlashScale()
    {

        if (
            muzzleFlashTransform == null ||
            muzzleFlashScales == null ||
            muzzleFlashScales.Length == 0
        )
        {
            return;
        }


        int index =
            Mathf.Clamp(
                currentLevel - 1,
                0,
                muzzleFlashScales.Length - 1
            );


        float scale =
            muzzleFlashScales[index];


        muzzleFlashTransform.localScale =
            Vector3.one * scale;

    }

}