using UnityEngine;

// Presentation only. Cargo, pathfinding and resident identity remain owned by their existing systems.
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(ResidentEntity))]
public sealed class ResidentVisual2D : MonoBehaviour
{
    public ResidentSpriteSet Sprites;
    public SpriteRenderer Body;
    public ResidentFacing Facing { get; private set; }
    public bool IsCarrying { get; private set; }
    private ResidentEntity resident;
    private Rigidbody2D bodyPhysics;
    private float nextCargoCheck;
    private bool dirtyCargo = true;

    private void OnEnable()
    {
        resident = GetComponent<ResidentEntity>();
        bodyPhysics = GetComponent<Rigidbody2D>();
        if (Body == null) Body = transform.Find("Visual_Sprite")?.GetComponent<SpriteRenderer>();
        if (Sprites == null) Sprites = Resources.Load<ResidentSpriteSet>(ResidentSpriteSet.DefaultResourcePath);
        Facing = ResidentFacing.Front;
        IsCarrying = false;
        dirtyCargo = true;
        RefreshVisual();
    }
    private void OnValidate()
    {
        // Inspector changes are applied by the next editor update, outside deserialization.
        dirtyCargo = true;
    }
    private void LateUpdate() => RefreshVisual();

    public void RefreshVisual()
    {
        if (Body == null || Sprites == null) return;
        if (Application.isPlaying)
        {
            if (bodyPhysics != null && bodyPhysics.velocity.sqrMagnitude > .01f)
                Facing = Direction(bodyPhysics.velocity);
            if (dirtyCargo || Time.unscaledTime >= nextCargoCheck)
            {
                dirtyCargo = false;
                nextCargoCheck = Time.unscaledTime + .1f;
                var id = resident != null ? resident.MyData?.InstanceID : null;
                var logistics = LogisticsManager.Instance;
                IsCarrying = !string.IsNullOrEmpty(id) && logistics != null && logistics.Ready &&
                    (logistics.Get(LogisticsManager.BagID(id))?.Used ?? 0f) > .0001f;
            }
        }
        else { Facing = ResidentFacing.Front; IsCarrying = false; }

        var sprite = Sprites.Get(Facing, IsCarrying);
        if (sprite == null) return;
        if (Body.sprite != sprite) Body.sprite = sprite;
        Body.flipX = Body.flipY = false;
        // Preserve native pixel density even when map CellSize changes. Physics stays on the root.
        float scale = sprite.pixelsPerUnit / WorldPixelMetrics.PixelsPerUnit;
        var parentScale = Body.transform.parent != null ? Body.transform.parent.lossyScale : Vector3.one;
        var desiredScale = new Vector3(scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
            scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), 1);
        if (Body.transform.localScale != desiredScale) Body.transform.localScale = desiredScale;
        // The sprite's pivot is at its feet; its frame remains centred in the resident's grid cell.
        var offset = new Vector3(-sprite.bounds.center.x * desiredScale.x, -sprite.bounds.center.y * desiredScale.y, 0);
        if (Body.transform.localPosition != offset) Body.transform.localPosition = offset;
        if (resident != null && resident.SelectionCircle != null)
        {
            var selection = resident.SelectionCircle.transform;
            var rootScale = transform.lossyScale;
            var ringScale = new Vector3(1f / Mathf.Max(.0001f, Mathf.Abs(rootScale.x)),
                1f / Mathf.Max(.0001f, Mathf.Abs(rootScale.y)), 1);
            if (selection.localScale != ringScale) selection.localScale = ringScale;
            float feetY = (-sprite.rect.height * .5f + sprite.pivot.y) / WorldPixelMetrics.PixelsPerUnit;
            var ringPosition = new Vector3(0, feetY * ringScale.y, 0);
            if (selection.localPosition != ringPosition) selection.localPosition = ringPosition;
        }
    }

    public static ResidentFacing Direction(Vector2 movement)
    {
        if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
            return movement.x < 0 ? ResidentFacing.Left : ResidentFacing.Right;
        return movement.y > 0 ? ResidentFacing.Back : ResidentFacing.Front;
    }
}
