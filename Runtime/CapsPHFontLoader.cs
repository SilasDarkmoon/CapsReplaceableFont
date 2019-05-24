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
        private static Object _PHDesc;
        private static Object _RDesc;

        public static void LoadFont()
        {
            _PHDesc = ResManager.LoadRes("font/placeholder");
            _RDesc = ResManager.LoadRes("font/replacement");
        }

        [RuntimeInitializeOnLoadMethod]
        private static void OnUnityStart()
        {
#if !UNITY_EDITOR
            ResManager.AddInitItem(ResManager.LifetimeOrders.PostResLoader - 5, LoadFont);
#endif
        }
    }
}