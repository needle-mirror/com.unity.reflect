using System;
using System.Text;
using UnityEditor.Experimental.EditorVR.Workspaces;
using UnityEngine;
using Unity.Labs.Utils;

#if INCLUDE_POLY_TOOLKIT
using PolyToolkit;
using System.Collections.Generic;
using Unity.Labs.EditorXR.Interfaces;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR.Utilities;
#endif

#if UNITY_EDITOR
[assembly: OptionalDependency("PolyToolkit.PolyApi", "INCLUDE_POLY_TOOLKIT")]
#endif

#if INCLUDE_POLY_TOOLKIT
namespace UnityEditor.Experimental.EditorVR.Modules
{
    public class PolyModule : IDelayedInitializationModule, IUsesFunctionalityInjection, IProvidesPoly
    {
        class RequestHandler
        {
            List<PolyGridAsset> m_Assets;
            Transform m_Container;
            Action<string> m_ListCallback;
            PolyModule m_PolyModule;

            public RequestHandler(PolyOrderBy orderBy, PolyMaxComplexityFilter complexity, PolyFormatFilter? format,
                PolyCategory category, int requestSize, List<PolyGridAsset> assets, Transform container,
                Action<string> listCallback, PolyModule polyModule, string nextPageToken = null)
            {
                m_Assets = assets;
                m_Container = container;
                m_ListCallback = listCallback;
                m_PolyModule = polyModule;

                var request = new PolyListAssetsRequest
                {
                    orderBy = orderBy,
                    maxComplexity = complexity,
                    formatFilter = format,
                    category = category
                };

                request.pageToken = nextPageToken;
                request.pageSize = requestSize;
                PolyApi.ListAssets(request, ListAssetsCallback);
            }

            // Callback invoked when the featured assets results are returned.
            void ListAssetsCallback(PolyStatusOr<PolyListAssetsResult> result)
            {
                if (!result.Ok)
                {
                    Debug.LogError("Failed to get featured assets. :( Reason: " + result.Status);
                    return;
                }

                if (m_ListCallback != null)
                    m_ListCallback(result.Value.nextPageToken);

                foreach (var asset in result.Value.assets)
                {
                    PolyGridAsset polyGridAsset;
                    var name = asset.name;
                    if (!k_AssetCache.TryGetValue(name, out polyGridAsset))
                    {
                        polyGridAsset = new PolyGridAsset(asset, m_Container);
                        m_PolyModule.InjectFunctionalitySingle(polyGridAsset);
                        k_AssetCache[name] = polyGridAsset;
                    }

                    m_Assets.Add(polyGridAsset);
                }
            }
        }

        const string k_APIKey = "QUl6YVN5QUZvMEp6ZVZZRFNDSURFa3hlWmdMNjg0OUM0MThoWlYw";

        static readonly Dictionary<string, PolyGridAsset> k_AssetCache = new Dictionary<string, PolyGridAsset>();

        Transform m_Container;

        public int initializationOrder { get { return 0; } }
        public int shutdownOrder { get { return 0; } }

#if !FI_AUTOFILL
        IProvidesFunctionalityInjection IFunctionalitySubscriber<IProvidesFunctionalityInjection>.provider { get; set; }
#endif

        public void LoadModule()
        {
            PolyApi.Init(new PolyAuthConfig(Encoding.UTF8.GetString(Convert.FromBase64String(k_APIKey)), "", ""));
        }

        public void UnloadModule()
        {
            k_AssetCache.Clear();
            PolyApi.Shutdown();
        }

        public void Initialize()
        {
            var moduleParent = ModuleLoaderCore.instance.GetModuleParent();
            m_Container = EditorXRUtils.CreateEmptyGameObject("Poly Prefabs", moduleParent.transform).transform;
        }

        public void Shutdown()
        {
            if (m_Container)
                UnityObjectUtils.Destroy(m_Container.gameObject);
        }

        public void GetAssetList(PolyOrderBy orderBy, PolyMaxComplexityFilter complexity, PolyFormatFilter? format,
            PolyCategory category, int requestSize, List<PolyGridAsset> assets, Action<string> listCallback,
            string nextPageToken = null)
        {
            new RequestHandler(orderBy, complexity, format,category, requestSize, assets, m_Container, listCallback, this, nextPageToken);
        }

        public void LoadProvider() { }

        public void ConnectSubscriber(object obj)
        {
#if !FI_AUTOFILL
            var polySubscriber = obj as IFunctionalitySubscriber<IProvidesPoly>;
            if (polySubscriber != null)
                polySubscriber.provider = this;
#endif
        }

        public void UnloadProvider() { }
    }
}
#endif
