using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GothicUISetupEditor
{
    private const string ArtDir = "Assets/project/UI/GothicMetroidvania/Art";
    private const string FontPath = "Assets/project/UI/GothicMetroidvania/Fonts/VT323-Regular.ttf";
    private const string PrefabPath = "Assets/project/UI/GothicMetroidvania/GothicGameUI.prefab";

    private const string BossArenaScenePath = "Assets/Scenes/BossArenaDemo.unity";
    private const string DungeonDemoScenePath = "Assets/Scenes/DungeonDemo.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    private const string DefaultOutputDir = @"C:\Users\Admin\.gemini\antigravity\brain\0985a74f-0a95-403a-b42c-5f96c6792073";

    private static readonly Color GoldColor = Hex("#f3b689");
    private static readonly Color MintColor = Hex("#98e8c8");
    private static readonly Color BoneColor = Hex("#e3ded6");
    private static readonly Color RoseColor = Hex("#c96b78");
    private static readonly Color SubtitleColor = Hex("#c7bfc9");
    private static readonly Color DividerColor = Hex("#7b2e64");
    private static readonly Color ObsidianColor = Hex("#0e101c");
    private static readonly Color TrackRimColor = Hex("#343c5b");
    private static readonly Color OutlineColor = new Color(0.043f, 0.047f, 0.078f, 0.96f);
    private static readonly Color ScrimColor = new Color(11f / 255f, 12f / 255f, 20f / 255f, 0.80f);

    private struct SpriteImportSpec
    {
        public string FileName;
        public Vector4 Border;
        public bool CropOnePxBorder;
    }

    private static readonly SpriteImportSpec[] SpriteSpecs =
    {
        new SpriteImportSpec { FileName = "MedallionGold.png", Border = Vector4.zero },
        new SpriteImportSpec { FileName = "PlayerPortrait.png", Border = Vector4.zero },
        new SpriteImportSpec { FileName = "PlayerBarFrame.png", Border = new Vector4(5, 5, 5, 5) },
        new SpriteImportSpec { FileName = "StaminaBarFrame.png", Border = new Vector4(3, 3, 3, 3) },
        new SpriteImportSpec { FileName = "RoseSlatePanel.png", Border = new Vector4(4, 4, 4, 4) },
        new SpriteImportSpec { FileName = "PillarMenuPanel.png", Border = new Vector4(3, 10, 12, 10) },
        new SpriteImportSpec { FileName = "HpFillCrimson.png", Border = Vector4.zero, CropOnePxBorder = true },
        new SpriteImportSpec { FileName = "BossFillRose.png", Border = Vector4.zero, CropOnePxBorder = true },
        new SpriteImportSpec { FileName = "FlatFillBlock.png", Border = Vector4.zero, CropOnePxBorder = true },
        new SpriteImportSpec { FileName = "CornerLGold.png", Border = Vector4.zero },
        new SpriteImportSpec { FileName = "SkullCrest.png", Border = Vector4.zero },
        new SpriteImportSpec { FileName = "ComboPipOff.png", Border = new Vector4(3, 3, 3, 3) },
        new SpriteImportSpec { FileName = "ComboPipOn.png", Border = new Vector4(3, 3, 3, 3) },
        new SpriteImportSpec { FileName = "DiamondGem.png", Border = Vector4.zero },
        new SpriteImportSpec { FileName = "MenuButtonHover.png", Border = new Vector4(4, 4, 4, 4) },
        new SpriteImportSpec { FileName = "LowHpVignette.png", Border = new Vector4(14, 14, 14, 14) }
    };

    [MenuItem("Tools/Gothic UI/Build And Verify All")]
    public static void BuildAndVerifyAll()
    {
        ConfigureAssets();
        BuildPrefab();
        WireAllScenes();
        CaptureVerificationScreenshots(DefaultOutputDir);
        Debug.Log("[GothicUISetupEditor] BuildAndVerifyAll completed cleanly.");
    }

    private static void ConfigureAssets()
    {
        var factory = new SpriteDataProviderFactories();
        factory.Init();

        foreach (SpriteImportSpec spec in SpriteSpecs)
        {
            string assetPath = $"{ArtDir}/{spec.FileName}";
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"Missing texture importer at {assetPath}");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 80f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spriteBorder = spec.Border;

            var tis = new TextureImporterSettings();
            importer.ReadTextureSettings(tis);
            tis.spriteMeshType = SpriteMeshType.FullRect;
            tis.spriteExtrude = 0;
            tis.spriteGenerateFallbackPhysicsShape = false;
            tis.spriteAlignment = (int)SpriteAlignment.Center;
            tis.spritePivot = new Vector2(0.5f, 0.5f);
            tis.spriteBorder = spec.Border;
            importer.SetTextureSettings(tis);
            importer.SaveAndReimport();

            if (spec.CropOnePxBorder)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex == null)
                    throw new InvalidOperationException($"Failed to load texture at {assetPath}");

                var expectedRect = new Rect(1f, 1f, tex.width - 2f, tex.height - 2f);
                ApplySpriteCropRect(factory, importer, assetPath, tex.width, tex.height, expectedRect);
            }

            Sprite loaded = LoadPrimarySprite(assetPath);
            if (loaded == null)
                throw new InvalidOperationException($"Failed to load sprite at {assetPath}");

            Debug.Log($"[GothicUISetupEditor] Sprite {spec.FileName}: rect={loaded.rect}, border={loaded.border}, ppu={loaded.pixelsPerUnit}");
        }

        var fontImporter = AssetImporter.GetAtPath(FontPath) as TrueTypeFontImporter;
        if (fontImporter == null)
            throw new InvalidOperationException($"Missing font importer at {FontPath}");

        fontImporter.fontRenderingMode = FontRenderingMode.HintedRaster;
        fontImporter.fontSize = 28;
        fontImporter.SaveAndReimport();

        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
            throw new InvalidOperationException($"Failed to load font at {FontPath}");

        Debug.Log($"[GothicUISetupEditor] Font configured: {font.name}, fontSize={font.fontSize}");
    }

    private static void ApplySpriteCropRect(
        SpriteDataProviderFactories factory,
        TextureImporter importer,
        string assetPath,
        int texWidth,
        int texHeight,
        Rect expectedRect)
    {
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider != null)
        {
            dataProvider.InitSpriteEditorDataProvider();
            SpriteRect[] rects = dataProvider.GetSpriteRects();
            if (rects != null && rects.Length > 0)
            {
                rects[0].rect = expectedRect;
                rects[0].alignment = SpriteAlignment.Center;
                rects[0].pivot = new Vector2(0.5f, 0.5f);
                dataProvider.SetSpriteRects(rects);
                dataProvider.Apply();
                importer.SaveAndReimport();
            }
        }

        Sprite check = LoadPrimarySprite(assetPath);
        if (check != null && check.rect == expectedRect)
            return;

        // Unity's Single sprite mode can override SpriteRect back to full texture dimensions; fall back to 1-entry Multiple.
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.SaveAndReimport();

        dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider != null)
        {
            dataProvider.InitSpriteEditorDataProvider();
            string spriteName = Path.GetFileNameWithoutExtension(assetPath);
            var cropped = new SpriteRect
            {
                name = spriteName,
                spriteID = GUID.Generate(),
                rect = expectedRect,
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero
            };
            dataProvider.SetSpriteRects(new[] { cropped });
            dataProvider.Apply();
            importer.SaveAndReimport();
        }

        Sprite verified = LoadPrimarySprite(assetPath);
        if (verified == null || verified.rect != expectedRect)
        {
            throw new InvalidOperationException(
                $"Crop rect failed for {assetPath}: expected {expectedRect}, actual {(verified != null ? verified.rect.ToString() : "null")} (tex {texWidth}x{texHeight})");
        }
    }

    private static Sprite LoadPrimarySprite(string assetPath)
    {
        Sprite direct = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (direct != null)
            return direct;

        return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault();
    }

    private static Sprite LoadArt(string fileName)
    {
        string path = $"{ArtDir}/{fileName}";
        Sprite sprite = LoadPrimarySprite(path);
        if (sprite == null)
            throw new InvalidOperationException($"Missing sprite asset {path}");
        return sprite;
    }

    private static Font LoadGothicFont()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
            throw new InvalidOperationException($"Missing font asset {FontPath}");
        return font;
    }

    private static void BuildPrefab()
    {
        Font font = LoadGothicFont();

        Sprite medallionGold = LoadArt("MedallionGold.png");
        Sprite playerPortrait = LoadArt("PlayerPortrait.png");
        Sprite playerBarFrame = LoadArt("PlayerBarFrame.png");
        Sprite staminaBarFrame = LoadArt("StaminaBarFrame.png");
        Sprite roseSlatePanel = LoadArt("RoseSlatePanel.png");
        Sprite pillarMenuPanel = LoadArt("PillarMenuPanel.png");
        Sprite hpFillCrimson = LoadArt("HpFillCrimson.png");
        Sprite bossFillRose = LoadArt("BossFillRose.png");
        Sprite flatFillBlock = LoadArt("FlatFillBlock.png");
        Sprite cornerLGold = LoadArt("CornerLGold.png");
        Sprite skullCrest = LoadArt("SkullCrest.png");
        Sprite comboPipOff = LoadArt("ComboPipOff.png");
        Sprite comboPipOn = LoadArt("ComboPipOn.png");
        Sprite diamondGem = LoadArt("DiamondGem.png");
        Sprite menuButtonHover = LoadArt("MenuButtonHover.png");
        Sprite lowHpVignetteSprite = LoadArt("LowHpVignette.png");

        var root = new GameObject("GothicGameUI", typeof(RectTransform), typeof(GothicUIController));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        StretchFull(rootRect);

        // 1. Low HP Vignette
        GameObject vignetteGo = CreateUIObject("LowHpVignette", rootRect);
        StretchFull(vignetteGo.GetComponent<RectTransform>());
        UnityEngine.UI.Image vignetteImg = vignetteGo.AddComponent<UnityEngine.UI.Image>();
        vignetteImg.sprite = lowHpVignetteSprite;
        vignetteImg.type = UnityEngine.UI.Image.Type.Sliced;
        vignetteImg.color = new Color(1f, 1f, 1f, 0f);
        vignetteImg.raycastTarget = false;
        vignetteGo.SetActive(false);

        // 2. HUDRoot
        GameObject hudRoot = CreateUIObject("HUDRoot", rootRect);
        RectTransform hudRect = hudRoot.GetComponent<RectTransform>();
        StretchFull(hudRect);

        // 2a. Top-Left PlayerStatusCluster (Sample 1)
        GameObject playerCluster = CreateUIObject("PlayerStatusCluster", hudRect);
        RectTransform clusterRect = playerCluster.GetComponent<RectTransform>();
        clusterRect.anchorMin = new Vector2(0f, 1f);
        clusterRect.anchorMax = new Vector2(0f, 1f);
        clusterRect.pivot = new Vector2(0f, 1f);
        clusterRect.anchoredPosition = new Vector2(36f, -24f);
        clusterRect.sizeDelta = new Vector2(560f, 144f);

        GameObject medallionWrap = CreateUIObject("MedallionWrap", clusterRect);
        RectTransform medallionWrapRect = medallionWrap.GetComponent<RectTransform>();
        medallionWrapRect.anchorMin = new Vector2(0f, 1f);
        medallionWrapRect.anchorMax = new Vector2(0f, 1f);
        medallionWrapRect.pivot = new Vector2(0f, 1f);
        medallionWrapRect.anchoredPosition = Vector2.zero;
        medallionWrapRect.sizeDelta = new Vector2(144f, 144f);

        GameObject medallionRing = CreateUIObject("MedallionGold", medallionWrapRect);
        RectTransform medallionRingRect = medallionRing.GetComponent<RectTransform>();
        medallionRingRect.anchorMin = new Vector2(0.5f, 0.5f);
        medallionRingRect.anchorMax = new Vector2(0.5f, 0.5f);
        medallionRingRect.pivot = new Vector2(0.5f, 0.5f);
        medallionRingRect.anchoredPosition = Vector2.zero;
        medallionRingRect.sizeDelta = new Vector2(144f, 144f);
        AddImage(medallionRing, medallionGold, UnityEngine.UI.Image.Type.Simple, Color.white, false);

        GameObject portraitGo = CreateUIObject("PlayerPortrait", medallionWrapRect);
        RectTransform portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
        portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
        portraitRect.pivot = new Vector2(0.5f, 0.5f);
        portraitRect.anchoredPosition = Vector2.zero;
        portraitRect.sizeDelta = new Vector2(90f, 90f);
        UnityEngine.UI.Image portraitImg = AddImage(portraitGo, playerPortrait, UnityEngine.UI.Image.Type.Simple, Color.white, false);
        portraitImg.preserveAspect = true;

        // Right stack beside medallion
        GameObject playerHeader = CreateUIObject("PlayerHeader", clusterRect);
        RectTransform playerHeaderRect = playerHeader.GetComponent<RectTransform>();
        playerHeaderRect.anchorMin = new Vector2(0f, 1f);
        playerHeaderRect.anchorMax = new Vector2(0f, 1f);
        playerHeaderRect.pivot = new Vector2(0f, 1f);
        playerHeaderRect.anchoredPosition = new Vector2(130f, -14f);
        playerHeaderRect.sizeDelta = new Vector2(412f, 34f);

        GameObject playerNameGo = CreateUIObject("PlayerNameText", playerHeaderRect);
        RectTransform playerNameRect = playerNameGo.GetComponent<RectTransform>();
        playerNameRect.anchorMin = new Vector2(0f, 0f);
        playerNameRect.anchorMax = new Vector2(0.72f, 1f);
        playerNameRect.offsetMin = Vector2.zero;
        playerNameRect.offsetMax = Vector2.zero;
        CreateText(playerNameGo, font, "KIẾM SĨ ĐỘC THỦ", 32, GoldColor, TextAnchor.MiddleLeft, true);

        GameObject playerHpValGo = CreateUIObject("PlayerHpValue", playerHeaderRect);
        RectTransform playerHpValRect = playerHpValGo.GetComponent<RectTransform>();
        playerHpValRect.anchorMin = new Vector2(0.68f, 0f);
        playerHpValRect.anchorMax = new Vector2(1f, 1f);
        playerHpValRect.offsetMin = Vector2.zero;
        playerHpValRect.offsetMax = Vector2.zero;
        UnityEngine.UI.Text playerHpValText = CreateText(playerHpValGo, font, "100/100", 30, MintColor, TextAnchor.MiddleRight, true);

        GameObject playerHpBarGo = CreateUIObject("PlayerHealthBar", clusterRect);
        RectTransform playerHpBarRect = playerHpBarGo.GetComponent<RectTransform>();
        playerHpBarRect.anchorMin = new Vector2(0f, 1f);
        playerHpBarRect.anchorMax = new Vector2(0f, 1f);
        playerHpBarRect.pivot = new Vector2(0f, 1f);
        playerHpBarRect.anchoredPosition = new Vector2(130f, -50f);
        playerHpBarRect.sizeDelta = new Vector2(412f, 34f);
        AddImage(playerHpBarGo, playerBarFrame, UnityEngine.UI.Image.Type.Sliced, Color.white, false);
        HealthBar playerHealthBar = playerHpBarGo.AddComponent<HealthBar>();

        GameObject playerTrackGo = CreateUIObject("FillTrack", playerHpBarRect);
        RectTransform playerTrackRect = playerTrackGo.GetComponent<RectTransform>();
        StretchWithPadding(playerTrackRect, 8f, 7f, 8f, 7f);
        AddImage(playerTrackGo, flatFillBlock, UnityEngine.UI.Image.Type.Simple, ObsidianColor, false);

        GameObject playerGhostGo = CreateUIObject("GhostFill", playerTrackRect);
        StretchFull(playerGhostGo.GetComponent<RectTransform>());
        UnityEngine.UI.Image playerGhostImg = AddFilledImage(playerGhostGo, flatFillBlock, GoldColor);

        GameObject playerFillGo = CreateUIObject("HealthFill", playerTrackRect);
        StretchFull(playerFillGo.GetComponent<RectTransform>());
        UnityEngine.UI.Image playerFillImg = AddFilledImage(playerFillGo, hpFillCrimson, Color.white);

        ConfigureHealthBar(playerHealthBar, playerFillImg, playerGhostImg, playerHpValText);

        // Stamina + Combo Row
        GameObject staminaTrackGo = CreateUIObject("StaminaTrack", clusterRect);
        RectTransform staminaTrackRect = staminaTrackGo.GetComponent<RectTransform>();
        staminaTrackRect.anchorMin = new Vector2(0f, 1f);
        staminaTrackRect.anchorMax = new Vector2(0f, 1f);
        staminaTrackRect.pivot = new Vector2(0f, 1f);
        staminaTrackRect.anchoredPosition = new Vector2(130f, -92f);
        staminaTrackRect.sizeDelta = new Vector2(284f, 18f);
        AddImage(staminaTrackGo, staminaBarFrame, UnityEngine.UI.Image.Type.Sliced, Color.white, false);

        GameObject staminaBgGo = CreateUIObject("StaminaBg", staminaTrackRect);
        RectTransform staminaBgRect = staminaBgGo.GetComponent<RectTransform>();
        StretchWithPadding(staminaBgRect, 4f, 4f, 4f, 4f);
        AddImage(staminaBgGo, flatFillBlock, UnityEngine.UI.Image.Type.Simple, ObsidianColor, false);

        GameObject staminaFillGo = CreateUIObject("StaminaFill", staminaTrackRect);
        RectTransform staminaFillRect = staminaFillGo.GetComponent<RectTransform>();
        StretchWithPadding(staminaFillRect, 4f, 4f, 4f, 4f);
        UnityEngine.UI.Image staminaFillImg = AddFilledImage(staminaFillGo, flatFillBlock, MintColor);

        GameObject comboRowGo = CreateUIObject("ComboRow", clusterRect);
        RectTransform comboRowRect = comboRowGo.GetComponent<RectTransform>();
        comboRowRect.anchorMin = new Vector2(0f, 1f);
        comboRowRect.anchorMax = new Vector2(0f, 1f);
        comboRowRect.pivot = new Vector2(0f, 1f);
        comboRowRect.anchoredPosition = new Vector2(426f, -91f);
        comboRowRect.sizeDelta = new Vector2(116f, 20f);
        HorizontalLayoutGroup comboLayout = comboRowGo.AddComponent<HorizontalLayoutGroup>();
        comboLayout.spacing = 7f;
        comboLayout.childAlignment = TextAnchor.MiddleLeft;
        comboLayout.childControlWidth = false;
        comboLayout.childControlHeight = false;
        comboLayout.childForceExpandWidth = false;
        comboLayout.childForceExpandHeight = false;

        var comboPips = new UnityEngine.UI.Image[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject pipGo = CreateUIObject($"ComboPip_{i + 1}", comboRowRect);
            pipGo.GetComponent<RectTransform>().sizeDelta = new Vector2(34f, 20f);
            comboPips[i] = AddImage(pipGo, comboPipOff, UnityEngine.UI.Image.Type.Sliced, Color.white, false);
        }

        // 2b. Top-Right AreaPlate (Sample 2)
        GameObject areaPlateGo = CreateUIObject("AreaPlate", hudRect);
        RectTransform areaPlateRect = areaPlateGo.GetComponent<RectTransform>();
        areaPlateRect.anchorMin = new Vector2(1f, 1f);
        areaPlateRect.anchorMax = new Vector2(1f, 1f);
        areaPlateRect.pivot = new Vector2(1f, 1f);
        areaPlateRect.anchoredPosition = new Vector2(-36f, -28f);
        areaPlateRect.sizeDelta = new Vector2(280f, 54f);
        AddImage(areaPlateGo, roseSlatePanel, UnityEngine.UI.Image.Type.Sliced, Color.white, false);

        HorizontalLayoutGroup areaLayout = areaPlateGo.AddComponent<HorizontalLayoutGroup>();
        areaLayout.padding = new RectOffset(16, 16, 8, 8);
        areaLayout.spacing = 10f;
        areaLayout.childAlignment = TextAnchor.MiddleCenter;
        areaLayout.childControlWidth = true;
        areaLayout.childControlHeight = false;
        areaLayout.childForceExpandWidth = false;
        areaLayout.childForceExpandHeight = false;

        ContentSizeFitter areaFitter = areaPlateGo.AddComponent<ContentSizeFitter>();
        areaFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        areaFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject areaGemGo = CreateUIObject("AreaGem", areaPlateRect);
        areaGemGo.GetComponent<RectTransform>().sizeDelta = new Vector2(22f, 22f);
        LayoutElement areaGemLe = areaGemGo.AddComponent<LayoutElement>();
        areaGemLe.preferredWidth = 22f;
        areaGemLe.preferredHeight = 22f;
        areaGemLe.minWidth = 22f;
        areaGemLe.minHeight = 22f;
        UnityEngine.UI.Image areaGemImg = AddImage(areaGemGo, diamondGem, UnityEngine.UI.Image.Type.Simple, Color.white, false);
        areaGemImg.preserveAspect = true;

        GameObject areaTitleGo = CreateUIObject("AreaTitleText", areaPlateRect);
        areaTitleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 38f);
        UnityEngine.UI.Text areaTitleText = CreateText(areaTitleGo, font, "HẦM NGỤC TỐI", 30, GoldColor, TextAnchor.MiddleLeft, true);
        areaTitleText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 2c. Bottom-Left ControlsStrip (Sample 2, swapped to Bottom-Left)
        GameObject controlsGo = CreateUIObject("ControlsStrip", hudRect);
        RectTransform controlsRect = controlsGo.GetComponent<RectTransform>();
        controlsRect.anchorMin = new Vector2(0f, 0f);
        controlsRect.anchorMax = new Vector2(0f, 0f);
        controlsRect.pivot = new Vector2(0f, 0f);
        controlsRect.anchoredPosition = new Vector2(36f, 26f);
        controlsRect.sizeDelta = new Vector2(560f, 50f);
        AddImage(controlsGo, roseSlatePanel, UnityEngine.UI.Image.Type.Sliced, Color.white, false);

        HorizontalLayoutGroup controlsLayout = controlsGo.AddComponent<HorizontalLayoutGroup>();
        controlsLayout.padding = new RectOffset(18, 18, 8, 8);
        controlsLayout.childAlignment = TextAnchor.MiddleCenter;
        controlsLayout.childControlWidth = true;
        controlsLayout.childControlHeight = true;
        controlsLayout.childForceExpandWidth = false;
        controlsLayout.childForceExpandHeight = false;

        ContentSizeFitter controlsFitter = controlsGo.AddComponent<ContentSizeFitter>();
        controlsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        controlsFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject controlsTextGo = CreateUIObject("ControlsText", controlsRect);
        UnityEngine.UI.Text controlsText = CreateText(
            controlsTextGo,
            font,
            "<color=#f3b689>A/D</color> Chạy  <color=#8f3c52>·</color>  <color=#f3b689>Space</color> Nhảy  <color=#8f3c52>·</color>  <color=#98e8c8>Click / Z</color> Chém  <color=#8f3c52>·</color>  <color=#c96b78>Esc</color> Dừng",
            28,
            BoneColor,
            TextAnchor.MiddleCenter,
            true);
        controlsText.supportRichText = true;
        controlsText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 2d. Bottom-Right BossHealthCard (Sample 4, swapped to Bottom-Right)
        GameObject bossCardGo = CreateUIObject("BossHealthCard", hudRect);
        RectTransform bossCardRect = bossCardGo.GetComponent<RectTransform>();
        bossCardRect.anchorMin = new Vector2(1f, 0f);
        bossCardRect.anchorMax = new Vector2(1f, 0f);
        bossCardRect.pivot = new Vector2(1f, 0f);
        bossCardRect.anchoredPosition = new Vector2(-36f, 26f);
        bossCardRect.sizeDelta = new Vector2(468f, 92f);
        AddImage(bossCardGo, roseSlatePanel, UnityEngine.UI.Image.Type.Sliced, Color.white, false);

        CreateCornerOrnament("CornerTopLeft", bossCardRect, cornerLGold, new Vector2(0f, 1f), new Vector2(-14f, 14f), Vector3.one);
        CreateCornerOrnament("CornerBottomRight", bossCardRect, cornerLGold, new Vector2(1f, 0f), new Vector2(14f, -14f), new Vector3(-1f, -1f, 1f));

        GameObject bossNameGo = CreateUIObject("BossNameText", bossCardRect);
        RectTransform bossNameRect = bossNameGo.GetComponent<RectTransform>();
        bossNameRect.anchorMin = new Vector2(0f, 1f);
        bossNameRect.anchorMax = new Vector2(0f, 1f);
        bossNameRect.pivot = new Vector2(0f, 1f);
        bossNameRect.anchoredPosition = new Vector2(20f, -14f);
        bossNameRect.sizeDelta = new Vector2(315f, 34f);
        UnityEngine.UI.Text bossNameText = CreateText(bossNameGo, font, "BOSS · HẮC THÚ", 30, GoldColor, TextAnchor.MiddleLeft, true);

        GameObject bossHpValGo = CreateUIObject("BossHpValue", bossCardRect);
        RectTransform bossHpValRect = bossHpValGo.GetComponent<RectTransform>();
        bossHpValRect.anchorMin = new Vector2(1f, 1f);
        bossHpValRect.anchorMax = new Vector2(1f, 1f);
        bossHpValRect.pivot = new Vector2(1f, 1f);
        bossHpValRect.anchoredPosition = new Vector2(-20f, -14f);
        bossHpValRect.sizeDelta = new Vector2(115f, 34f);
        UnityEngine.UI.Text bossHpValText = CreateText(bossHpValGo, font, "140/140", 28, RoseColor, TextAnchor.MiddleRight, true);

        GameObject bossBarGo = CreateUIObject("BossHealthBar", bossCardRect);
        RectTransform bossBarRect = bossBarGo.GetComponent<RectTransform>();
        bossBarRect.anchorMin = new Vector2(0f, 0f);
        bossBarRect.anchorMax = new Vector2(0f, 0f);
        bossBarRect.pivot = new Vector2(0f, 0f);
        bossBarRect.anchoredPosition = new Vector2(20f, 16f);
        bossBarRect.sizeDelta = new Vector2(428f, 24f);
        AddImage(bossBarGo, flatFillBlock, UnityEngine.UI.Image.Type.Simple, TrackRimColor, false);
        HealthBar bossHealthBar = bossBarGo.AddComponent<HealthBar>();

        GameObject bossTrackBgGo = CreateUIObject("TrackBg", bossBarRect);
        RectTransform bossTrackBgRect = bossTrackBgGo.GetComponent<RectTransform>();
        StretchWithPadding(bossTrackBgRect, 2f, 2f, 2f, 2f);
        AddImage(bossTrackBgGo, flatFillBlock, UnityEngine.UI.Image.Type.Simple, ObsidianColor, false);

        GameObject bossGhostGo = CreateUIObject("GhostFill", bossTrackBgRect);
        StretchFull(bossGhostGo.GetComponent<RectTransform>());
        UnityEngine.UI.Image bossGhostImg = AddFilledImage(bossGhostGo, flatFillBlock, GoldColor);

        GameObject bossFillGo = CreateUIObject("HealthFill", bossTrackBgRect);
        StretchFull(bossFillGo.GetComponent<RectTransform>());
        UnityEngine.UI.Image bossFillImg = AddFilledImage(bossFillGo, bossFillRose, Color.white);

        ConfigureHealthBar(bossHealthBar, bossFillImg, bossGhostImg, bossHpValText);

        // 3. MainMenuPanel (Sample 2 Left-Aligned Gothic Pillar)
        GameObject mainMenuGo = CreateUIObject("MainMenuPanel", rootRect);
        RectTransform mainMenuRect = mainMenuGo.GetComponent<RectTransform>();
        mainMenuRect.anchorMin = new Vector2(0f, 0f);
        mainMenuRect.anchorMax = new Vector2(0f, 1f);
        mainMenuRect.pivot = new Vector2(0f, 0.5f);
        mainMenuRect.anchoredPosition = Vector2.zero;
        mainMenuRect.offsetMin = new Vector2(0f, 0f);
        mainMenuRect.offsetMax = new Vector2(540f, 0f);
        AddImage(mainMenuGo, pillarMenuPanel, UnityEngine.UI.Image.Type.Sliced, Color.white, true);

        GameObject menuStackGo = CreateUIObject("PillarContent", mainMenuRect);
        RectTransform menuStackRect = menuStackGo.GetComponent<RectTransform>();
        menuStackRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuStackRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuStackRect.pivot = new Vector2(0.5f, 0.5f);
        menuStackRect.anchoredPosition = new Vector2(-6f, 20f);
        menuStackRect.sizeDelta = new Vector2(440f, 520f);

        GameObject menuCrestGo = CreateUIObject("SkullCrest", menuStackRect);
        RectTransform menuCrestRect = menuCrestGo.GetComponent<RectTransform>();
        menuCrestRect.anchorMin = new Vector2(0.5f, 1f);
        menuCrestRect.anchorMax = new Vector2(0.5f, 1f);
        menuCrestRect.pivot = new Vector2(0.5f, 1f);
        menuCrestRect.anchoredPosition = new Vector2(0f, -8f);
        menuCrestRect.sizeDelta = new Vector2(130f, 45f);
        UnityEngine.UI.Image menuCrestImg = AddImage(menuCrestGo, skullCrest, UnityEngine.UI.Image.Type.Simple, Color.white, false);
        menuCrestImg.preserveAspect = true;

        GameObject menuTitleGo = CreateUIObject("MenuTitle", menuStackRect);
        RectTransform menuTitleRect = menuTitleGo.GetComponent<RectTransform>();
        menuTitleRect.anchorMin = new Vector2(0.5f, 1f);
        menuTitleRect.anchorMax = new Vector2(0.5f, 1f);
        menuTitleRect.pivot = new Vector2(0.5f, 1f);
        menuTitleRect.anchoredPosition = new Vector2(0f, -66f);
        menuTitleRect.sizeDelta = new Vector2(420f, 58f);
        CreateText(menuTitleGo, font, "BIÊN NIÊN", 52, MintColor, TextAnchor.MiddleCenter, true);

        GameObject menuSubGo = CreateUIObject("MenuSubtitle", menuStackRect);
        RectTransform menuSubRect = menuSubGo.GetComponent<RectTransform>();
        menuSubRect.anchorMin = new Vector2(0.5f, 1f);
        menuSubRect.anchorMax = new Vector2(0.5f, 1f);
        menuSubRect.pivot = new Vector2(0.5f, 1f);
        menuSubRect.anchoredPosition = new Vector2(0f, -124f);
        menuSubRect.sizeDelta = new Vector2(420f, 44f);
        CreateText(menuSubGo, font, "KIẾM SĨ ĐỘC THỦ", 36, GoldColor, TextAnchor.MiddleCenter, true);

        GameObject menuRuleGo = CreateUIObject("DividerRule", menuStackRect);
        RectTransform menuRuleRect = menuRuleGo.GetComponent<RectTransform>();
        menuRuleRect.anchorMin = new Vector2(0.5f, 1f);
        menuRuleRect.anchorMax = new Vector2(0.5f, 1f);
        menuRuleRect.pivot = new Vector2(0.5f, 1f);
        menuRuleRect.anchoredPosition = new Vector2(0f, -182f);
        menuRuleRect.sizeDelta = new Vector2(420f, 3f);
        AddImage(menuRuleGo, flatFillBlock, UnityEngine.UI.Image.Type.Simple, DividerColor, false);

        string[] mainMenuLabelsText = { "BẮT ĐẦU", "ĐỔI MÀN", "CHƠI LẠI", "THOÁT" };
        BuildButtonGroup(
            menuStackRect,
            mainMenuLabelsText,
            startY: -214f,
            rowWidth: 420f,
            rowHeight: 52f,
            spacing: 12f,
            fontSize: 34,
            font: font,
            hoverSprite: menuButtonHover,
            diamondGem: diamondGem,
            out UnityEngine.UI.Button[] mainMenuButtons,
            out UnityEngine.UI.Image[] mainMenuBackgrounds,
            out UnityEngine.UI.Image[] mainMenuCursors,
            out UnityEngine.UI.Text[] mainMenuButtonLabels);

        GameObject menuHintGo = CreateUIObject("MenuHint", mainMenuRect);
        RectTransform menuHintRect = menuHintGo.GetComponent<RectTransform>();
        menuHintRect.anchorMin = new Vector2(0.5f, 0f);
        menuHintRect.anchorMax = new Vector2(0.5f, 0f);
        menuHintRect.pivot = new Vector2(0.5f, 0f);
        menuHintRect.anchoredPosition = new Vector2(-6f, 34f);
        menuHintRect.sizeDelta = new Vector2(460f, 34f);
        CreateText(menuHintGo, font, "W/S hoặc Chuột · Enter để chọn", 26, SubtitleColor, TextAnchor.MiddleCenter, true);

        mainMenuGo.SetActive(false);

        // 4. PauseModal
        BuildModalPanel(
            "PauseModal",
            rootRect,
            font,
            flatFillBlock,
            roseSlatePanel,
            cornerLGold,
            skullCrest,
            menuButtonHover,
            diamondGem,
            title: "TẠM DỪNG",
            titleColor: MintColor,
            subtitle: "NHẤN ESC ĐỂ TIẾP TỤC",
            buttonLabels: new[] { "TIẾP TỤC", "CHƠI LẠI", "ĐỔI MÀN", "VỀ MENU" },
            out GameObject pauseModalGo,
            out _,
            out _,
            out UnityEngine.UI.Button[] pauseButtons,
            out UnityEngine.UI.Image[] pauseBackgrounds,
            out UnityEngine.UI.Image[] pauseCursors,
            out UnityEngine.UI.Text[] pauseButtonLabels);
        pauseModalGo.SetActive(false);

        // 5. EndModal
        BuildModalPanel(
            "EndModal",
            rootRect,
            font,
            flatFillBlock,
            roseSlatePanel,
            cornerLGold,
            skullCrest,
            menuButtonHover,
            diamondGem,
            title: "NGÃ GỤC",
            titleColor: GoldColor,
            subtitle: "HỒI SINH TẠI ĐIỂM KHỞI ĐẦU",
            buttonLabels: new[] { "HỒI SINH", "CHƠI LẠI", "VỀ MENU" },
            out GameObject endModalGo,
            out UnityEngine.UI.Text endTitleText,
            out UnityEngine.UI.Text endSubtitleText,
            out UnityEngine.UI.Button[] endButtons,
            out UnityEngine.UI.Image[] endBackgrounds,
            out UnityEngine.UI.Image[] endCursors,
            out UnityEngine.UI.Text[] endButtonLabels);
        endModalGo.SetActive(false);

        // Wire GothicUIController Serialized Fields
        var controller = root.GetComponent<GothicUIController>();
        var so = new SerializedObject(controller);
        so.FindProperty("lowHpVignette").objectReferenceValue = vignetteImg;
        so.FindProperty("hudRoot").objectReferenceValue = hudRoot;
        so.FindProperty("playerHealthBar").objectReferenceValue = playerHealthBar;
        so.FindProperty("staminaFillImage").objectReferenceValue = staminaFillImg;
        SetObjectArray(so.FindProperty("comboPips"), comboPips);
        so.FindProperty("comboPipOffSprite").objectReferenceValue = comboPipOff;
        so.FindProperty("comboPipOnSprite").objectReferenceValue = comboPipOn;
        so.FindProperty("areaTitleText").objectReferenceValue = areaTitleText;

        so.FindProperty("bossCardRoot").objectReferenceValue = bossCardGo;
        so.FindProperty("bossNameText").objectReferenceValue = bossNameText;
        so.FindProperty("bossHealthBar").objectReferenceValue = bossHealthBar;

        so.FindProperty("mainMenuPanel").objectReferenceValue = mainMenuGo;
        so.FindProperty("pauseModal").objectReferenceValue = pauseModalGo;
        so.FindProperty("endModal").objectReferenceValue = endModalGo;
        so.FindProperty("endTitleText").objectReferenceValue = endTitleText;
        so.FindProperty("endSubtitleText").objectReferenceValue = endSubtitleText;

        SetObjectArray(so.FindProperty("mainMenuButtons"), mainMenuButtons);
        SetObjectArray(so.FindProperty("mainMenuButtonBackgrounds"), mainMenuBackgrounds);
        SetObjectArray(so.FindProperty("mainMenuCursors"), mainMenuCursors);
        SetObjectArray(so.FindProperty("mainMenuButtonLabels"), mainMenuButtonLabels);

        SetObjectArray(so.FindProperty("pauseButtons"), pauseButtons);
        SetObjectArray(so.FindProperty("pauseButtonBackgrounds"), pauseBackgrounds);
        SetObjectArray(so.FindProperty("pauseCursors"), pauseCursors);
        SetObjectArray(so.FindProperty("pauseButtonLabels"), pauseButtonLabels);

        SetObjectArray(so.FindProperty("endButtons"), endButtons);
        SetObjectArray(so.FindProperty("endButtonBackgrounds"), endBackgrounds);
        SetObjectArray(so.FindProperty("endCursors"), endCursors);
        SetObjectArray(so.FindProperty("endButtonLabels"), endButtonLabels);

        so.FindProperty("buttonNormalSprite").objectReferenceValue = null;
        so.FindProperty("buttonHoverSprite").objectReferenceValue = menuButtonHover;
        so.FindProperty("buttonNormalTextColor").colorValue = BoneColor;
        so.FindProperty("buttonHoverTextColor").colorValue = MintColor;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GothicUISetupEditor] Saved prefab: {PrefabPath}");
    }

    private static void BuildModalPanel(
        string name,
        RectTransform parent,
        Font font,
        Sprite flatFillBlock,
        Sprite roseSlatePanel,
        Sprite cornerLGold,
        Sprite skullCrest,
        Sprite hoverSprite,
        Sprite diamondGem,
        string title,
        Color titleColor,
        string subtitle,
        string[] buttonLabels,
        out GameObject modalGo,
        out UnityEngine.UI.Text titleText,
        out UnityEngine.UI.Text subtitleText,
        out UnityEngine.UI.Button[] buttons,
        out UnityEngine.UI.Image[] backgrounds,
        out UnityEngine.UI.Image[] cursors,
        out UnityEngine.UI.Text[] labels)
    {
        modalGo = CreateUIObject(name, parent);
        RectTransform modalRect = modalGo.GetComponent<RectTransform>();
        StretchFull(modalRect);

        UnityEngine.UI.Image scrim = AddImage(modalGo, flatFillBlock, UnityEngine.UI.Image.Type.Simple, ScrimColor, true);
        scrim.raycastTarget = true;

        float boxWidth = 480f;
        float rowHeight = 52f;
        float spacing = 10f;
        float verticalPad = 24f;
        float boxHeight = verticalPad * 2f + buttonLabels.Length * rowHeight + (buttonLabels.Length - 1) * spacing;
        float headerHeight = 148f;
        float gapBetweenHeaderAndBox = 18f;
        float totalHeight = headerHeight + gapBetweenHeaderAndBox + boxHeight;

        GameObject centerGroup = CreateUIObject("CenterGroup", modalRect);
        RectTransform centerRect = centerGroup.GetComponent<RectTransform>();
        centerRect.anchorMin = new Vector2(0.5f, 0.5f);
        centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.pivot = new Vector2(0.5f, 0.5f);
        centerRect.anchoredPosition = Vector2.zero;
        centerRect.sizeDelta = new Vector2(640f, totalHeight);

        // Header with SkullCrest + side rules
        GameObject crestRow = CreateUIObject("CrestRow", centerRect);
        RectTransform crestRowRect = crestRow.GetComponent<RectTransform>();
        crestRowRect.anchorMin = new Vector2(0.5f, 1f);
        crestRowRect.anchorMax = new Vector2(0.5f, 1f);
        crestRowRect.pivot = new Vector2(0.5f, 1f);
        crestRowRect.anchoredPosition = Vector2.zero;
        crestRowRect.sizeDelta = new Vector2(460f, 46f);

        GameObject leftRule = CreateUIObject("LeftRule", crestRowRect);
        RectTransform leftRuleRect = leftRule.GetComponent<RectTransform>();
        leftRuleRect.anchorMin = new Vector2(0f, 0.5f);
        leftRuleRect.anchorMax = new Vector2(0f, 0.5f);
        leftRuleRect.pivot = new Vector2(0f, 0.5f);
        leftRuleRect.anchoredPosition = Vector2.zero;
        leftRuleRect.sizeDelta = new Vector2(140f, 3f);
        AddImage(leftRule, flatFillBlock, UnityEngine.UI.Image.Type.Simple, DividerColor, false);

        GameObject crestGo = CreateUIObject("SkullCrest", crestRowRect);
        RectTransform crestRect = crestGo.GetComponent<RectTransform>();
        crestRect.anchorMin = new Vector2(0.5f, 0.5f);
        crestRect.anchorMax = new Vector2(0.5f, 0.5f);
        crestRect.pivot = new Vector2(0.5f, 0.5f);
        crestRect.anchoredPosition = Vector2.zero;
        crestRect.sizeDelta = new Vector2(130f, 45f);
        UnityEngine.UI.Image crestImg = AddImage(crestGo, skullCrest, UnityEngine.UI.Image.Type.Simple, Color.white, false);
        crestImg.preserveAspect = true;

        GameObject rightRule = CreateUIObject("RightRule", crestRowRect);
        RectTransform rightRuleRect = rightRule.GetComponent<RectTransform>();
        rightRuleRect.anchorMin = new Vector2(1f, 0.5f);
        rightRuleRect.anchorMax = new Vector2(1f, 0.5f);
        rightRuleRect.pivot = new Vector2(1f, 0.5f);
        rightRuleRect.anchoredPosition = Vector2.zero;
        rightRuleRect.sizeDelta = new Vector2(140f, 3f);
        AddImage(rightRule, flatFillBlock, UnityEngine.UI.Image.Type.Simple, DividerColor, false);

        GameObject titleGo = CreateUIObject("TitleText", centerRect);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -52f);
        titleRect.sizeDelta = new Vector2(640f, 60f);
        titleText = CreateText(titleGo, font, title, 54, titleColor, TextAnchor.MiddleCenter, true);

        GameObject subGo = CreateUIObject("SubtitleText", centerRect);
        RectTransform subRect = subGo.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 1f);
        subRect.anchorMax = new Vector2(0.5f, 1f);
        subRect.pivot = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -112f);
        subRect.sizeDelta = new Vector2(640f, 36f);
        subtitleText = CreateText(subGo, font, subtitle, 30, SubtitleColor, TextAnchor.MiddleCenter, true);

        // ModalBox
        GameObject boxGo = CreateUIObject("ModalBox", centerRect);
        RectTransform boxRect = boxGo.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 1f);
        boxRect.anchorMax = new Vector2(0.5f, 1f);
        boxRect.pivot = new Vector2(0.5f, 1f);
        boxRect.anchoredPosition = new Vector2(0f, -(headerHeight + gapBetweenHeaderAndBox));
        boxRect.sizeDelta = new Vector2(boxWidth, boxHeight);
        AddImage(boxGo, roseSlatePanel, UnityEngine.UI.Image.Type.Sliced, Color.white, true);

        CreateCornerOrnament("CornerTopLeft", boxRect, cornerLGold, new Vector2(0f, 1f), new Vector2(-14f, 14f), Vector3.one);
        CreateCornerOrnament("CornerTopRight", boxRect, cornerLGold, new Vector2(1f, 1f), new Vector2(14f, 14f), new Vector3(-1f, 1f, 1f));
        CreateCornerOrnament("CornerBottomLeft", boxRect, cornerLGold, new Vector2(0f, 0f), new Vector2(-14f, -14f), new Vector3(1f, -1f, 1f));
        CreateCornerOrnament("CornerBottomRight", boxRect, cornerLGold, new Vector2(1f, 0f), new Vector2(14f, -14f), new Vector3(-1f, -1f, 1f));

        BuildButtonGroup(
            boxRect,
            buttonLabels,
            startY: -verticalPad,
            rowWidth: 420f,
            rowHeight: rowHeight,
            spacing: spacing,
            fontSize: 34,
            font: font,
            hoverSprite: hoverSprite,
            diamondGem: diamondGem,
            out buttons,
            out backgrounds,
            out cursors,
            out labels);
    }

    private static void BuildButtonGroup(
        RectTransform parent,
        string[] buttonTexts,
        float startY,
        float rowWidth,
        float rowHeight,
        float spacing,
        int fontSize,
        Font font,
        Sprite hoverSprite,
        Sprite diamondGem,
        out UnityEngine.UI.Button[] buttons,
        out UnityEngine.UI.Image[] backgrounds,
        out UnityEngine.UI.Image[] cursors,
        out UnityEngine.UI.Text[] labels)
    {
        int count = buttonTexts.Length;
        buttons = new UnityEngine.UI.Button[count];
        backgrounds = new UnityEngine.UI.Image[count];
        cursors = new UnityEngine.UI.Image[count];
        labels = new UnityEngine.UI.Text[count];

        for (int i = 0; i < count; i++)
        {
            float y = startY - i * (rowHeight + spacing);
            GameObject btnGo = CreateUIObject($"Btn_{i + 1}", parent);
            RectTransform btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 1f);
            btnRect.anchorMax = new Vector2(0.5f, 1f);
            btnRect.pivot = new Vector2(0.5f, 1f);
            btnRect.anchoredPosition = new Vector2(0f, y);
            btnRect.sizeDelta = new Vector2(rowWidth, rowHeight);

            UnityEngine.UI.Image bg = btnGo.AddComponent<UnityEngine.UI.Image>();
            bg.sprite = i == 0 ? hoverSprite : null;
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            bg.color = i == 0 ? Color.white : new Color(1f, 1f, 1f, 0f);
            bg.raycastTarget = true;

            UnityEngine.UI.Button btn = btnGo.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;

            GameObject cursorGo = CreateUIObject("Cursor", btnRect);
            RectTransform cursorRect = cursorGo.GetComponent<RectTransform>();
            cursorRect.anchorMin = new Vector2(0f, 0.5f);
            cursorRect.anchorMax = new Vector2(0f, 0.5f);
            cursorRect.pivot = new Vector2(0f, 0.5f);
            cursorRect.anchoredPosition = new Vector2(20f, 0f);
            cursorRect.sizeDelta = new Vector2(22f, 22f);
            UnityEngine.UI.Image cursorImg = AddImage(cursorGo, diamondGem, UnityEngine.UI.Image.Type.Simple, Color.white, false);
            cursorImg.preserveAspect = true;
            cursorImg.enabled = i == 0;

            GameObject labelGo = CreateUIObject("Label", btnRect);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(56f, 0f);
            labelRect.offsetMax = new Vector2(-18f, 0f);
            UnityEngine.UI.Text labelText = CreateText(
                labelGo,
                font,
                buttonTexts[i],
                fontSize,
                i == 0 ? MintColor : BoneColor,
                TextAnchor.MiddleLeft,
                true);

            buttons[i] = btn;
            backgrounds[i] = bg;
            cursors[i] = cursorImg;
            labels[i] = labelText;
        }
    }

    private static void WireAllScenes()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"Missing prefab at {PrefabPath}");

        WireScene(
            BossArenaScenePath,
            prefab,
            areaTitle: "ĐẤU TRƯỜNG HẮC KỴ",
            bossDisplayName: "BOSS · HẮC KỴ SĨ",
            startAtMainMenu: false,
            legacyObjectsToDelete: new[] { "PlayerHeartIcon", "PlayerHealthBar", "BossHealthBar" });

        WireScene(
            DungeonDemoScenePath,
            prefab,
            areaTitle: "HẦM NGỤC TỐI",
            bossDisplayName: "BOSS · HẮC THÚ",
            startAtMainMenu: false,
            legacyObjectsToDelete: Array.Empty<string>());

        WireScene(
            SampleScenePath,
            prefab,
            areaTitle: "THỬ THÁCH CỔ ĐÀI",
            bossDisplayName: string.Empty,
            startAtMainMenu: true,
            legacyObjectsToDelete: new[] { "HealthBar" });
    }

    private static void WireScene(
        string scenePath,
        GameObject prefab,
        string areaTitle,
        string bossDisplayName,
        bool startAtMainMenu,
        string[] legacyObjectsToDelete)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        EnsureSingleEventSystem();
        Canvas canvas = EnsureMainCanvas();

        // Remove legacy HealthBar-Sheet UI and any previous GothicGameUI instance
        var toDestroy = new List<GameObject>();
        for (int i = 0; i < canvas.transform.childCount; i++)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child.name == "GothicGameUI" || legacyObjectsToDelete.Contains(child.name))
                toDestroy.Add(child.gameObject);
        }

        foreach (GameObject go in toDestroy)
            UnityEngine.Object.DestroyImmediate(go);

        GameObject uiInstance = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
        if (uiInstance == null)
            throw new InvalidOperationException($"Failed to instantiate {PrefabPath} in {scenePath}");

        uiInstance.name = "GothicGameUI";
        StretchFull(uiInstance.GetComponent<RectTransform>());

        GothicUIController controller = uiInstance.GetComponent<GothicUIController>();
        Player player = UnityEngine.Object.FindAnyObjectByType<Player>();
        DoomKnightBoss boss = UnityEngine.Object.FindAnyObjectByType<DoomKnightBoss>();
        DungeonTrainingDummy dummy = UnityEngine.Object.FindAnyObjectByType<DungeonTrainingDummy>();
        LevelGoal goal = UnityEngine.Object.FindAnyObjectByType<LevelGoal>();

        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("player").objectReferenceValue = player;
        controllerSo.FindProperty("boss").objectReferenceValue = boss;
        controllerSo.FindProperty("trainingDummy").objectReferenceValue = dummy;
        controllerSo.FindProperty("levelGoal").objectReferenceValue = goal;
        controllerSo.FindProperty("areaTitle").stringValue = areaTitle;
        controllerSo.FindProperty("bossDisplayName").stringValue = bossDisplayName;
        controllerSo.FindProperty("startAtMainMenu").boolValue = startAtMainMenu;

        SerializedProperty playerHpBarProp = controllerSo.FindProperty("playerHealthBar");
        SerializedProperty bossHpBarProp = controllerSo.FindProperty("bossHealthBar");
        SerializedProperty areaTitleTextProp = controllerSo.FindProperty("areaTitleText");
        SerializedProperty bossNameTextProp = controllerSo.FindProperty("bossNameText");
        SerializedProperty bossCardRootProp = controllerSo.FindProperty("bossCardRoot");

        if (areaTitleTextProp.objectReferenceValue is UnityEngine.UI.Text areaText)
            areaText.text = areaTitle;
        if (bossNameTextProp.objectReferenceValue is UnityEngine.UI.Text bossText && !string.IsNullOrEmpty(bossDisplayName))
            bossText.text = bossDisplayName;
        if (bossCardRootProp.objectReferenceValue is GameObject bossCardGo)
            bossCardGo.SetActive(boss != null || dummy != null);

        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire Player.healthBar, DoomKnightBoss.healthBar, DungeonTrainingDummy.healthBar
        HealthBar playerHb = playerHpBarProp.objectReferenceValue as HealthBar;
        HealthBar bossHb = bossHpBarProp.objectReferenceValue as HealthBar;

        if (player != null)
        {
            var playerSo = new SerializedObject(player);
            playerSo.FindProperty("healthBar").objectReferenceValue = playerHb;
            playerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        if (boss != null)
        {
            var bossSo = new SerializedObject(boss);
            bossSo.FindProperty("healthBar").objectReferenceValue = bossHb;
            bossSo.ApplyModifiedPropertiesWithoutUndo();
        }

        if (dummy != null)
        {
            var dummySo = new SerializedObject(dummy);
            dummySo.FindProperty("healthBar").objectReferenceValue = bossHb;
            dummySo.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Failed to save scene {scenePath}");

        Debug.Log($"[GothicUISetupEditor] Wired and saved {scenePath}");
    }

    private static void EnsureSingleEventSystem()
    {
        EventSystem[] systems = UnityEngine.Object.FindObjectsByType<EventSystem>();
        EventSystem primary = systems.FirstOrDefault();
        for (int i = 1; i < systems.Length; i++)
            UnityEngine.Object.DestroyImmediate(systems[i].gameObject);

        if (primary == null)
        {
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            primary = esGo.GetComponent<EventSystem>();
        }

        var inputModule = primary.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
            inputModule = primary.gameObject.AddComponent<InputSystemUIInputModule>();

        if (inputModule.actionsAsset == null)
            inputModule.AssignDefaultActions();
    }

    private static Canvas EnsureMainCanvas()
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>();
        Canvas canvas = canvases.FirstOrDefault(c => c.name == "Canvas") ?? canvases.FirstOrDefault();
        foreach (Canvas other in canvases)
        {
            if (other != canvas)
                UnityEngine.Object.DestroyImmediate(other.gameObject);
        }

        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvas = canvasGo.AddComponent<Canvas>();
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;

        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>() ?? canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        return canvas;
    }

    private static void CaptureVerificationScreenshots(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        // 1. BossArenaDemo HUD (100/100 player, 200/200 boss)
        RenderStateToPng(
            BossArenaScenePath,
            Path.Combine(outputDir, "01_boss_arena_hud.png"),
            ctrl => ctrl.ApplyPreviewState(100f, 100f, 100f, 0, 200f, false, false, false, false));

        // 2. DungeonDemo HUD damaged preview (75/100 player, 95 ghost, 70 stamina, 2 combo pips, 112/140 boss)
        RenderStateToPng(
            DungeonDemoScenePath,
            Path.Combine(outputDir, "02_dungeon_demo_hud.png"),
            ctrl => ctrl.ApplyPreviewState(75f, 95f, 70f, 2, 112f, false, false, false, false));

        // 3. Main Menu Pillar over DungeonDemo idle scene
        RenderStateToPng(
            DungeonDemoScenePath,
            Path.Combine(outputDir, "03_main_menu_pillar.png"),
            ctrl => ctrl.ApplyPreviewState(100f, 100f, 100f, 0, 140f, true, false, false, false));

        // 4. Pause Modal over BossArenaDemo
        RenderStateToPng(
            BossArenaScenePath,
            Path.Combine(outputDir, "04_pause_modal.png"),
            ctrl => ctrl.ApplyPreviewState(100f, 100f, 100f, 0, 200f, false, true, false, false));

        // 5. Victory Modal over BossArenaDemo
        RenderStateToPng(
            BossArenaScenePath,
            Path.Combine(outputDir, "05_victory_modal.png"),
            ctrl => ctrl.ApplyPreviewState(100f, 100f, 100f, 0, 0f, false, false, false, true));
    }

    private static void RenderStateToPng(string scenePath, string pngPath, Action<GothicUIController> applyState)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        Camera mainCam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (mainCam == null)
            throw new InvalidOperationException($"No Camera found in {scenePath}");

        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
            throw new InvalidOperationException($"No Canvas found in {scenePath}");

        GothicUIController controller = UnityEngine.Object.FindAnyObjectByType<GothicUIController>();
        if (controller == null)
            throw new InvalidOperationException($"No GothicUIController found in {scenePath}");

        // Render-only camera & canvas adjustments (scene is NOT re-saved after this)
        mainCam.aspect = 1920f / 1080f;
        DungeonCameraFit cameraFit = mainCam.GetComponent<DungeonCameraFit>();
        if (cameraFit != null)
        {
            var fitSo = new SerializedObject(cameraFit);
            var backdropProp = fitSo.FindProperty("backdrop");
            if (backdropProp != null && backdropProp.objectReferenceValue is SpriteRenderer backdrop && backdrop != null)
            {
                Vector3 halfRoom = backdrop.bounds.extents;
                mainCam.orthographicSize = Mathf.Max(halfRoom.y, halfRoom.x / mainCam.aspect);
            }
        }

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = mainCam;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 100;

        applyState(controller);
        Canvas.ForceUpdateCanvases();

        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevTarget = mainCam.targetTexture;

        try
        {
            mainCam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            mainCam.Render();
            mainCam.Render();

            RenderTexture.active = rt;
            var shot = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            shot.Apply();

            byte[] bytes = shot.EncodeToPNG();
            File.WriteAllBytes(pngPath, bytes);
            UnityEngine.Object.DestroyImmediate(shot);
            Debug.Log($"[GothicUISetupEditor] Captured {pngPath} ({bytes.Length} bytes)");
        }
        finally
        {
            mainCam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void StretchWithPadding(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
    }

    private static UnityEngine.UI.Image AddImage(
        GameObject go,
        Sprite sprite,
        UnityEngine.UI.Image.Type imageType,
        Color color,
        bool raycastTarget)
    {
        UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>() ?? go.AddComponent<UnityEngine.UI.Image>();
        img.sprite = sprite;
        img.type = imageType;
        img.color = color;
        img.raycastTarget = raycastTarget;
        return img;
    }

    private static UnityEngine.UI.Image AddFilledImage(GameObject go, Sprite sprite, Color color)
    {
        UnityEngine.UI.Image img = AddImage(go, sprite, UnityEngine.UI.Image.Type.Filled, color, false);
        img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        img.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
        return img;
    }

    private static UnityEngine.UI.Text CreateText(
        GameObject go,
        Font font,
        string content,
        int fontSize,
        Color color,
        TextAnchor alignment,
        bool addOutline)
    {
        UnityEngine.UI.Text txt = go.GetComponent<UnityEngine.UI.Text>() ?? go.AddComponent<UnityEngine.UI.Text>();
        txt.font = font;
        txt.text = content;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = alignment;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;

        if (addOutline)
        {
            UnityEngine.UI.Outline outline = go.GetComponent<UnityEngine.UI.Outline>() ?? go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = OutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }

        return txt;
    }

    private static void CreateCornerOrnament(
        string name,
        RectTransform parent,
        Sprite cornerSprite,
        Vector2 anchor,
        Vector2 anchoredPos,
        Vector3 localScale)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(48f, 48f);
        rect.localScale = localScale;
        AddImage(go, cornerSprite, UnityEngine.UI.Image.Type.Simple, Color.white, false);
    }

    private static void ConfigureHealthBar(
        HealthBar healthBar,
        UnityEngine.UI.Image fillImage,
        UnityEngine.UI.Image ghostFillImage,
        UnityEngine.UI.Text valueLabel)
    {
        var so = new SerializedObject(healthBar);
        so.FindProperty("fillImage").objectReferenceValue = fillImage;
        so.FindProperty("ghostFillImage").objectReferenceValue = ghostFillImage;
        so.FindProperty("valueLabel").objectReferenceValue = valueLabel;
        so.FindProperty("valueFormat").stringValue = "{0}/{1}";
        so.FindProperty("snapGhostOnInitialize").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectArray<T>(SerializedProperty prop, T[] items) where T : UnityEngine.Object
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static Color Hex(string hex)
    {
        if (!ColorUtility.TryParseHtmlString(hex, out Color color))
            throw new InvalidOperationException($"Invalid hex color {hex}");
        return color;
    }
}
