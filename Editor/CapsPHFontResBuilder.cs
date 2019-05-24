using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Capstones.UnityEngineEx;

namespace Capstones.UnityEditorEx
{
    public class CapsPHFontResBuilder : CapsResBuilder.IResBuilderEx
    {
        private HashSet<string> _PHFonts;
        private HashSet<string> _PHFontDescs;
        private HashSet<string> _ReplacementFonts;
        private HashSet<string> _ReplacementDescs;

        public void Prepare()
        {
            CapsPHFontEditor.ReplaceAllPHFonts();
            _PHFonts = new HashSet<string>();
            _PHFontDescs = new HashSet<string>();
            _ReplacementFonts = new HashSet<string>();
            _ReplacementDescs = new HashSet<string>();
            foreach (var phpath in CapsPHFontEditor._PHFontAssetNameToFontName.Keys)
            {
                _PHFonts.Add(phpath);
                if (phpath.EndsWith(".otf"))
                {
                    var phdesc = phpath.Substring(0, phpath.Length - ".otf".Length) + ".phf.asset";
                    _PHFontDescs.Add(phdesc);
                }
            }
            foreach (var phpath in _PHFonts)
            {
                var deps = AssetDatabase.GetDependencies(phpath);
                if (deps != null)
                {
                    for (int i = 0; i < deps.Length; ++i)
                    {
                        var dep = deps[i];
                        if (!_PHFonts.Contains(dep))
                        {
                            _ReplacementFonts.Add(dep);
                        }
                    }
                }
            }
            _ReplacementDescs.UnionWith(CapsPHFontEditor._FontReplacementDescs.Keys);
        }
        public void Cleanup()
        {
            _PHFonts = null;
            _PHFontDescs = null;
            _ReplacementFonts = null;
            _ReplacementDescs = null;
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
        public string FormatBundleName(string asset, string mod, string dist, string norm)
        {
            _Building = null;
            if (_PHFonts.Contains(asset) || _PHFontDescs.Contains(asset) || _ReplacementFonts.Contains(asset) || _ReplacementDescs.Contains(asset))
            {
                _Building = new BuildingItemInfo()
                {
                    Asset = asset,
                    Mod = mod,
                    Dist = dist,
                    Norm = norm,
                    Bundle = "m-" + (mod ?? "") + "-d-" + (dist ?? "") + "-font.f.=.ab",
                };
                return _Building.Bundle;
            }
            return null;
        }
        public bool CreateItem(CapsResManifestNode node)
        {
            if (_Building != null)
            {
                return true;
            }
            return false;
        }
        public void ModifyItem(CapsResManifestItem item)
        {
            if (_Building != null)
            {
                var node = item.Node;
                var asset = _Building.Asset;
                string rootpath = "Assets/CapsRes/";
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

                if (_PHFontDescs.Contains(asset))
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
                }
                else if (_ReplacementDescs.Contains(asset))
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
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class CapsPHFontResBuilderEntry
    {
        private static CapsPHFontResBuilder _Builder = new CapsPHFontResBuilder();
        static CapsPHFontResBuilderEntry()
        {
            CapsResBuilder.ResBuilderEx.Add(_Builder);
        }
    }
}