using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JU.GameData.Demo.Editor
{
    public static class GameDataDemoSceneBuilder
    {
        public const string ScenePath = "Assets/GameData/Demo/Scenes/GameDataDemo.unity";
        private static readonly Color Background = Hex("101923"), Panel = Hex("1B2936"), Ink = Hex("E3EBF2"), Muted = Hex("9AABBA");
        private static Font font;

        [MenuItem("Tools/JU/Game Data/打开Demo场景")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        // 构建工具只写新场景，现有场景不会被覆盖。
        [MenuItem("Tools/JU/Game Data/创建Demo场景副本")]
        public static void CreateCopy()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var path = EditorUtility.SaveFilePanelInProject("创建独立Demo场景", "GameDataDemoCopy", "unity", "请选择新的场景文件名");
            if (string.IsNullOrEmpty(path)) return;
            if (File.Exists(path)) { EditorUtility.DisplayDialog("文件已存在", "请选择新文件名。", "确定"); return; }
            Build(path);
        }
        public static void BuildForBatch()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("场景已存在，不覆盖：" + ScenePath);
            Build(ScenePath);
        }
        private static void Build(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Background;
            camera.orthographic = true; cameraObject.transform.position = new Vector3(0,0,-10);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvasObject = new GameObject("GameDataDemoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
            var controller = canvasObject.AddComponent<GameDataDemoController>();
            controller.configuration = AssetDatabase.LoadAssetAtPath<GameDataConfig>("Assets/GameData/Config/DefaultGameDataConfig.asset");
            if (controller.configuration == null) throw new InvalidOperationException("默认数据配置未导入。");

            var backdrop = Box("Background", canvasObject.transform, Background, 0,0,1,1);
            backdrop.SetAsFirstSibling();
            var day = Box("01_DayPanel", canvasObject.transform, Panel, .025f,.815f,.975f,.975f);
            var document = Box("02_DocumentPanel", canvasObject.transform, Hex("EEE7D8"), .025f,.25f,.34f,.795f);
            var profile = Box("03_ProfilePanel", canvasObject.transform, Panel, .355f,.25f,.70f,.795f);
            var debug = Box("04_DebugPanel", canvasObject.transform, Panel, .715f,.25f,.975f,.795f);
            var actions = Box("05_ActionPanel", canvasObject.transform, Panel, .025f,.025f,.975f,.23f);

            Label("DemoTitle", day, "边境审批 / 数据 DEMO", 16, Muted, .025f,.74f,.63f,.94f);
            controller.dayLabel = Label("Day", day, "第 01 天", 37, Ink, .025f,.34f,.22f,.73f);
            controller.stateText = Label("WorldState", day, "纯净 50.0     秩序 50.0     信任 50     两界正常", 20, Ink, .235f,.39f,.97f,.70f);
            controller.queueCountText = Label("QueueCount", day, "待审批 8    收容 0    已放行 0", 17, Muted, .025f,.07f,.47f,.30f);
            controller.npcDropdown = DropdownControl(day, .51f,.07f,.975f,.31f);

            PanelHeading(document,"通关文书", "申报信息 / 保留原始申请", Hex("293D47"), Hex("756E60"));
            PanelHeading(profile,"人物档案", "真实记录 / 随收容状态更新", Ink, Muted);
            PanelHeading(debug,"调试数值", "开发可见 / 数值与判定依据", Ink, Muted);
            controller.documentText = ScrollBody(document, "Document", Hex("34404A"), 21);
            controller.profileText = ScrollBody(profile, "Profile", Ink, 19);
            controller.debugText = ScrollBody(debug, "Debug", Hex("9BD8C9"), 19);
            controller.documentText.text = "申请编号   NPC-000001\n\n姓名   —\n原籍   —\n申请目的地   —\n\n【身体状况】\n运行场景后显示申报信息。\n\n【入境理由】\n运行场景后生成。";
            controller.profileText.text = "运行场景后显示真实身份、动机、二维标签和四项定性记录。";
            controller.debugText.text = "记忆 / 关系 / 欲望 / 意志\n\n欺骗偏差与状态\n\n预计放行影响";

            Label("ActionsTitle", actions, "审批操作", 22, Ink, .025f,.77f,.24f,.96f);
            Label("ActionsHint", actions, "审批后自动切换申请者；也可从上方列表查看收容人员。", 16, Muted, .25f,.78f,.975f,.96f);
            controller.feedbackText = Label("Feedback", actions, "点击 Play 开始独立模拟。闭馆时统一结算世界变化与信任。", 18, Ink, .025f,.44f,.975f,.76f);
            controller.utopiaButton = ActionButton(actions,"ReleaseUtopia","放行乌托邦", Hex("246E62"), .025f,.09f,.245f,.37f);
            controller.antiUtopiaButton = ActionButton(actions,"ReleaseAntiUtopia","放行反乌托邦", Hex("375E84"), .26f,.09f,.48f,.37f);
            controller.rejectButton = ActionButton(actions,"Reject","拒签入所", Hex("855E3E"), .495f,.09f,.715f,.37f);
            controller.closeDayButton = ActionButton(actions,"CloseDay","闭馆结算 / 次日", Hex("40505E"), .73f,.09f,.975f,.37f);
            controller.closeDayButtonLabel = controller.closeDayButton.GetComponentInChildren<Text>();
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();
            Debug.Log("已创建独立数据Demo场景：" + path);
        }
        private static void PanelHeading(RectTransform parent, string title, string subtitle, Color main, Color secondary)
        {
            Label("Title",parent,title,30,main,.055f,.875f,.945f,.965f);
            Label("Subtitle",parent,subtitle,15,secondary,.055f,.823f,.945f,.875f);
            Box("Divider",parent,secondary,.055f,.804f,.945f,.806f);
        }
        private static Text ScrollBody(RectTransform parent, string name, Color color, int size)
        {
            var view = Node(name+"Scroll",parent,.055f,.035f,.945f,.78f);
            var scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
            // 透明背景接收滚轮，RectMask2D避免正文越过标题或按钮区。
            view.gameObject.AddComponent<Image>().color = new Color(0,0,0,0);
            view.gameObject.AddComponent<RectMask2D>();
            var content = Node(name+"Content",view,0,1,1,1);
            content.pivot = new Vector2(.5f,1);
            var text = content.gameObject.AddComponent<Text>(); text.font=font; text.fontSize=size; text.color=color;
            text.alignment=TextAnchor.UpperLeft; text.horizontalOverflow=HorizontalWrapMode.Wrap;
            text.verticalOverflow=VerticalWrapMode.Overflow; text.lineSpacing=1.05f; text.supportRichText=false; text.raycastTarget=false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=view; scroll.content=content;
            return text;
        }
        private static Dropdown DropdownControl(RectTransform parent,float x0,float y0,float x1,float y1)
        {
            var root = Box("NpcSelector",parent,Hex("101D29"),x0,y0,x1,y1);
            var dropdown=root.gameObject.AddComponent<Dropdown>(); dropdown.targetGraphic=root.GetComponent<Image>();
            var caption=Label("Label",root,"待审批 · 当前申请者",18,Ink,.025f,.05f,.92f,.95f);
            Label("Arrow",root,"▾",22,Muted,.93f,0,.99f,1).alignment=TextAnchor.MiddleCenter;
            var template=Box("Template",root,Hex("1B2936"),0,0,1,0);
            template.pivot=new Vector2(.5f,1); template.sizeDelta=new Vector2(0,260); template.anchoredPosition=Vector2.zero;
            var scroll=template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped;
            var viewport=Node("Viewport",template,.01f,.02f,.99f,.98f); viewport.gameObject.AddComponent<RectMask2D>();
            var content=Node("Content",viewport,0,1,1,1); content.pivot=new Vector2(.5f,1); content.sizeDelta=new Vector2(0,44);
            var item=Box("Item",content,Hex("263C4C"),0,0,1,1);
            var toggle=item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic=item.GetComponent<Image>();
            var check=Box("Check",item,Hex("8FCDB7"),.015f,.25f,.028f,.75f); toggle.graphic=check.GetComponent<Image>();
            var label=Label("ItemLabel",item,"申请者",18,Ink,.045f,.05f,.98f,.95f);
            scroll.viewport=viewport; scroll.content=content; dropdown.template=template; dropdown.captionText=caption; dropdown.itemText=label;
            dropdown.options.Add(new Dropdown.OptionData("待审批 · 当前申请者")); template.gameObject.SetActive(false);
            return dropdown;
        }
        private static Button ActionButton(RectTransform parent,string name,string label,Color color,float x0,float y0,float x1,float y1)
        {
            var root=Box(name,parent,color,x0,y0,x1,y1);
            var button=root.gameObject.AddComponent<Button>(); button.targetGraphic=root.GetComponent<Image>();
            var colors=button.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1.18f,1.18f,1.18f);
            colors.pressedColor=new Color(.78f,.78f,.78f); colors.disabledColor=new Color(.45f,.45f,.45f,.7f); button.colors=colors;
            var text=Label("Label",root,label,23,Ink,.025f,0,.975f,1); text.alignment=TextAnchor.MiddleCenter;
            return button;
        }
        private static RectTransform Box(string name,Transform parent,Color color,float x0,float y0,float x1,float y1)
        {
            var rect=Node(name,parent,x0,y0,x1,y1); var image=rect.gameObject.AddComponent<Image>(); image.color=color;
            // 背景与板块无需拦截鼠标，实际控件再显式开启。
            image.raycastTarget = name == "NpcSelector" || name == "Template" || name == "Item" || name == "ReleaseUtopia" || name == "ReleaseAntiUtopia" || name == "Reject" || name == "CloseDay";
            return rect;
        }
        private static Text Label(string name,Transform parent,string value,int size,Color color,float x0,float y0,float x1,float y1)
        {
            var rect=Node(name,parent,x0,y0,x1,y1); var text=rect.gameObject.AddComponent<Text>();
            text.font=font; text.fontSize=size; text.color=color; text.text=value; text.alignment=TextAnchor.MiddleLeft;
            text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate;
            text.supportRichText=false; text.raycastTarget=false;
            return text;
        }
        private static RectTransform Node(string name,Transform parent,float x0,float y0,float x1,float y1)
        {
            var obj=new GameObject(name,typeof(RectTransform)); var rect=obj.GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin=new Vector2(x0,y0); rect.anchorMax=new Vector2(x1,y1); rect.offsetMin=rect.offsetMax=Vector2.zero;
            return rect;
        }
        private static Color Hex(string value) { Color color; ColorUtility.TryParseHtmlString("#"+value,out color); return color; }
    }
}
