using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
#if Aicomi
using R3;
using R3.Triggers;
#else
using UniRx;
using UniRx.Triggers;
#endif
using UnityEngine.UI;
using Character;
using CharacterCreation;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using CoastalSmell;

namespace DorsalFin
{
    public struct Palette
    {
        public float Hue { get; set; } = 0.0f;
        public float Saturation { get; set; } = 0.5f;
        public float Brightness { get; set; } = 0.5f;
        public float HueGap { get; set; } = 0.125f;
        public float SaturationGap { get; set; } = 0.0f;
        public float BrightnessGap { get; set; } = 0.0f;
        public float IsoscelesSize { get; set; } = 0.25f;
        public float IsoscelesAngle { get; set; } = (float)(Math.PI / 6.0);
        public float IsoscelesRotation { get; set; } = (float)(Math.PI / 6.0);
        public Palette() { }
        float Normalize(float value) =>
            value switch
            {
                < 0.0f => Math.Min(value + 1.0f, 1.0f),
                > 1.0f => Math.Max(value - 1.0f, 0.0f),
               _ => value
            };
        Dictionary<Variants, Vector3> Translate(float sGapA, float vGapA, float sGapB, float vGapB) => new()
        {
            [Variants.M1] = new (Hue, Saturation, Brightness),
            [Variants.M2] = new (Hue, Normalize(Saturation + sGapA), Normalize(Brightness + vGapA)),
            [Variants.M3] = new (Hue, Normalize(Saturation + sGapB), Normalize(Brightness + vGapB)),
            [Variants.S1] = new (Normalize(Hue + HueGap), Normalize(Saturation - SaturationGap), Normalize(Brightness - BrightnessGap)),
            [Variants.S2] = new (Normalize(Hue + HueGap), Normalize(Saturation - SaturationGap + sGapA), Normalize(Brightness - BrightnessGap + vGapA)),
            [Variants.S3] = new (Normalize(Hue + HueGap), Normalize(Saturation - SaturationGap + sGapB), Normalize(Brightness - BrightnessGap + vGapB)),
        };
        internal Dictionary<Variants, Vector3> Vectors => Translate(
            (float)(IsoscelesSize * Math.Cos(IsoscelesRotation + IsoscelesAngle)),
            (float)(IsoscelesSize * Math.Sin(IsoscelesRotation + IsoscelesAngle)),
            (float)(IsoscelesSize * Math.Cos(IsoscelesRotation - IsoscelesAngle)),
            (float)(IsoscelesSize * Math.Sin(IsoscelesRotation - IsoscelesAngle)));
    }
    enum Variants
    {
        NA, M1, M2, M3, S1, S2, S3
    }
    internal class Window
    {
        static WindowHandle Handle;
        internal Palette Palette = new ();
        internal event Action<string> OnSaveMapping = delegate { };
        internal event Action<string> OnLoadMapping = delegate { };
        internal event Action<Dictionary<Variants,Vector3>> OnHueChange = delegate { };
        internal event Action<Dictionary<Variants,Vector3>> OnIsoscelesChange = delegate { };
        Window(GameObject window) =>
            new GameObject("Content").With(UGUI.Go(parent: window.transform))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>(spacing: 5)))
                .With(PropertiesMenu.Create(this)).With(PaletteView.Create(this))
                .With(F.Apply(OnIsoscelesChange, Palette.Vectors));
        internal void SaveMapping(string path) => OnSaveMapping(path);
        internal void LoadMapping(string path) => OnLoadMapping(path);
        internal void SavePalette(string path) =>
            Json<Palette>.Save(Plugin.Instance.Log.LogMessage, File.OpenWrite(path), Palette);
        internal void LoadPalette(string path) =>
            (Palette = Json<Palette>.Load(Plugin.Instance.Log.LogMessage,
                File.OpenRead(path))).Vectors.With(OnHueChange).With(OnIsoscelesChange);
        internal void ChangeHue(float value) =>
            (Palette.Hue = value).With(() => OnHueChange(Palette.Vectors));
        internal void ChangeHueGap(float value) =>
            (Palette.HueGap = value).With(() => OnHueChange(Palette.Vectors));
        internal void ChangeSaturation(float value) =>
            (Palette.Saturation = value).With(() => OnIsoscelesChange(Palette.Vectors));
        internal void ChangeBrightness(float value) =>
            (Palette.Brightness = value).With(() => OnIsoscelesChange(Palette.Vectors));
        internal void ChangeSaturationGap(float value) =>
            (Palette.SaturationGap = value).With(() => OnIsoscelesChange(Palette.Vectors));
        internal void ChangeBrightnessGap(float value) =>
            (Palette.BrightnessGap = value).With(() => OnIsoscelesChange(Palette.Vectors));
        internal void ChangeIsoscelesSize(float value) =>
            (Palette.IsoscelesSize = value * 0.5f).With(() => OnIsoscelesChange(Palette.Vectors));
        internal void ChangeIsoscelesAngle(float value) =>
            (Palette.IsoscelesAngle = (float)(Math.PI * 0.5 * value)).With(() => OnIsoscelesChange(Palette.Vectors));
        internal void ChangeIsoscelesRotation(float value) =>
            (Palette.IsoscelesRotation = (float)(Math.PI * 2.0 * (value - 0.5))).With(() => OnIsoscelesChange(Palette.Vectors));
        internal Action OnActive(Human human) =>
            () => Handle.Title.SetText(human.With(PrepareOnDestroy).fileParam.fullname, false);
        void PrepareOnDestroy(Human human) =>
            Handle.Disposables.Add(human.gameObject
                .GetComponent<ObservableDestroyTrigger>()
                .OnDestroyAsObservable().Subscribe(F.Ignoring<Unit>(Handle.Dispose)));
        static void Prepare() =>
            new Window(UGUI.Window(503, 500, Plugin.Name, Handle)
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>(spacing: 6))));
        internal static void Initialize()
        {
            Handle = new WindowHandle(Plugin.Instance, "DorsalFin",
                new(1000, -400), new KeyboardShortcut(KeyCode.D, KeyCode.LeftControl));
            Util<HumanCustom>.Hook(Util.OnCustomHumanReady.Apply(Prepare), Handle.Dispose);
        }
    }
    internal class PropertiesMenu
    {
        internal static Action<GameObject> Create(Window window) => go => new PropertiesMenu(window, go);
        internal event Action<Dictionary<Variants, Color>> OnColorChange = delegate { };
        internal Dictionary<Variants, Color> Colors;
        internal Options Target =
            Options.Body | Options.Face | Options.Hairs | Options.Clothes | Options.Acs;
        Action<bool> ToggleTarget(Options target) =>
            value => Target = value ? Target | target : Target & ~target;
        List<PropertyEdit> Edits = [];
        void Save(string path) =>
            PropertyEdit.Save(Edits, path);
        void Load(string path) =>
            PropertyEdit.Load(Edits, path);
        void Clear() =>
            PropertyEdit.Clear(Edits);
        PropertiesMenu(Window window)
        {
            window.OnHueChange += NotifyColorChange; 
            window.OnIsoscelesChange += NotifyColorChange;
            window.OnSaveMapping += Save;
            window.OnLoadMapping += Load;
        }
        void NotifyColorChange(Dictionary<Variants, Vector3> vectors) =>
            OnColorChange(Colors = Translate(vectors));
        Dictionary<Variants, Color> Translate(Dictionary<Variants, Vector3> vectors) =>
            Colors = vectors.ToDictionary(entry => entry.Key, entry => entry.Key switch
            {
                Variants.M1 => Color.HSVToRGB(entry.Value.x, entry.Value.y, entry.Value.z),
                Variants.M2 => Color.HSVToRGB(entry.Value.x, entry.Value.y, entry.Value.z),
                Variants.M3 => Color.HSVToRGB(entry.Value.x, entry.Value.y, entry.Value.z),
                Variants.S1 => Color.HSVToRGB(entry.Value.x, entry.Value.y, entry.Value.z),
                Variants.S2 => Color.HSVToRGB(entry.Value.x, entry.Value.y, entry.Value.z),
                Variants.S3 => Color.HSVToRGB(entry.Value.x, entry.Value.y, entry.Value.z),
                _ => Color.black
            });
        PropertiesMenu(Window window, GameObject go) : this(window) =>
            new GameObject("PropertiesMenu")
                .With(UGUI.Go(parent: go.transform))
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>()))
                .With(PrepareOptions)
                .With(PrepareControl)
                .With(PrepareHeaders)
                .With(PrepareProperties)
                .With(SaveLoadView.Create(window))
                .AddComponent<ObservableUpdateTrigger>()
                    .UpdateAsObservable().Subscribe(F.Ignoring<Unit>(UpdateUI));
        void PrepareOptions(GameObject go) =>
            UGUI.Panel("Options", go).With(UGUI.Go(active: true))
                .With(UGUI.Cmp(UGUI.Layout(width: 370, height: 24)))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>(
                    padding: new() { left = 5, right = 5 }, childAlignment: TextAnchor.MiddleLeft
                )))
                .With(PrepareOption(Options.Body))
                .With(UGUI.Label.Apply(40).Apply(24).Apply("Body"))
                .With(PrepareOption(Options.Face))
                .With(UGUI.Label.Apply(40).Apply(24).Apply("Face"))
                .With(PrepareOption(Options.Hairs))
                .With(UGUI.Label.Apply(40).Apply(24).Apply("Hairs"))
                .With(PrepareOption(Options.Clothes))
                .With(UGUI.Label.Apply(60).Apply(24).Apply("Clothes"))
                .With(PrepareOption(Options.Acs))
                .With(UGUI.Label.Apply(30).Apply(24).Apply("Acs"));
        void PrepareControl(GameObject go) =>
            UGUI.Panel("Controls", go).With(UGUI.Go(active: true))
                .With(UGUI.Cmp(UGUI.Layout(width: 370, height: 24)))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>(
                    padding: new() { left = 5, right = 5 }, childAlignment: TextAnchor.MiddleLeft
                )))
                .With(PrepareOption(Options.Assigned))
                .With(UGUI.Label.Apply(80).Apply(24).Apply("Assigned"))
                .With(PrepareOption(Options.NotAvailable))
                .With(UGUI.Label.Apply(120).Apply(24).Apply("N/A"))
                .With(UGUI.Toggle.Apply(100).Apply(24).Apply("Save&Load"))
                .With(UGUI.ModifyAt("Save&Load")(
                    UGUI.Cmp<Toggle>(ui => ui.OnValueChangedAsObservable()
                        .Subscribe((Action<bool>)(value => (go.transform.childCount > 3).Maybe(() =>
                            go.transform.GetChild(3).gameObject.active =
                            go.transform.GetChild(2).gameObject.active = 
                            !(go.transform.GetChild(4).gameObject.active = value)))))));
        Action<GameObject> PrepareOption(Options option) => go =>
            UGUI.Check(18, 18, "Toggle", go).With(UGUI.ModifyAt("Toggle")(
                UGUI.Cmp<Toggle>(ui => ui.Set((Target & option) is not Options.None, false)) +
                UGUI.Cmp<Toggle>(ui => ui.OnValueChangedAsObservable().Subscribe(ToggleTarget(option)))));
        void PrepareHeaders(GameObject go) =>
            new GameObject("Headers").With(UGUI.Go(parent: go.transform))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>(padding: new() { left = 5, right = 5 })))
                .With(UGUI.Button.Apply(60).Apply(24).Apply("Clear"))
                .With(UGUI.ModifyAt("Clear")(UGUI.Cmp<Button>(ui =>
                    ui.OnClickAsObservable().Subscribe(F.Ignoring<Unit>(Clear)))))
                .With(UGUI.Content("Space")(UGUI.Cmp(UGUI.Layout(width: 140, height: 24))))
                .With(Enum.GetValues<Variants>()
                    .Where(variant => variant is not Variants.NA)
                    .Select(entry => UGUI.Content(entry.ToString())(
                        UGUI.Cmp(UGUI.Layout(width: 24, height: 24)) + UGUI.Cmp(UGUI.Image()) +
                        UGUI.Cmp<Image>(ui => OnColorChange += colors => ui.color = colors[entry])))
                    .Aggregate((a1, a2) => a1 + a2));
        void PrepareProperties(GameObject go) =>
            PrepareEdits(UGUI.ScrollView(370, 450, "Pallettes", go)
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>()))
                .With(UGUI.Cmp(UGUI.Fitter())));
        void PrepareEdits(GameObject go) =>
            Edits = ColorProperty.All.Select(value => new PropertyEdit(this, value, go)).ToList();
        void UpdateUI() => Edits.ForEach(edit => edit.UpdateUI());
    }
    internal class SaveLoadView
    {
        internal static Action<GameObject> Create(Window window) => go => new SaveLoadView(window, go);
        TMPro.TMP_InputField InputField;
        GameObject FileList;
        Action NotifySave; 
        Action NotifyLoad;
        void Save() =>
            (!string.IsNullOrEmpty(InputField?.text)).Maybe(NotifySave);
        void Load() =>
            (!string.IsNullOrEmpty(InputField?.text)).Maybe(NotifyLoad);
        SaveLoadView(Window window, GameObject go) =>
            new GameObject("SaveLoadView")
                .With(UGUI.Go(active: false, parent: go.transform))
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>()))
                .With(PrepareControls(window)).With(PrepareInputField).With(PrepareFileList);
        Action<GameObject> PrepareControls(Window window) =>
            go => UGUI.Panel("Controls", go).With(UGUI.Go(active: true))
                .With(UGUI.Cmp(UGUI.Layout(width: 370, height: 24)))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>()))
                .With(UGUI.Cmp(UGUI.ToggleGroup()))
                .With(PrepareDataPathToggle("Mapping", window.SaveMapping, window.LoadMapping))
                .With(PrepareDataPathToggle("Palette", window.SavePalette, window.LoadPalette))
                .With(UGUI.Content("Spacing")(UGUI.Cmp(UGUI.Layout(width: 90, height: 24))))
                .With(PrepareSaveLoad("Save", Save))
                .With(PrepareSaveLoad("Load", Load));
        Action<GameObject> PrepareDataPathToggle(string name, Action<string> onSave, Action<string> onLoad) =>
            go => UGUI.Toggle(80, 24, name, go)
                .With(UGUI.Cmp<Toggle, ToggleGroup>((ui, group) => ui.group = group))
                .GetComponent<Toggle>().OnValueChangedAsObservable()
                .Subscribe((Action<bool>)(value => value.Maybe(F.Apply(UpdateUI, onSave, onLoad,
                    Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Name, name)))));
        Action<GameObject> PrepareSaveLoad(string name, Action action) =>
            go => UGUI.Button(60, 24, name, go).GetComponent<Button>()
                .OnClickAsObservable().Subscribe(F.Ignoring<Unit>(action)); 
        void PrepareInputField(GameObject go) =>
            UGUI.Panel("InputField", go).With(UGUI.Go(active: true))
                .With(UGUI.Cmp(UGUI.Layout(width: 370, height: 24)))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>()))
                .With(UGUI.Input.Apply(370).Apply(24).Apply("FileName"))
                .With(UGUI.ModifyAt("FileName")(
                    UGUI.Cmp<TMPro.TMP_InputField>(ui => InputField = ui) +
                    UGUI.ModifyAt("FileName.Area", "FileName.Content")
                        (UGUI.Cmp(UGUI.Text(hrAlign: TMPro.HorizontalAlignmentOptions.Left)))));
        void PrepareFileList(GameObject go) =>
            FileList = UGUI.ScrollView(370, 400, "Files", go)
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>()))
                .With(UGUI.Cmp(UGUI.ToggleGroup(allowSwitchOff: true)))
                .With(UGUI.Cmp(UGUI.Fitter()));
        void UpdateUI(Action<string> onSave, Action<string> onLoad, string dataPath) =>
            dataPath.With(UpdateOnSave(onSave)).With(UpdateOnLoad(onLoad)).With(UpdateUI);
        Action<string> UpdateOnSave(Action<string> action) =>
            dataPath => NotifySave =
                (() => action(Path.Combine(dataPath, InputField.text))) +
                Util.DoNextFrame.Apply(F.Apply(UpdateUI, dataPath));
        Action<string> UpdateOnLoad(Action<string> action) =>
            dataPath => NotifyLoad =
                () => action(Path.Combine(dataPath, InputField.text));
        void UpdateUI(string dataPath) =>
            Directory.GetFiles(dataPath)
                .With(UGUI.DestroyChildren.Apply(FileList))
                .Select(path => Path.GetRelativePath(dataPath, path))
                .ForEach(path => UGUI.Toggle(370, 24, path, FileList)
                    .With(UGUI.Cmp<Toggle, ToggleGroup>((ui, group) => ui.group = group))
                    .With(UGUI.Cmp<Toggle>(ui => ui.OnValueChangedAsObservable()
                        .Subscribe((Action<bool>)(value => value.Maybe(() => InputField.SetText(path)))))));
    }
    internal partial class ColorProperty
    {
        internal Options Group { get; init; }
        internal string Name { get; init; }
        internal Func<bool> Available { get; init; }
        internal Func<Color> Get { get; init; }
        Action<Color, Color> Set { get; init; }
        ColorProperty(Options group, string name) => (Group, Name) = (group, name);
        internal void SetValue(Color colors) => Set(Get(), colors);
    }
    internal class PropertyEdit
    {
        internal static void Save(IEnumerable<PropertyEdit> edits, string path) =>
            Json<Dictionary<string, Variants>>.Save(Plugin.Instance.Log.LogMessage, File.OpenWrite(path),
                edits.Where(edit => edit.Variant is not Variants.NA).ToDictionary(edit => edit.Value.Name, edit => edit.Variant));
        internal static void Load(IEnumerable<PropertyEdit> edits, string path) =>
            Json<Dictionary<string, Variants>>.Load(Plugin.Instance.Log.LogMessage, File.OpenRead(path))
                .With(mapping => edits.ForEach(edit => mapping
                    .TryGetValue(edit.With(edit.Backup).Value.Name, out var variant)
                    .Either(F.Apply(edit.Toggle, edit.Variant = Variants.NA),
                        F.Apply(edit.Toggle, variant) + F.Apply(edit.Assign, variant))));
        internal static void Clear(IEnumerable<PropertyEdit> edits) =>
            edits.ForEach(edit => edit.With(edit.Backup).Toggle(Variants.NA));
        void Toggle(Variants variant) =>
            Edit.With(UGUI.ModifyAt("Content", variant.ToString())(UGUI.Cmp<Toggle>(ui => ui.Set(true, false))));
        Variants Variant = Variants.NA;
        Color Color = Color.black;
        PropertiesMenu Parent;
        ColorProperty Value;
        GameObject Edit;
        PropertyEdit(ColorProperty value) =>
            Value = value; 
        PropertyEdit(PropertiesMenu parent, ColorProperty value) : this(value) =>
            (Parent = parent).OnColorChange += Update;
        internal PropertyEdit(PropertiesMenu parent, ColorProperty value, GameObject go) : this(parent, value) =>
            (Edit = new GameObject("Edit"))
            .With(UGUI.Go(parent: go.transform))
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>()))
                .With(PrepareToggles)
                .With(UGUI.Content("Border")(
                    UGUI.Cmp(UGUI.Layout(width: 350, height: 1)) +
                    UGUI.Cmp(UGUI.Image(color: new(0.9f, 0.9f, 0.9f, 0.8f)))))
                .With(UpdateLabel);
        void PrepareToggles(GameObject go) =>
            new GameObject("Content").With(UGUI.Go(parent: go.transform))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>(padding: new() { left = 5, right = 5 })))
                .With(UGUI.Cmp(UGUI.ToggleGroup()))
                .With(UGUI.Label.Apply(176).Apply(24).Apply("Name"))
                .With(Enum.GetValues<Variants>().Select(PrepareToggle).Aggregate((a1, a2) => a1 + a2));
        Action<GameObject> PrepareToggle(Variants entry) =>
            go => UGUI.Toggle(24, 24, entry.ToString(), go)
                .With(UGUI.Cmp<Toggle, ToggleGroup>((ui, group) => ui.group = group))
                .With(UGUI.Cmp<Toggle>(ui => ui.OnValueChangedAsObservable().Subscribe(OnSelect(entry))));
        Action<bool> OnSelect(Variants variant) => variant switch
        {
            Variants.NA => value => value.Either(Backup, Restore), 
            _ => value => value.Maybe(F.Apply(Assign, variant))
        };
        internal void UpdateUI() =>
            (Edit.active =
                ((Parent.Target & Options.NotAvailable) is not Options.None || Value.Available()) &&
                ((Parent.Target & Options.Assigned) is Options.None || Variant is not Variants.NA) &&
                ((Parent.Target & Value.Group) is not Options.None)).With(UpdateLabel);
        void UpdateLabel() =>
            Edit.With(UGUI.ModifyAt("Content", "Name")
                (UGUI.Cmp(UGUI.Text(text: Value.Available() ? Value.Name : $"{Value.Name}(N/A)"))));
        void Update(Dictionary<Variants, Color> colors) =>
            (Variant is not Variants.NA).Maybe(F.Apply(Update, colors.GetValueOrDefault(Variant)));
        void Update(Color color) =>
            Value.Available().Maybe(F.Apply(Value.SetValue, color));
        void Backup() =>
            Color = Value.Get();
        void Restore() =>
            (Variant = Variants.NA).With(F.Apply(Update, Color));
        void Assign(Variants variant) =>
            (Variant = variant).With(F.Apply(Update, Parent.Colors[variant]));
    }
    internal partial class PaletteView
    {
        internal static Action<GameObject> Create(Window window) => go => new PaletteView(window, go);
        Sprite Reticle = GenerateReticle;
        Sprite Cross = GenerateCross;
        Texture2D Colors = GenerateColors;
        Sprite ToSprite(int index) =>
            Sprite.Create(Colors, new(0, index % 128 * 128, 128, 128), new(0.5f, 0.5f));
        Window Window;
        PaletteView(Window window) => Window = window;
        PaletteView(Window window, GameObject go) : this(window) =>
            new GameObject("PaletteView").With(UGUI.Go(parent: go.transform))
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>(spacing: 10)))
                .With(PrepareControl).With(PrepareColors(Window.Palette.Vectors));
        Action<GameObject> PrepareControl =>
            go => UGUI.Panel("Control", go).With(UGUI.Go(active: true))
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>(
                    spacing: 5, padding: new() { left = 5, right = 5 })))
                .With(UGUI.Label.Apply(118).Apply(24).Apply("Main Color"))
                .With(PrepareSlider("H", () => Window.Palette.Hue, Window.ChangeHue))
                .With(PrepareSlider("S", () => Window.Palette.Saturation, Window.ChangeSaturation))
                .With(PrepareSlider("V", () => Window.Palette.Brightness, Window.ChangeBrightness))
                .With(UGUI.Label.Apply(118).Apply(24).Apply("Sub Color Gap"))
                .With(PrepareSlider("H", () => Window.Palette.HueGap, Window.ChangeHueGap))
                .With(PrepareSlider("S", () => Window.Palette.SaturationGap, Window.ChangeSaturationGap))
                .With(PrepareSlider("V", () => Window.Palette.BrightnessGap, Window.ChangeBrightnessGap))
                .With(UGUI.Label.Apply(118).Apply(24).Apply("Tone Isosceles"))
                .With(PrepareSlider("A", () => (float)(Window.Palette.IsoscelesAngle / Math.PI * 2.0), Window.ChangeIsoscelesAngle))
                .With(PrepareSlider("R", () => (float)(Window.Palette.IsoscelesRotation / Math.PI / 2.0 + 0.5), Window.ChangeIsoscelesRotation))
                .With(PrepareSlider("D", () => Window.Palette.IsoscelesSize * 2.0f, Window.ChangeIsoscelesSize));
        Action<GameObject> PrepareSlider(string name, Func<float> get, Action<float> set) =>
            go => new GameObject(name).With(UGUI.Go(parent: go.transform))
                .With(UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>(childAlignment: TextAnchor.MiddleLeft)))
                .With(UGUI.Cmp(UGUI.Layout(width: 118, height: 24)))
                .With(UGUI.Label.Apply(18).Apply(24).Apply($"Label.{name}"))
                .With(UGUI.Slider.Apply(100).Apply(20).Apply($"Value.{name}"))
                .With(UGUI.ModifyAt($"Label.{name}")(UGUI.Cmp(UGUI.Text(text: name))))
                .With(UGUI.ModifyAt($"Value.{name}")(UGUI.Cmp<Slider>(ui =>
                    ui.With(() => ui.Set(get(), false)).OnValueChangedAsObservable().Subscribe(set))));
        Action<GameObject> PrepareColors(Dictionary<Variants, Vector3> vectors) =>
            PrepareColors(vectors,
                vs => ToSprite((int)(vs[Variants.M1].x * 128)),
                vs => new(vs[Variants.M1].y - 0.5f, vs[Variants.M1].z - 0.5f),
                vs => new(vs[Variants.M2].y - 0.5f, vs[Variants.M2].z - 0.5f),
                vs => new(vs[Variants.M3].y - 0.5f, vs[Variants.M3].z - 0.5f)) +
            PrepareColors(vectors,
                vs => ToSprite((int)(vs[Variants.S1].x * 128)),
                vs => new(vs[Variants.S1].y - 0.5f, vs[Variants.S1].z - 0.5f),
                vs => new(vs[Variants.S2].y - 0.5f, vs[Variants.S2].z - 0.5f),
                vs => new(vs[Variants.S3].y - 0.5f, vs[Variants.S3].z - 0.5f));
        Action < GameObject > PrepareColors(
            Dictionary < Variants, Vector3 > vectors,
            Func<Dictionary<Variants, Vector3>, Sprite> hue,
            Func<Dictionary<Variants, Vector3>, Vector2> anchor,
            Func<Dictionary<Variants, Vector3>, Vector2> pointA,
            Func<Dictionary<Variants, Vector3>, Vector2> pointB) =>
            go => new GameObject("Colors").With(UGUI.Go(parent: go.transform))
                .With(UGUI.Cmp(UGUI.Image(type: Image.Type.Filled, sprite: hue(vectors))))
                .With(UGUI.Cmp(UGUI.Layout(width: 128, height: 128)))
                .With(UGUI.Cmp<Image>(ui => Window.OnHueChange += vs => ui.sprite = hue(vs)))
                .With(PreparePoint(Reticle, anchor)).With(PreparePoint(Cross, pointA)).With(PreparePoint(Cross, pointB));
        Action<GameObject> PreparePoint(Sprite sprite, Func<Dictionary<Variants, Vector3>, Vector2> point) =>
            go => new GameObject("Point").With(UGUI.Go(parent: go.transform))
                .With(UGUI.Cmp(UGUI.Image(sprite: sprite)))
                .With(UGUI.Cmp(UGUI.Rt(sizeDelta: new(sprite.texture.width, sprite.texture.height))))
                .With(UGUI.Cmp<RectTransform>(ui => Window.OnIsoscelesChange += vs => ui.localPosition = point(vs) * new Vector2(128.0f, 128.0f)));
    };

    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CoastalSmell.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        internal static Plugin Instance;
        public const string Name = "DorsalFin";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "1.0.0";
        public override void Load() =>
            (Instance = this).With(Window.Initialize);
    }
}