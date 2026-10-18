using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienPurge.Level01.RegionLoop
{
    // Reads the existing immutable run snapshot and settlement; never grants, discards or saves loot.
    public sealed class RegionResultPresentation : MonoBehaviour
    {
        public TMP_Text heading, subtitle, timeValue, killValue, levelValue, lootTitle, lootSummary, settlement;
        public Button restart, returnToBase, retrySave;
        RunResultSnapshot lastSnapshot;
        RunSettlementResult lastSettlement;
        void OnEnable() { lastSnapshot=null; lastSettlement=null; Refresh(); }
        void Update() { Refresh(); }
        public void Refresh()
        {
            var gm=GameManager.Instance; if(gm==null || gm.CurrentRunResultSnapshot==null)return;
            var result=gm.CurrentRunResultSnapshot;var saved=gm.CurrentRunSettlementResult;
            if(result==lastSnapshot && saved==lastSettlement)return;
            lastSnapshot=result;lastSettlement=saved;
            bool win=result.IsExtractionSuccess, committed=saved!=null&&saved.Committed;
            heading.text=win?"撤离成功":"行动失败";
            heading.color=win?new Color(.86f,.75f,.45f):new Color(1,.48f,.36f);
            subtitle.text=win?"已抵达安全区域  /  行动报告":"生命信号中断  /  行动报告";
            timeValue.text=GameManager.FormatTime(result.SurvivalTime);
            killValue.text=result.KillCount.ToString(); levelValue.text=result.PlayerLevel.ToString();
            lootTitle.text=win?"带回物资":"遗失物资";
            lootSummary.text="物品数量  "+result.LootItemCount+"      总价值  "+result.LootTotalValue+"\n"+
                (result.LootSummary.Count==0?"本次行动未携带物资":string.Join("\n",result.LootSummary.Take(4).Select(x=>x.DisplayName+"  ×"+x.Quantity)))+
                (result.LootSummary.Count>4?"\n另有 "+(result.LootSummary.Count-4)+" 种物资":"");
            settlement.text=!win?"本局携带物资已遗失。\n仓库中的物资不受影响。":committed?
                "已存入仓库 "+saved.StoredItemCount+" 件 · 待领取 "+saved.PendingItemCount+" 件\n返回基地后可在仓库查看。":
                "结算尚未保存，物资仍保留在本局。\n请先重试保存，再离开。";
            restart.interactable=returnToBase.interactable=!win||committed;
            retrySave.gameObject.SetActive(win&&!committed);
        }
    }
}
