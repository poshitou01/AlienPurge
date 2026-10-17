using UnityEngine;
using TMPro;

namespace AlienPurge.Level01ConnectedV01
{
    [DisallowMultipleComponent]
    public sealed class L01Terminal : InteractableBase
    {
        public L01Progression progression;
        public string requiredFlag,completesFlag,label;
        public float duration=4f,cancelDistance=2.6f;
        public TMP_Text status;
        [SerializeField] float elapsed;
        PlayerInteractor active;
        public override bool CanInteract(PlayerInteractor interactor)
        {
            return base.CanInteract(interactor)&&progression!=null&&progression.Has(requiredFlag)&&!progression.Has(completesFlag);
        }
        public override void Interact(PlayerInteractor interactor)
        {
            if(CanInteract(interactor))active=interactor;
        }
        void Update()
        {
            if(progression==null)return;
            bool complete=progression.Has(completesFlag),available=progression.Has(requiredFlag);
            if(active!=null && !complete)
            {
                if(Vector2.Distance(active.transform.position,transform.position)>cancelDistance)active=null;
                else if(L01ProjectAccess.IsRunActive())
                {
                    elapsed+=Time.deltaTime;
                    if(elapsed>=Mathf.Max(.1f,duration)){progression.Complete(completesFlag);active=null;complete=true;}
                }
            }
            if(status!=null)
            {
                status.gameObject.SetActive(available||complete);
                status.text=label+(complete?"\nONLINE":elapsed>0?"\n"+Mathf.RoundToInt(Mathf.Clamp01(elapsed/Mathf.Max(.1f,duration))*100)+"%":"");
                status.color=complete?new Color(.43f,.93f,.72f):new Color(.83f,.95f,1);
            }
        }
    }
}
