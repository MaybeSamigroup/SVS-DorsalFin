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
            Colors[(int)Mathf.Repeat(hsv.x * 128f, 128f)];
        internal static Vector2 ToPosition(Vector3 hsv) =>
            new((hsv.y - 0.5f) * 128f, (hsv.z - 0.5f) * 128f);
        internal static IDisposable Initialize() =>
            UGUI.OnCommonSpaceInitialize.Subscribe(tf => tf.With(Plugin.Name.AsChild(
                "Reticle".AsChild(UGUI.Image(sprite: Reticle)) +
                "Cross".AsChild(UGUI.Image(sprite: Cross)) +
                Colors.Select((color, index) => $"Hue{index}".AsChild(UGUI.Image(sprite: color))).Aggregate())));
    }
    class Vector3Edit
    {
        Slider V1, V2, V3;
        Vector3Edit(GameObject go, string name, string v1, string v2, string v3) =>
            go.With(
                UGUI.LayoutV(spacing: 5, padding: UGUI.Offset(5, 0)) + UGUI.ColorPanel +
                "Label".AsChild(UGUI.Label(118, 24) + UGUI.Text(text: name)) +
                "V1".AsChild(PrepareSlider(v1, cmp => V1 = cmp)) +
                "V2".AsChild(PrepareSlider(v2, cmp => V2 = cmp)) +
                "V3".AsChild(PrepareSlider(v3, cmp => V3 = cmp)));
        UIAction PrepareSlider(string name, Action<Slider> action) =>
            UGUI.LayoutH(childAlignment: TextAnchor.MiddleLeft) + UGUI.Size(118, 24) +
            "Label".AsChild(UGUI.Label(18, 24) + UGUI.Text(text: name)) +
            "Value".AsChild(UGUI.Slider(100, 20) + UGUI.Component(action));
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
        internal static UIAction AsChild(string name, string v1, string v2, string v3, Action<Vector3Edit> action) =>
            new UIAction(go => action(new Vector3Edit(go, name, v1, v2, v3)));
    }
    class PaletteEdit
    {
        Vector3Edit Main, Gaps, Tone;
        PaletteEdit(GameObject go) =>
            go.With(
                UGUI.LayoutV(spacing: 5) +
                "Main".AsChild(Vector3Edit.AsChild("Main Color", "H", "S", "V", ui => Main = ui)) +
                "Gaps".AsChild(Vector3Edit.AsChild("Sub Color Gaps", "H", "S", "V", ui => Gaps = ui)) +
                "Tone".AsChild(Vector3Edit.AsChild("Tone Isosceles", "A", "R", "D", ui => Tone = ui)));
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
                Tone.OnValueChange.Select(value => Value with { IsoscelesAngle = value.x, IsoscelesRotation = value.y, IsoscelesSize = value.z })
        );
        internal static UIAction AsChild(Action<PaletteEdit> action) =>
            new UIAction(go => action(new PaletteEdit(go)));
        internal void Save(string path) =>
            Json<Palette>.Save(Plugin.Instance.Log.LogMessage, File.OpenWrite(path), Value);
        internal void Load(string path) =>
            File.Exists(path).Maybe(() => Value = Json<Palette>.Load(Plugin.Instance.Log.LogMessage, File.OpenRead(path)));
    }
    class ColorView
    {
        ColorView(GameObject go, IObservable<Sprite> hue, IObservable<Vector2> p1, IObservable<Vector2> p2, IObservable<Vector2> p3) =>
            go.With(
                UGUI.Size(128, 128) + UGUI.Image(type: Image.Type.Filled) +
                UGUI.Component<Image>(image => hue.Subscribe(sprite => image.sprite = sprite)) +
                "P1".AsChild(
                    UGUI.Image(sprite: UI.Reticle) + UGUI.Rt(sizeDelta: new(21, 21)) +
                    UGUI.Component<RectTransform>(rt => p1.Subscribe(p => rt.localPosition = p))) +
                "P2".AsChild(
                    UGUI.Image(sprite: UI.Cross) + UGUI.Rt(sizeDelta: new(11, 11)) +
                    UGUI.Component<RectTransform>(rt => p2.Subscribe(p => rt.localPosition = p))) +
                "P3".AsChild(
                    UGUI.Image(sprite: UI.Cross) + UGUI.Rt(sizeDelta: new(11, 11)) +
                    UGUI.Component<RectTransform>(rt => p3.Subscribe(p => rt.localPosition = p))));
        internal static UIAction AsChild(IObservable<Vector3> hsv1, IObservable<Vector3> hsv2, IObservable<Vector3> hsv3) =>
            new UIAction(go => new ColorView(go, hsv1.Select(UI.ToHueSprite), hsv1.Select(UI.ToPosition), hsv2.Select(UI.ToPosition), hsv3.Select(UI.ToPosition)));
    }
    class PalettePanel
    {
        PaletteEdit Control;
        IObservable<PaletteHSV> OnHSVChange;
        PalettePanel(GameObject go) =>
            go.With(
                UGUI.LayoutV(spacing: 10) +
                "Control".AsChild(PaletteEdit.AsChild(cmp => OnHSVChange = (Control = cmp).OnValueChange.Select(p => p.HSV)))
            ).With(
                "MainColor".AsChild(ColorView.AsChild(
                    OnHSVChange.Select(p => p.M1),
                    OnHSVChange.Select(p => p.M2),
                    OnHSVChange.Select(p => p.M3))) +
                "SubColor".AsChild(ColorView.AsChild(
                    OnHSVChange.Select(p => p.S1),
                    OnHSVChange.Select(p => p.S2),
                    OnHSVChange.Select(p => p.S3))));
        internal static UIAction AsChild(Action<PaletteEdit> action) =>
            new UIAction(go => action(new PalettePanel(go).Control));
    }
    class PropertyEdit
    {
        string Name;
        Image Image;
        Toggle NA, M1, M2, M3, S1, S2, S3;
        PropertyEdit(GameObject go, string name, ColorProperty property, IObservable<PaletteRGB> rgbs) =>
            go.With(
                UGUI.GameObject(active: property.Available()) +
                UGUI.LayoutH(padding: UGUI.Offset(5, 0)) +
                UGUI.UnderlinePanel + UGUI.ToggleGroup() +
                "Name".AsChild(UGUI.Label(150, 24) + UGUI.Text(text: Name = name)) +
                "State".AsChild(UGUI.Label(42, 24) + UGUI.Text(text: property.Available() ? "" : "(N/A)") +
                    UGUI.Component<TMPro.TextMeshProUGUI>(cmp => cmp
                        .OnEnableAsObservable().Subscribe(_ => cmp.SetText(property.Available() ? "" : "(N/A)")))) +
                "NA".AsChild(PrepareReset(property, cmp => Image = cmp, cmp => NA = cmp)) +
                "M1".AsChild(PrepareRadio(property, rgbs.Select(colors => colors.M1), cmp => M1 = cmp)) +
                "M2".AsChild(PrepareRadio(property, rgbs.Select(colors => colors.M2), cmp => M2 = cmp)) +
                "M3".AsChild(PrepareRadio(property, rgbs.Select(colors => colors.M3), cmp => M3 = cmp)) +
                "S1".AsChild(PrepareRadio(property, rgbs.Select(colors => colors.S1), cmp => S1 = cmp)) +
                "S2".AsChild(PrepareRadio(property, rgbs.Select(colors => colors.S2), cmp => S2 = cmp)) +
                "S3".AsChild(PrepareRadio(property, rgbs.Select(colors => colors.S3), cmp => S3 = cmp)));

        UIAction PrepareReset(ColorProperty property, Action<Image> image, Action<Toggle> action) =>
            UGUI.Image(color: Color.clear, alphaHit: 1.0f) + UGUI.Radio(24, 24) + UGUI.GroupToggle +
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
            UGUI.Image(color: Color.clear, alphaHit: 1.0f) + UGUI.Radio(24, 24) + UGUI.GroupToggle +
            UGUI.Component<Image>(image => rgb.Subscribe(color => image.color = color)) +
            UGUI.Component<Image, Toggle>((image, toggle) =>
                toggle.With(action).OnValueChangedAsObservable()
                    .Where(value => value & property.Available())
                    .Select(_ => image.color with { a = property.Get().a }).Subscribe(property.Set));
        PropertyEdit(GameObject go, string name, ColorProperty property, IObservable<PaletteRGB> rgbs, IObservable<bool> update) : this(go, name, property, rgbs) =>
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

        internal static UIAction AsChild(
            Dictionary<string, ColorProperty> properties,
            Toggle showAssignedOnly, IObservable<PaletteRGB> rgbs, Action<IEnumerable<PropertyEdit>> action) =>
            new UIAction(go => action(properties.Select(entry =>
                new PropertyEdit(new GameObject(entry.Key).With(go.AsParent()),
                    entry.Key, entry.Value, rgbs, go.OnUpdateAsObservable().Select(_ => showAssignedOnly.isOn)))));
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
    class GroupEdit
    {
        Toggle Toggle;
        internal IEnumerable<PropertyEdit> Values;
        GroupEdit(GameObject go, string name, IObservable<PaletteRGB> rgbs) =>
            go.With($"{name}Title".AsChild(
                UGUI.LayoutH(padding: UGUI.Offset(5, 0)) +
                UGUI.UnderlinePanel + UGUI.Size(370, 24) +
                "Check".AsChild(UGUI.Fold(24, 24) + UGUI.Component<Toggle>(cmp => (Toggle = cmp).Set(true))) +
                "Label".AsChild(UGUI.Label(192, 24) + UGUI.Text(text: name)) +
                "M1".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M1))) +
                "M2".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M2))) +
                "M3".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M3))) +
                "S1".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S1))) +
                "S2".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S2))) +
                "S3".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S3)))));
                
        GroupEdit(GameObject go, string name, Toggle showAssignedOnly, IObservable<PaletteRGB> rgbs, Dictionary<string, ColorProperty> properties) : this(go, name, rgbs) =>
            go.With($"{name}Edits".AsChild(
                UGUI.LayoutV() +
                PropertyEdit.AsChild(properties, showAssignedOnly, rgbs, cmps => Values = cmps.ToList()) +
                new UIAction(go => Toggle.OnValueChangedAsObservable().Subscribe(go.SetActive))));

        internal static UIAction AsChild(string name, Toggle showAssignedOnly,
            IObservable<PaletteRGB> rgbs, Dictionary<string, ColorProperty> properties, Action<GroupEdit> action) =>
            new UIAction(go => action(new GroupEdit(go, name, showAssignedOnly, rgbs, properties)));
    }
    class MappingEdit
    {
        GroupEdit Body, Face, Hairs, Clothes, Accessories;
        internal IEnumerable<PropertyEdit> Values =>
            [.. Body.Values, .. Face.Values, .. Hairs.Values, .. Clothes.Values, .. Accessories.Values];
        MappingEdit(GameObject go, Toggle showAssignedOnly, IObservable<PaletteRGB> rgbs, Human human) =>
            go.With(
                UGUI.LayoutV() +
                "Headers".AsChild(
                    UGUI.ClearPanel + UGUI.Size(375, 24) +
                    UGUI.LayoutH(padding: UGUI.Offset(5, 0)) +
                    "Clear".AsChild(
                        UGUI.Button(60, 24, UGUI.Text(text: "Clear")) +
                        UGUI.Component<Button>(cmp => cmp.OnClickAsObservable().Subscribe(_ => PropertyEdit.Clear(Values)))) +
                    "Space".AsChild(UGUI.Size(156, 24)) +
                    "M1".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M1))) +
                    "M2".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M2))) +
                    "M3".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.M3))) +
                    "S1".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S1))) +
                    "S2".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S2))) +
                    "S3".AsChild(UGUI.Size(24, 24) + UGUI.Component<Image>(cmp => rgbs.Subscribe(colors => cmp.color = colors.S3)))) +
                "Values".AsChild(
                    UGUI.Scroll(375, 560,
                        "Groups".AsChild(UGUI.LayoutV() + UGUI.ColorPanel +
                            GroupEdit.AsChild("Body", showAssignedOnly, rgbs, ColorProperties.Body(human), cmp => Body = cmp) +
                            GroupEdit.AsChild("Face", showAssignedOnly, rgbs, ColorProperties.Face(human), cmp => Face = cmp) +
                            GroupEdit.AsChild("Hairs", showAssignedOnly, rgbs, ColorProperties.Hairs(human), cmp => Hairs = cmp) +
                            GroupEdit.AsChild("Clothes", showAssignedOnly, rgbs, ColorProperties.Clothes(human), cmp => Clothes = cmp) +
                            GroupEdit.AsChild("Accessories", showAssignedOnly, rgbs, ColorProperties.Accessories(human), cmp => Accessories = cmp)))));
        MappingEdit(GameObject go, Toggle mode, Toggle showAssignedOnly, IObservable<PaletteRGB> rgbs, Human human) : this(go, showAssignedOnly, rgbs, human) =>
                mode.OnValueChangedAsObservable().Subscribe(go.SetActive);
        internal static UIAction AsChild(Toggle mode, Toggle showAssignedOnly, PaletteEdit palette, Human human, Action<MappingEdit> action) =>
            new UIAction(go => action(new MappingEdit(go, mode, showAssignedOnly, palette.OnValueChange.Select(p => p.RGB), human)));
        internal void Save(string path) =>
            PropertyEdit.Save(Values, path);
        internal void Load(string path) =>
            PropertyEdit.Load(Values, path);
    }
    class OptionsUI
    {
        Toggle ShowAssignedOnly, Edit, SaveLoad;
        PaletteEdit Palette;
        MappingEdit Mapping;
        OptionsUI(GameObject go) =>
            go.With(
                UGUI.RtFill + UGUI.LayoutH(spacing: 5) +
                "ModePanel".AsChild(
                    UGUI.LayoutV() +
                    "Controls".AsChild(
                        UGUI.ColorPanel + UGUI.Size(375, 24) +
                        UGUI.LayoutH(padding: UGUI.Offset(5, 0)) +
                        UGUI.ToggleGroup() +
                        "CheckAssigned".AsChild(UGUI.Check(24, 24) + UGUI.Component<Toggle>(cmp => ShowAssignedOnly = cmp)) +
                        "LabelAssigned".AsChild(UGUI.Label(80, 24) + UGUI.Text(text: "Assigned")) +
                        "Space".AsChild(UGUI.Size(91, 24)) +
                        "Edit".AsChild(
                            UGUI.Toggle(60, 24, UGUI.Text(text: "Edit")) +
                            UGUI.GroupToggle + UGUI.Component<Toggle>(cmp => Edit = cmp)) +
                        "ModeToggle".AsChild(
                            UGUI.Toggle(110, 24, UGUI.Text(text: "Save&Load")) +
                            UGUI.GroupToggle + UGUI.Component<Toggle>(cmp => SaveLoad = cmp)))) +
                "Palette".AsChild(PalettePanel.AsChild(cmp => Palette = cmp)));
        OptionsUI(GameObject go, Human human) : this(go) =>
            go.With("Mapping".AsChild(MappingEdit.AsChild(Edit,
                ShowAssignedOnly, Palette, human, cmp => Mapping = cmp)).At("ModePanel"))
                .With("SaveLoad".AsChild(SaveLoadUI.AsChild(SaveLoad, Mapping, Palette)).At("ModePanel"));
        static IDisposable Initialize(WindowConfig config) =>
            HumanCustomExtension.OnHumanInitialize.Subscribe(tuple =>
                new OptionsUI(config.Create(508, 500, "DorsalFin").Content, tuple.Human)
                    .Palette.Value = new (0.0f, 0.5f, 0.5f, 0.125f, 0.0f, 0.0f, Mathf.PI / 6.0f, Mathf.PI / 6.0f, 0.25f));
        internal static IDisposable Initialize(Plugin plugin) =>
            Initialize(new WindowConfig(plugin, "DorsalFin", new(1000, -400), new KeyboardShortcut(KeyCode.D, KeyCode.LeftControl)));
    }
    class SaveLoadUI
    {
        static readonly string MappingPath = Path.Combine(Util.UserDataPath, "plugins", Plugin.Name, "Mapping");
        static readonly string PalettePath = Path.Combine(Util.UserDataPath, "plugins", Plugin.Name, "Palette");
        GameObject FileList;
        Toggle Mapping, Palette;
        Button Save, Load;
        TMPro.TMP_InputField InputField;
        SaveLoadUI(GameObject go) =>
            go.With(UGUI.GameObject(active: false) + UGUI.LayoutV() +
                "Controls".AsChild(
                    UGUI.ColorPanel + UGUI.Size(375, 24) + UGUI.LayoutH() +
                    UGUI.ToggleGroup() +
                    "Mapping".AsChild(
                        UGUI.Toggle(100, 24, UGUI.Text(text: "Mapping")) +
                        UGUI.Component<Toggle>(cmp => Mapping = cmp) + UGUI.GroupToggle) +
                    "Palette".AsChild(
                        UGUI.Toggle(100, 24, UGUI.Text(text: "Palette")) +
                        UGUI.Component<Toggle>(cmp => Palette = cmp) + UGUI.GroupToggle) +
                    "Spacing".AsChild(UGUI.Size(50, 24)) +
                    "Save".AsChild(
                        UGUI.Button(60, 24, UGUI.Text(text: "Save")) +
                        UGUI.Component<Button>(cmp => Save = cmp)) +
                    "Load".AsChild(
                        UGUI.Button(60, 24, UGUI.Text(text: "Load")) +
                        UGUI.Component<Button>(cmp => Load = cmp))) +
                "Input".AsChild(
                    UGUI.ColorPanel +
                    UGUI.Size(375, 24) +
                    UGUI.LayoutH() +
                    "FileName".AsChild(
                        UGUI.Input(375, 24, UGUI.Text(align: TMPro.TextAlignmentOptions.Left)) +
                        UGUI.Component<TMPro.TMP_InputField>(cmp => InputField = cmp))) +
                "Scroll".AsChild(UGUI.Scroll(375, 540,
                    UGUI.ColorPanel + "Files".AsChild(
                        UGUI.LayoutV() +
                        UGUI.ToggleGroup(allowSwitchOff: true) +
                        new UIAction(go => FileList = go)))));
        SaveLoadUI(GameObject go, Toggle mode, MappingEdit mapping, PaletteEdit palette) : this(go) =>
            _ = (
                mode.OnValueChangedAsObservable().Subscribe(go.SetActive),
                Mapping.OnValueChangedAsObservable()
                    .Where(value => value).Select(_ => MappingPath).Subscribe(UpdateFileList),
                Palette.OnValueChangedAsObservable()
                    .Where(value => value).Select(_ => PalettePath).Subscribe(UpdateFileList),
                Load.OnClickAsObservable()
                    .Where(_ => Mapping.isOn && InputField.text is not "")
                    .Select(_ => Path.Combine(MappingPath, InputField.text))
                    .Where(File.Exists).Subscribe(mapping.Load),
                Load.OnClickAsObservable()
                    .Where(_ => Palette.isOn && InputField.text is not "")
                    .Select(_ => Path.Combine(PalettePath, InputField.text))
                    .Where(File.Exists).Subscribe(palette.Load),
                Save.OnClickAsObservable()
                    .Where(_ => Mapping.isOn && InputField.text is not "")
                    .Select(_ => Path.Combine(MappingPath, InputField.text))
                    .Subscribe(path => path.With(mapping.Save).With(UpdateFileList)),
                Save.OnClickAsObservable()
                    .Where(_ => Palette.isOn && InputField.text is not "")
                    .Select(_ => Path.Combine(PalettePath, InputField.text))
                    .Subscribe(path => path.With(palette.Save).With(UpdateFileList))
            );
        internal static UIAction AsChild(Toggle mode, MappingEdit mapping, PaletteEdit palette) =>
            new UIAction(go => new SaveLoadUI(go, mode, mapping, palette));
        void UpdateFileList(string dataPath) =>
            FileList.With(() => InputField.text = "")
                .With(UGUI.DestroyChildren)
                .With(Directory.GetFiles(dataPath)
                    .Select(path => Path.GetRelativePath(dataPath, path))
                    .Select((path, index) => $"Item{index}".AsChild(
                        UGUI.Toggle(370, 24, UGUI.Text(text: path)) + UGUI.GroupToggle +
                        UGUI.Component<Toggle>(cmp => cmp.OnValueChangedAsObservable()
                            .Where(value => value).Subscribe(_ => InputField.SetText(path))))).Aggregate());
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
            Subscription = [UI.Initialize(), OptionsUI.Initialize(this)];
        public override bool Unload() =>
            true.With(Subscription.Dispose) && base.Unload();
    }
}