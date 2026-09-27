using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene folders are organizational only: identity transforms and no gameplay state.</summary>
public static class SceneOrganization
{
    public const string Systems = "01_Systems";
    public const string World = "02_World";
    public const string Buildings = "03_Buildings";
    public const string Units = "04_Units";
    public const string UI = "05_UI";
    public const string Debug = "06_Debug";
    public const string Editor = "90_Editor";

    public static bool HasMarker(Scene scene, string marker) => scene.IsValid() && scene.isLoaded &&
        scene.GetRootGameObjects().Any(root => root.GetComponentsInChildren<Transform>(true).Any(t => t.name == marker));

    // Only adopt unparented live entities in an explicitly organized scene. Authored parents,
    // prefab previews and persistent managers keep their own ownership and lifetime.
    public static void PlaceWorldObject(Component component, string folder)
    {
        if (!Application.isPlaying || component.transform.parent != null) return;
        var scene = component.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded) return;
        var group = scene.GetRootGameObjects().FirstOrDefault(root => root.name == folder);
        if (group != null && group.transform.position == Vector3.zero &&
            group.transform.rotation == Quaternion.identity && group.transform.lossyScale == Vector3.one)
            component.transform.SetParent(group.transform, true);
    }
}
