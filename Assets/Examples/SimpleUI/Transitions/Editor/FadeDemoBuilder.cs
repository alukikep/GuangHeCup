using System.Linq;
using ModuleFactory.SceneFlow;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using ModuleFactory.SimpleUI;

public static class FadeDemoBuilder
{
    public static string Folder
    {
        get
        {
            var script = AssetDatabase.FindAssets("FadeDemoControls t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).First(p=>p.EndsWith("/FadeDemoControls.cs"));
            return System.IO.Path.GetDirectoryName(script).Replace((char)92,'/');
        }
    }
    static RectTransform Rect(string name, Transform parent)
    {
        var r = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return r;
    }
    static void Label(Transform parent,string text,Vector2 min,Vector2 max,int size)
    {
        var r=Rect(text,parent);r.anchorMin=min;r.anchorMax=max;
        var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=size;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;
    }
    static void Button(Transform parent,string text,int row,UnityAction action)
    {
        var r=Rect(text,parent);r.anchorMin=new Vector2(.25f,.71f-row*.105f);r.anchorMax=new Vector2(.75f,.79f-row*.105f);
        r.gameObject.AddComponent<Image>().color=new Color(.13f,.3f,.48f);
        UnityEventTools.AddPersistentListener(r.gameObject.AddComponent<Button>().onClick,action);
        Label(r,text,Vector2.zero,Vector2.one,23);
    }
    [MenuItem("Tools/Module Factory/Simple UI/Create Transition Scenes")]
    public static void Ensure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        // Leave unsaved user scenes untouched. The batch entry point creates its own bootstrap.
        for (int i=0;i<SceneManager.sceneCount;i++)
            if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)) return;
        Build("FadeDemoA","FadeDemoB");Build("FadeDemoB","FadeDemoA");
    }
    static void Build(string name,string target)
    {
        string path=Folder+"/"+name+".unity";
        if(System.IO.File.Exists(path))return;
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        try
        {
            var ui=new GameObject("Transition Demo UI",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));ui.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=ui.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var bg=Rect("Background",ui.transform);bg.gameObject.AddComponent<Image>().color=name.EndsWith("A")?new Color(.03f,.08f,.15f):new Color(.12f,.05f,.15f);
            Label(ui.transform,"SCREEN TRANSITION / "+name,new Vector2(.05f,.84f),new Vector2(.95f,.98f),32);
            var fadeRoot=new GameObject("Dedicated Transition Root");var fade=fadeRoot.AddComponent<UIScreenFade>();
            // Reset creates a serialized, editable full-screen Image in edit mode.
            var loader=fadeRoot.AddComponent<SceneFlowLoader>();
            var controls=ui.AddComponent<FadeDemoControls>();controls.Fade=fade;controls.Loader=loader;controls.TargetScene=target;
            Button(ui.transform,"Blackout and restore",0,controls.Blackout);Button(ui.transform,"Blackout while paused",1,controls.PausedBlackout);
            Button(ui.transform,"Reverse in progress",2,controls.Reverse);Button(ui.transform,"Cancel and restore",3,controls.CancelAndRestore);
            Button(ui.transform,"Load "+target,4,controls.LoadScene);
            Label(ui.transform,"Enable both demo scenes in Build Settings for scene loading.",new Vector2(.05f,.04f),new Vector2(.95f,.12f),18);
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene,path);
        }
        finally { if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true); }
    }
    [MenuItem("Tools/Module Factory/Simple UI/Add Transition Scenes To Build Settings")]
    public static void AddBuildScenes()
    {
        Ensure();var list=EditorBuildSettings.scenes.ToList();
        foreach(var name in new[]{"FadeDemoA","FadeDemoB"})
        {
            string path=Folder+"/"+name+".unity";var existing=list.Find(s=>s.path==path);
            if(existing!=null)existing.enabled=true;else list.Add(new EditorBuildSettingsScene(path,true));
        }
        EditorBuildSettings.scenes=list.ToArray();
    }
    // Explicit entry point used by the isolated package validation tool.
    public static void PrepareValidation()
    {
        if (!Application.isBatchMode) throw new System.InvalidOperationException("Validation preparation is batch-only.");
        var scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene, "Assets/ValidationBootstrap.unity");
        Ensure(); AddBuildScenes(); AssetDatabase.SaveAssets();
    }
}
