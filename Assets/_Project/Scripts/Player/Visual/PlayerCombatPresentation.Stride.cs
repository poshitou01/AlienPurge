using System.Collections.Generic;
using UnityEngine;

public sealed partial class PlayerCombatPresentation
{
    public Material heldStrideMaterial;
    Material strideMaterial;
    readonly Dictionary<Sprite,Sprite> heldBodies=new Dictionary<Sprite,Sprite>();
    static readonly int Walking=Shader.PropertyToID("_Walking"),LegJoin=Shader.PropertyToID("_LegJoin"),StridePhaseId=Shader.PropertyToID("_Stride"),Facing=Shader.PropertyToID("_Facing");

    void CreateHeldBodies()
    {
        if(heldStrideMaterial==null)return;
        strideMaterial=new Material(heldStrideMaterial){name="Player_HeldStride_Runtime"};
        bodyRenderer.sharedMaterial=strideMaterial;
        foreach(var pose in poses)
        {
            var source=pose.idle;
            // Full quad lets the animated legs extend outside the idle sprite's tight silhouette.
            var body=Sprite.Create(source.texture,source.rect,source.pivot/source.rect.size,source.pixelsPerUnit,0,SpriteMeshType.FullRect);
            body.name=source.name+"_HeldBody";heldBodies.Add(source,body);
        }
    }
    void ApplyHeldBody(Pose pose)
    {
        if(strideMaterial==null){bodyRenderer.sprite=pose.idle;return;}
        bodyRenderer.sprite=heldBodies[pose.idle];
        strideMaterial.SetFloat(Walking,strideBlend);
        strideMaterial.SetFloat(StridePhaseId,stride);
        strideMaterial.SetVector(Facing,new Vector4(Mathf.Cos(sector*Mathf.PI/4),Mathf.Sin(sector*Mathf.PI/4),0,0));
        strideMaterial.SetFloat(LegJoin,pose.legJoin);
    }
    void DisposeHeldBodies()
    {
        foreach(var sprite in heldBodies.Values)Destroy(sprite);
        heldBodies.Clear();if(strideMaterial!=null)Destroy(strideMaterial);
    }
}
