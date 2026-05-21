using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class LocalizedTmpFontProvider
{
    private static TMP_FontAsset localizedFontAsset;
    private static bool fontLookupFailed;
    private static bool globalFallbackApplied;

    public static void Apply(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        TMP_FontAsset fontAsset = GetFontAsset();
        if (fontAsset != null)
        {
            AddFallback(text.font, fontAsset);
            AddGlobalFallback(fontAsset);
            text.SetAllDirty();
        }
    }

    private static TMP_FontAsset GetFontAsset()
    {
        if (localizedFontAsset != null)
        {
            return localizedFontAsset;
        }

        if (fontLookupFailed)
        {
            return null;
        }

        string[] fontPaths =
        {
            "C:/Windows/Fonts/malgun.ttf",
            "C:/Windows/Fonts/malgunbd.ttf",
            "C:/Windows/Fonts/NotoSansCJK-Regular.ttc"
        };

        foreach (string fontPath in fontPaths)
        {
            if (!System.IO.File.Exists(fontPath))
            {
                continue;
            }

            Font sourceFont = new Font(fontPath);
            localizedFontAsset = CreateTmpFontAsset(sourceFont);
            if (localizedFontAsset != null)
            {
                return localizedFontAsset;
            }
        }

        fontLookupFailed = true;
        Debug.LogWarning("Korean/Japanese TMP font was not found. Install Malgun Gothic or add a TMP font fallback asset.");
        return null;
    }

    private static TMP_FontAsset CreateTmpFontAsset(Font sourceFont)
    {
        if (sourceFont == null)
        {
            return null;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic);
        if (fontAsset == null)
        {
            return null;
        }

        fontAsset.name = "Runtime Korean Japanese TMP Font";
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.TryAddCharacters(GetCharacterSet());
        return fontAsset;
    }

    private static void AddFallback(TMP_FontAsset targetFont, TMP_FontAsset fallbackFont)
    {
        if (targetFont == null || fallbackFont == null || targetFont == fallbackFont)
        {
            return;
        }

        if (targetFont.fallbackFontAssetTable == null)
        {
            targetFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
        }

        if (!targetFont.fallbackFontAssetTable.Contains(fallbackFont))
        {
            targetFont.fallbackFontAssetTable.Add(fallbackFont);
        }
    }

    private static void AddGlobalFallback(TMP_FontAsset fallbackFont)
    {
        if (globalFallbackApplied || fallbackFont == null)
        {
            return;
        }

        List<TMP_FontAsset> globalFallbacks = TMP_Settings.fallbackFontAssets;
        if (globalFallbacks != null && !globalFallbacks.Contains(fallbackFont))
        {
            globalFallbacks.Add(fallbackFont);
        }

        globalFallbackApplied = true;
    }

    private static string GetCharacterSet()
    {
        return "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /&-.?:>"
            + "가나다라마바사아자차카타파하"
            + "방로비비공개게임친구참가설정종료뒤로준비취소시작대기비어있음호스트플레이어"
            + "임무브리핑시설목표위험도팀인원상태신호조사알수없음대원중연결포톤생성실패끊김"
            + "시스템로그초기화음성채널목록제목만들기검색없습니다"
            + "ルームロビープライベート公開ゲームフレンド参加設定終了戻る準備取消開始待機中空き自分ホストプレイヤー"
            + "任務ブリーフィング施設目標脅威度チーム人数状態信号を調査不明クルー接続Photon作成失敗切断"
            + "システムログ初期化ボイスチャンネル一覧名ありませんキャンセル";
    }
}
