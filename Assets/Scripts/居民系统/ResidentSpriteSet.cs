using UnityEngine;

public enum ResidentFacing { Front, Left, Right, Back }

[CreateAssetMenu(menuName = "Chimera/居民精灵组", fileName = "WorkerSprites")]
public sealed class ResidentSpriteSet : ScriptableObject
{
    public const string DefaultResourcePath = "Residents/WorkerSprites";
    [Tooltip("顺序：正面、左侧、右侧、背面。当前为48×48、50 PPU，定位点保持一致。")]
    public Sprite[] Idle = new Sprite[4];
    public Sprite[] Carry = new Sprite[4];
    public Sprite Portrait => Get(ResidentFacing.Front, false);

    public Sprite Get(ResidentFacing facing, bool carrying)
    {
        int index = (int)facing;
        var sprites = carrying ? Carry : Idle;
        if (sprites != null && index >= 0 && index < sprites.Length && sprites[index] != null)
            return sprites[index];
        return Idle != null && Idle.Length > 0 ? Idle[0] : null;
    }
}
