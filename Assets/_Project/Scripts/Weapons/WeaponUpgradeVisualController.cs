using UnityEngine;


[DisallowMultipleComponent]
public class WeaponUpgradeVisualController :
    MonoBehaviour
{
    // =========================================================
    // Weapon Visual
    // =========================================================

    [Header("Weapon Visual")]

    [SerializeField]
    private SpriteRenderer weaponRenderer;

    [SerializeField]
    private Sprite[] weaponLevelSprites;


    // =========================================================
    // Muzzle Flash
    // =========================================================

    [Header("Muzzle Flash")]

    [SerializeField]
    private Transform muzzleFlashTransform;

    [SerializeField]
    private SpriteRenderer muzzleFlashRenderer;

    [SerializeField]
    private float[] muzzleFlashScales;


    // =========================================================
    // Runtime
    // =========================================================

    private int currentLevel = 1;

    private WeaponEvolutionData
        currentEvolutionData;

    private Sprite starterWeaponSprite;

    private Sprite starterMuzzleFlashSprite;

    private Vector3 starterWeaponLocalPosition;

    private Vector3 starterWeaponLocalScale;

    private Vector3 starterMuzzleFlashLocalPosition;

    private Vector3 starterMuzzleFlashLocalScale;

    private bool initialized;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        EnsureInitialized();
    }


    // =========================================================
    // Initialization
    // =========================================================

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }


        if (muzzleFlashRenderer == null
            && muzzleFlashTransform != null)
        {
            muzzleFlashRenderer =
                muzzleFlashTransform
                    .GetComponent<SpriteRenderer>();
        }


        if (weaponRenderer != null)
        {
            starterWeaponSprite =
                weaponRenderer.sprite;

            starterWeaponLocalPosition =
                weaponRenderer
                    .transform.localPosition;

            starterWeaponLocalScale =
                weaponRenderer
                    .transform.localScale;
        }


        if (muzzleFlashTransform != null)
        {
            starterMuzzleFlashLocalPosition =
                muzzleFlashTransform.localPosition;

            starterMuzzleFlashLocalScale =
                muzzleFlashTransform.localScale;
        }


        if (muzzleFlashRenderer != null)
        {
            starterMuzzleFlashSprite =
                muzzleFlashRenderer.sprite;
        }


        initialized =
            true;
    }


    // =========================================================
    // Public API
    // =========================================================

    public void ApplyWeaponLevel(
        int level)
    {
        EnsureInitialized();


        currentLevel =
            Mathf.Max(
                1,
                level
            );


        RefreshVisual();
    }


    public void ApplyEvolutionVisual(
        WeaponEvolutionData evolutionData)
    {
        EnsureInitialized();


        currentEvolutionData =
            evolutionData;


        RefreshVisual();
    }


    public void ResetEvolutionVisual()
    {
        EnsureInitialized();


        currentEvolutionData =
            null;


        RefreshVisual();
    }


    // =========================================================
    // Refresh
    // =========================================================

    private void RefreshVisual()
    {
        ApplyWeaponVisual();

        ApplyMuzzleFlashVisual();
    }


    // =========================================================
    // Weapon Visual
    // =========================================================

    private void ApplyWeaponVisual()
    {
        if (weaponRenderer == null)
        {
            return;
        }


        Sprite targetSprite =
            starterWeaponSprite;


        if (currentEvolutionData != null
            && currentEvolutionData
                    .WeaponSprite != null)
        {
            targetSprite =
                currentEvolutionData
                    .WeaponSprite;
        }
        else if (weaponLevelSprites != null
                 && weaponLevelSprites.Length > 0)
        {
            int index =
                Mathf.Clamp(
                    currentLevel - 1,
                    0,
                    weaponLevelSprites.Length - 1
                );


            if (weaponLevelSprites[index] != null)
            {
                targetSprite =
                    weaponLevelSprites[index];
            }
        }


        weaponRenderer.sprite =
            targetSprite;


        Vector3 targetPosition =
            starterWeaponLocalPosition;


        float scaleMultiplier =
            1f;


        if (currentEvolutionData != null)
        {
            Vector2 offset =
                currentEvolutionData
                    .WeaponVisualLocalOffset;


            targetPosition.x +=
                offset.x;

            targetPosition.y +=
                offset.y;


            scaleMultiplier =
                currentEvolutionData
                    .WeaponVisualScaleMultiplier;
        }


        weaponRenderer.transform.localPosition =
            targetPosition;


        weaponRenderer.transform.localScale =
            starterWeaponLocalScale
            * scaleMultiplier;
    }


    // =========================================================
    // Muzzle Flash
    // =========================================================

    private void ApplyMuzzleFlashVisual()
    {
        if (muzzleFlashTransform == null)
        {
            return;
        }


        if (muzzleFlashRenderer != null)
        {
            if (currentEvolutionData != null
                && currentEvolutionData
                        .MuzzleFlashSprite != null)
            {
                muzzleFlashRenderer.sprite =
                    currentEvolutionData
                        .MuzzleFlashSprite;
            }
            else
            {
                muzzleFlashRenderer.sprite =
                    starterMuzzleFlashSprite;
            }
        }


        int levelIndex =
            0;


        if (muzzleFlashScales != null
            && muzzleFlashScales.Length > 0)
        {
            levelIndex =
                Mathf.Clamp(
                    currentLevel - 1,
                    0,
                    muzzleFlashScales.Length - 1
                );
        }


        float levelMultiplier =
            1f;


        if (muzzleFlashScales != null
            && muzzleFlashScales.Length > 0)
        {
            levelMultiplier =
                muzzleFlashScales[levelIndex];
        }


        float evolutionMultiplier =
            currentEvolutionData != null
                ? currentEvolutionData
                    .MuzzleFlashScaleMultiplier
                : 1f;


        muzzleFlashTransform.localScale =
            starterMuzzleFlashLocalScale
            * levelMultiplier
            * evolutionMultiplier;


        Vector3 targetPosition =
            starterMuzzleFlashLocalPosition;


        if (currentEvolutionData != null)
        {
            Vector2 offset =
                currentEvolutionData
                    .MuzzleFlashLocalOffset;


            targetPosition.x +=
                offset.x;

            targetPosition.y +=
                offset.y;
        }


        muzzleFlashTransform.localPosition =
            targetPosition;
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Reset Evolution Visual")]
    private void DebugResetEvolutionVisual()
    {
        ResetEvolutionVisual();
    }
}