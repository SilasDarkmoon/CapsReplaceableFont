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
        private HashSet<string> _PHFonts = new HashSet<string>();
        private HashSet<string> _PHFontDescs = new HashSet<string>();
        private HashSet<string> _ReplacementFonts = new HashSet<string>();
        private HashSet<string> _ReplacementDescs = new HashSet<string>();
        private string _InfoFile;
        private int _PlaceHolderIndex;
        private int _ReplacementIndex;

        public void Prepare(string output)
        {
            CapsPHFontEditor.ReplaceAllPHFonts();
            _PHFonts.Clear();
            _PHFontDescs.Clear();
            _ReplacementFonts.Clear();
            _ReplacementDescs.Clear();
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
            _PlaceHolderIndex = 0;
            _ReplacementIndex = 0;
        }
        public void Cleanup()
        {
            _InfoFile = null;
            _PlaceHolderIndex = 0;
            _ReplacementIndex = 0;
            _PHFonts.Clear();
            _PHFontDescs.Clear();
            _ReplacementFonts.Clear();
            _ReplacementDescs.Clear();
            CapsPHFontEditor.ReplaceRuntimePHFonts();
        }
        public void OnSuccess()
        {
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
                    newpath = rootpath + "placeholder" + (_PlaceHolderIndex++).ToString();
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
                    newpath = rootpath + "replacement" + (_ReplacementIndex++).ToString();
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

        public void GenerateBuildWork(string bundleName, IList<string> assets, ref AssetBundleBuild abwork, CapsResBuilder.CapsResBuildWork modwork, int abindex)
        {
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