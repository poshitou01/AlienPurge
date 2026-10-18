using UnityEngine;

public sealed partial class PlayerCombatPresentation
{
    sealed class HandPair
    {
        public Sprite near,far;
        public Vector3 nearOffset,farOffset;
    }
    void CacheHandMesh(Sprite source,Pose pose)
    {
        if(source==null||handMeshes.ContainsKey(source)||pose.weaponBehindBody)return;
        var pair=new HandPair();
        pair.near=HandSprite(source,pose.nearHand,out pair.nearOffset);
        pair.far=HandSprite(source,pose.farHand,out pair.farOffset);
        handMeshes.Add(source,pair);
    }
    static Sprite HandSprite(Sprite source,Rect normalized,out Vector3 offset)
    {
        // Sub-sprites share the source pixels; no textures or shared import settings are modified.
        Rect rect=source.rect;
        var crop=new Rect(normalized.x*rect.width,(1-normalized.yMax)*rect.height,
            normalized.width*rect.width,normalized.height*rect.height);
        offset=(crop.center-source.pivot)/source.pixelsPerUnit;
        crop.position+=rect.position;
        var sprite=Sprite.Create(source.texture,crop,new Vector2(.5f,.5f),source.pixelsPerUnit,0,SpriteMeshType.FullRect);
        sprite.name=source.name+"_Hand";return sprite;
    }
    void PlaceHand(SpriteRenderer hand,Vector3 localOffset)
    {
        hand.transform.position=bodyRenderer.transform.TransformPoint(localOffset);
        hand.transform.localScale=bodyRenderer.transform.localScale;
        hand.transform.localRotation=bodyRenderer.transform.localRotation;
        hand.color=bodyRenderer.color;
    }
}
