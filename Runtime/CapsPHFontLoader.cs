using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Object = UnityEngine.Object;

namespace Capstones.UnityEngineEx
{
    public static class CapsPHFontLoader
    {
        private readonly static List<Object> _PHDescs = new List<Object>();
        private readonly static List<Object> _RPDescs = new List<Object>();

        public static void LoadFont()
        {
            _PHDescs.Clear();
            _RPDescs.Clear();
            var infoasset = ResManager.LoadRes("font/info") as TextAsset;
            if (infoasset)
            {
                var info = infoasset.text;
                int phcnt;
                if (int.TryParse(info, out phcnt))
                {
                    for (int i = 0; i < phcnt; ++i)
                    {
                        var strindex = i.ToString();
                        var phname = "font/placeholder" + strindex;
                        var rpname = "font/replacement" + strindex;
                        var phdesc = ResManager.LoadRes(phname);
                        var rpdesc = ResManager.LoadRes(rpname);
                        _PHDescs.Add(phdesc);
                        _RPDescs.Add(rpdesc);
                        ResManager.MarkPermanent(phname);
                        ResManager.MarkPermanent(rpname);
                    }
                }
            }
            else
            {
                var phdesc = ResManager.LoadRes("font/placeholder");
                var rpdesc = ResManager.LoadRes("font/replacement");
                _PHDescs.Add(phdesc);
                _RPDescs.Add(rpdesc);
                ResManager.MarkPermanent("font/placeholder");
                ResManager.MarkPermanent("font/replacement");
            }
        }

        private class CapsPHFontLoaderBundleLoaderEx : ResManager.IAssetBundleLoaderEx
        {
            public bool LoadAssetBundle(string mod, string name, bool isContainingBundle, out ResManager.AssetBundleInfo bi)
            {
                bi = null;
                if (!isContainingBundle && name.EndsWith(".=.ab"))
                { // this special name means the assetbundle should not be dep of other bundle. for example, replaceable font.
                    return true;
                }
                return false;
            }
        }
        private static CapsPHFontLoaderBundleLoaderEx __CapsPHFontLoaderBundleLoaderEx = new CapsPHFontLoaderBundleLoaderEx();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnUnityStart()
        {
            ResManager.AssetBundleLoaderEx.Add(__CapsPHFontLoaderBundleLoaderEx);

            if (ResManager.ResLoader is ResManager.ClientResLoader)
            {
                ResManager.AddInitItem(ResManager.LifetimeOrders.PostResLoader - 5, LoadFont);
            }
        }
    }
}