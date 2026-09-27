using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class UISkinChecks
{
    static void Check(bool value,string message)
    {
        if(!value) throw new Exception(message);
        File.AppendAllText("SkinResults/checks.txt","PASS "+message+"\n");
    }
    public static void Run()
    {
        Directory.CreateDirectory("SkinResults");File.WriteAllText("SkinResults/checks.txt","");
        try
        {
            string logistics = File.ReadAllText("Assets/Resources/UI/LogisticsPanel.prefab");
            ChimeraUISkinAuthoring.BakeProject();
            Check(logistics == File.ReadAllText("Assets/Resources/UI/LogisticsPanel.prefab"),"logistics prefab remains byte-identical");
            EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
            Check(Object.FindObjectsOfType<UIThemeBinding>(true).Length > 60,"theme roles are serialized in the scene before play");
            var config = ChimeraUITheme.Config;
            Check(AssetDatabase.GetAssetPath(config)==ChimeraUISkinAuthoring.ConfigPath,"runtime resolves the editable theme resource");
            var texture = new Texture2D(32,32);
            var art = Sprite.Create(texture,new Rect(0,0,32,32),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(6,6,6,6));
            var alternate = Sprite.Create(texture,new Rect(0,0,32,32),new Vector2(.5f,.5f));
            var go = new GameObject("SkinProbe",typeof(RectTransform),typeof(Image),typeof(Button));
            var image=go.GetComponent<Image>();image.raycastTarget=false;
            var button=go.GetComponent<Button>();button.targetGraphic=image;
            var binding=UIThemeBinding.Bind(image,UIThemeRole.Button,true);
            var original=config.ButtonArt;
            config.ButtonArt=new UIThemeArtwork{Normal=art,Hover=alternate,Pressed=alternate,Selected=alternate,Disabled=alternate};
            binding.Apply();
            Check(image.sprite==art&&image.type==Image.Type.Sliced&&image.color==Color.white,"colored nine-slice artwork preserves its own color");
            Check(!image.raycastTarget&&!image.GetComponent<Outline>().enabled,"artwork removes duplicate border and preserves hit testing");
            Check(button.transition==Selectable.Transition.SpriteSwap&&button.spriteState.disabledSprite==alternate,"button sprite states are wired");
            config.ButtonArt=new UIThemeArtwork();binding.Apply();
            Check(image.sprite==null&&image.color==config.Button&&image.GetComponent<Outline>().enabled,"removing optional art restores the pure fallback");
            Check(button.transition==Selectable.Transition.ColorTint&&button.spriteState.disabledSprite==null,"old button state sprites do not leak after removing a skin");
            image.sprite=alternate;binding.Apply();
            Check(image.sprite==alternate,"applying a theme preserves a manually assigned new sprite");
            config.ButtonArt=original;
            Object.DestroyImmediate(go);Object.DestroyImmediate(art);Object.DestroyImmediate(alternate);Object.DestroyImmediate(texture);
            foreach(var task in Resources.FindObjectsOfTypeAll<ProductionTaskUIItem>())
            {
                if(!task.gameObject.scene.IsValid())continue;
                Check(task.GetComponent<LayoutElement>()?.preferredHeight>=62,"production queue has a readable authored row height");
            }
            File.AppendAllText("SkinResults/checks.txt","EDITOR COMPLETE\n");
            IvoryThemeChecks.Run();
        }
        catch(Exception e){File.AppendAllText("SkinResults/checks.txt","FAIL "+e+"\n");Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
