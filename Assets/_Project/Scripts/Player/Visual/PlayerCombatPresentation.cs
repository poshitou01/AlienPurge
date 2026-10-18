using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Scene-local eight-way aim/strafe presentation. Native weapons retain firing, evolution and pools.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-100)]
public sealed partial class PlayerCombatPresentation : MonoBehaviour
{
    [Serializable] public class Pose
    {
        public string direction;
        public Sprite idle;
        [Tooltip("Hand centre in source sprite pixels, normalized, measured from the top left.")]
        public Vector2 grip;
        public Rect nearHand, farHand;
        public bool weaponBehindBody;
        [Range(.7f,.85f)] public float legJoin=.75f;
    }
    [Serializable] public class WeaponGrip
    {
        public Sprite sprite;
        public Vector2 normalizedGrip;
    }
    [HideInInspector] public int installationRevision;
    public Transform stanceRoot, weaponSocket, weaponHolder;
    public SpriteRenderer bodyRenderer, handOcclusion, supportHandOcclusion;
    public SortingGroup weaponSorting;
    public Pose[] poses;
    public WeaponGrip[] weaponGrips;
    [Min(.1f)] public float strideLength = 2.5f;
    [Range(0, 8)] public float directionHysteresis = 4;
    [Range(0, 4)] public float movementLean = 1.4f;
    [Range(0, .03f)] public float stepBob = .012f;
    [Range(.5f,1)] public float verticalWeaponLength = .78f;

    PlayerMovement movement;
    PlayerHealth health;
    PlayerAbilityState abilities;
    WeaponManager weapons;
    WeaponController trackedWeapon;
    SpriteRenderer gunRenderer;
    Vector2 aimTarget;
    bool hasAim;
    float stride, strideBlend, lean, bob;
    int sector = 6;
    Vector3 holderScale;
    readonly Dictionary<Sprite, HandPair> handMeshes = new Dictionary<Sprite, HandPair>();
    HandPair currentHands;
    public int FacingSector => sector; // E, NE, N, NW, W, SW, S, SE
    public Vector2 AimTarget => aimTarget;
    public Vector3 HandAnchor { get; private set; }
    public Vector3 WeaponGripPosition { get; private set; }
    public Sprite CurrentBodySprite => bodyRenderer.sprite;
    public float StridePhase => stride;

    bool Allowed => Time.timeScale > 0 && !PauseMenuController.IsPaused && !UpgradeManager.IsChoosingUpgrade
        && !WeaponModuleSelectionManager.IsChoosingModule && (health == null || !health.IsDead)
        && (GameManager.Instance == null || GameManager.Instance.IsPlaying);

    void Awake()
    {
        movement=GetComponent<PlayerMovement>();health=GetComponent<PlayerHealth>();
        abilities=GetComponent<PlayerAbilityState>();weapons=GetComponent<WeaponManager>();
        if (poses == null || poses.Length != 8 || bodyRenderer == null || handOcclusion == null || supportHandOcclusion == null
            || stanceRoot == null || weaponHolder == null || weaponSocket == null || weaponSorting == null || movement == null || weapons == null)
        { enabled=false; return; }
        holderScale=weaponHolder.localScale;
        CreateHeldBodies();
        // Reuse the original textures. Hand sub-sprites are cached once and share their source pixels.
        foreach(var pose in poses)
        {
            CacheHandMesh(pose.idle, pose);
        }
        ApplyBody();
    }
    void Update()
    {
        if (!Allowed) return;
        float speed = movement.isActiveAndEnabled ? movement.MeasuredVelocity.magnitude : 0;
        bool airborne=abilities!=null&&(abilities.IsAirborne||abilities.IsDashing);
        bool walking=speed>.12f&&!airborne;
        strideBlend=Mathf.MoveTowards(strideBlend,walking?1:0,Time.deltaTime*16);
        if(walking)
        {
            var facing=new Vector2(Mathf.Cos(sector*Mathf.PI/4),Mathf.Sin(sector*Mathf.PI/4));
            float sign=Vector2.Dot(movement.MeasuredVelocity,facing)<-.2f?-1:1;
            stride=Mathf.Repeat(stride+sign*speed*Time.deltaTime/strideLength,1);
        }
        else if(strideBlend<=0)stride=0;
        float targetLean=airborne?0:-movement.MeasuredVelocity.x/Mathf.Max(1,movement.MoveSpeed)*movementLean;
        lean=Mathf.MoveTowards(lean,targetLean,24*Time.deltaTime);
        bob=Mathf.MoveTowards(bob,walking?Mathf.Abs(Mathf.Sin(stride*Mathf.PI*4))*stepBob:0,.2f*Time.deltaTime);
        Vector2 shoulderRecoil=trackedWeapon!=null?-trackedWeapon.AimDirection*(Mathf.Abs(trackedWeapon.transform.localPosition.x)*.3f):Vector2.zero;
        stanceRoot.localPosition=new Vector3(shoulderRecoil.x,bob+shoulderRecoil.y,0);
        stanceRoot.localRotation=Quaternion.Euler(0,0,lean);
        ApplyBody();
        // Retain the last valid gameplay aim when the cursor is over UI; keep the grip attached while moving.
        if(weapons.CurrentWeapon!=null)
            PrepareAim(weapons.CurrentWeapon,hasAim?aimTarget:(Vector2)bodyRenderer.transform.position+Vector2.down*10);
    }
    public void PrepareAim(WeaponController weapon,Vector2 target)
    {
        if (!isActiveAndEnabled || !Allowed || weapon==null || weaponHolder==null || weaponSocket==null)return;
        Vector2 direction=target-(Vector2)bodyRenderer.transform.position;
        // Cursor inside the body must not make the body and gun flicker between opposite directions.
        if(direction.sqrMagnitude<.16f)return;
        aimTarget=target;hasAim=true;
        float angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
        if(Mathf.Abs(Mathf.DeltaAngle(sector*45,angle))>22.5f+directionHysteresis)
            sector=(Mathf.RoundToInt(angle/45)+8)%8;
        ApplyBody();
        if(trackedWeapon!=weapon)
        {
            trackedWeapon=weapon;
            var visual=weapon.transform.Find("WeaponVisual");
            gunRenderer=visual!=null?visual.GetComponent<SpriteRenderer>():weapon.GetComponentInChildren<SpriteRenderer>();
        }
        if(gunRenderer==null||gunRenderer.sprite==null)return;
        float vertical=Mathf.Abs(direction.normalized.y);
        weaponHolder.localScale=new Vector3(holderScale.x*Mathf.Lerp(1,verticalWeaponLength,vertical*vertical),holderScale.y,holderScale.z);
        var pose=poses[sector];
        HandAnchor=bodyRenderer.transform.TransformPoint(SpritePoint(bodyRenderer.sprite,pose.grip));
        Vector2 normalized=new Vector2(.366f,.61f);
        foreach(var profile in weaponGrips)if(profile.sprite==gunRenderer.sprite){normalized=profile.normalizedGrip;break;}
        // Remove the native recoil offset when solving the neutral grip. Recoil then moves into the palm instead of being cancelled.
        Vector3 gripInHolder=weaponHolder.InverseTransformPoint(gunRenderer.transform.TransformPoint(SpritePoint(gunRenderer.sprite,normalized)))
            -weapon.transform.localPosition;
        for(int i=0;i<3;i++)
        {
            Vector2 aim=target-(Vector2)weaponHolder.position;
            float a=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
            weaponHolder.rotation=Quaternion.Euler(0,0,a);
            // Native AimAt owns weapon flipping; solve with the same sign before it is called this frame.
            Vector3 localGrip=gripInHolder;
            bool aimLeft=aim.x<-.001f?true:aim.x>.001f?false:weapon.IsAimingLeft;
            if (aimLeft != weapon.IsAimingLeft) localGrip.y=-localGrip.y;
            weaponSocket.position=HandAnchor-weaponHolder.TransformVector(localGrip);
        }
        weaponSorting.sortingLayerID=bodyRenderer.sortingLayerID;
        weaponSorting.sortingOrder=pose.weaponBehindBody?-2:2;
        weapon.AimAt(target);
        WeaponGripPosition=gunRenderer.transform.TransformPoint(SpritePoint(gunRenderer.sprite,normalized));
    }
    void ApplyBody()
    {
        var pose=poses[sector];
        ApplyHeldBody(pose);
        currentHands=handMeshes.TryGetValue(pose.idle,out var hands)?hands:null;
        handOcclusion.sprite=currentHands!=null?currentHands.near:null;
        supportHandOcclusion.sprite=currentHands!=null?currentHands.far:null;
        handOcclusion.enabled=!pose.weaponBehindBody&&handOcclusion.sprite!=null;
        supportHandOcclusion.enabled=handOcclusion.enabled;
    }
    void LateUpdate()
    {
        if(bodyRenderer==null||handOcclusion==null)return;
        if(currentHands!=null)
        {
            PlaceHand(handOcclusion,currentHands.nearOffset);
            PlaceHand(supportHandOcclusion,currentHands.farOffset);
        }
    }
    static Vector3 SpritePoint(Sprite sprite,Vector2 topLeftNormalized)
    {
        var rect=sprite.rect;
        return new Vector3((topLeftNormalized.x*rect.width-sprite.pivot.x)/sprite.pixelsPerUnit,
            ((1-topLeftNormalized.y)*rect.height-sprite.pivot.y)/sprite.pixelsPerUnit,0);
    }
    void OnDisable()
    {
        if(handOcclusion!=null)handOcclusion.enabled=false;
        if(supportHandOcclusion!=null)supportHandOcclusion.enabled=false;
        if(stanceRoot!=null){stanceRoot.localPosition=Vector3.zero;stanceRoot.localRotation=Quaternion.identity;}
        stride=strideBlend=lean=bob=0;hasAim=false;
    }
    void OnDestroy()
    {
        foreach(var hands in handMeshes.Values){Destroy(hands.near);Destroy(hands.far);}
        handMeshes.Clear();
        DisposeHeldBodies();
    }
}
