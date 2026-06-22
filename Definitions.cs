using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Character;
using CoastalSmell;
using ColorProperty = (
    System.Func<bool> Available,
    System.Func<UnityEngine.Color> Get,
    System.Action<UnityEngine.Color> Set
);

namespace DorsalFin
{
    static partial class UI
    {
        internal static readonly Sprite Reticle =
            Sprite.Create(new Texture2D(21, 21).With(t2d => t2d.SetPixels(
                Enumerable.Range(-10, 21).SelectMany(x =>
                Enumerable.Range(-10, 21).Select(y =>
                (Math.Abs(x), Math.Abs(y)) switch
                {
                    (1, 4) or (2, 3) or (3, 2) or (4, 1) or (0, > 5) or ( > 5, 0) => new Color(0.1f, 0.1f, 0.1f, 1f),
                    (2, 4) or (3, 3) or (4, 2) or (1, > 5) or ( > 5, 1) => new Color(0.9f, 0.9f, 0.9f, 1f),
                    (_, _) => new Color(0f, 0f, 0f, 0f)
                })).ToArray())).With(t2d => t2d.Apply(false)), new(0, 0, 21, 21), new(0.5f, 0.5f));
        internal static readonly Sprite Cross =
            Sprite.Create(new Texture2D(11, 11).With(t2d => t2d.SetPixels(
                Enumerable.Range(-5, 11).SelectMany(x =>
                Enumerable.Range(-5, 11).Select(y =>
                (Math.Abs(x), Math.Abs(y)) switch
                {
                    (1, 2) or (2, 1) or (0, > 3) or ( > 3, 0) => new Color(0.1f, 0.1f, 0.1f, 1f),
                    (2, 2) or (1, > 3) or ( > 3, 1) => new Color(0.9f, 0.9f, 0.9f, 1f),
                    (_, _) => new Color(0f, 0f, 0f, 0f)
                })).ToArray())).With(t2d => t2d.Apply(false)), new(0, 0, 11, 11), new(0.5f, 0.5f));
        static readonly Texture2D PaletteTexture = new Texture2D(128, 128 * 128)
            .With(t2d => t2d.SetPixels(
                Enumerable.Range(0, 128).SelectMany(hue =>
                Enumerable.Range(0, 128).SelectMany(brightness =>
                Enumerable.Range(0, 128).Select(saturation =>
                    Color.HSVToRGB(
                        0.0078125f * hue,
                        0.0078125f * saturation,
                        0.0078125f * brightness)))).ToArray()))
                .With(t2d => t2d.Compress(true))
                .With(t2d => t2d.Apply(false));
        internal static readonly GameObject ModePanel =
            new GameObject("ModePanel").With(EditWindow.PrepareTemplate);
        internal static readonly GameObject PaletteEdit =
            new GameObject("PalettePanel").With(DorsalFin.PaletteEdit.PrepareTemplate);
    }
    static class ColorProperties
    {
        static HumanDataCoordinate NowCoordinate(Human human) =>
            Coordinate(human, human?.data?.Status?.coordinateType ?? 0);
        static HumanDataCoordinate Coordinate(Human human, int type) =>
            human?.data?.Coordinates?[type];
        static CustomTextureControl BodyCtc(Human human) =>
#if Aicomi
            human?.body?._customTexCtrlBody;
#else
            human?.body?.customTexCtrlBody;
#endif
        static CustomTextureControl FaceCtc(Human human) =>
#if Aicomi
            human?.face?._customTexCtrlFace;
#else
            human?.face?.customTexCtrlFace;
#endif
        static Action<int, Color> ClothesCtc(Human human, int part) =>
            ClothesCtc(human.cloth.Clothess[part]);
        static Action<int, Color> ClothesCtc(HumanCloth.Clothes part) =>
            ClothesCtc(part.cusClothesCmp, part.ctCreateClothes);
        static Action<int, Color> ClothesCtc(ChaClothesComponent cmp, params CustomTextureCreate[] ctcs) =>
            ctcs.Where(ctc => ctc?._matCreate is not null)
                .Select(ctc => ClothesCtc(ctc, cmp.Rebuild01, cmp.Rebuild02, cmp.Rebuild03)).Aggregate((a1, a2) => a1 + a2);
        static Action<int, Color> ClothesCtc(CustomTextureCreate ctc, params Func<CustomTextureCreate, int, bool>[] rebuilds) =>
            ClothesCtc(rebuilds.Aggregate((f1, f2) => f1 + f2), ctc);
        static Action<int, Color> ClothesCtc(Func<CustomTextureCreate, int, bool> rebuild, CustomTextureCreate ctc) =>
            (id, color) => rebuild.With(F.Apply(ctc._matCreate.SetColor, id, color)).Invoke(ctc, id);
        static bool HasColor(CustomTextureControl ctc, int id) =>
            ctc?._matCreate.HasColor(id) ?? false;
        static void SetColor(CustomTextureControl ctc, int id, Color color) =>
            ctc.With(F.Apply(ctc._matCreate.SetColor, id, color)).SetNewCreateTexture();
        static void SetColor(int id, Color color, params CustomTextureControl[] ctcs) =>
            ctcs.ForEach(ctc => SetColor(ctc, id, color));
        static void SetColor(int id, Color color, params Renderer[] renderers) =>
            renderers.ForEach(rend => rend.material.SetColor(id, color));
        internal static Dictionary<string, Dictionary<string, ColorProperty>> Groups(Human human) =>
            Definitions.ToDictionary(entry => entry.Key, entry => Groups(human, entry.Value));
        static Dictionary<string, ColorProperty> Groups(Human human, Dictionary<string, Func<Human, ColorProperty>> definitions) =>
            definitions.ToDictionary(entry => entry.Key, entry => entry.Value(human));
        internal static readonly Dictionary<string, Dictionary<string, Func<Human, ColorProperty>>> Definitions = new()
        {
            ["Body"] = new()
            {
                ["SkinMain"] = human =>
                (
                    () => HasColor(BodyCtc(human), ChaShader.Body.CreateShader.sMainColorID),
                    () => human.data.Custom.Body.skinMainColor,
                    color => SetColor(
                        ChaShader.Body.CreateShader.sMainColorID, human.data.Custom.Body.skinMainColor = color, BodyCtc(human), FaceCtc(human))
                ),
                ["SkinDetail1"] = human =>
                (
                    () => human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sDetailColor01) ?? false,
                    () => human.data.Custom.Body.skinDetailColors[0],
                    color => human.body.rendBody.material
                        .SetColor(ChaShader.Body.sDetailColor01, human.data.Custom.Body.skinDetailColors[0] = color)
                ),
                ["SkinDetail2"] = human =>
                (
                    () => human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sDetailColor02) ?? false,
                    () => human.data.Custom.Body.skinDetailColors[1],
                    color => human.body.rendBody.material
                        .SetColor(ChaShader.Body.sDetailColor02, human.data.Custom.Body.skinDetailColors[1] = color)
                ),
                ["SkinDetail3"] = human =>
                (
                    () => human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sDetailColor03) ?? false,
                    () => human.data.Custom.Body.skinDetailColors[2],
                    color => human.body.rendBody.material
                        .SetColor(ChaShader.Body.sDetailColor03, human.data.Custom.Body.skinDetailColors[2] = color)
                ),
                ["SkinHighlight"] = human =>
                (
                    () => human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sHighlightColorID) ?? false,
                    () => human.data.Custom.Body.skinHighlightColor,
                    color => human.body.rendBody.material
                        .SetColor(ChaShader.Body.sHighlightColorID, human.data.Custom.Body.skinHighlightColor = color)
                ),
                ["SkinShadow"] = human =>
                (
                    () => human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sShadowColorID) ?? false,
                    () => human.data.Custom.Body.skinShadowColor,
                    color => human.body.rendBody.material
                        .SetColor(ChaShader.Body.sShadowColorID, human.data.Custom.Body.skinShadowColor = color)
                ),
                ["Nip"] = human =>
                (
                    () => human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sNipColorID) ?? false,
                    () => human.data.Custom.Body.nipColor,
                    color => human.body.rendBody.material
                        .SetColor(ChaShader.Body.sNipColorID, human.data.Custom.Body.nipColor = color)
                ),
                ["Underhair"] = human =>
                (
                    () => (human?.data?.Custom?.Body?.underhairId ?? 0) is not 0,
                    () => human.data.Custom.Body.underhairColor,
                    color => human.body.rendBody.material
                        .SetColor(ChaShader.Body.sUnderHairColorID, human.data.Custom.Body.underhairColor = color)
                ),
                ["Sunburn"] = human =>
                (
                    () => (human?.data?.Custom?.Body?.sunburnId ?? 0) is not 0,
                    () => human.data.Custom.Body.sunburnColor,
                    color => SetColor(BodyCtc(human),
                        ChaShader.Body.CreateShader.sSunburnColorID, human.data.Custom.Body.skinMainColor = color)
                ),
                ["BodyPaint1"] = human =>
                (
                    () => (NowCoordinate(human)?.BodyMakeup?.paintInfos?[0]?.ID ?? 0) is not 0,
                    () => NowCoordinate(human).BodyMakeup.paintInfos[0].color,
                    color => SetColor(BodyCtc(human),
                        ChaShader.Body.CreateShader.sPaintColor01, NowCoordinate(human).BodyMakeup.paintInfos[0].color = color)
                ),
                ["BodyPaint2"] = human =>
                (
                    () => (NowCoordinate(human)?.BodyMakeup?.paintInfos?[1]?.ID ?? 0) is not 0,
                    () => NowCoordinate(human).BodyMakeup.paintInfos[1].color,
                    color => SetColor(BodyCtc(human),
                        ChaShader.Body.CreateShader.sPaintColor02, NowCoordinate(human).BodyMakeup.paintInfos[1].color = color)
                ),
                ["BodyPaint3"] = human =>
                (
                    () => (NowCoordinate(human)?.BodyMakeup?.paintInfos?[2]?.ID ?? 0) is not 0,
                    () => NowCoordinate(human).BodyMakeup.paintInfos[2].color,
                    color => SetColor(BodyCtc(human),
                        ChaShader.Body.CreateShader.sPaintColor03, NowCoordinate(human).BodyMakeup.paintInfos[2].color = color)
                ),
                ["HandNail1"] = human =>
                (
                    () => human?.body?.hand?.nailObject?.component?.useColor01 ?? false,
                    () => NowCoordinate(human).BodyMakeup.nailInfo.colors[0],
                    color => human.body.hand.nailObject.material
                        .SetColor(ChaShader.Body.Nail.sMainColor01, NowCoordinate(human).BodyMakeup.nailInfo.colors[0] = color)
                ),
                ["HandNail2"] = human =>
                (
                    () => human?.body?.hand?.nailObject?.component?.useColor02 ?? false,
                    () => NowCoordinate(human).BodyMakeup.nailInfo.colors[1],
                    color => human.body.hand.nailObject.material
                        .SetColor(ChaShader.Body.Nail.sMainColor02, NowCoordinate(human).BodyMakeup.nailInfo.colors[1] = color)
                ),
                ["HandNail3"] = human =>
                (
                    () => human?.body?.hand?.nailObject?.component?.useColor03 ?? false,
                    () => NowCoordinate(human).BodyMakeup.nailInfo.colors[2],
                    color => human.body.hand.nailObject.material
                        .SetColor(ChaShader.Body.Nail.sMainColor03, NowCoordinate(human).BodyMakeup.nailInfo.colors[2] = color)
                ),
                ["LegNail1"] = human =>
                (
                    () => human?.body?.leg?.nailObject?.material?.HasColor(ChaShader.Body.Nail.sMainColor01) ?? false,
                    () => NowCoordinate(human).BodyMakeup.nailLegInfo.colors[0],
                    color => human.body.leg.nailObject.material
                        .SetColor(ChaShader.Body.Nail.sMainColor01, NowCoordinate(human).BodyMakeup.nailInfo.colors[0] = color)
                ),
                ["LegNail2"] = human =>
                (
                    () => human?.body?.leg?.nailObject?.material?.HasColor(ChaShader.Body.Nail.sMainColor02) ?? false,
                    () => NowCoordinate(human).BodyMakeup.nailLegInfo.colors[1],
                    color => human.body.leg.nailObject.material
                        .SetColor(ChaShader.Body.Nail.sMainColor02, NowCoordinate(human).BodyMakeup.nailInfo.colors[1] = color)
                ),
                ["LegNail3"] = human =>
                (
                    () => human?.body?.leg?.nailObject?.material?.HasColor(ChaShader.Body.Nail.sMainColor03) ?? false,
                    () => NowCoordinate(human).BodyMakeup.nailLegInfo.colors[2],
                    color => human.body.leg.nailObject.material
                        .SetColor(ChaShader.Body.Nail.sMainColor03, NowCoordinate(human).BodyMakeup.nailInfo.colors[2] = color)
                ),
            },
            ["Face"] = new()
            {
                ["FaceDetail"] = human =>
                (
                    () => (human?.data?.Custom?.Face?.detailId ?? 0) is not 0,
                    () => human.data.Custom.Face.detailColor,
                    color => human.face.rendFace.material
                        .SetColor(ChaShader.Face.sDetailColorID, human.data.Custom.Face.detailColor = color)
                ),
                ["Mole"] = human =>
                (
                    () => (human?.data?.Custom?.Face?.moleInfo?.ID ?? 0) is not 0,
                    () => human.data.Custom.Face.moleInfo.color,
                    color => SetColor(FaceCtc(human),
                        ChaShader.Face.CreateShader.sMoleColorID, NowCoordinate(human).FaceMakeup.eyeshadowColor = color)
                ),
                ["Eyebrows1"] = human =>
                (
                    () => human?.face?.rendEyebrow?.material?.HasColor(ChaShader.Face.Eyebrow.sColor01) ?? false,
                    () => human.data.Custom.Face.eyebrowColor,
                    color => human.face.rendEyebrow.material
                        .SetColor(ChaShader.Face.Eyebrow.sColor01, human.data.Custom.Face.eyebrowColor = color)
                ),
                ["Eyebrows2"] = human =>
                (
                    () => human?.face?.rendEyebrow?.material?.HasColor(ChaShader.Face.Eyebrow.sColor02) ?? false,
                    () => human.data.Custom.Face.eyebrowColor2,
                    color => human.face.rendEyebrow.material
                        .SetColor(ChaShader.Face.Eyebrow.sColor02, human.data.Custom.Face.eyebrowColor2 = color)
                ),
                ["Eyeline1"] = human =>
                (
                    () => human?.face?.rendEyeline?.material?.HasColor(ChaShader.Face.Eyeline.sColor01) ?? false,
                    () => human.data.Custom.Face.eyelineColor,
                    color => SetColor(
                        ChaShader.Face.Eyeline.sColor01,
                        human.data.Custom.Face.eyelineColor = color,
                        human.face.rendEyeline, human.face.rendEyelineUp, human.face.rendEyelineDown)
                ),
                ["Eyeline2"] = human =>
                (
                    () => human?.face?.rendEyeline?.material?.HasColor(ChaShader.Face.Eyeline.sColor02) ?? false,
                    () => human.data.Custom.Face.eyelineColor2,
                    color => SetColor(
                        ChaShader.Face.Eyeline.sColor02,
                        human.data.Custom.Face.eyelineColor2 = color,
                        human.face.rendEyeline, human.face.rendEyelineUp, human.face.rendEyelineDown)
                ),
                ["Eyeline3"] = human =>
                (
                    () => human?.face?.rendEyeline?.material?.HasColor(ChaShader.Face.Eyeline.sColor03) ?? false,
                    () => human.data.Custom.Face.eyelineColor3,
                    color => SetColor(
                        ChaShader.Face.Eyeline.sColor03,
                        human.data.Custom.Face.eyelineColor3 = color,
                        human.face.rendEyeline, human.face.rendEyelineUp, human.face.rendEyelineDown)
                ),
                ["EyeWhiteMain"] = human =>
                (
                    () => human?.face?.rendEye?.All(rend => rend?.material?.HasColor(ChaShader.Face.Eyes.WhiteColor01) ?? false) ?? false,
                    () => human.data.Custom.Face.whiteBaseColor,
                    color =>
                        SetColor(ChaShader.Face.Eyes.WhiteColor01, human.data.Custom.Face.whiteBaseColor = color, human.face.rendEye)
                ),
                ["EyeWhiteSub"] = human =>
                (
                    () => human?.face?.rendEye?.All(rend => rend?.material?.HasColor(ChaShader.Face.Eyes.WhiteColor02) ?? false) ?? false,
                    () => human.data.Custom.Face.whiteSubColor,
                    color =>
                        SetColor(ChaShader.Face.Eyes.WhiteColor02, human.data.Custom.Face.whiteSubColor = color, human.face.rendEye)
                ),
                ["Eye1"] = human =>
                (
                    () => (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0,
                    () => human.data.Custom.Face.pupil[0].eye01Color,
                    color =>
                        SetColor(ChaShader.Face.Eyes.Color01,
                            human.data.Custom.Face.pupil[0].eye01Color =
                            human.data.Custom.Face.pupil[1].eye01Color = color, human.face.rendEye)
                ),
                ["Eye2"] = human =>
                (
                    () => (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0,
                    () => human.data.Custom.Face.pupil[0].eye02Color,
                    color =>
                        SetColor(ChaShader.Face.Eyes.Color02,
                            human.data.Custom.Face.pupil[0].eye02Color =
                            human.data.Custom.Face.pupil[1].eye02Color = color, human.face.rendEye)
                ),
                ["Eye3"] = human =>
                (
                    () => (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0,
                    () => human.data.Custom.Face.pupil[0].eye03Color,
                    color =>
                        SetColor(ChaShader.Face.Eyes.Color03,
                            human.data.Custom.Face.pupil[0].eye03Color =
                            human.data.Custom.Face.pupil[1].eye03Color = color, human.face.rendEye)
                ),
                ["Pupil1"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].pupil01Color,
                    color =>
                        SetColor(ChaShader.Face.Eyes.PipilColor01,
                            human.data.Custom.Face.pupil[0].pupil01Color =
                            human.data.Custom.Face.pupil[1].pupil01Color = color, human.face.rendEye)
                ),
                ["Pupil2"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].pupil02Color,
                    color =>
                        SetColor(ChaShader.Face.Eyes.PipilColor02,
                            human.data.Custom.Face.pupil[0].pupil02Color =
                            human.data.Custom.Face.pupil[1].pupil02Color = color, human.face.rendEye)
                ),
                ["Pupil3"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].pupil03Color,
                    color =>
                        SetColor(ChaShader.Face.Eyes.PipilColor03,
                            human.data.Custom.Face.pupil[0].pupil03Color =
                            human.data.Custom.Face.pupil[1].pupil03Color = color, human.face.rendEye)
                ),
                ["EyeGradation"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.gradMaskId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].eyeGradColor,
                    color =>
                        SetColor(ChaShader.Face.Eyes.sGradeColorID,
                            human.data.Custom.Face.pupil[0].eyeGradColor =
                            human.data.Custom.Face.pupil[1].eyeGradColor = color, human.face.rendEye)
                ),
                ["EyeHighlight1"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[0]?.id ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].highlightInfos[0].color,
                    color =>
                        SetColor(ChaShader.Face.Eyes.HighlightColor01,
                            human.data.Custom.Face.pupil[0].highlightInfos[0].color =
                            human.data.Custom.Face.pupil[1].highlightInfos[0].color = color, human.face.rendEye)
                ),
                ["EyeHighlight2"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[1]?.id ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].highlightInfos[1].color,
                    color =>
                        SetColor(ChaShader.Face.Eyes.HighlightColor02,
                            human.data.Custom.Face.pupil[0].highlightInfos[1].color =
                            human.data.Custom.Face.pupil[1].highlightInfos[1].color = color, human.face.rendEye)
                ),
                ["EyeHighlight3"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[2]?.id ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].highlightInfos[2].color,
                    color =>
                        SetColor(ChaShader.Face.Eyes.HighlightColor03,
                            human.data.Custom.Face.pupil[0].highlightInfos[2].color =
                            human.data.Custom.Face.pupil[1].highlightInfos[2].color = color, human.face.rendEye)
                ),
                ["LeftEye1"] = human =>
                (
                    () => (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                    () => human.data.Custom.Face.pupil[0].eye01Color,
                    color => human.face.rendEye[0].material
                        .SetColor(ChaShader.Face.Eyes.Color01, human.data.Custom.Face.pupil[0].eye01Color = color)
                ),
                ["LeftEye2"] = human =>
                (
                    () => (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                    () => human.data.Custom.Face.pupil[0].eye02Color,
                    color => human.face.rendEye[0].material
                        .SetColor(ChaShader.Face.Eyes.Color02, human.data.Custom.Face.pupil[0].eye02Color = color)
                ),
                ["LeftEye3"] = human =>
                (
                    () => (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                    () => human.data.Custom.Face.pupil[0].eye03Color,
                    color => human.face.rendEye[0].material
                        .SetColor(ChaShader.Face.Eyes.Color03, human.data.Custom.Face.pupil[0].eye03Color = color)
                ),
                ["LeftPupil1"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].pupil01Color,
                    color => human.face.rendEye[0].material
                        .SetColor(ChaShader.Face.Eyes.PipilColor01, human.data.Custom.Face.pupil[0].pupil01Color = color)
                ),
                ["LeftPupil2"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].pupil02Color,
                    color => human.face.rendEye[0].material
                        .SetColor(ChaShader.Face.Eyes.PipilColor02, human.data.Custom.Face.pupil[0].pupil02Color = color)
                ),
                ["LeftPupil3"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].pupil03Color,
                    color => human.face.rendEye[0].material
                        .SetColor(ChaShader.Face.Eyes.PipilColor03, human.data.Custom.Face.pupil[0].pupil03Color = color)
                ),
                ["LeftEyeGradation"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.gradMaskId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].eyeGradColor,
                    color => human.face.rendEye[0].material
                        .SetColor(ChaShader.Face.Eyes.sGradeColorID, human.data.Custom.Face.pupil[0].eyeGradColor = color)
                ),
                ["LeftEyeHighlight1"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[0]?.id ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].highlightInfos[0].color,
                    color => human.face.rendEye[0].material
                        .SetColor(ChaShader.Face.Eyes.HighlightColor01, human.data.Custom.Face.pupil[0].highlightInfos[0].color = color)
                ),
                ["LeftEyeHighlight2"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[1]?.id ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].highlightInfos[1].color,
                    color => human.face.rendEye[0].material
                        .SetColor(ChaShader.Face.Eyes.HighlightColor02, human.data.Custom.Face.pupil[0].highlightInfos[1].color = color)
                ),
                ["LeftEyeHighlight3"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[2]?.id ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[0].highlightInfos[2].color,
                    color => human.face.rendEye[0].material
                        .SetColor(ChaShader.Face.Eyes.HighlightColor03, human.data.Custom.Face.pupil[0].highlightInfos[2].color = color)
                ),
                ["RightEye1"] = human =>
                (
                    () => (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                    () => human.data.Custom.Face.pupil[1].eye01Color,
                    color => human.face.rendEye[1].material
                        .SetColor(ChaShader.Face.Eyes.Color01, human.data.Custom.Face.pupil[1].eye01Color = color)
                ),
                ["RightEye2"] = human =>
                (
                    () => (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                    () => human.data.Custom.Face.pupil[1].eye02Color,
                    color => human.face.rendEye[1].material
                        .SetColor(ChaShader.Face.Eyes.Color02, human.data.Custom.Face.pupil[1].eye02Color = color)
                ),
                ["RightEye3"] = human =>
                (
                    () => (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                    () => human.data.Custom.Face.pupil[1].eye03Color,
                    color => human.face.rendEye[1].material
                        .SetColor(ChaShader.Face.Eyes.Color03, human.data.Custom.Face.pupil[1].eye03Color = color)
                ),
                ["RightPupil1"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[1]?.overId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[1].pupil01Color,
                    color => human.face.rendEye[1].material
                        .SetColor(ChaShader.Face.Eyes.PipilColor01, human.data.Custom.Face.pupil[1].pupil01Color = color)
                ),
                ["RightPupil2"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[1]?.overId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[1].pupil02Color,
                    color => human.face.rendEye[1].material
                        .SetColor(ChaShader.Face.Eyes.PipilColor02, human.data.Custom.Face.pupil[1].pupil02Color = color)
                ),
                ["RightPupil3"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[1]?.overId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[1].pupil03Color,
                    color => human.face.rendEye[1].material
                        .SetColor(ChaShader.Face.Eyes.PipilColor03, human.data.Custom.Face.pupil[1].pupil03Color = color)
                ),
                ["RightEyeGradation"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[1]?.gradMaskId ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[1].eyeGradColor,
                    color => human.face.rendEye[1].material
                        .SetColor(ChaShader.Face.Eyes.sGradeColorID, human.data.Custom.Face.pupil[1].eyeGradColor = color)
                ),
                ["RightEyeHighlight1"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[1]?.highlightInfos?[0]?.id ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[1].highlightInfos[0].color,
                    color => human.face.rendEye[1].material
                        .SetColor(ChaShader.Face.Eyes.HighlightColor01, human.data.Custom.Face.pupil[1].highlightInfos[0].color = color)
                ),
                ["RightEyeHighlight2"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[1]?.highlightInfos?[1]?.id ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[1].highlightInfos[1].color,
                    color => human.face.rendEye[1].material
                        .SetColor(ChaShader.Face.Eyes.HighlightColor02, human.data.Custom.Face.pupil[1].highlightInfos[1].color = color)
                ),
                ["RightEyeHighlight3"] = human =>
                (
                    () =>
                        (human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                        (human?.data?.Custom?.Face?.pupil?[1]?.highlightInfos?[2]?.id ?? 0) is not 0,
                    () => human.data.Custom.Face.pupil[1].highlightInfos[2].color,
                    color => human.face.rendEye[1].material
                        .SetColor(ChaShader.Face.Eyes.HighlightColor03, human.data.Custom.Face.pupil[1].highlightInfos[2].color = color)
                ),
                ["Cheek"] = human =>
                (
                    () => (NowCoordinate(human)?.FaceMakeup?.cheekId ?? 0) is not 0,
                    () => NowCoordinate(human).FaceMakeup.cheekColor,
                    color => human.face.rendFace.material
                        .SetColor(ChaShader.Face.sCheekColorID, NowCoordinate(human).FaceMakeup.cheekColor = color)
                ),
                ["CheekHighlight"] = human =>
                (
                    () => (NowCoordinate(human)?.FaceMakeup?.cheekId ?? 0) is not 0,
                    () => NowCoordinate(human).FaceMakeup.cheekHighlightColor,
                    color => human.face.rendFace.material
                        .SetColor(ChaShader.Face.sCheekHighlightColorID, NowCoordinate(human).FaceMakeup.cheekHighlightColor = color)
                ),
                ["Eyeshadow"] = human =>
                (
                    () => (NowCoordinate(human)?.FaceMakeup?.eyeshadowId ?? 0) is not 0,
                    () => NowCoordinate(human).FaceMakeup.eyeshadowColor,
                    color => SetColor(FaceCtc(human),
                        ChaShader.Face.CreateShader.sEyeshadowColorID, NowCoordinate(human).FaceMakeup.eyeshadowColor = color)
                ),
                ["Lip"] = human =>
                (
                    () => (NowCoordinate(human)?.FaceMakeup?.lipId ?? 0) is not 0,
                    () => NowCoordinate(human).FaceMakeup.lipColor,
                    color => human.face.rendFace.material
                        .SetColor(ChaShader.Face.sLipColorID, NowCoordinate(human).FaceMakeup.lipColor = color)
                ),
                ["LipHighlight"] = human =>
                (
                    () => (NowCoordinate(human)?.FaceMakeup?.lipId ?? 0) is not 0,
                    () => NowCoordinate(human).FaceMakeup.lipHighlightColor,
                    color => human.face.rendFace.material
                        .SetColor(ChaShader.Face.sLipHighlightColorID, NowCoordinate(human).FaceMakeup.lipHighlightColor = color)
                ),
                ["FacePaint1"] = human =>
                (
                    () => (NowCoordinate(human)?.FaceMakeup?.paintInfos?[0].ID ?? 0) is not 0,
                    () => NowCoordinate(human).FaceMakeup.paintInfos[0].color,
                    color => SetColor(FaceCtc(human),
                        ChaShader.Face.CreateShader.sPaintColor01, NowCoordinate(human).FaceMakeup.paintInfos[0].color = color)
                ),
                ["FacePaint2"] = human =>
                (
                    () => (NowCoordinate(human)?.FaceMakeup?.paintInfos?[1].ID ?? 0) is not 0,
                    () => NowCoordinate(human).FaceMakeup.paintInfos[1].color,
                    color => SetColor(FaceCtc(human),
                        ChaShader.Face.CreateShader.sPaintColor02, NowCoordinate(human).FaceMakeup.paintInfos[1].color = color)
                ),
                ["FacePaint3"] = human =>
                (
                    () => (NowCoordinate(human)?.FaceMakeup?.paintInfos?[2].ID ?? 0) is not 0,
                    () => NowCoordinate(human).FaceMakeup.paintInfos[2].color,
                    color => SetColor(FaceCtc(human),
                        ChaShader.Face.CreateShader.sPaintColor03, NowCoordinate(human).FaceMakeup.paintInfos[2].color = color)
                ),
            },
            ["Hairs"] = F.ToDictionary([
                ..Hairs("Back", 0),
                ..Hairs("Front", 1),
                ..Hairs("Side", 2),
                ..Hairs("Option", 3)
            ]),
            ["Clothes"] = F.ToDictionary([
                .. Clothes("Top", 0),
                .. Clothes("Bottom", 1),
                .. Clothes("Bra", 2),
                .. Clothes("Shorts", 3),
                .. Clothes("Gloves", 4),
                .. Clothes("Panst", 5),
                .. Clothes("Socks", 6),
                .. Clothes("Shoes", 7),
            ]),
            ["Accessories"] = Enumerable.Range(0, 100).SelectMany(index => Accessories(index)).ToDictionary()
        };
        static IEnumerable<(string, Func<Human, ColorProperty>)> Hairs(string name, int part) => [
            ($"{name}Main", human => (
                () => human?.hair?.Hairs?[part]?.renderers?
                    .Any(rend => rend.material.HasColor(ChaShader.Hair.sMainColor)) ?? false,
                () => NowCoordinate(human).Hair.parts[part].baseColor,
                color => SetColor(ChaShader.Hair.sMainColor,
                    NowCoordinate(human).Hair.parts[part].baseColor = color, human.hair.Hairs[part].renderers)
            )),
            ($"{name}Sub1", human => (
                () => human?.hair?.Hairs?[part]?.renderers?
                    .Any(rend => rend.material.HasColor(ChaShader.Hair.sSub01Color)) ?? false,
                () => NowCoordinate(human).Hair.parts[part].startColor,
                color => SetColor(ChaShader.Hair.sSub01Color,
                    NowCoordinate(human).Hair.parts[part].startColor = color, human.hair.Hairs[part].renderers)
            )),
            ($"{name}Sub2", human => (
                () => human?.hair?.Hairs?[part]?.renderers?
                    .Any(rend => rend.material.HasColor(ChaShader.Hair.sSub02Color)) ?? false,
                () => NowCoordinate(human).Hair.parts[part].endColor,
                color => SetColor(ChaShader.Hair.sSub02Color,
                    NowCoordinate(human).Hair.parts[part].endColor = color, human.hair.Hairs[part].renderers)
            )),
            ($"{name}Gloss", human => (
                () => (NowCoordinate(human)?.Hair?.glossId ?? 0) is not 0 &&
                    (human?.hair?.Hairs?[part]?.renderers?
                        .Any(rend => rend.material.HasColor(ChaShader.Hair.sGlossColorID)) ?? false),
                () => NowCoordinate(human).Hair.parts[part].glossColor,
                color => SetColor(ChaShader.Hair.sGlossColorID,
                    NowCoordinate(human).Hair.parts[part].glossColor = color, human.hair.Hairs[part].renderers)
            )),
            ($"{name}Shadow", human => (
                () => human?.hair?.Hairs?[part]?.renderers?
                    .Any(rend => rend.material.HasColor(ChaShader.Hair.sShadowColorID)) ?? false,
                () => NowCoordinate(human).Hair.parts[part].shadowColor,
                color => SetColor(ChaShader.Hair.sShadowColorID,
                    NowCoordinate(human).Hair.parts[part].shadowColor = color, human.hair.Hairs[part].renderers)
            )),
            ($"{name}Outline", human => (
                () => human?.hair?.Hairs?[part]?.renderers?
                    .Any(rend => rend.material.HasColor(ChaShader.Hair.sLineColorID)) ?? false,
                () => NowCoordinate(human).Hair.parts[part].outlineColor,
                color => SetColor(ChaShader.Hair.sLineColorID,
                    NowCoordinate(human).Hair.parts[part].outlineColor = color, human.hair.Hairs[part].renderers)
            )),
            ($"{name}Mesh", human => (
                () => (NowCoordinate(human)?.Hair?.parts?[part]?.useMesh ?? false) &&
                    (human?.hair?.Hairs?[part]?.renderers?
                        .Any(rend => rend.material.HasColor(ChaShader.Hair.sMeshColorID)) ?? false),
                () => NowCoordinate(human).Hair.parts[part].meshColor,
                color => SetColor(ChaShader.Hair.sMeshColorID,
                    NowCoordinate(human).Hair.parts[part].meshColor = color, human.hair.Hairs[part].renderers)
            )),
            ($"{name}Inner", human => (
                () => (NowCoordinate(human)?.Hair?.parts?[part]?.useInner ?? false) &&
                    (human?.hair?.Hairs?[part]?.renderers?
                        .Any(rend => rend.material.HasColor(ChaShader.Hair.sInnerColorID)) ?? false),
                () => NowCoordinate(human).Hair.parts[part].innerColor,
                color => SetColor(ChaShader.Hair.sInnerColorID,
                    NowCoordinate(human).Hair.parts[part].innerColor = color, human.hair.Hairs[part].renderers)
            )),
            ($"{name}Acs1", human => (
                () => 0 < (human?.hair?.GetHairAcsColorNum(part) ?? 0),
                () => NowCoordinate(human).Hair.parts[part].acsColor[0],
                color => SetColor(ChaShader.Hair.sAcsColor01,
                    NowCoordinate(human).Hair.parts[part].acsColor[0] = color, human.hair.Hairs[part].renderers)
            )),
            ($"{name}Acs2", human => (
                () => 1 < (human?.hair?.GetHairAcsColorNum(part) ?? 0),
                () => NowCoordinate(human)?.Hair?.parts?[part]?.acsColor[1] ?? human.hair.Hairs[part].cusHairCmp.acsDefColor[1],
                color => SetColor(ChaShader.Hair.sAcsColor02,
                    NowCoordinate(human).Hair.parts[part].acsColor[1] = color, human.hair.Hairs[part].renderers)
            )),
            ($"{name}Acs3", human => (
                () => 2 < (human?.hair?.GetHairAcsColorNum(part) ?? 0),
                () => NowCoordinate(human).Hair.parts[part].acsColor[2],
                color => SetColor(ChaShader.Hair.sAcsColor03,
                    NowCoordinate(human).Hair.parts[part].acsColor[2] = color, human.hair.Hairs[part].renderers)
            ))
        ];
        static IEnumerable<(string, Func<Human, ColorProperty>)> Clothes(string name, int part) => [
            ($"{name}1", human => (
                () => human?.cloth?.Clothess?[part]?.cusClothesCmp?.useColorN01 ?? false,
                () => NowCoordinate(human).Clothes.parts[part].colorInfo[0].baseColor,
                color => ClothesCtc(human, part).Invoke(ChaShader.Clothes.CreateShader.sMainColor01,
                    NowCoordinate(human).Clothes.parts[part].colorInfo[0].baseColor = color)
            )),
            ($"{name}2", human => (
                () => human?.cloth?.Clothess?[part]?.cusClothesCmp?.useColorN02 ?? false,
                () => NowCoordinate(human).Clothes.parts[part].colorInfo[1].baseColor,
                color => ClothesCtc(human, part).Invoke(ChaShader.Clothes.CreateShader.sMainColor02,
                    NowCoordinate(human).Clothes.parts[part].colorInfo[1].baseColor = color)
            )),
            ($"{name}3", human => (
                () => human?.cloth?.Clothess?[part]?.cusClothesCmp?.useColorN03 ?? false,
                () => NowCoordinate(human).Clothes.parts[part].colorInfo[2].baseColor,
                color => ClothesCtc(human, part).Invoke(ChaShader.Clothes.CreateShader.sMainColor03,
                    NowCoordinate(human).Clothes.parts[part].colorInfo[2].baseColor = color)
            )),
            ($"{name}4", human => (
                () => human?.cloth?.Clothess?[part]?.cusClothesCmp?.useColorN04 ?? false,
                () => NowCoordinate(human).Clothes.parts[part].colorInfo[3].baseColor,
                color => ClothesCtc(human, part).Invoke(ChaShader.Clothes.CreateShader.sMainColor04,
                    NowCoordinate(human).Clothes.parts[part].colorInfo[3].baseColor = color)
            )),
            ($"{name}Pattern1", human => (
                () =>
                    (human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPattern(0) ?? false) &&
                    ((NowCoordinate(human)?.Clothes?.parts?[part]?.colorInfo?[0]?.patternInfo?.pattern ?? 0) is not 0),
                () => NowCoordinate(human).Clothes.parts[part].colorInfo[0].patternInfo.patternColor,
                color => ClothesCtc(human, part).Invoke(ChaShader.Clothes.CreateShader.sSubColor01,
                    NowCoordinate(human).Clothes.parts[part].colorInfo[0].patternInfo.patternColor = color)
            )),
            ($"{name}Pattern2", human => (
                () =>
                    (human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPattern(1) ?? false) &&
                    ((NowCoordinate(human)?.Clothes?.parts?[part]?.colorInfo?[1]?.patternInfo?.pattern ?? 0) is not 0),
                () => NowCoordinate(human).Clothes.parts[part].colorInfo[1].patternInfo.patternColor,
                color => ClothesCtc(human, part).Invoke(ChaShader.Clothes.CreateShader.sSubColor02,
                    NowCoordinate(human).Clothes.parts[part].colorInfo[1].patternInfo.patternColor = color)
            )),
            ($"{name}Pattern3", human => (
                () =>
                    (human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPattern(2) ?? false) &&
                    ((NowCoordinate(human)?.Clothes?.parts?[part]?.colorInfo?[2]?.patternInfo?.pattern ?? 0) is not 0),
                () => NowCoordinate(human).Clothes.parts[part].colorInfo[2].patternInfo.patternColor,
                color => ClothesCtc(human, part).Invoke(ChaShader.Clothes.CreateShader.sSubColor03,
                    NowCoordinate(human).Clothes.parts[part].colorInfo[2].patternInfo.patternColor = color)
            )),
            ($"{name}Pattern4", human => (
                () =>
                    (human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPattern(3) ?? false) &&
                    ((NowCoordinate(human)?.Clothes?.parts?[part]?.colorInfo?[3]?.patternInfo?.pattern ?? 0) is not 0),
                () => NowCoordinate(human).Clothes.parts[part].colorInfo[3].patternInfo.patternColor,
                color => ClothesCtc(human, part).Invoke(ChaShader.Clothes.CreateShader.sSubColor04,
                    NowCoordinate(human).Clothes.parts[part].colorInfo[3].patternInfo.patternColor = color)
            )),
            ($"{name}Paint1", human => (
                () =>
                    (human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPaint(0) ?? false) &&
                    ((NowCoordinate(human)?.Clothes?.parts?[part]?.paintInfos?[0]?.ID ?? 0) is not 0),
                () => NowCoordinate(human).Clothes.parts[part].paintInfos[0].color,
                color => ClothesCtc(human, part).Invoke(ChaShader.Clothes.CreateShader.sPaintColor01,
                    NowCoordinate(human).Clothes.parts[part].paintInfos[0].color = color)
            )),
            ($"{name}Paint2", human => (
                () =>
                    (human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPaint(1) ?? false) &&
                    ((NowCoordinate(human)?.Clothes?.parts?[part]?.paintInfos?[1]?.ID ?? 0) is not 0),
                () => NowCoordinate(human).Clothes.parts[part].paintInfos[1].color,
                color => ClothesCtc(human, part).Invoke(ChaShader.Clothes.CreateShader.sPaintColor02,
                    NowCoordinate(human).Clothes.parts[part].paintInfos[1].color = color)
            )),
            ($"{name}Paint3", human => (
                () =>
                    (human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPaint(2) ?? false) &&
                    ((NowCoordinate(human)?.Clothes?.parts?[part]?.paintInfos?[2]?.ID ?? 0) is not 0),
                () => NowCoordinate(human).Clothes.parts[part].paintInfos[2].color,
                color => ClothesCtc(human, part).Invoke(ChaShader.Clothes.CreateShader.sPaintColor03,
                    NowCoordinate(human).Clothes.parts[part].paintInfos[2].color = color)
            ))
        ];

        static HumanAccessory.Accessory Acs(Human human, int slot) =>
            (slot < (human?.acs?.Accessories?.Count ?? 0)) ? human.acs.Accessories[slot] : null;

        static IEnumerable<(string, Func<Human, ColorProperty>)> Accessories(int part) => [
            ($"Acs{part+1:00}Main1", human => (
                () => Acs(human, part)?.cusAcsCmp?.useColor01 ?? false,
                () => NowCoordinate(human).Accessory.parts[part].color[0],
                color => SetColor(ChaShader.Accessory.sMainColor01,
                    NowCoordinate(human).Accessory.parts[part].color[0] = color, human.acs.Accessories[part].renderers)
            )),
            ($"Acs{part+1:00}Main2", human => (
                () => Acs(human, part)?.cusAcsCmp?.useColor02 ?? false,
                () => NowCoordinate(human).Accessory.parts[part].color[1],
                color => SetColor(ChaShader.Accessory.sMainColor02,
                    NowCoordinate(human).Accessory.parts[part].color[1] = color, human.acs.Accessories[part].renderers)
            )),
            ($"Acs{part+1:00}Main3", human => (
                () => Acs(human, part)?.cusAcsCmp?.useColor03 ?? false,
                () => NowCoordinate(human).Accessory.parts[part].color[2],
                color => SetColor(ChaShader.Accessory.sMainColor03,
                    NowCoordinate(human).Accessory.parts[part].color[2] = color, human.acs.Accessories[part].renderers)
            )),
            ($"Acs{part+1:00}Main4", human => (
                () => Acs(human, part)?.cusAcsCmp?.rendAlpha?.Length > 0,
                () => NowCoordinate(human).Accessory.parts[part].color[3],
                color => SetColor(ChaShader.Accessory.sMainColor04,
                    NowCoordinate(human).Accessory.parts[part].color[3] = color, human.acs.Accessories[part].renderers)
            )),
            ($"Acs{part+1:00}Pattern1", human => (
                () => (Acs(human, part)?.cusAcsCmp?.HasPattern(0) ?? false) &&
                    ((NowCoordinate(human)?.Accessory?.parts?[part]?.colorInfo?[0]?.pattern ?? 0) is not 0),
                () => NowCoordinate(human).Accessory.parts[part].colorInfo[0].patternColor,
                color => SetColor(ChaShader.Accessory.sSubColor01,
                    NowCoordinate(human).Accessory.parts[part].colorInfo[0].patternColor = color, human.acs.Accessories[part].renderers)
            )),
            ($"Acs{part+1:00}Pattern2", human => (
                () => (Acs(human, part)?.cusAcsCmp?.HasPattern(1) ?? false) &&
                    ((NowCoordinate(human)?.Accessory?.parts?[part]?.colorInfo?[1]?.pattern ?? 0) is not 0),
                () => NowCoordinate(human).Accessory.parts[part].colorInfo[1].patternColor,
                color => SetColor(ChaShader.Accessory.sSubColor02,
                    NowCoordinate(human).Accessory.parts[part].colorInfo[1].patternColor = color, human.acs.Accessories[part].renderers)
            )),
            ($"Acs{part+1:00}Pattern3", human => (
                () => (Acs(human, part)?.cusAcsCmp?.HasPattern(2) ?? false) &&
                    ((NowCoordinate(human)?.Accessory?.parts?[part]?.colorInfo?[2]?.pattern ?? 0) is not 0),
                () => NowCoordinate(human).Accessory.parts[part].colorInfo[2].patternColor,
                color => SetColor(ChaShader.Accessory.sSubColor03,
                    NowCoordinate(human).Accessory.parts[part].colorInfo[2].patternColor = color, human.acs.Accessories[part].renderers)
            ))
        ];
    }
}
 