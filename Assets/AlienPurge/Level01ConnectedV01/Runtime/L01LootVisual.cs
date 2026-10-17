using UnityEngine;

namespace AlienPurge.Level01ConnectedV01
{
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class L01LootVisual : MonoBehaviour
    {
        public LootContainer container;
        public MeshRenderer visual;
        [Range(0,3)]public int kind;
        public float openedYOffset=-.06f;
        [SerializeField] bool hasOpenedVisual;
        static readonly int CellId=Shader.PropertyToID("_Cell"),FocusId=Shader.PropertyToID("_Focus");
        static PlayerInteractor sharedInteractor;
        static float nextPlayerLookup;
        PlayerInteractor interactor;
        MaterialPropertyBlock properties;
        Vector3 closedLocalPosition;
        bool initialized;
        MeshRenderer appliedVisual;
        int appliedKind;
        bool applied,appliedOpen,appliedFocus;
        float appliedYOffset;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSharedLookup(){sharedInteractor=null;nextPlayerLookup=0;}
        void Awake()
        {
            if(container==null)container=GetComponent<LootContainer>();
            if(visual!=null)closedLocalPosition=visual.transform.localPosition;
            initialized=true;
        }
        void OnEnable()
        {
            applied=false;
            if(container==null)container=GetComponent<LootContainer>();
            if(container!=null){container.SearchStateChanged-=OnSearchState;container.SearchStateChanged+=OnSearchState;}
        }
        void OnDisable(){if(container!=null)container.SearchStateChanged-=OnSearchState;}
        void OnSearchState(LootContainer value)
        {
            if(value.State!=LootContainerState.Closed)hasOpenedVisual=true;
            Apply();
        }
        void LateUpdate()
        {
            if(Application.isPlaying)
            {
                // All cases share one lookup. A missing player must not cause
                // 76 scene searches every frame; retry globally twice a second.
                if(sharedInteractor==null&&Time.unscaledTime>=nextPlayerLookup)
                {
                    nextPlayerLookup=Time.unscaledTime+.5f;
                    GameObject p=GameObject.FindGameObjectWithTag("Player");
                    if(p!=null)sharedInteractor=p.GetComponentInChildren<PlayerInteractor>();
                }
                interactor=sharedInteractor;
            }
            Apply();
        }
        void Apply()
        {
            if(visual==null)return;
            if(container!=null&&container.State!=LootContainerState.Closed)hasOpenedVisual=true;
            int cell=Mathf.Clamp(kind,0,3);
            bool focus=interactor!=null&&interactor.CurrentInteractable==container;
            if(applied&&appliedVisual==visual&&appliedKind==cell&&appliedOpen==hasOpenedVisual
                &&appliedFocus==focus&&appliedYOffset==openedYOffset)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            visual.GetPropertyBlock(properties);
            properties.SetVector(CellId,new Vector4(cell*.25f,hasOpenedVisual?0:.5f,.25f,.5f));
            properties.SetFloat(FocusId,focus?1:0);
            visual.SetPropertyBlock(properties);
            if(initialized)
            {
                Vector3 position=closedLocalPosition+Vector3.up*(hasOpenedVisual?openedYOffset:0);
                if(visual.transform.localPosition!=position)visual.transform.localPosition=position;
            }
            applied=true;appliedVisual=visual;appliedKind=cell;appliedOpen=hasOpenedVisual;
            appliedFocus=focus;appliedYOffset=openedYOffset;
        }
    }
}
