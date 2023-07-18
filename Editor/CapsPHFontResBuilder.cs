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
    public class CapsPHFontResBuilder : CapsResBuilder.BaseResBuilderEx<CapsPHFontResBuilder>
    {
        private static HierarchicalInitializer _Initializer = new HierarchicalInitializer(0);

        private Dictionary<string, int> _PHFonts = new Dictionary<string, int>();
        private Dictionary<string, int> _PHFontDescs = new Dictionary<string, int>();
        private Dictionary<string, int> _ReplacementFonts = new Dictionary<string, int>();
        private Dictionary<string, int> _ReplacementDescs = new Dictionary<string, int>();
        private string _InfoFile;

        public override void Prepare(string output)
        {
            CapsPHFontEditor.ClearAndRebuildCache();
            CapsPHFontEditor.ReplaceAllPHFonts();
            _PHFonts.Clear();
            _PHFontDescs.Clear();
            _ReplacementFonts.Clear();
            _ReplacementDescs.Clear();
            int phindex = 0;
            foreach (var phpath in CapsPHFontEditor._PHFontAssetNameToFontName.Keys)
            {
                var curphindex = phindex++;
                _PHFonts[phpath] = curphindex;
                if (phpath.EndsWith(".otf"))
                {
                    var phdesc = phpath.Substring(0, phpath.Length - ".otf".Length) + ".phf.asset";
                    _PHFontDescs[phdesc] = curphindex;
                }
            }
            foreach (var kvpph in _PHFonts)
            {
                var phpath = kvpph.Key;
                var curphindex = kvpph.Value;
                var deps = AssetDatabase.GetDependencies(phpath);
                if (deps != null)
                {
                    for (int i = 0; i < deps.Length; ++i)
                    {
                        var dep = deps[i];
                        if (!_PHFonts.ContainsKey(dep))
                        {
                            _ReplacementFonts[dep] = curphindex;
                        }
                    }
                }
            }
            foreach (var kvprpinfo in CapsPHFontEditor._FontReplacementDescs)
            {
                var rpdescpath = kvprpinfo.Key;
                var curphindex = _PHFonts[CapsPHFontEditor._PHFontNameToAssetName[kvprpinfo.Value.PlaceHolderFontName]];
                _ReplacementDescs[rpdescpath] = curphindex;
            }

            var curmod = CapsEditorUtils.__MOD__;
            var infofile = "Assets/Mods/" + curmod + "/CapsRes/Build/phfinfo.txt";
            if (_PHFontDescs.Count == 0)
            {
                _InfoFile = null;
                PlatDependant.DeleteFile(infofile);
            }
            else
            {
                _InfoFile = infofile;
                PlatDependant.WriteAllText(infofile, _PHFontDescs.Count.ToString());
            }
        }
        public override void Cleanup()
        {
            _InfoFile = null;
            _PHFonts.Clear();
            _PHFontDescs.Clear();
            _ReplacementFonts.Clear();
            _ReplacementDescs.Clear();
            CapsPHFontEditor.ReplaceRuntimePHFonts();
        }

        private class BuildingItemInfo
        {
            public string Asset;
            public string Mod;
            public string Dist;
            public string Norm;
            public string Bundle;
        }
        private BuildingItemInfo _Building;
        public override string FormatBundleName(string asset, string mod, string dist, string norm)
        {
            _Building = null;
            if (string.Equals(asset, _InfoFile) ||  _PHFonts.ContainsKey(asset) || _PHFontDescs.ContainsKey(asset) || _ReplacementFonts.ContainsKey(asset) || _ReplacementDescs.ContainsKey(asset))
            {
                _Building = new BuildingItemInfo()
                {
                    Asset = asset,
                    Mod = mod,
                    Dist = dist,
                    Norm = norm,
                    Bundle = "m-" + (mod ?? "").ToLower() + "-d-" + (dist ?? "").ToLower() + "-font.f.=.ab",
                };
                return _Building.Bundle;
            }
            return null;
        }
        public override bool CreateItem(CapsResManifestNode node)
        {
            if (_Building != null)
            {
                return true;
            }
            return false;
        }
        public override void ModifyItem(CapsResManifestItem item)
        {
            if (_Building != null)
            {
                string rootpath = "Assets/CapsRes/";
                var asset = _Building.Asset;
                var node = item.Node;
                bool inPackage = false;
                if (asset.StartsWith("Assets/Mods/") || (inPackage = asset.StartsWith("Packages/")))
                {
                    int index;
                    if (inPackage)
                    {
                        index = asset.IndexOf('/', "Packages/".Length);
                    }
                    else
                    {
                        index = asset.IndexOf('/', "Assets/Mods/".Length);
                    }
                    if (index > 0)
                    {
                        rootpath = asset.Substring(0, index) + "/CapsRes/";
                    }
                }
                var dist = _Building.Dist;
                if (string.IsNullOrEmpty(dist))
                {
                    rootpath += "font/";
                }
                else
                {
                    rootpath = rootpath + "dist/" + dist + "/font/";
                }

                var newpath = rootpath + node.PPath;
                CapsResManifestNode newnode = item.Manifest.AddOrGetItem(newpath);
                var newitem = new CapsResManifestItem(newnode);
                newitem.Type = (int)CapsResManifestItemType.Redirect;
                newitem.BRef = item.BRef;
                newitem.Ref = item;
                newnode.Item = newitem;

                if (string.Equals(asset, _InfoFile))
                {
                    newpath = rootpath + "info";
                    newnode = item.Manifest.AddOrGetItem(newpath);
                    if (newnode.Item == null)
                    {
                        newitem = new CapsResManifestItem(newnode);
                        newitem.Type = (int)CapsResManifestItemType.Redirect;
                        newitem.BRef = item.BRef;
                        newitem.Ref = item;
                        newnode.Item = newitem;
                    }
                }
                else if (_PHFonts.ContainsKey(asset))
                {
                    newpath = rootpath + "font";
                    newnode = item.Manifest.AddOrGetItem(newpath);
                    if (newnode.Item == null)
                    {
                        newitem = new CapsResManifestItem(newnode);
                        newitem.Type = (int)CapsResManifestItemType.Redirect;
                        newitem.BRef = item.BRef;
                        newitem.Ref = item;
                        newnode.Item = newitem;
                    }
                    newpath = rootpath + "font" + _PHFonts[asset].ToString();
                    newnode = item.Manifest.AddOrGetItem(newpath);
                    if (newnode.Item == null)
                    {
                        newitem = new CapsResManifestItem(newnode);
                        newitem.Type = (int)CapsResManifestItemType.Redirect;
                        newitem.BRef = item.BRef;
                        newitem.Ref = item;
                        newnode.Item = newitem;
                    }
                }
                else if (_PHFontDescs.ContainsKey(asset))
                {
                    newpath = rootpath + "placeholder";
                    newnode = item.Manifest.AddOrGetItem(newpath);
                    if (newnode.Item == null)
                    {
                        newitem = new CapsResManifestItem(newnode);
                        newitem.Type = (int)CapsResManifestItemType.Redirect;
                        newitem.BRef = item.BRef;
                        newitem.Ref = item;
                        newnode.Item = newitem;
                    }
                    newpath = rootpath + "placeholder" + _PHFontDescs[asset].ToString();
                    newnode = item.Manifest.AddOrGetItem(newpath);
                    if (newnode.Item == null)
                    {
                        newitem = new CapsResManifestItem(newnode);
                        newitem.Type = (int)CapsResManifestItemType.Redirect;
                        newitem.BRef = item.BRef;
                        newitem.Ref = item;
                        newnode.Item = newitem;
                    }
                }
                else if (_ReplacementDescs.ContainsKey(asset))
                {
                    newpath = rootpath + "replacement";
                    newnode = item.Manifest.AddOrGetItem(newpath);
                    if (newnode.Item == null)
                    {
                        newitem = new CapsResManifestItem(newnode);
                        newitem.Type = (int)CapsResManifestItemType.Redirect;
                        newitem.BRef = item.BRef;
                        newitem.Ref = item;
                        newnode.Item = newitem;
                    }
                    newpath = rootpath + "replacement" + _ReplacementDescs[asset].ToString();
                    newnode = item.Manifest.AddOrGetItem(newpath);
                    if (newnode.Item == null)
                    {
                        newitem = new CapsResManifestItem(newnode);
                        newitem.Type = (int)CapsResManifestItemType.Redirect;
                        newitem.BRef = item.BRef;
                        newitem.Ref = item;
                        newnode.Item = newitem;
                    }
                }
            }
        }
    }
}