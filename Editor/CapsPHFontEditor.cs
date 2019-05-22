using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Capstones.UnityEngineEx;

namespace Capstones.UnityEditorEx
{
    public static class CapsPHFontEditor
    {
        private static readonly Dictionary<string, string> _PHFontNameToAssetName = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _PHFontAssetNameToFontName = new Dictionary<string, string>();

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
                sw.Write(jo.ToString());
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

        [MenuItem("Assets/Create/Place Holder Font", priority = 1010)]
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

        private class CapsPHFontPostprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                bool dirty = false;
                if (importedAssets != null)
                {
                    for (int i = 0; i < importedAssets.Length; ++i)
                    {
                        var asset = importedAssets[i];
                        if (asset.EndsWith(".phf.asset"))
                        {
                            dirty = AddPHFont(asset) || dirty;
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
                    }
                }
                if (dirty)
                {
                    SaveCachedPHFonts();
                }
            }
        }
    }
}