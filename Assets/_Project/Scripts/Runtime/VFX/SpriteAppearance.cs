using UnityEngine;

// Copy only the visual properties shared by the three animated display copies.
// The caller still owns visibility, transforms, timing and forceRenderingOff.
internal static class SpriteAppearance
{
    public static void Copy(SpriteRenderer source, SpriteRenderer display)
    {
        display.sprite = source.sprite;
        display.color = source.color;
        display.sharedMaterial = source.sharedMaterial;
        display.sortingLayerID = source.sortingLayerID;
        display.sortingOrder = source.sortingOrder;
        display.flipX = source.flipX;
        display.flipY = source.flipY;
        display.maskInteraction = source.maskInteraction;
        display.spriteSortPoint = source.spriteSortPoint;
    }
}
