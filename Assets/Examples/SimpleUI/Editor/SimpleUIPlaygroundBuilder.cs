using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using ModuleFactory.SimpleUI;

public static class SimpleUIPlaygroundBuilder
{
    const string Path = "Assets/Examples/SimpleUI/SimpleUIPlayground.unity";
    static RectTransform Rect(string name, Transform parent)
    {
        var r = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent,false); r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; return r;
    }
    static void Label(Transform parent,string text,Vector2 min,Vector2 max,int size)
    {
        var r=Rect(text,parent); r.anchorMin=min; r.anchorMax=max;
        var t=r.gameObject.AddComponent<Text>(); t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text=text; t.fontSize=size; t.alignment=TextAnchor.MiddleCenter; t.color=Color.white; t.raycastTarget=false;
    }
    static UIPanel Panel(string name,RectTransform root,UILayer layer,bool modal=false,
        bool canGoBack=true,bool initiallyOpen=false)
    {
        var r=Rect(name,root); r.anchorMin=new Vector2(.22f,.12f); r.anchorMax=new Vector2(.78f,.88f);
        r.gameObject.AddComponent<Image>().color=new Color(.08f,.12f,.2f,.98f);
        var p=r.gameObject.AddComponent<UIPanel>();
        p.Configure(layer, modal, canGoBack, initiallyOpen);
        Label(r,name,new Vector2(.05f,.78f),new Vector2(.95f,.95f),32);return p;
    }
    static void Button(UIPanel panel,string text,int row,UnityAction action)
    {
        var r=Rect(text,panel.transform);r.anchorMin=new Vector2(.14f,.58f-row*.16f);r.anchorMax=new Vector2(.86f,.7f-row*.16f);
        r.gameObject.AddComponent<Image>().color=new Color(.15f,.35f,.55f);
        var b=r.gameObject.AddComponent<Button>(); UnityEventTools.AddPersistentListener(b.onClick,action);
        Label(r,text,Vector2.zero,Vector2.one,22);
    }
    [MenuItem("Tools/Module Factory/Simple UI/Create Playground")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || System.IO.File.Exists(Path)) return;
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var go=new GameObject("UI Playground",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        var manager=go.AddComponent<UIManager>();
        manager.NormalRoot=Rect("Normal",go.transform);manager.PopupRoot=Rect("Popup",go.transform);manager.OverlayRoot=Rect("Overlay",go.transform);
        var demo=go.AddComponent<SimpleUIPlayground>();demo.UI=manager;
        demo.Menu=Panel("Main Menu",manager.NormalRoot,UILayer.Normal,canGoBack:false,initiallyOpen:true);
        demo.Settings=Panel("Settings",manager.PopupRoot,UILayer.Popup);
        demo.Confirm=Panel("Reset settings?",manager.PopupRoot,UILayer.Popup,true);
        demo.Game=Panel("Game",manager.NormalRoot,UILayer.Normal,canGoBack:false);
        demo.Pause=Panel("Paused",manager.PopupRoot,UILayer.Popup,true);
        demo.Result=Panel("Results",manager.PopupRoot,UILayer.Popup,true);
        Button(demo.Menu,"Settings",0,demo.OpenSettings);Button(demo.Menu,"Start game",1,demo.StartGame);
        Button(demo.Settings,"Reset settings",0,demo.AskReset);Button(demo.Settings,"Back",1,demo.Back);
        Button(demo.Confirm,"Confirm (demo)",0,demo.CloseConfirm);Button(demo.Confirm,"Cancel",1,demo.CloseConfirm);
        Button(demo.Game,"Pause",0,demo.PauseGame);Button(demo.Game,"Finish game",1,demo.FinishGame);
        Button(demo.Pause,"Resume",0,demo.ResumeGame);Button(demo.Pause,"Finish game",1,demo.FinishGame);
        UnityEventTools.AddPersistentListener(demo.Pause.Hidden,demo.RestoreTime);
        Button(demo.Result,"Main menu",0,demo.MainMenu);
        manager.Panels=new[]{demo.Menu,demo.Settings,demo.Confirm,demo.Game,demo.Pause,demo.Result};
        foreach(var panel in manager.Panels)panel.gameObject.SetActive(panel.InitiallyOpen);
        new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        EditorSceneManager.SaveScene(scene,Path);
        if(previous.IsValid())SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(scene,true);
        Debug.Log("Simple UI playground created: "+Path);
    }
}
