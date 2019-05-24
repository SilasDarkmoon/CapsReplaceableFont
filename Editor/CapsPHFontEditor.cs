using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Capstones.UnityEngineEx;

namespace Capstones.UnityEditorEx
{
    [InitializeOnLoad]
    public static class CapsPHFontEditor
    {
        internal static readonly Dictionary<string, string> _PHFontNameToAssetName = new Dictionary<string, string>();
        internal static readonly Dictionary<string, string> _PHFontAssetNameToFontName = new Dictionary<string, string>();

        internal class FontReplacement
        {
            public string PlaceHolderFontName;
            public Font SubstituteFont;

            public string DescAssetPath;
            public string Mod;
            public string Dist;
        }
        // ph-font-name -> List<FontReplacement>
        internal static readonly Dictionary<string, List<FontReplacement>> _FontReplacements = new Dictionary<string, List<FontReplacement>>();
        // CapsFontReplacement's path -> FontReplacement
        internal static readonly Dictionary<string, FontReplacement> _FontReplacementDescs = new Dictionary<string, FontReplacement>();

        public static string GetDefaultPHFontAssetPath()
        {
            string fname = "CapstonesPHFont99999";
            string asset = null;
            foreach (var kvp in _PHFontNameToAssetName)
            {
                if (string.Compare(kvp.Key, fname) < 0)
                {
                    fname = kvp.Key;
                    asset = kvp.Value;
                }
            }
            return asset ?? "CapstonesPHFont00001";
        }

        static CapsPHFontEditor()
        {
            if (PlatDependant.IsFileExist("EditorOutput/Runtime/phfont.txt"))
            {
                ParseCachedPHFonts();
                if (CheckCachedPHFonts())
                {
                    SaveCachedPHFonts();
                }
            }
            else
            {
                CheckAllPHFonts();
                SaveCachedPHFonts();
            }

            CapsModEditor.ShouldAlreadyInit();
            CapsPackageEditor.OnPackagesChanged += () =>
            {
                if (PlatDependant.IsFileExist("EditorOutput/Runtime/rfont.txt"))
                {
                    if (LoadCachedReplacement())
                    {
                        SaveCachedReplacement();
                    }
                }
                else
                {
                    CheckAllReplacements();
                    SaveCachedReplacement();
                }

                ReplaceRuntimePHFonts();
            };
            CapsDistributeEditor.OnDistributeFlagsChanged += ReplaceRuntimePHFonts;
        }
        private static void ParseCachedPHFonts()
        {
            _PHFontNameToAssetName.Clear();
            _PHFontAssetNameToFontName.Clear();
            if (PlatDependant.IsFileExist("EditorOutput/Runtime/phfont.txt"))
            {
                string json = "";
                using (var sr = PlatDependant.OpenReadText("EditorOutput/Runtime/phfont.txt"))
                {
                    json = sr.ReadToEnd();
                }
                try
                {
                    var jo = new JSONObject(json);
                    try
                    {
                        var phf = jo["phfonts"] as JSONObject;
                        if (phf != null && phf.type == JSONObject.Type.OBJECT)
                        {
                            for (int i = 0; i < phf.list.Count; ++i)
                            {
                                var key = phf.keys[i];
                                var val = phf.list[i].str;
                                _PHFontNameToAssetName[key] = val;
                                _PHFontAssetNameToFontName[val] = key;
                            }
                        }
                    }
                    catch { }
                }
                catch { }
            }
        }
        private static void SaveCachedPHFonts()
        {
            var jo = new JSONObject(JSONObject.Type.OBJECT);
            var phfontsnode = new JSONObject(_PHFontNameToAssetName);
            jo["phfonts"] = phfontsnode;
            using (var sw = PlatDependant.OpenWriteText("EditorOutput/Runtime/phfont.txt"))
            {
                sw.Write(jo.ToString(true));
            }
        }
        private static bool CheckCachedPHFonts()
        {
            bool dirty = false;
            var assets = _PHFontAssetNameToFontName.Keys.ToArray();
            foreach (var font in assets)
            {
                if (!PlatDependant.IsFileExist(font))
                {
                    if (!CachePHFont(font))
                    {
                        var path = _PHFontNameToAssetName[font];
                        _PHFontNameToAssetName.Remove(font);
                        _PHFontAssetNameToFontName.Remove(path);
                        dirty = true;
                    }
                }
            }
            return dirty;
        }
        private static void CheckAllPHFonts()
        {
            var assets = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < assets.Length; ++i)
            {
                var asset = assets[i];
                if (asset.EndsWith(".phf.asset"))
                {
                    AddPHFont(asset);
                }
            }
        }
        private static bool AddPHFont(string descasset)
        {
            if (descasset.EndsWith(".phf.asset"))
            {
                var fontasset = descasset.Substring(0, descasset.Length - ".phf.asset".Length);
                var fontname = System.IO.Path.GetFileName(fontasset);
                fontasset += ".otf";
                bool dirty = !_PHFontNameToAssetName.ContainsKey(fontname);
                _PHFontNameToAssetName[fontname] = fontasset;
                _PHFontAssetNameToFontName[fontasset] = fontname;

                dirty = CachePHFont(fontasset) || dirty;
                return dirty;
            }
            return false;
        }
        private static bool RemovePHFontRecord(string descasset)
        {
            if (descasset.EndsWith(".phf.asset"))
            {
                var fontasset = descasset.Substring(0, descasset.Length - ".phf.asset".Length);
                var fontname = System.IO.Path.GetFileName(fontasset);
                fontasset += ".otf";
                bool dirty = _PHFontNameToAssetName.ContainsKey(fontname);
                _PHFontNameToAssetName.Remove(fontname);
                _PHFontAssetNameToFontName.Remove(fontasset);

                DeletePHFont(fontasset);

                return dirty;
            }
            return false;
        }
        private static bool CachePHFont(string fontasset)
        {
            var src = fontasset + ".~";
            var meta = fontasset + ".meta";
            var srcmeta = fontasset + ".meta.~";

            if (PlatDependant.IsFileExist(src) && !PlatDependant.IsFileExist(fontasset))
            {
                PlatDependant.CopyFile(src, fontasset);
                if (PlatDependant.IsFileExist(srcmeta))
                {
                    PlatDependant.CopyFile(srcmeta, meta);
                }
                AssetDatabase.ImportAsset(fontasset);
                return true;
            }
            return false;
        }
        private static void DeletePHFont(string fontasset)
        {
            var src = fontasset + ".~";
            var meta = fontasset + ".meta";
            var srcmeta = fontasset + ".meta.~";
            PlatDependant.DeleteFile(src);
            PlatDependant.DeleteFile(srcmeta);

            if (PlatDependant.IsFileExist(fontasset))
            {
                AssetDatabase.DeleteAsset(fontasset);
            }
        }

        private static bool AddFontReplacement(string asset)
        {
            if (PlatDependant.IsFileExist(asset))
            {
                try
                {
                    var desc = AssetDatabase.LoadAssetAtPath<CapsFontReplacement>(asset);
                    if (desc)
                    {
                        try
                        {
                            var phname = desc.PlaceHolderFontName;
                            var rfont = desc.SubstituteFont;
                            string type, mod, dist;
                            string norm = ResManager.GetAssetNormPath(asset, out type, out mod, out dist);

                            if (!_FontReplacementDescs.ContainsKey(asset))
                            {
                                var info = new FontReplacement()
                                {
                                    PlaceHolderFontName = phname,
                                    SubstituteFont = rfont,
                                    DescAssetPath = asset,
                                    Mod = mod,
                                    Dist = dist,
                                };
                                List<FontReplacement> list;
                                if (!_FontReplacements.TryGetValue(phname, out list))
                                {
                                    list = new List<FontReplacement>();
                                    _FontReplacements[phname] = list;
                                }
                                list.Add(info);

                                _FontReplacementDescs[asset] = info;
                                return true;
                            }
                            else
                            {
                                var info = _FontReplacementDescs[asset];
                                if (info.PlaceHolderFontName != desc.name)
                                {
                                    RemoveFontReplacement(asset);
                                    AddFontReplacement(asset);
                                    return true;
                                }
                            }
                        }
                        finally
                        {
                            Resources.UnloadAsset(desc);
                        }
                    }
                }
                catch { }
            }
            return false;
        }
        private static bool RemoveFontReplacement(string asset)
        {
            FontReplacement info;
            if (_FontReplacementDescs.TryGetValue(asset, out info))
            {
                _FontReplacementDescs.Remove(asset);

                List<FontReplacement> list;
                if (_FontReplacements.TryGetValue(info.PlaceHolderFontName, out list))
                {
                    for (int i = 0; i < list.Count; ++i)
                    {
                        if (list[i].DescAssetPath == asset)
                        {
                            list.RemoveAt(i--);
                        }
                    }
                    if (list.Count == 0)
                    {
                        _FontReplacements.Remove(info.PlaceHolderFontName);
                    }
                }
                return true;
            }
            return false;
        }
        private static bool LoadCachedReplacement()
        {
            bool dirty = false;
            _FontReplacements.Clear();
            _FontReplacementDescs.Clear();
            if (PlatDependant.IsFileExist("EditorOutput/Runtime/rfont.txt"))
            {
                string json = "";
                using (var sr = PlatDependant.OpenReadText("EditorOutput/Runtime/rfont.txt"))
                {
                    json = sr.ReadToEnd();
                }
                try
                {
                    var jo = new JSONObject(json);
                    try
                    {
                        var phr = jo["replacements"] as JSONObject;
                        if (phr != null && phr.type == JSONObject.Type.ARRAY)
                        {
                            for (int i = 0; i < phr.list.Count; ++i)
                            {
                                var val = phr.list[i].str;
                                dirty |= !AddFontReplacement(val);
                            }
                        }
                    }
                    catch { }
                }
                catch { }
            }
            return dirty;
        }
        private static void SaveCachedReplacement()
        {
            var jo = new JSONObject(JSONObject.Type.OBJECT);
            var rnode = new JSONObject(JSONObject.Type.ARRAY);
            jo["replacements"] = rnode;
            foreach (var asset in _FontReplacementDescs.Keys)
            {
                rnode.list.Add(new JSONObject(JSONObject.Type.STRING) { str = asset });
            }
            using (var sw = PlatDependant.OpenWriteText("EditorOutput/Runtime/rfont.txt"))
            {
                sw.Write(jo.ToString(true));
            }
        }
        private static void CheckAllReplacements()
        {
            var assets = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < assets.Length; ++i)
            {
                var asset = assets[i];
                if (asset.EndsWith(".fr.asset"))
                {
                    AddFontReplacement(asset);
                }
            }
        }

        private static Dictionary<string, Dictionary<string, FontReplacement>> GetFontReplacementDMFDict(string fname)
        {
            // dist -> mod -> FontReplacement
            Dictionary<string, Dictionary<string, FontReplacement>> dmfr = new Dictionary<string, Dictionary<string, FontReplacement>>();
            List<FontReplacement> list;
            if (_FontReplacements.TryGetValue(fname, out list))
            {
                for (int i = 0; i < list.Count; ++i)
                {
                    var info = list[i];
                    var mod = info.Mod ?? "";
                    var dist = info.Dist ?? "";
                    // check critical mod
                    var moddesc = ResManager.GetDistributeDesc(mod);
                    var inPackage = (info.DescAssetPath ?? "").StartsWith("Packages/");
                    bool isMainPackage = inPackage && !CapsModEditor.ShouldTreatPackageAsMod(CapsModEditor.GetPackageName(mod));
                    if (moddesc == null || !moddesc.IsOptional || isMainPackage)
                    {
                        mod = "";
                    }

                    Dictionary<string, FontReplacement> mdict;
                    if (!dmfr.TryGetValue(dist, out mdict))
                    {
                        mdict = new Dictionary<string, FontReplacement>();
                        dmfr[dist] = mdict;
                    }
                    mdict[mod] = info;
                }
            }
            return dmfr;
        }
        private static List<string> GetFallbackFontNames(string fname, IList<string> flags)
        {
            // dist -> mod -> FontReplacement
            var dmfr = GetFontReplacementDMFDict(fname);
            List<string> list = new List<string>() { fname };
            if (flags != null)
            {
                for (int i = flags.Count - 1; i >= 0; --i)
                {
                    var flag = flags[i];
                    Dictionary<string, FontReplacement> mdict;
                    if (dmfr.TryGetValue(flag, out mdict))
                    {
                        for (int j = flags.Count - 1; j >= 0; --j)
                        {
                            var mod = flags[j];
                            if (mdict.ContainsKey(mod))
                            {
                                var info = mdict[mod];
                                if (info != null && info.SubstituteFont != null)
                                {
                                    var path = AssetDatabase.GetAssetPath(info.SubstituteFont);
                                    if (!string.IsNullOrEmpty(path))
                                    {
                                        var fi = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
                                        if (fi != null)
                                        {
                                            var rfname = fi.fontTTFName;
                                            if (!string.IsNullOrEmpty(rfname))
                                            {
                                                list.Add(rfname);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return list;
        }
        private static bool ListEquals(IList<string> lsta, IList<string> lstb)
        {
            if (lsta == null || lsta.Count == 0)
            {
                return lstb == null || lstb.Count == 0;
            }
            if (lstb == null || lstb.Count == 0)
            {
                return false;
            }
            if (lsta.Count != lstb.Count)
            {
                return false;
            }
            for (int i = 0; i < lsta.Count; ++i)
            {
                if (lsta[i] != lstb[i])
                {
                    return false;
                }
            }
            return true;
        }
        public static void ReplaceRuntimePHFonts()
        {
            foreach (var kvp in _PHFontNameToAssetName)
            {
                List<string> flags = new List<string>() { "" };
                flags.AddRange(ResManager.GetValidDistributeFlags());
                var list = GetFallbackFontNames(kvp.Key, flags.ToArray());

                var fiph = AssetImporter.GetAtPath(kvp.Value) as TrueTypeFontImporter;
                if (fiph != null)
                {
                    if (!ListEquals(fiph.fontNames, list))
                    {
                        fiph.fontNames = list.ToArray();
                        fiph.fontReferences = null;
                        EditorUtility.SetDirty(fiph);
                        AssetDatabase.WriteImportSettingsIfDirty(kvp.Value);
                        AssetDatabase.ImportAsset(kvp.Value);
                        AssetDatabase.Refresh();
                        Resources.UnloadAsset(AssetDatabase.LoadAssetAtPath<Font>(kvp.Value));
                    }
                }
            }
        }
        public static void ReplaceAllPHFonts()
        {
            foreach (var kvp in _PHFontNameToAssetName)
            {
                List<string> flags = new List<string>() { "" };
                var allflags = CapsDistributeEditor.GetOptionalDistributes();
                for (int i = 0; i < allflags.Length; ++i)
                {
                    var flag = allflags[i];
                    if (!ResManager.GetValidDistributeFlagsSet().Contains(flag))
                    {
                        flags.Add(flag);
                    }
                }
                flags.AddRange(ResManager.GetValidDistributeFlags());
                var list = GetFallbackFontNames(kvp.Key, flags.ToArray());

                var fiph = AssetImporter.GetAtPath(kvp.Value) as TrueTypeFontImporter;
                if (fiph != null)
                {
                    if (!ListEquals(fiph.fontNames, list))
                    {
                        fiph.fontNames = list.ToArray();
                        fiph.fontReferences = null;
                        EditorUtility.SetDirty(fiph);
                        AssetDatabase.WriteImportSettingsIfDirty(kvp.Value);
                        AssetDatabase.ImportAsset(kvp.Value);
                        AssetDatabase.Refresh();
                    }
                }
            }
        }

        [MenuItem("Assets/Create/Place Holder Font", priority = 1011)]
        public static void CreatePlaceHolderFont()
        {
            var srcpath = CapsModEditor.GetPackageOrModRoot(CapsEditorUtils.__MOD__);
            srcpath += "/~Tools~/CapstonesPlaceHolder.otf";

            if (PlatDependant.IsFileExist(srcpath))
            {
                var sids = Selection.instanceIDs;
                if (sids != null && sids.Length > 0)
                {
                    bool found = false;
                    int fid = 0;
                    for (int i = sids.Length - 1; i >= 0; --i)
                    {
                        var sid = sids[i];
                        if (ProjectWindowUtil.IsFolder(sid))
                        {
                            fid = sid;
                            found = true;
                            break;
                        }
                    }
                    string folder;
                    if (!found)
                    {
                        folder = ProjectWindowUtil.GetContainingFolder(AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(sids[0])));
                    }
                    else
                    {
                        folder = AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(fid));
                    }
                    var asset = folder;
                    folder = CapsModEditor.GetAssetPath(folder); // this seems to be useless. Unity's System.IO lib can handle path like Packages/cn.capstones.phfont/xxx

                    string fontName = "";
                    string fileName;

                    for (int i = 1; i <= 99999; ++i)
                    {
                        fontName = "CapstonesPHFont" + i.ToString("00000");
                        if (!_PHFontNameToAssetName.ContainsKey(fontName))
                        {
                            break;
                        }
                    }
                    fileName = fontName;
                    if (PlatDependant.IsFileExist(folder + "/" + fileName + ".otf"))
                    {
                        for (int i = 0; ; ++i)
                        {
                            fileName = fontName + "_" + i;
                            if (!PlatDependant.IsFileExist(folder + "/" + fileName + ".otf"))
                            {
                                break;
                            }
                        }
                    }

                    PlatDependant.CopyFile(srcpath, folder + "/" + fileName + ".otf");

                    // Modify the otf file.
                    using (var stream = PlatDependant.OpenAppend(folder + "/" + fileName + ".otf"))
                    {
                        stream.Seek(0x3cc, System.IO.SeekOrigin.Begin);
                        var buffer = System.Text.Encoding.ASCII.GetBytes(fontName);
                        stream.Write(buffer, 0, buffer.Length);
                        stream.Seek(0x4d0, System.IO.SeekOrigin.Begin);
                        buffer = System.Text.Encoding.BigEndianUnicode.GetBytes(fontName);
                        stream.Write(buffer, 0, buffer.Length);
                    }
                    AssetDatabase.ImportAsset(asset + "/" + fileName + ".otf");

                    PlatDependant.CopyFile(asset + "/" + fileName + ".otf", asset + "/" + fileName + ".otf.~");
                    PlatDependant.CopyFile(asset + "/" + fileName + ".otf.meta", asset + "/" + fileName + ".otf.meta.~");

                    AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CapsPHFontDesc>(), asset + "/" + fileName + ".phf.asset");
                    AssetDatabase.ImportAsset(asset + "/" + fileName + ".phf.asset");
                    AddPHFont(asset + "/" + fileName + ".phf.asset");
                    SaveCachedPHFonts();
                }
            }
        }

        [MenuItem("Assets/Create/Font Replacement", priority = 1010)]
        public static void CreateFontReplacement()
        {
            var sids = Selection.instanceIDs;
            if (sids != null && sids.Length > 0)
            {
                bool found = false;
                Font selectedFont = null;
                int fid = 0;
                for (int i = sids.Length - 1; i >= 0; --i)
                {
                    var sid = sids[i];
                    var obj = EditorUtility.InstanceIDToObject(sid);
                    if (obj is Font)
                    {
                        var font = obj as Font;
                        try
                        {
                            var fi = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(font)) as TrueTypeFontImporter;
                            if (fi != null)
                            {
                                if (!_PHFontNameToAssetName.ContainsKey(fi.fontTTFName))
                                {
                                    selectedFont = font;
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }
                for (int i = sids.Length - 1; i >= 0; --i)
                {
                    var sid = sids[i];
                    if (ProjectWindowUtil.IsFolder(sid))
                    {
                        fid = sid;
                        found = true;
                        break;
                    }
                }
                string folder;
                if (!found)
                {
                    folder = ProjectWindowUtil.GetContainingFolder(AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(sids[0])));
                }
                else
                {
                    folder = AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(fid));
                }
                var asset = folder;
                folder = CapsModEditor.GetAssetPath(folder); // this seems to be useless. Unity's System.IO lib can handle path like Packages/cn.capstones.phfont/xxx

                var desc = ScriptableObject.CreateInstance<CapsFontReplacement>();
                desc.PlaceHolderFontName = GetFontReplacementPHFontName(asset) ?? "CapstonesPHFont00000";
                desc.SubstituteFont = selectedFont;

                var fileName = "FontReplacement";
                if (PlatDependant.IsFileExist(folder + "/" + fileName + ".fr.asset"))
                {
                    for (int i = 0; ; ++i)
                    {
                        fileName = "FontReplacement" + i;
                        if (!PlatDependant.IsFileExist(folder + "/" + fileName + ".fr.asset"))
                        {
                            break;
                        }
                    }
                }

                AssetDatabase.CreateAsset(desc, asset + "/" + fileName + ".fr.asset");
                AssetDatabase.ImportAsset(asset + "/" + fileName + ".fr.asset");
            }
        }

        private static string GetFontReplacementPHFontName(string asset)
        {
            if (_PHFontNameToAssetName.Count == 0)
            {
                Debug.LogError("No Place Holder Font to Replace!");
                return null;
            }

            string type, mod, dist;
            string norm = ResManager.GetAssetNormPath(asset, out type, out mod, out dist);

            FontReplacement found = null;
            foreach (var fname in _PHFontNameToAssetName.Keys)
            {
                bool exist = false;
                List<FontReplacement> list;
                if (_FontReplacements.TryGetValue(fname, out list))
                {
                    for (int i = 0; i < list.Count; ++i)
                    {
                        var info = list[i];
                        if (info.Mod == mod && info.Dist == dist)
                        {
                            found = info;
                            exist = true;
                            break;
                        }
                    }
                }
                if (!exist)
                {
                    return fname;
                }
            }
            Debug.LogError("All Place Holder Font are already replaced in current Mod&Dist! See " + found.DescAssetPath);
            return null;
        }

        private class CapsPHFontPostprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                bool dirty = false;
                bool rdirty = false;
                if (importedAssets != null)
                {
                    for (int i = 0; i < importedAssets.Length; ++i)
                    {
                        var asset = importedAssets[i];
                        if (asset.EndsWith(".phf.asset"))
                        {
                            dirty = AddPHFont(asset) || dirty;
                        }
                        else if (asset.EndsWith(".fr.asset"))
                        {
                            rdirty |= AddFontReplacement(asset);
                        }
                    }
                }
                if (deletedAssets != null)
                {
                    for (int i = 0; i < deletedAssets.Length; ++i)
                    {
                        var asset = deletedAssets[i];
                        if (asset.EndsWith(".phf.asset"))
                        {
                            dirty = RemovePHFontRecord(asset) || dirty;
                        }
                        else if (asset.EndsWith(".fr.asset"))
                        {
                            rdirty |= RemoveFontReplacement(asset);
                        }
                    }
                }
                if (movedAssets != null)
                {
                    for (int i = 0; i < movedAssets.Length; ++i)
                    {
                        var asset = importedAssets[i];
                        if (asset.EndsWith(".phf.asset"))
                        {
                            dirty = AddPHFont(asset) || dirty;
                        }
                        else if (asset.EndsWith(".fr.asset"))
                        {
                            rdirty |= AddFontReplacement(asset);
                        }
                    }
                }
                if (movedFromAssetPaths != null)
                {
                    for (int i = 0; i < movedFromAssetPaths.Length; ++i)
                    {
                        var asset = movedFromAssetPaths[i];
                        if (asset.EndsWith(".phf.asset"))
                        {
                            dirty = RemovePHFontRecord(asset) || dirty;
                        }
                        else if (asset.EndsWith(".fr.asset"))
                        {
                            rdirty |= RemoveFontReplacement(asset);
                        }
                    }
                }
                if (dirty)
                {
                    SaveCachedPHFonts();
                }
                if (rdirty)
                {
                    SaveCachedReplacement();
                }
            }
        }
    }
}