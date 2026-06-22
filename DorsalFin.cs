using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Character;
using CoastalSmell;
using ColorProperty = (
    System.Func<bool> Available,
    System.Func<UnityEngine.Color> Get,
    System.Action<UnityEngine.Color> Set
);
using PaletteHSV = (
    UnityEngine.Vector3 M1,
    UnityEngine.Vector3 M2,
    UnityEngine.Vector3 M3,
    UnityEngine.Vector3 S1,
    UnityEngine.Vector3 S2,
    UnityEngine.Vector3 S3
);
using PaletteRGB = (
    UnityEngine.Color M1,
    UnityEngine.Color M2,
    UnityEngine.Color M3,
    UnityEngine.Color S1,
    UnityEngine.Color S2,
    UnityEngine.Color S3
);
namespace DorsalFin
{
    public record struct Palette(
        float Hue,
        float Saturation,
        float Brightness,
        float HueGap,
        float SaturationGap,
        float BrightnessGap,
        float IsoscelesAngle,
        float IsoscelesRotation,
        float IsoscelesSize)
    {
        public PaletteHSV HSV => (
            IsoscelesSize * Mathf.Cos(IsoscelesRotation + IsoscelesAngle),
            IsoscelesSize * Mathf.Sin(IsoscelesRotation + IsoscelesAngle),
            IsoscelesSize * Mathf.Cos(IsoscelesRotation - IsoscelesAngle),
            IsoscelesSize * Mathf.Sin(IsoscelesRotation - IsoscelesAngle)
        ) switch
        {
            (var sGapA, var vGapA, var sGapB, var vGapB) => (
                 new (Hue, Saturation, Brightness),
                 new (Hue, Mathf.Repeat(Saturation + sGapA, 1.0f), Mathf.Repeat(Brightness + vGapA, 1.0f)),
                 new (Hue, Mathf.Repeat(Saturation + sGapB, 1.0f), Mathf.Repeat(Brightness + vGapB, 1.0f)),
                 new (Mathf.Repeat(Hue + HueGap, 1.0f), Mathf.Repeat(Saturation + SaturationGap, 1.0f), Mathf.Repeat(Brightness + BrightnessGap, 1.0f)),
                 new (Mathf.Repeat(Hue + HueGap, 1.0f), Mathf.Repeat(Saturation + SaturationGap + sGapA, 1.0f), Mathf.Repeat(Brightness + BrightnessGap + vGapA, 1.0f)),
                 new (Mathf.Repeat(Hue + HueGap, 1.0f), Mathf.Repeat(Saturation + SaturationGap + sGapB, 1.0f), Mathf.Repeat(Brightness + BrightnessGap + vGapB, 1.0f))
            )
        };
        public PaletteRGB RGB => HSVToRGB(HSV); 
        public static PaletteRGB HSVToRGB(PaletteHSV hsv) => hsv switch
        {
            (var M1, var M2, var M3, var S1, var S2, var S3) => (
                Color.HSVToRGB(M1.x, M1.y, M1.z),
                Color.HSVToRGB(M2.x, M2.y, M2.z),
                Color.HSVToRGB(M3.x, M3.y, M3.z),
                Color.HSVToRGB(S1.x, S1.y, S1.z),
                Color.HSVToRGB(S2.x, S2.y, S2.z),
                Color.HSVToRGB(S3.x, S3.y, S3.z)
            )
        };
    }
    static partial class UI
    {
        internal static Sprite ToHueSprite(Vector3 hsv) =>
            Sprite.Create(PaletteTexture, new(0, ((int)Mathf.Repeat(hsv.x * 128f, 128f)) * 128, 128, 128), new(0.5f, 0.5f));
        internal static Vector2 ToPosition(Vector3 hsv) =>
            new((hsv.y - 0.5f) * 128f, (hsv.z - 0.5f) * 128f);
        internal static IDisposable Initialize() =>
            UGUI.Ready.Subscribe(tf => tf.With(Plugin.Name.AsChild(ModePanel.AsChild() + PaletteEdit.AsChild() +
                "PaletteTexture".AsChild(UGUI.Image(sprite: Sprite.Create(PaletteTexture, new(0, 0, 128, 128 * 128), new(0.5f, 0.5f)))))));
    }
    class Vector3Edit
    {
        internal static UIAction PrepareTemplate(string label, string v1, string v2, string v3) =>
            UGUI.LayoutV(spacing: 5, padding: UGUI.Offset(5, 0)) +
            UGUI.ColorPanel +
            "Label".AsChild(UGUI.Label(118, 24) + UGUI.Text(text: label)) +
            "V1".AsChild(PrepareSlider(v1)) +
            "V2".AsChild(PrepareSlider(v2)) +
            "V3".AsChild(PrepareSlider(v3));
        static UIAction PrepareSlider(string name) =>
            UGUI.LayoutH(childAlignment: TextAnchor.MiddleLeft) + UGUI.Size(118, 24) +
            "Label".AsChild(UGUI.Label(18, 24) + UGUI.Text(text: name)) + "Value".AsChild(UGUI.Slider(100, 20));
        Slider V1, V2, V3;
        internal Vector3Edit(Transform tf) =>
            tf.With(
                UGUI.Component<Slider>(cmp => V1 = cmp).At("V1", "Value") +
                UGUI.Component<Slider>(cmp => V2 = cmp).At("V2", "Value") +
                UGUI.Component<Slider>(cmp => V3 = cmp).At("V3", "Value"));
        internal Vector3 Value
        {
            set => (
                F.Apply(V1.Set, value.x, true) +
                F.Apply(V2.Set, value.y, true) +
                F.Apply(V3.Set, value.z, true)
            ).Invoke();
            get => new(V1.value, V2.value, V3.value);
        }
        internal IObservable<Vector3> OnValueChange => Observable.Merge(
            V1.OnValueChangedAsObservable().Select(value => Value with { x = value }),
            V2.OnValueChangedAsObservable().Select(value => Value with { y = value }),
            V3.OnValueChangedAsObservable().Select(value => Value with { z = value }));
    }
    class PaletteEdit
    {
        static readonly UIAction PrepareColorView =
            UGUI.Size(128, 128) + UGUI.Image(type: Image.Type.Filled, sprite: UI.ToHueSprite(new ())) +
            "P1".AsChild(UGUI.Image(sprite: UI.Reticle) + UGUI.Rt(sizeDelta: new(21, 21))) +
            "P2".AsChild(UGUI.Image(sprite: UI.Cross) + UGUI.Rt(sizeDelta: new(11, 11))) +
            "P3".AsChild(UGUI.Image(sprite: UI.Cross) + UGUI.Rt(sizeDelta: new(11, 11)));
        internal static readonly UIAction PrepareTemplate =
            UGUI.LayoutV(spacing: 10) +
            "Controls".AsChild(
                UGUI.LayoutV(spacing: 5) +
                "Main".AsChild(Vector3Edit.PrepareTemplate("Main Color", "H", "S", "V")) +
                "Gaps".AsChild(Vector3Edit.PrepareTemplate("Sub Color Gaps", "H", "S", "V")) +
                "Tone".AsChild(Vector3Edit.PrepareTemplate("Tone Isosceles", "A", "R", "D"))) +
            "MainColor".AsChild(PrepareColorView) +
            "SubColor".AsChild(PrepareColorView);
        static UIAction InitializeColorView(IObservable<Sprite> hue, IObservable<Vector2> p1, IObservable<Vector2> p2, IObservable<Vector2> p3) =>
            UGUI.Component<Image>(image => hue.Subscribe(sprite => image.sprite = sprite)) +
            UGUI.Component<RectTransform>(rt => p1.Subscribe(p => rt.localPosition = p)).At("P1") +
            UGUI.Component<RectTransform>(rt => p2.Subscribe(p => rt.localPosition = p)).At("P2") +
            UGUI.Component<RectTransform>(rt => p3.Subscribe(p => rt.localPosition = p)).At("P3");
        static UIAction InitializeColorView(IObservable<Vector3> v1, IObservable<Vector3> v2, IObservable<Vector3> v3) =>
            InitializeColorView(v1.Select(UI.ToHueSprite), v1.Select(UI.ToPosition), v2.Select(UI.ToPosition), v3.Select(UI.ToPosition));
        static UIAction InitializeColorViews(IObservable<PaletteHSV> hsv) =>
            InitializeColorView(hsv.Select(hsv => hsv.M1), hsv.Select(hsv => hsv.M2), hsv.Select(hsv => hsv.M3)).At("MainColor") +
            InitializeColorView(hsv.Select(hsv => hsv.S1), hsv.Select(hsv => hsv.S2), hsv.Select(hsv => hsv.S3)).At("SubColor");
        Vector3Edit Main, Gaps, Tone;
        PaletteEdit(Transform tf) =>
            (Main, Gaps, Tone) = (new Vector3Edit(tf.Find("Main")), new Vector3Edit(tf.Find("Gaps")), new Vector3Edit(tf.Find("Tone")));
        internal PaletteEdit(GameObject go) : this(go.transform.Find("Controls")) =>
            go.With(InitializeColorViews(OnValueChange.Select(p => p.HSV)));
        internal Palette Value
        {
            set => (Main.Value, Gaps.Value, Tone.Value) = (
                new (value.Hue, value.Saturation, value.Brightness),
                new (value.HueGap, value.SaturationGap, value.BrightnessGap),
                new (value.IsoscelesAngle / Mathf.PI * 2.0f, value.IsoscelesRotation / 2.0f / Mathf.PI + 0.5f, value.IsoscelesSize * 2.0f)
            );
            get => new(
                Main.Value.x, Main.Value.y, Main.Value.z,
                Gaps.Value.x, Gaps.Value.y, Gaps.Value.z,
                Tone.Value.x * Mathf.PI * 0.5f, (Tone.Value.y - 0.5f) * Mathf.PI * 2.0f, Tone.Value.z * 0.5f);
        }
        internal IObservable<Palette> OnValueChange => Observable.Merge(
                Main.OnValueChange.Select(value => Value with { Hue = value.x, Saturation = value.y, Brightness = value.z }),
                Gaps.OnValueChange.Select(value => Value with { HueGap = value.x, SaturationGap = value.y, BrightnessGap = value.z }),
                Tone.OnValueChange.Select(value => Value with {
                    IsoscelesAngle = value.x * Mathf.PI * 0.5f,
                    IsoscelesRotation = (value.y - 0.5f) * Mathf.PI * 2.0f,
                    IsoscelesSize = value.z * 0.5f
                }));
        internal void Save(string path) =>
            Json<Palette>.Save(Plugin.Instance.Log.LogMessage, File.OpenWrite(path), Value);
        internal void Load(string path) =>
            File.Exists(path).Maybe(() => Value = Json<Palette>.Load(Plugin.Instance.Log.LogMessage, File.OpenRead(path)));
    }
    class PropertyEdit
    {
        internal static UIAction PrepareTemplate(string name, IEnumerable<string> properties) =>
            $"{name}Title".AsChild(
                UGUI.LayoutH(padding: UGUI.Offset(5, 0)) +
                UGUI.UnderlinePanel + UGUI.Size(370, 24) +
                "Check".AsChild(UGUI.Fold(24, 24)) +
                "Label".AsChild(UGUI.Label(192, 24) + UGUI.Text(text: name)) +
                "M1".AsChild(UGUI.Size(24, 24)) +
                "M2".AsChild(UGUI.Size(24, 24)) +
                "M3".AsChild(UGUI.Size(24, 24)) +
                "S1".AsChild(UGUI.Size(24, 24)) +
                "S2".AsChild(UGUI.Size(24, 24)) +
                "S3".AsChild(UGUI.Size(24, 24))) +
            $"{name}Values".AsChild(UGUI.LayoutV() + properties.Select(PrepareTemplate).Aggregate());
        internal static IEnumerable<PropertyEdit> Initialize(Transform mapping, Toggle showAssignedOnly, IObservable<PaletteRGB> rgbs, Dictionary<string, Dictionary<string, ColorProperty>> properties) =>
            properties.SelectMany(entry => Initialize(mapping.Find($"{entry.Key}Values").With(InitializeTitle(entry.Key, rgbs)), entry.Value, showAssignedOnly, rgbs));
        static UIAction InitializeTitle(string group, IObservable<PaletteRGB> rgbs) =>
            new UIAction(values => values.transform.parent.Find($"{group}Title").With(InitializeTitle(values, rgbs)));
        static UIAction InitializeTitle(GameObject values, IObservable<PaletteRGB> rgbs) =>
            (
                UGUI.Component<Toggle>(cmp => cmp.Set(true, false)) +
                UGUI.Component<Toggle>(cmp => cmp.OnValueChangedAsObservable().Subscribe(values.SetActive))
            ).At("Check") +
            UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M1)).At("M1") +
            UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M2)).At("M2") +
            UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M3)).At("M3") +
            UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S1)).At("S1") +
            UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S2)).At("S2") +
            UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S3)).At("S3");
        static UIAction PrepareTemplate(string name) =>
            name.AsChild(
                UGUI.GameObject(active: false) +
                UGUI.LayoutH(padding: UGUI.Offset(5, 0)) +
                UGUI.UnderlinePanel + UGUI.ToggleGroup() +
                "State".AsChild(UGUI.Label(42, 24)) +
                "Name".AsChild(UGUI.Label(150, 24) + UGUI.Text(text: name)) +
                "NA".AsChild(UGUI.Image(color: Color.clear, alphaHit: 1.0f) + UGUI.Radio(24, 24) + UGUI.GroupToggle) +
                "M1".AsChild(UGUI.Image(color: Color.clear, alphaHit: 1.0f) + UGUI.Radio(24, 24) + UGUI.GroupToggle) +
                "M2".AsChild(UGUI.Image(color: Color.clear, alphaHit: 1.0f) + UGUI.Radio(24, 24) + UGUI.GroupToggle) +
                "M3".AsChild(UGUI.Image(color: Color.clear, alphaHit: 1.0f) + UGUI.Radio(24, 24) + UGUI.GroupToggle) +
                "S1".AsChild(UGUI.Image(color: Color.clear, alphaHit: 1.0f) + UGUI.Radio(24, 24) + UGUI.GroupToggle) +
                "S2".AsChild(UGUI.Image(color: Color.clear, alphaHit: 1.0f) + UGUI.Radio(24, 24) + UGUI.GroupToggle) +
                "S3".AsChild(UGUI.Image(color: Color.clear, alphaHit: 1.0f) + UGUI.Radio(24, 24) + UGUI.GroupToggle));
        static IEnumerable<PropertyEdit> Initialize(Transform group, Dictionary<string, ColorProperty> properties, Toggle showAssignedOnly, IObservable<PaletteRGB> rgbs) =>
            properties.Select(entry => new PropertyEdit(group.Find(entry.Key).gameObject,
                entry.Value, rgbs, group.OnUpdateAsObservable().Select(_ => showAssignedOnly.isOn)));
        string Name;
        Image Image;
        Toggle NA, M1, M2, M3, S1, S2, S3;
        PropertyEdit(GameObject go, ColorProperty property, IObservable<PaletteRGB> rgbs) =>
            Name = go.With(
                UGUI.Component<TMPro.TextMeshProUGUI>(cmp => cmp
                        .OnEnableAsObservable().Subscribe(_ => cmp.SetText(property.Available() ? "" : "(N/A)"))).At("State") +
                PrepareReset(property, cmp => Image = cmp, cmp => NA = cmp).At("NA") +
                PrepareRadio(property, rgbs.Select(colors => colors.M1), cmp => M1 = cmp).At("M1") +
                PrepareRadio(property, rgbs.Select(colors => colors.M2), cmp => M2 = cmp).At("M2") +
                PrepareRadio(property, rgbs.Select(colors => colors.M3), cmp => M3 = cmp).At("M3") +
                PrepareRadio(property, rgbs.Select(colors => colors.S1), cmp => S1 = cmp).At("S1") +
                PrepareRadio(property, rgbs.Select(colors => colors.S2), cmp => S2 = cmp).At("S2") +
                PrepareRadio(property, rgbs.Select(colors => colors.S3), cmp => S3 = cmp).At("S3")).name;
        UIAction PrepareReset(ColorProperty property, Action<Image> image, Action<Toggle> action) =>
            UGUI.Component(image) +
            UGUI.Component<Image, Toggle>((image, toggle) =>
                toggle.OnValueChangedAsObservable()
                    .Where(value => !value & property.Available())
                    .Subscribe(_ => image.color = property.Get() with { a = 1.0f })) +
            UGUI.Component<Image, Toggle>((image, toggle) =>
                toggle.With(action).OnValueChangedAsObservable()
                    .Where(value => value & property.Available() && image.color != Color.clear)
                    .Select(_ => property.Set.Apply(image.color with { a = property.Get().a }))
                    .Subscribe(action => image.color = Color.clear.With(action)));
        UIAction PrepareRadio(ColorProperty property, IObservable<Color> rgb, Action<Toggle> action) =>
            UGUI.Component<Image>(image => rgb.Subscribe(color => image.color = color)) +
            UGUI.Component<Image, Toggle>((image, toggle) =>
                toggle.With(action).OnValueChangedAsObservable()
                    .Where(value => value & property.Available())
                    .Select(_ => image.color with { a = property.Get().a }).Subscribe(property.Set));
        PropertyEdit(GameObject go, ColorProperty property, IObservable<PaletteRGB> rgbs, IObservable<bool> update) : this(go, property, rgbs) =>
            _ = (
                update.Select(showAssignedOnly => showAssignedOnly ? !NA.isOn : property.Available())
                    .Where(value => value != go.active).Subscribe(go.SetActive),
                rgbs.Where(_ => M1.isOn && property.Available())
                    .Select(colors => colors.M1 with { a = property.Get().a }).Subscribe(property.Set),
                rgbs.Where(_ => M2.isOn && property.Available())
                    .Select(colors => colors.M2 with { a = property.Get().a }).Subscribe(property.Set),
                rgbs.Where(_ => M3.isOn && property.Available())
                    .Select(colors => colors.M3 with { a = property.Get().a }).Subscribe(property.Set),
                rgbs.Where(_ => S1.isOn && property.Available())
                    .Select(colors => colors.S1 with { a = property.Get().a }).Subscribe(property.Set),
                rgbs.Where(_ => S2.isOn && property.Available())
                    .Select(colors => colors.S2 with { a = property.Get().a }).Subscribe(property.Set),
                rgbs.Where(_ => S3.isOn && property.Available())
                    .Select(colors => colors.S3 with { a = property.Get().a }).Subscribe(property.Set)
            );
        int Value
        {
            set => (value switch { 1 => M1, 2 => M2, 3 => M3, 4 => S1, 5 => S2, 6 => S3, _ => NA }).Set(true, true);
            get => M1.isOn ? 1 : M2.isOn ? 2 : M3.isOn ? 3 : S1.isOn ? 4 : S2.isOn ? 5 : S3.isOn ? 6 : 0;
        }
        internal static void Save(IEnumerable<PropertyEdit> edits, string path) =>
            Json<Dictionary<string, int>>.Save(Plugin.Instance.Log.LogMessage, File.OpenWrite(path),
                edits.Where(edit => !edit.NA.isOn).ToDictionary(edit => edit.Name, edit => edit.Value));
        internal static void Load(IEnumerable<PropertyEdit> edits, string path) =>
            Load(edits, Json<Dictionary<string, int>>.Load(Plugin.Instance.Log.LogMessage, File.OpenRead(path)));
        static void Load(IEnumerable<PropertyEdit> edits, Dictionary<string, int> mapping) =>
            edits.With(Clear).ForEach(edit => edit.Value = mapping.GetValueOrDefault(edit.Name, 0));
        internal static void Clear(IEnumerable<PropertyEdit> edits) =>
            edits.With(Apply).ForEach(edit => edit.NA.Set(true, true));
        static void Apply(IEnumerable<PropertyEdit> edits) =>
            edits.ForEach(edit => edit.Image.color = Color.clear);
    }
    class MappingEdit
    {
        internal static readonly UIAction PrepareTemplate =
            "Mapping".AsChild(
                UGUI.LayoutV() +
                "Headers".AsChild(
                    UGUI.ClearPanel + UGUI.Size(375, 24) +
                    UGUI.LayoutH(padding: UGUI.Offset(5, 0)) +
                    "Clear".AsChild(UGUI.Button(60, 24, UGUI.Text(text: "Clear"))) +
                    "Space".AsChild(UGUI.Size(156, 24)) +
                    "M1".AsChild(UGUI.Size(24, 24)) +
                    "M2".AsChild(UGUI.Size(24, 24)) +
                    "M3".AsChild(UGUI.Size(24, 24)) +
                    "S1".AsChild(UGUI.Size(24, 24)) +
                    "S2".AsChild(UGUI.Size(24, 24)) +
                    "S3".AsChild(UGUI.Size(24, 24))) +
                "Scroll".AsChild(
                    UGUI.Scroll(375, 560, "Groups".AsChild(UGUI.LayoutV() + UGUI.ColorPanel +
                        ColorProperties.Definitions.Select(entry => PropertyEdit.PrepareTemplate(entry.Key, entry.Value.Keys)).Aggregate()))));
        internal List<PropertyEdit> Values;
        MappingEdit(GameObject go, IObservable<PaletteRGB> rgbs) =>
            go.With((
                UGUI.Component<Button>(cmp => cmp.OnClickAsObservable().Subscribe(_ => PropertyEdit.Clear(Values))).At("Clear") +
                UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M1)).At("M1") +
                UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M2)).At("M2") +
                UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M3)).At("M3") +
                UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S1)).At("S1") +
                UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S2)).At("S2") +
                UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S3)).At("S3")
            ).At("Mapping", "Headers"));
        internal MappingEdit(GameObject go, Toggle showAssignedOnly, IObservable < PaletteRGB > rgbs, Human human) : this(go, rgbs) =>
            Values = PropertyEdit.Initialize(go.transform.Find("Mapping").Find("Scroll").Find("ViewPort").Find("Groups"), showAssignedOnly, rgbs, ColorProperties.Groups(human)).ToList();
        internal void Save(string path) =>
            PropertyEdit.Save(Values, path);
        internal void Load(string path) =>
            PropertyEdit.Load(Values, path);
    }
    class EditWindow
    {
        internal static readonly UIAction PrepareTemplate =
            UGUI.LayoutV() +
            "Controls".AsChild(
                UGUI.ColorPanel + UGUI.Size(375, 24) +
                UGUI.LayoutH(padding: UGUI.Offset(5, 0)) +
                UGUI.ToggleGroup() +
                "CheckAssigned".AsChild(UGUI.Check(24, 24)) +
                "LabelAssigned".AsChild(UGUI.Label(80, 24) + UGUI.Text(text: "Assigned")) +
                "Space".AsChild(UGUI.Size(91, 24)) +
                "Edit".AsChild(UGUI.Toggle(60, 24, UGUI.Text(text: "Edit")) + UGUI.GroupToggle) +
                "SaveLoad".AsChild(UGUI.Toggle(110, 24, UGUI.Text(text: "Save&Load")) + UGUI.GroupToggle)) +
            MappingEdit.PrepareTemplate +
            "SaveLoad".AsChild(
                UGUI.GameObject(active: false) +
                UGUI.LayoutV() +
                "Controls".AsChild(
                    UGUI.ColorPanel + UGUI.Size(375, 24) + UGUI.LayoutH() +
                    UGUI.ToggleGroup() +
                    "Mapping".AsChild(UGUI.Toggle(100, 24, UGUI.Text(text: "Mapping")) + UGUI.GroupToggle) +
                    "Palette".AsChild(UGUI.Toggle(100, 24, UGUI.Text(text: "Palette")) + UGUI.GroupToggle) +
                    "Spacing".AsChild(UGUI.Size(50, 24)) +
                    "Save".AsChild(UGUI.Button(60, 24, UGUI.Text(text: "Save"))) +
                    "Load".AsChild(UGUI.Button(60, 24, UGUI.Text(text: "Load")))) +
                "Input".AsChild(
                    UGUI.ColorPanel +
                    UGUI.Size(375, 24) +
                    UGUI.LayoutH() +
                    "FileName".AsChild(UGUI.Input(375, 24, UGUI.Text(align: TMPro.TextAlignmentOptions.Left)))) +
                "Scroll".AsChild(UGUI.Scroll(375, 540,
                    UGUI.ColorPanel + "Files".AsChild(UGUI.LayoutV() + UGUI.ToggleGroup(allowSwitchOff: true)))));
        static readonly string MappingPath = Path.Combine(Util.UserDataPath, "plugins", Plugin.Name, "Mapping");
        static readonly string PalettePath = Path.Combine(Util.UserDataPath, "plugins", Plugin.Name, "Palette");
        Toggle ShowAssignedOnly, EditMode, SaveLoadMode, SaveLoadMapping, SaveLoadPalette;
        Button Save, Load;
        TMPro.TMP_InputField InputField;
        GameObject FileList;
        PaletteEdit Palette;
        MappingEdit Mapping;
        EditWindow(GameObject mode) =>
            mode.With((
                UGUI.Component<Toggle>(cmp => ShowAssignedOnly = cmp).At("CheckAssigned") +
                UGUI.Component<Toggle>(cmp => EditMode = cmp).At("Edit") +
                UGUI.Component<Toggle>(cmp => SaveLoadMode = cmp).At("SaveLoad")
            ).At("Controls") + ((
                    UGUI.Component<Toggle>(cmp => SaveLoadMapping = cmp).At("Mapping") +
                    UGUI.Component<Toggle>(cmp => SaveLoadPalette = cmp).At("Palette") +
                    UGUI.Component<Button>(cmp => Save = cmp).At("Save") +
                    UGUI.Component<Button>(cmp => Load = cmp).At("Load")
                ).At("Controls") +
                UGUI.Component<TMPro.TMP_InputField>(cmp => InputField = cmp).At("Input", "FileName") +
                new UIAction(go => FileList = go).At("Scroll", "ViewPort", "Files")
            ).At("SaveLoad"));
        EditWindow(GameObject mode, PaletteEdit palette, Human human) : this(mode) =>
            Mapping = new (mode, ShowAssignedOnly, (Palette = palette).OnValueChange.Select(p => p.RGB), human);
        EditWindow(GameObject mode, GameObject palette, Human human) : this(mode, new PaletteEdit(palette), human) =>
            _ = (
                EditMode.OnValueChangedAsObservable().Subscribe(mode.transform.Find("Mapping").gameObject.SetActive),
                SaveLoadMode.OnValueChangedAsObservable().Subscribe(mode.transform.Find("SaveLoad").gameObject.SetActive),
                SaveLoadMapping.OnValueChangedAsObservable()
                    .Where(value => value).Select(_ => MappingPath).Subscribe(UpdateFileList),
                SaveLoadPalette.OnValueChangedAsObservable()
                    .Where(value => value).Select(_ => PalettePath).Subscribe(UpdateFileList),
                Load.OnClickAsObservable()
                    .Where(_ => SaveLoadMapping.isOn && InputField.text is not "")
                    .Select(_ => Path.Combine(MappingPath, InputField.text))
                    .Where(File.Exists).Subscribe(Mapping.Load),
                Load.OnClickAsObservable()
                    .Where(_ => SaveLoadPalette.isOn && InputField.text is not "")
                    .Select(_ => Path.Combine(PalettePath, InputField.text))
                    .Where(File.Exists).Subscribe(Palette.Load),
                Save.OnClickAsObservable()
                    .Where(_ => SaveLoadMapping.isOn && InputField.text is not "")
                    .Select(_ => Path.Combine(MappingPath, InputField.text))
                    .Subscribe(path => path.With(Mapping.Save).With(UpdateFileList)),
                Save.OnClickAsObservable()
                    .Where(_ => SaveLoadPalette.isOn && InputField.text is not "")
                    .Select(_ => Path.Combine(PalettePath, InputField.text))
                    .Subscribe(path => path.With(Palette.Save).With(UpdateFileList)));
        EditWindow(GameObject go, Human human) : this(
            UnityEngine.Object.Instantiate(UI.ModePanel, go.transform),
            UnityEngine.Object.Instantiate(UI.PaletteEdit, go.transform), human) =>
            Palette.Value = new (0.0f, 0.5f, 0.5f, 0.125f, 0.0f, 0.0f, Mathf.PI / 6.0f, Mathf.PI / 6.0f, 0.25f);
        void UpdateFileList(string dataPath) =>
            FileList.With(() => InputField.text = "")
                .With(UGUI.DestroyChildren)
                .With(Directory.GetFiles(dataPath)
                    .Select(path => Path.GetRelativePath(dataPath, path))
                    .Select((path, index) => $"Item{index}".AsChild(
                        UGUI.Toggle(370, 24, UGUI.Text(text: path)) + UGUI.GroupToggle +
                        UGUI.Component<Toggle>(cmp => cmp.OnValueChangedAsObservable()
                            .Where(value => value).Subscribe(_ => InputField.SetText(path))))).Aggregate());
        static IDisposable Initialize(WindowConfig config) =>
            HumanCustomExtension.OnHumanInitialize.Subscribe(tuple =>
                new EditWindow(config.Create(508, 500, "DorsalFin").Content.With(UGUI.RtFill + UGUI.LayoutH(spacing: 5)), tuple.Human));
        internal static IDisposable Initialize(Plugin plugin) =>
            Initialize(new WindowConfig(plugin, "DorsalFin", new(1000, -400), new KeyboardShortcut(KeyCode.D, KeyCode.LeftControl)));
    }
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CoastalSmell.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        public const string Name = "DorsalFin";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "1.1.0";
        internal static Plugin Instance;
        CompositeDisposable Subscription;
        public Plugin() : base() => Instance = this;
        public override void Load() =>
            Subscription = [UI.Initialize(), EditWindow.Initialize(this)];
        public override bool Unload() =>
            true.With(Subscription.Dispose) && base.Unload();
    }
}