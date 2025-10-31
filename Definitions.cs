using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Character;
using CharacterCreation;
using CoastalSmell;

namespace DorsalFin
{
    internal partial class PaletteView
    {
        static Texture2D GenerateColors =>
            new Texture2D(128, 128 * 128)
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
        static Sprite GenerateReticle =>
            Sprite.Create(new Texture2D(21, 21).With(t2d => t2d.SetPixels(
                Enumerable.Range(-10, 21).SelectMany(x =>
                Enumerable.Range(-10, 21).Select(y =>
                (Math.Abs(x), Math.Abs(y)) switch
                {
                    (1, 4) or (2, 3) or (3, 2) or (4, 1) or (0, > 5) or ( > 5, 0) => new Color(0.1f, 0.1f, 0.1f, 1f),
                    (2, 4) or (3, 3) or (4, 2) or (1, > 5) or ( > 5, 1) => new Color(0.9f, 0.9f, 0.9f, 1f),
                    (_, _) => new Color(0f, 0f, 0f, 0f)
                })).ToArray())).With(t2d => t2d.Apply(false)), new(0, 0, 21, 21), new(0.5f, 0.5f));
        static Sprite GenerateCross =>
            Sprite.Create(new Texture2D(11, 11).With(t2d => t2d.SetPixels(
                Enumerable.Range(-5, 11).SelectMany(x =>
                Enumerable.Range(-5, 11).Select(y =>
                (Math.Abs(x), Math.Abs(y)) switch
                {
                    (1, 2) or (2, 1) or (0, > 3) or ( > 3, 0) => new Color(0.1f, 0.1f, 0.1f, 1f),
                    (2, 2) or (1, > 3) or ( > 3, 1) => new Color(0.9f, 0.9f, 0.9f, 1f),
                    (_, _) => new Color(0f, 0f, 0f, 0f)
                })).ToArray())).With(t2d => t2d.Apply(false)), new(0, 0, 11, 11), new(0.5f, 0.5f));
    }
    internal enum Options
    {
        None = 0,
        Body = 1,
        Face = 2,
        Hairs = 4,
        Clothes = 8,
        Acs = 16,
        Assigned = 32,
        NotAvailable = 64,
    }
    internal partial class ColorProperty
    {
        static Human Human => HumanCustom.Instance.Human;
        static HumanDataCoordinate Coordinate =>
            Human.data.Coordinates[Human.data.Status.coordinateType];
        static CustomTextureControl BodyCtc =>
#if Aicomi
            Human?.body?._customTexCtrlBody;
#else
            Human?.body?.customTexCtrlBody;
#endif
        static CustomTextureControl FaceCtc =>
#if Aicomi
            Human?.face?._customTexCtrlFace;
#else
            Human?.face?.customTexCtrlFace;
#endif
        static Action<int, Color> ClothesCtc(int part) =>
            ClothesCtc(Human.cloth.Clothess[part]);
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
        static Color Merge(Color target, Color palette) => target with { r = palette.r, g = palette.g, b = palette.b };
        internal static IEnumerable<ColorProperty> All = [
            new ColorProperty(Options.Body, "SkinMain")
            {
                Available = () => HasColor(BodyCtc, ChaShader.Body.CreateShader.sMainColorID),
                Get = () => Human.data.Custom.Body.skinMainColor,
                Set = (target, color) => SetColor(
                    ChaShader.Body.CreateShader.sMainColorID, Human.data.Custom.Body.skinMainColor = Merge(target, color), BodyCtc, FaceCtc)
            },
            new ColorProperty(Options.Body, "SkinDetail1")
            {
                Available = () => Human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sDetailColor01) ?? false,
                Get = () => Human.data.Custom.Body.skinDetailColors[0],
                Set = (target, color) => Human.body.rendBody.material
                    .SetColor(ChaShader.Body.sDetailColor01, Human.data.Custom.Body.skinDetailColors[0] = Merge(target, color))
            },
            new ColorProperty(Options.Body, "SkinDetail2")
            {
                Available = () => Human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sDetailColor02) ?? false,
                Get = () => Human.data.Custom.Body.skinDetailColors[1],
                Set = (target, color) => Human.body.rendBody.material
                    .SetColor(ChaShader.Body.sDetailColor02, Human.data.Custom.Body.skinDetailColors[1] = Merge(target, color))
            },
            new ColorProperty(Options.Body, "SkinDetail3")
            {
                Available = () => Human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sDetailColor03) ?? false,
                Get = () => Human.data.Custom.Body.skinDetailColors[2],
                Set = (target, color) => Human.body.rendBody.material
                    .SetColor(ChaShader.Body.sDetailColor03, Human.data.Custom.Body.skinDetailColors[2] = Merge(target, color))
            },
            new ColorProperty(Options.Body, "SkinHighlight")
            {
                Available = () => Human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sHighlightColorID) ?? false,
                Get = () => Human.data.Custom.Body.skinHighlightColor,
                Set = (target, color) => Human.body.rendBody.material
                    .SetColor(ChaShader.Body.sHighlightColorID, Human.data.Custom.Body.skinHighlightColor = Merge(target, color))
            },
            new ColorProperty(Options.Body, "SkinShadow")
            {
                Available = () => Human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sShadowColorID) ?? false,
                Get = () => Human.data.Custom.Body.skinShadowColor,
                Set = (target, color) => Human.body.rendBody.material
                    .SetColor(ChaShader.Body.sShadowColorID, Human.data.Custom.Body.skinShadowColor = Merge(target, color))
            },
            new ColorProperty(Options.Body, "Nip")
            {
                Available = () => Human?.body?.rendBody?.material?.HasColor(ChaShader.Body.sNipColorID) ?? false,
                Get = () => Human.data.Custom.Body.nipColor,
                Set = (target, color) => Human.body.rendBody.material
                    .SetColor(ChaShader.Body.sNipColorID, Human.data.Custom.Body.nipColor = Merge(target, color))
            },
            new ColorProperty(Options.Body, "Underhair")
            {
                Available = () => (Human?.data?.Custom?.Body?.underhairId ?? 0) is not 0,
                Get = () => Human.data.Custom.Body.underhairColor,
                Set = (target, color) => Human.body.rendBody.material
                    .SetColor(ChaShader.Body.sUnderHairColorID, Human.data.Custom.Body.underhairColor = Merge(target, color))
            },
            new ColorProperty(Options.Body, "Sunburn")
            {
                Available = () => (Human?.data?.Custom?.Body?.sunburnId ?? 0) is not 0,
                Get = () => Human.data.Custom.Body.sunburnColor,
                Set = (target, color) => SetColor(BodyCtc,
                    ChaShader.Body.CreateShader.sSunburnColorID, Human.data.Custom.Body.skinMainColor = Merge(target, color))
            },
            new ColorProperty(Options.Body, "BodyPaint1")
            {
                Available = () => (Coordinate?.BodyMakeup?.paintInfos?[0]?.ID ?? 0) is not 0,
                Get = () => Coordinate.BodyMakeup.paintInfos[0].color,
                Set = (target, color) => SetColor(BodyCtc,
                    ChaShader.Body.CreateShader.sPaintColor01, Coordinate.BodyMakeup.paintInfos[0].color = Merge(target, color))
            },
            new ColorProperty(Options.Body, "BodyPaint2")
            {
                Available = () => (Coordinate?.BodyMakeup?.paintInfos?[1]?.ID ?? 0) is not 0,
                Get = () => Coordinate.BodyMakeup.paintInfos[1].color,
                Set = (target, color) => SetColor(BodyCtc,
                    ChaShader.Body.CreateShader.sPaintColor02, Coordinate.BodyMakeup.paintInfos[1].color = Merge(target, color))
            },
            new ColorProperty(Options.Body, "BodyPaint3")
            {
                Available = () => (Coordinate?.BodyMakeup?.paintInfos?[2]?.ID ?? 0) is not 0,
                Get = () => Coordinate.BodyMakeup.paintInfos[2].color,
                Set = (target, color) => SetColor(BodyCtc,
                    ChaShader.Body.CreateShader.sPaintColor03, Coordinate.BodyMakeup.paintInfos[2].color = Merge(target, color))
            },
            new ColorProperty(Options.Body, "HandNail1")
            {
                Available = () => Human?.body?.hand?.nailObject?.component?.useColor01 ?? false,
                Get = () => Coordinate.BodyMakeup.nailInfo.colors[0],
                Set = (target, color) => Human.body.hand.nailObject.material
                    .SetColor(ChaShader.Body.Nail.sMainColor01, Coordinate.BodyMakeup.nailInfo.colors[0] = Merge(target, color))
            },
            new ColorProperty(Options.Body, "HandNail2")
            {
                Available = () => Human?.body?.hand?.nailObject?.component?.useColor02 ?? false,
                Get = () => Coordinate.BodyMakeup.nailInfo.colors[1],
                Set = (target, color) => Human.body.hand.nailObject.material
                    .SetColor(ChaShader.Body.Nail.sMainColor02, Coordinate.BodyMakeup.nailInfo.colors[1] = Merge(target, color))
            },
            new ColorProperty(Options.Body, "HandNail3")
            {
                Available = () => Human?.body?.hand?.nailObject?.component?.useColor03 ?? false,
                Get = () => Coordinate.BodyMakeup.nailInfo.colors[2],
                Set = (target, color) => Human.body.hand.nailObject.material
                    .SetColor(ChaShader.Body.Nail.sMainColor03, Coordinate.BodyMakeup.nailInfo.colors[2] = Merge(target, color))
            },
            new ColorProperty(Options.Body, "LegNail1")
            {
                Available = () => Human?.body?.leg?.nailObject?.material?.HasColor(ChaShader.Body.Nail.sMainColor01) ?? false,
                Get = () => Coordinate.BodyMakeup.nailLegInfo.colors[0],
                Set = (target, color) => Human.body.leg.nailObject.material
                    .SetColor(ChaShader.Body.Nail.sMainColor01, Coordinate.BodyMakeup.nailInfo.colors[0] = Merge(target, color))
            },
            new ColorProperty(Options.Body, "LegNail2")
            {
                Available = () => Human?.body?.leg?.nailObject?.material?.HasColor(ChaShader.Body.Nail.sMainColor02) ?? false,
                Get = () => Coordinate.BodyMakeup.nailLegInfo.colors[1],
                Set = (target, color) => Human.body.leg.nailObject.material
                    .SetColor(ChaShader.Body.Nail.sMainColor02, Coordinate.BodyMakeup.nailInfo.colors[1] = Merge(target, color))
            },
            new ColorProperty(Options.Body, "LegNail3")
            {
                Available = () => Human?.body?.leg?.nailObject?.material?.HasColor(ChaShader.Body.Nail.sMainColor03) ?? false,
                Get = () => Coordinate.BodyMakeup.nailLegInfo.colors[2],
                Set = (target, color) => Human.body.leg.nailObject.material
                    .SetColor(ChaShader.Body.Nail.sMainColor03, Coordinate.BodyMakeup.nailInfo.colors[2] = Merge(target, color))
            },
            new ColorProperty(Options.Face, "FaceDetail")
            {
                Available = () => (Human?.data?.Custom?.Face?.detailId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.detailColor,
                Set = (target, color) => Human.face.rendFace.material
                    .SetColor(ChaShader.Face.sDetailColorID, Human.data.Custom.Face.detailColor = Merge(target, color))
            },
            new ColorProperty(Options.Face, "Mole")
            {
                Available = () => (Human?.data?.Custom?.Face?.moleInfo?.ID ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.moleInfo.color,
                Set = (target, color) => SetColor(FaceCtc,
                    ChaShader.Face.CreateShader.sMoleColorID, Coordinate.FaceMakeup.eyeshadowColor = Merge(target, color))
            },
            new ColorProperty(Options.Face, "Eyebrows1")
            {
                Available = () => Human?.face?.rendEyebrow?.material?.HasColor(ChaShader.Face.Eyebrow.sColor01) ?? false,
                Get = () => Human.data.Custom.Face.eyebrowColor,
                Set = (target, color) => Human.face.rendEyebrow.material
                    .SetColor(ChaShader.Face.Eyebrow.sColor01, Human.data.Custom.Face.eyebrowColor = Merge(target, color))
            },
            new ColorProperty(Options.Face, "Eyebrows2")
            {
                Available = () => Human?.face?.rendEyebrow?.material?.HasColor(ChaShader.Face.Eyebrow.sColor02) ?? false,
                Get = () => Human.data.Custom.Face.eyebrowColor2,
                Set = (target, color) => Human.face.rendEyebrow.material
                    .SetColor(ChaShader.Face.Eyebrow.sColor02, Human.data.Custom.Face.eyebrowColor2 = Merge(target, color))
            },
            new ColorProperty(Options.Face, "Eyeline1")
            {
                Available = () => Human?.face?.rendEyeline?.material?.HasColor(ChaShader.Face.Eyeline.sColor01) ?? false,
                Get = () => Human.data.Custom.Face.eyelineColor,
                Set = (target, color) => SetColor(
                    ChaShader.Face.Eyeline.sColor01,
                    Human.data.Custom.Face.eyelineColor = Merge(target, color),
                    Human.face.rendEyeline, Human.face.rendEyelineUp, Human.face.rendEyelineDown)
            },
            new ColorProperty(Options.Face, "Eyeline2")
            {
                Available = () => Human?.face?.rendEyeline?.material?.HasColor(ChaShader.Face.Eyeline.sColor02) ?? false,
                Get = () => Human.data.Custom.Face.eyelineColor2,
                Set = (target, color) => SetColor(
                    ChaShader.Face.Eyeline.sColor02,
                    Human.data.Custom.Face.eyelineColor2 = Merge(target, color),
                    Human.face.rendEyeline, Human.face.rendEyelineUp, Human.face.rendEyelineDown)
            },
            new ColorProperty(Options.Face, "Eyeline3")
            {
                Available = () => Human?.face?.rendEyeline?.material?.HasColor(ChaShader.Face.Eyeline.sColor03) ?? false,
                Get = () => Human.data.Custom.Face.eyelineColor3,
                Set = (target, color) => SetColor(
                    ChaShader.Face.Eyeline.sColor03,
                    Human.data.Custom.Face.eyelineColor3 = Merge(target, color),
                    Human.face.rendEyeline, Human.face.rendEyelineUp, Human.face.rendEyelineDown)
            },
            new ColorProperty(Options.Face, "EyeWhiteMain")
            {
                Available = () => Human?.face?.rendEye?.All(rend => rend?.material?.HasColor(ChaShader.Face.Eyes.WhiteColor01) ?? false) ?? false,
                Get = () => Human.data.Custom.Face.whiteBaseColor,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.WhiteColor01, Human.data.Custom.Face.whiteBaseColor = Merge(target, color), Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "EyeWhiteSub")
            {
                Available = () => Human?.face?.rendEye?.All(rend => rend?.material?.HasColor(ChaShader.Face.Eyes.WhiteColor02) ?? false) ?? false,
                Get = () => Human.data.Custom.Face.whiteSubColor,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.WhiteColor02, Human.data.Custom.Face.whiteSubColor = Merge(target, color),  Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "Eye1")
            {
                Available = () => (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0,
                Get = () => Human.data.Custom.Face.pupil[0].eye01Color,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.Color01,
                        Human.data.Custom.Face.pupil[0].eye01Color =
                        Human.data.Custom.Face.pupil[1].eye01Color = Merge(target, color), Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "Eye2")
            {
                Available = () => (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0,
                Get = () => Human.data.Custom.Face.pupil[0].eye02Color,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.Color02,
                        Human.data.Custom.Face.pupil[0].eye02Color =
                        Human.data.Custom.Face.pupil[1].eye02Color = Merge(target, color), Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "Eye3")
            {
                Available = () => (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0,
                Get = () => Human.data.Custom.Face.pupil[0].eye03Color,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.Color03,
                        Human.data.Custom.Face.pupil[0].eye03Color =
                        Human.data.Custom.Face.pupil[1].eye03Color = Merge(target, color), Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "Pupil1")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].pupil01Color,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.PipilColor01,
                        Human.data.Custom.Face.pupil[0].pupil01Color =
                        Human.data.Custom.Face.pupil[1].pupil01Color = Merge(target, color), Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "Pupil2")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].pupil02Color,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.PipilColor02,
                        Human.data.Custom.Face.pupil[0].pupil02Color =
                        Human.data.Custom.Face.pupil[1].pupil02Color = Merge(target, color), Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "Pupil3")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].pupil03Color,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.PipilColor03,
                        Human.data.Custom.Face.pupil[0].pupil03Color =
                        Human.data.Custom.Face.pupil[1].pupil03Color = Merge(target, color), Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "EyeGradation")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.gradMaskId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].eyeGradColor,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.sGradeColorID,
                        Human.data.Custom.Face.pupil[0].eyeGradColor =
                        Human.data.Custom.Face.pupil[1].eyeGradColor = Merge(target, color), Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "EyeHighlight1")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[0]?.id ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].highlightInfos[0].color,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.HighlightColor01,
                        Human.data.Custom.Face.pupil[0].highlightInfos[0].color =
                        Human.data.Custom.Face.pupil[1].highlightInfos[0].color = Merge(target, color), Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "EyeHighlight2")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[1]?.id ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].highlightInfos[1].color,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.HighlightColor02,
                        Human.data.Custom.Face.pupil[0].highlightInfos[1].color =
                        Human.data.Custom.Face.pupil[1].highlightInfos[1].color = Merge(target, color), Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "EyeHighlight3")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 0 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[2]?.id ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].highlightInfos[2].color,
                Set = (target, color) =>
                    SetColor(ChaShader.Face.Eyes.HighlightColor03,
                        Human.data.Custom.Face.pupil[0].highlightInfos[2].color =
                        Human.data.Custom.Face.pupil[1].highlightInfos[2].color = Merge(target, color), Human.face.rendEye)
            },
            new ColorProperty(Options.Face, "LeftEye1")
            {
                Available = () => (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                Get = () => Human.data.Custom.Face.pupil[0].eye01Color,
                Set = (target, color) => Human.face.rendEye[0].material
                    .SetColor(ChaShader.Face.Eyes.Color01, Human.data.Custom.Face.pupil[0].eye01Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "LeftEye2")
            {
                Available = () => (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                Get = () => Human.data.Custom.Face.pupil[0].eye02Color,
                Set = (target, color) => Human.face.rendEye[0].material
                    .SetColor(ChaShader.Face.Eyes.Color02, Human.data.Custom.Face.pupil[0].eye02Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "LeftEye3")
            {
                Available = () => (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                Get = () => Human.data.Custom.Face.pupil[0].eye03Color,
                Set = (target, color) => Human.face.rendEye[0].material
                    .SetColor(ChaShader.Face.Eyes.Color03, Human.data.Custom.Face.pupil[0].eye03Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "LeftPupil1")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].pupil01Color,
                Set = (target, color) => Human.face.rendEye[0].material
                    .SetColor(ChaShader.Face.Eyes.PipilColor01, Human.data.Custom.Face.pupil[0].pupil01Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "LeftPupil2")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].pupil02Color,
                Set = (target, color) => Human.face.rendEye[0].material
                    .SetColor(ChaShader.Face.Eyes.PipilColor02, Human.data.Custom.Face.pupil[0].pupil02Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "LeftPupil3")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.overId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].pupil03Color,
                Set = (target, color) => Human.face.rendEye[0].material
                    .SetColor(ChaShader.Face.Eyes.PipilColor03, Human.data.Custom.Face.pupil[0].pupil03Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "LeftEyeGradation")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.gradMaskId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].eyeGradColor,
                Set = (target, color) => Human.face.rendEye[0].material
                    .SetColor(ChaShader.Face.Eyes.sGradeColorID, Human.data.Custom.Face.pupil[0].eyeGradColor = Merge(target, color))
            },
            new ColorProperty(Options.Face, "LeftEyeHighlight1")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[0]?.id ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].highlightInfos[0].color,
                Set = (target, color) => Human.face.rendEye[0].material
                    .SetColor(ChaShader.Face.Eyes.HighlightColor01, Human.data.Custom.Face.pupil[0].highlightInfos[0].color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "LeftEyeHighlight2")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[1]?.id ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].highlightInfos[1].color,
                Set = (target, color) => Human.face.rendEye[0].material
                    .SetColor(ChaShader.Face.Eyes.HighlightColor02, Human.data.Custom.Face.pupil[0].highlightInfos[1].color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "LeftEyeHighlight3")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[0]?.highlightInfos?[2]?.id ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[0].highlightInfos[2].color,
                Set = (target, color) => Human.face.rendEye[0].material
                    .SetColor(ChaShader.Face.Eyes.HighlightColor03, Human.data.Custom.Face.pupil[0].highlightInfos[2].color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "RightEye1")
            {
                Available = () => (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                Get = () => Human.data.Custom.Face.pupil[1].eye01Color,
                Set = (target, color) => Human.face.rendEye[1].material
                    .SetColor(ChaShader.Face.Eyes.Color01, Human.data.Custom.Face.pupil[1].eye01Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "RightEye2")
            {
                Available = () => (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                Get = () => Human.data.Custom.Face.pupil[1].eye02Color,
                Set = (target, color) => Human.face.rendEye[1].material
                    .SetColor(ChaShader.Face.Eyes.Color02, Human.data.Custom.Face.pupil[1].eye02Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "RightEye3")
            {
                Available = () => (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1,
                Get = () => Human.data.Custom.Face.pupil[1].eye03Color,
                Set = (target, color) => Human.face.rendEye[1].material
                    .SetColor(ChaShader.Face.Eyes.Color03, Human.data.Custom.Face.pupil[1].eye03Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "RightPupil1")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[1]?.overId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[1].pupil01Color,
                Set = (target, color) => Human.face.rendEye[1].material
                    .SetColor(ChaShader.Face.Eyes.PipilColor01, Human.data.Custom.Face.pupil[1].pupil01Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "RightPupil2")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[1]?.overId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[1].pupil02Color,
                Set = (target, color) => Human.face.rendEye[1].material
                    .SetColor(ChaShader.Face.Eyes.PipilColor02, Human.data.Custom.Face.pupil[1].pupil02Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "RightPupil3")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[1]?.overId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[1].pupil03Color,
                Set = (target, color) => Human.face.rendEye[1].material
                    .SetColor(ChaShader.Face.Eyes.PipilColor03, Human.data.Custom.Face.pupil[1].pupil03Color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "RightEyeGradation")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[1]?.gradMaskId ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[1].eyeGradColor,
                Set = (target, color) => Human.face.rendEye[1].material
                    .SetColor(ChaShader.Face.Eyes.sGradeColorID, Human.data.Custom.Face.pupil[1].eyeGradColor = Merge(target, color))
            },
            new ColorProperty(Options.Face, "RightEyeHighlight1")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[1]?.highlightInfos?[0]?.id ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[1].highlightInfos[0].color,
                Set = (target, color) => Human.face.rendEye[1].material
                    .SetColor(ChaShader.Face.Eyes.HighlightColor01, Human.data.Custom.Face.pupil[1].highlightInfos[0].color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "RightEyeHighlight2")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[1]?.highlightInfos?[1]?.id ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[1].highlightInfos[1].color,
                Set = (target, color) => Human.face.rendEye[1].material
                    .SetColor(ChaShader.Face.Eyes.HighlightColor02, Human.data.Custom.Face.pupil[1].highlightInfos[1].color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "RightEyeHighlight3")
            {
                Available = () =>
                    (Human?.data?.Custom?.Face?.pupilEditType ?? -1) is 1 &&
                    (Human?.data?.Custom?.Face?.pupil?[1]?.highlightInfos?[2]?.id ?? 0) is not 0,
                Get = () => Human.data.Custom.Face.pupil[1].highlightInfos[2].color,
                Set = (target, color) => Human.face.rendEye[1].material
                    .SetColor(ChaShader.Face.Eyes.HighlightColor03, Human.data.Custom.Face.pupil[1].highlightInfos[2].color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "Cheek")
            {
                Available = () => (Coordinate?.FaceMakeup?.cheekId ?? 0) is not 0,
                Get = () => Coordinate.FaceMakeup.cheekColor,
                Set = (target, color) => Human.face.rendFace.material
                    .SetColor(ChaShader.Face.sCheekColorID, Coordinate.FaceMakeup.cheekColor = Merge(target, color))
            },
            new ColorProperty(Options.Face, "CheekHighlight")
            {
                Available = () => (Coordinate?.FaceMakeup?.cheekId ?? 0) is not 0,
                Get = () => Coordinate.FaceMakeup.cheekHighlightColor,
                Set = (target, color) => Human.face.rendFace.material
                    .SetColor(ChaShader.Face.sCheekHighlightColorID, Coordinate.FaceMakeup.cheekHighlightColor = Merge(target, color))
            },
            new ColorProperty(Options.Face, "Eyeshadow")
            {
                Available = () => (Coordinate?.FaceMakeup?.eyeshadowId ?? 0) is not 0,
                Get = () => Coordinate.FaceMakeup.eyeshadowColor,
                Set = (target, color) => SetColor(FaceCtc,
                    ChaShader.Face.CreateShader.sEyeshadowColorID, Coordinate.FaceMakeup.eyeshadowColor = Merge(target, color))
            },
            new ColorProperty(Options.Face, "Lip")
            {
                Available = () => (Coordinate?.FaceMakeup?.lipId ?? 0) is not 0,
                Get = () => Coordinate.FaceMakeup.lipColor,
                Set = (target, color) => Human.face.rendFace.material
                    .SetColor(ChaShader.Face.sLipColorID, Coordinate.FaceMakeup.lipColor = Merge(target, color))
            },
            new ColorProperty(Options.Face, "LipHighlight")
            {
                Available = () => (Coordinate?.FaceMakeup?.lipId ?? 0) is not 0,
                Get = () => Coordinate.FaceMakeup.lipHighlightColor,
                Set = (target, color) => Human.face.rendFace.material
                    .SetColor(ChaShader.Face.sLipHighlightColorID, Coordinate.FaceMakeup.lipHighlightColor = Merge(target, color))
            },
            new ColorProperty(Options.Face, "FacePaint1")
            {
                Available = () => (Coordinate?.FaceMakeup?.paintInfos?[0].ID ?? 0) is not 0,
                Get = () => Coordinate.FaceMakeup.paintInfos[0].color,
                Set = (target, color) => SetColor(FaceCtc,
                    ChaShader.Face.CreateShader.sPaintColor01, Coordinate.FaceMakeup.paintInfos[0].color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "FacePaint2")
            {
                Available = () => (Coordinate?.FaceMakeup?.paintInfos?[1].ID ?? 0) is not 0,
                Get = () => Coordinate.FaceMakeup.paintInfos[1].color,
                Set = (target, color) => SetColor(FaceCtc,
                    ChaShader.Face.CreateShader.sPaintColor02, Coordinate.FaceMakeup.paintInfos[1].color = Merge(target, color))
            },
            new ColorProperty(Options.Face, "FacePaint3")
            {
                Available = () => (Coordinate?.FaceMakeup?.paintInfos?[2].ID ?? 0) is not 0,
                Get = () => Coordinate.FaceMakeup.paintInfos[2].color,
                Set = (target, color) => SetColor(FaceCtc,
                    ChaShader.Face.CreateShader.sPaintColor03, Coordinate.FaceMakeup.paintInfos[2].color = Merge(target, color))
            },
            .. HairProperties("Back", 0),
            .. HairProperties("Front", 1),
            .. HairProperties("Side", 2),
            .. HairProperties("Option", 3),
            .. ClothesProperties("Top", 0),
            .. ClothesProperties("Bottom", 1),
            .. ClothesProperties("Bra", 2),
            .. ClothesProperties("Shorts", 3),
            .. ClothesProperties("Gloves", 4),
            .. ClothesProperties("Panst", 5),
            .. ClothesProperties("Socks", 6),
            .. ClothesProperties("Shoes", 7),
            .. Enumerable.Range(0, Human.acs.Accessories.Count).SelectMany(AcsProperties)
        ];

        static IEnumerable<ColorProperty> HairProperties(string name, int part) => [
            new ColorProperty(Options.Hairs, $"{name}Main") {
                Available = () => Human?.hair?.Hairs?[part]?.renderers?
                    .Any(rend => rend.material.HasColor(ChaShader.Hair.sMainColor)) ?? false,
                Get = () => Coordinate.Hair.parts[part].baseColor,
                Set = (target, color) => SetColor(ChaShader.Hair.sMainColor,
                    Coordinate.Hair.parts[part].baseColor = Merge(target, color), Human.hair.Hairs[part].renderers)
            },
            new ColorProperty(Options.Hairs, $"{name}Sub1") {
                Available = () => Human?.hair?.Hairs?[part]?.renderers?
                    .Any(rend => rend.material.HasColor(ChaShader.Hair.sSub01Color)) ?? false,
                Get = () => Coordinate.Hair.parts[part].startColor,
                Set = (target, color) => SetColor(ChaShader.Hair.sSub01Color,
                    Coordinate.Hair.parts[part].startColor = Merge(target, color), Human.hair.Hairs[part].renderers)
            },
            new ColorProperty(Options.Hairs, $"{name}Sub2") {
                Available = () => Human?.hair?.Hairs?[part]?.renderers?
                    .Any(rend => rend.material.HasColor(ChaShader.Hair.sSub02Color)) ?? false,
                Get = () => Coordinate.Hair.parts[part].endColor,
                Set = (target, color) => SetColor(ChaShader.Hair.sSub02Color,
                    Coordinate.Hair.parts[part].endColor = Merge(target, color), Human.hair.Hairs[part].renderers)
            },
            new ColorProperty(Options.Hairs, $"{name}Gloss") {
                Available = () => (Coordinate?.Hair?.glossId ?? 0) is not 0 &&
                    (Human?.hair?.Hairs?[part]?.renderers?
                        .Any(rend => rend.material.HasColor(ChaShader.Hair.sGlossColorID)) ?? false),
                Get = () => Coordinate.Hair.parts[part].glossColor,
                Set = (target, color) => SetColor(ChaShader.Hair.sGlossColorID,
                    Coordinate.Hair.parts[part].glossColor = Merge(target, color), Human.hair.Hairs[part].renderers)
            },
            new ColorProperty(Options.Hairs, $"{name}Shadow") {
                Available = () => Human?.hair?.Hairs?[part]?.renderers?
                    .Any(rend => rend.material.HasColor(ChaShader.Hair.sShadowColorID)) ?? false,
                Get = () => Coordinate.Hair.parts[part].shadowColor,
                Set = (target, color) => SetColor(ChaShader.Hair.sShadowColorID,
                    Coordinate.Hair.parts[part].shadowColor = Merge(target, color), Human.hair.Hairs[part].renderers)
            },
            new ColorProperty(Options.Hairs, $"{name}Outline") {
                Available = () => Human?.hair?.Hairs?[part]?.renderers?
                    .Any(rend => rend.material.HasColor(ChaShader.Hair.sLineColorID)) ?? false,
                Get = () => Coordinate.Hair.parts[part].outlineColor,
                Set = (target, color) => SetColor(ChaShader.Hair.sLineColorID,
                    Coordinate.Hair.parts[part].outlineColor = Merge(target, color), Human.hair.Hairs[part].renderers)
            },
            new ColorProperty(Options.Hairs, $"{name}Mesh") {
                Available = () => (Coordinate?.Hair?.parts?[part]?.useMesh ?? false) &&
                    (Human?.hair?.Hairs?[part]?.renderers?
                        .Any(rend => rend.material.HasColor(ChaShader.Hair.sMeshColorID)) ?? false),
                Get = () => Coordinate.Hair.parts[part].meshColor,
                Set = (target, color) => SetColor(ChaShader.Hair.sMeshColorID,
                    Coordinate.Hair.parts[part].meshColor = Merge(target, color), Human.hair.Hairs[part].renderers)
            },
            new ColorProperty(Options.Hairs, $"{name}Inner") {
                Available = () => (Coordinate?.Hair?.parts?[part]?.useInner ?? false) &&
                    (Human?.hair?.Hairs?[part]?.renderers?
                        .Any(rend => rend.material.HasColor(ChaShader.Hair.sInnerColorID)) ?? false),
                Get = () => Coordinate.Hair.parts[part].innerColor,
                Set = (target, color) => SetColor(ChaShader.Hair.sInnerColorID,
                    Coordinate.Hair.parts[part].innerColor = Merge(target, color), Human.hair.Hairs[part].renderers)
            },
            new ColorProperty(Options.Hairs, $"{name}Acs1") {
                Available = () => 0 < Human.hair.GetHairAcsColorNum(part),
                Get = () => Coordinate.Hair.parts[part].acsColor[0],
                Set = (target, color) => SetColor(ChaShader.Hair.sAcsColor01,
                    Coordinate.Hair.parts[part].acsColor[0] = Merge(target, color), Human.hair.Hairs[part].renderers)
            },
            new ColorProperty(Options.Hairs, $"{name}Acs2") {
                Available = () => 1 < Human.hair.GetHairAcsColorNum(part),
                Get = () => Coordinate?.Hair?.parts?[part]?.acsColor[1] ?? Human.hair.Hairs[part].cusHairCmp.acsDefColor[1],
                Set = (target, color) => SetColor(ChaShader.Hair.sAcsColor02,
                    Coordinate.Hair.parts[part].acsColor[1] = Merge(target, color), Human.hair.Hairs[part].renderers)
            },
            new ColorProperty(Options.Hairs, $"{name}Acs3") {
                Available = () => 2 < Human.hair.GetHairAcsColorNum(part),
                Get = () => Coordinate.Hair.parts[part].acsColor[2],
                Set = (target, color) => SetColor(ChaShader.Hair.sAcsColor03,
                    Coordinate.Hair.parts[part].acsColor[2] = Merge(target, color), Human.hair.Hairs[part].renderers)
            }
        ];
        static IEnumerable<ColorProperty> ClothesProperties(string name, int part) => [
            new ColorProperty(Options.Clothes, $"{name}1") {
                Available = () => Human?.cloth?.Clothess?[part]?.cusClothesCmp?.useColorN01 ?? false,
                Get = () => Coordinate.Clothes.parts[part].colorInfo[0].baseColor,
                Set = (target, color) => ClothesCtc(part).Invoke(ChaShader.Clothes.CreateShader.sMainColor01,
                    Coordinate.Clothes.parts[part].colorInfo[0].baseColor = Merge(target, color))
            },
            new ColorProperty(Options.Clothes, $"{name}2") {
                Available = () => Human?.cloth?.Clothess?[part]?.cusClothesCmp?.useColorN02 ?? false,
                Get = () => Coordinate.Clothes.parts[part].colorInfo[1].baseColor,
                Set = (target, color) => ClothesCtc(part).Invoke(ChaShader.Clothes.CreateShader.sMainColor02,
                    Coordinate.Clothes.parts[part].colorInfo[1].baseColor = Merge(target, color))
            },
            new ColorProperty(Options.Clothes, $"{name}3") {
                Available = () => Human?.cloth?.Clothess?[part]?.cusClothesCmp?.useColorN03 ?? false,
                Get = () => Coordinate.Clothes.parts[part].colorInfo[2].baseColor,
                Set = (target, color) => ClothesCtc(part).Invoke(ChaShader.Clothes.CreateShader.sMainColor03,
                    Coordinate.Clothes.parts[part].colorInfo[2].baseColor = Merge(target, color))
            },
            new ColorProperty(Options.Clothes, $"{name}4") {
                Available = () => Human?.cloth?.Clothess?[part]?.cusClothesCmp?.useColorN04 ?? false,
                Get = () => Coordinate.Clothes.parts[part].colorInfo[3].baseColor,
                Set = (target, color) => ClothesCtc(part).Invoke(ChaShader.Clothes.CreateShader.sMainColor04,
                    Coordinate.Clothes.parts[part].colorInfo[3].baseColor = Merge(target, color))
            },
            new ColorProperty(Options.Clothes, $"{name}Pattern1") {
                Available = () =>
                    (Human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPattern(0) ?? false) &&
                    ((Coordinate?.Clothes?.parts?[part]?.colorInfo?[0]?.patternInfo?.pattern ?? 0) is not 0),
                Get = () => Coordinate.Clothes.parts[part].colorInfo[0].patternInfo.patternColor,
                Set = (target, color) => ClothesCtc(part).Invoke(ChaShader.Clothes.CreateShader.sSubColor01,
                    Coordinate.Clothes.parts[part].colorInfo[0].patternInfo.patternColor = Merge(target, color))
            },
            new ColorProperty(Options.Clothes, $"{name}Pattern2") {
                Available = () =>
                    (Human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPattern(1) ?? false) &&
                    ((Coordinate?.Clothes?.parts?[part]?.colorInfo?[1]?.patternInfo?.pattern ?? 0) is not 0),
                Get = () => Coordinate.Clothes.parts[part].colorInfo[1].patternInfo.patternColor,
                Set = (target, color) => ClothesCtc(part).Invoke(ChaShader.Clothes.CreateShader.sSubColor02,
                    Coordinate.Clothes.parts[part].colorInfo[1].patternInfo.patternColor = Merge(target, color))
            },
            new ColorProperty(Options.Clothes, $"{name}Pattern3") {
                Available = () =>
                    (Human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPattern(2) ?? false) &&
                    ((Coordinate?.Clothes?.parts?[part]?.colorInfo?[2]?.patternInfo?.pattern ?? 0) is not 0),
                Get = () => Coordinate.Clothes.parts[part].colorInfo[2].patternInfo.patternColor,
                Set = (target, color) => ClothesCtc(part).Invoke(ChaShader.Clothes.CreateShader.sSubColor03,
                    Coordinate.Clothes.parts[part].colorInfo[2].patternInfo.patternColor = Merge(target, color))
            },
            new ColorProperty(Options.Clothes, $"{name}Pattern4") {
                Available = () =>
                    (Human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPattern(3) ?? false) &&
                    ((Coordinate?.Clothes?.parts?[part]?.colorInfo?[3]?.patternInfo?.pattern ?? 0) is not 0),
                Get = () => Coordinate.Clothes.parts[part].colorInfo[3].patternInfo.patternColor,
                Set = (target, color) => ClothesCtc(part).Invoke(ChaShader.Clothes.CreateShader.sSubColor04,
                    Coordinate.Clothes.parts[part].colorInfo[3].patternInfo.patternColor = Merge(target, color))
            },
            new ColorProperty(Options.Clothes, $"{name}Paint1") {
                Available = () =>
                    (Human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPaint(0) ?? false) &&
                    ((Coordinate?.Clothes?.parts?[part]?.paintInfos?[0]?.ID ?? 0) is not 0),
                Get = () => Coordinate.Clothes.parts[part].paintInfos[0].color,
                Set = (target, color) => ClothesCtc(part).Invoke(ChaShader.Clothes.CreateShader.sPaintColor01,
                    Coordinate.Clothes.parts[part].paintInfos[0].color = Merge(target, color))
            },
            new ColorProperty(Options.Clothes, $"{name}Paint2") {
                Available = () =>
                    (Human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPaint(1) ?? false) &&
                    ((Coordinate?.Clothes?.parts?[part]?.paintInfos?[1]?.ID ?? 0) is not 0),
                Get = () => Coordinate.Clothes.parts[part].paintInfos[1].color,
                Set = (target, color) => ClothesCtc(part).Invoke(ChaShader.Clothes.CreateShader.sPaintColor02,
                    Coordinate.Clothes.parts[part].paintInfos[1].color = Merge(target, color))
            },
            new ColorProperty(Options.Clothes, $"{name}Paint3") {
                Available = () =>
                    (Human?.cloth?.Clothess?[part]?.cusClothesCmp?.HasPaint(2) ?? false) &&
                    ((Coordinate?.Clothes?.parts?[part]?.paintInfos?[2]?.ID ?? 0) is not 0),
                Get = () => Coordinate.Clothes.parts[part].paintInfos[2].color,
                Set = (target, color) => ClothesCtc(part).Invoke(ChaShader.Clothes.CreateShader.sPaintColor03,
                    Coordinate.Clothes.parts[part].paintInfos[2].color = Merge(target, color))
            },
        ];
        static IEnumerable<ColorProperty> AcsProperties(int part) => [
            new ColorProperty(Options.Acs, $"Acs{part+1:00}Main1") {
                Available = () => Human?.acs?.Accessories?[part]?.cusAcsCmp?.useColor01 ?? false,
                Get = () => Coordinate.Accessory.parts[part].color[0],
                Set = (target, color) => SetColor(ChaShader.Accessory.sMainColor01,
                    Coordinate.Accessory.parts[part].color[0] = Merge(target, color), Human.acs.Accessories[part].renderers)
            },
            new ColorProperty(Options.Acs, $"Acs{part+1:00}Main2") {
                Available = () => Human?.acs?.Accessories?[part]?.cusAcsCmp?.useColor02 ?? false,
                Get = () => Coordinate.Accessory.parts[part].color[1],
                Set = (target, color) => SetColor(ChaShader.Accessory.sMainColor02,
                    Coordinate.Accessory.parts[part].color[1] = Merge(target, color), Human.acs.Accessories[part].renderers)
            },
            new ColorProperty(Options.Acs, $"Acs{part+1:00}Main3") {
                Available = () => Human?.acs?.Accessories?[part]?.cusAcsCmp?.useColor03 ?? false,
                Get = () => Coordinate.Accessory.parts[part].color[2],
                Set = (target, color) => SetColor(ChaShader.Accessory.sMainColor03,
                    Coordinate.Accessory.parts[part].color[2] = Merge(target, color), Human.acs.Accessories[part].renderers)
            },
            new ColorProperty(Options.Acs, $"Acs{part+1:00}Pattern1") {
                Available = () => (Human?.acs?.Accessories?[part]?.cusAcsCmp?.HasPattern(0) ?? false) &&
                    ((Coordinate?.Accessory?.parts?[part]?.colorInfo?[0]?.pattern ?? 0) is not 0),
                Get = () => Coordinate.Accessory.parts[part].colorInfo[0].patternColor,
                Set = (target, color) => SetColor(ChaShader.Accessory.sSubColor01,
                    Coordinate.Accessory.parts[part].colorInfo[0].patternColor = Merge(target, color), Human.acs.Accessories[part].renderers)
            },
            new ColorProperty(Options.Acs, $"Acs{part+1:00}Pattern2") {
                Available = () => (Human?.acs?.Accessories?[part]?.cusAcsCmp?.HasPattern(1) ?? false) &&
                    ((Coordinate?.Accessory?.parts?[part]?.colorInfo?[1]?.pattern ?? 0) is not 0),
                Get = () => Coordinate.Accessory.parts[part].colorInfo[1].patternColor,
                Set = (target, color) => SetColor(ChaShader.Accessory.sSubColor02,
                    Coordinate.Accessory.parts[part].colorInfo[1].patternColor = Merge(target, color), Human.acs.Accessories[part].renderers)
            },
            new ColorProperty(Options.Acs, $"Acs{part+1:00}Pattern3") {
                Available = () => (Human?.acs?.Accessories?[part]?.cusAcsCmp?.HasPattern(2) ?? false) &&
                    ((Coordinate?.Accessory?.parts?[part]?.colorInfo?[2]?.pattern ?? 0) is not 0),
                Get = () => Coordinate.Accessory.parts[part].colorInfo[2].patternColor,
                Set = (target, color) => SetColor(ChaShader.Accessory.sSubColor03,
                    Coordinate.Accessory.parts[part].colorInfo[2].patternColor = Merge(target, color), Human.acs.Accessories[part].renderers)
            }
        ];
    }
}
 