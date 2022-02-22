using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors
{
    /// <summary>
    /// This actor controls when we remove GameObjects from memory, when we enable the
    /// automatic cache cleaning, and the estimate of the appropriate number of GameObjects
    /// loaded to make sure the streaming continue even when there are memory issues
    /// </summary>
    [Actor("26e2aeb0-be50-46a0-b35b-6b1be156b9ee")]
    public class GameObjectRemoverActor
    {
        const int k_AbsoluteMaxNbLoadedGameObjects = 100_000;

#pragma warning disable 649
        EventOutput<MaxLoadedGameObjectsChanged> m_MaxLoadedGameObjectsChangedOutput;
        NetOutput<SetAutomaticCacheCleaning> m_SetAutomaticCacheCleaningOutput;
        NetOutput<DestroyGameObjectLifecycle> m_DestroyGameObjectLifecycleOutput;
#pragma warning restore 649

        int m_NbLoadedGameObjects;
        HashSet<DynamicGuid> m_DisabledGameObjects = new HashSet<DynamicGuid>();
        HashSet<DynamicGuid> m_EnabledGameObjects = new HashSet<DynamicGuid>();
        Dictionary<DynamicGuid, int> m_VisibleInstances = new Dictionary<DynamicGuid, int>();

        long m_PreviousTotalAppMemory;
        int m_MaxNbGameObjects = k_AbsoluteMaxNbLoadedGameObjects;
        
        TimeSpan m_LastTime;

        [NetInput]
        void OnMemoryStateChanged(NetContext<MemoryStateChanged> ctx)
        {
            if (ctx.Data.IsMemoryLevelTooHigh)
            {
                // Let's just assume that our estimate was too high
                UpdateMaxNbGameObjects((int)(m_MaxNbGameObjects * 0.95f));
                return;
            }
            
            var isMediumOrHigher = ctx.Data.TotalAppMemory > ctx.Data.MediumThreshold;
            var wasLowerThanMedium = WasLower(ctx.Data.MediumThreshold);
            var wasLowerThanHigh = WasLower(ctx.Data.HighThreshold);
            var isHighOrHigher = ctx.Data.TotalAppMemory > ctx.Data.HighThreshold;
            var isMediumOrLower = ctx.Data.TotalAppMemory <= ctx.Data.HighThreshold;

            if (ctx.Data.TotalAppMemory <= ctx.Data.MediumThreshold && WasHigher(ctx.Data.MediumThreshold))
                m_SetAutomaticCacheCleaningOutput.Send(new SetAutomaticCacheCleaning(false));

            if (isMediumOrLower)
            {
                // Slowly increase the maximum number of loaded game objects
                if (m_MaxNbGameObjects < k_AbsoluteMaxNbLoadedGameObjects &&
                    m_NbLoadedGameObjects * 1.05f > m_MaxNbGameObjects)
                {
                    var curTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
                    if (m_LastTime < curTime + TimeSpan.FromSeconds(1))
                    {
                        m_LastTime = curTime;
                        UpdateMaxNbGameObjects((int)(m_MaxNbGameObjects * 1.05f));
                    }
                }
            }

            if (isMediumOrHigher)
            {
                // On medium reached, start cleaning caches for unused resources and
                // destroy game objects that are not visible in the current RTree result
                DestroyCachedGameObjects();

                if (wasLowerThanMedium)
                    m_SetAutomaticCacheCleaningOutput.Send(new SetAutomaticCacheCleaning(true));
            }
            
            if (isHighOrHigher)
            {
                // On high reached, block the maximum number of objects in memory and
                // start re-prioritizing objects to make sure we display what has the highest priority
                DestroyDeprioritizedGameObjects();

                if (wasLowerThanHigh)
                    UpdateMaxNbGameObjects(m_NbLoadedGameObjects);
            }

            m_PreviousTotalAppMemory = ctx.Data.TotalAppMemory;
        }

        [PipeInput]
        void OnGameObjectCreating(PipeContext<GameObjectCreating> ctx)
        {
            m_NbLoadedGameObjects += ctx.Data.GameObjectIds.Count;
            foreach (var go in ctx.Data.GameObjectIds)
                m_DisabledGameObjects.Add(go.Id);
            
            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectDestroying(PipeContext<GameObjectDestroying> ctx)
        {
            m_NbLoadedGameObjects -= ctx.Data.GameObjectIds.Count;
            foreach (var go in ctx.Data.GameObjectIds)
                m_DisabledGameObjects.Remove(go.Id);
            
            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectEnabling(PipeContext<GameObjectEnabling> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
            {
                m_DisabledGameObjects.Remove(go.Id);
                m_EnabledGameObjects.Add(go.Id);
            }
            
            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectDisabling(PipeContext<GameObjectDisabling> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
            {
                m_EnabledGameObjects.Remove(go.Id);
                m_DisabledGameObjects.Add(go.Id);
            }
            
            ctx.Continue();
        }

        [NetInput]
        void OnUpdateStreaming(NetContext<UpdateStreaming> ctx)
        {
            m_VisibleInstances.Clear();
            for (var i = 0; i < ctx.Data.VisibleInstances.Count; ++i)
                m_VisibleInstances.Add(ctx.Data.VisibleInstances[i], i);
        }

        bool WasLower(long current)
        {
            return m_PreviousTotalAppMemory < current;
        }

        bool WasHigher(long current)
        {
            return m_PreviousTotalAppMemory > current;
        }

        void UpdateMaxNbGameObjects(int newValue)
        {
            m_MaxNbGameObjects = newValue;
            m_MaxLoadedGameObjectsChangedOutput.Broadcast(new MaxLoadedGameObjectsChanged(m_MaxNbGameObjects));
        }
        
        void DestroyCachedGameObjects()
        {
            var toDestroy = new List<DynamicGuid>();
            foreach (var id in m_DisabledGameObjects)
            {
                var excludeFromScene = !m_VisibleInstances.TryGetValue(id, out var priority);
                if (excludeFromScene || priority > m_MaxNbGameObjects)
                    toDestroy.Add(id);
            }

            if (toDestroy.Count > 0)
                m_DestroyGameObjectLifecycleOutput.Send(new DestroyGameObjectLifecycle(toDestroy));
        }

        void DestroyDeprioritizedGameObjects()
        {
            var toDestroy = new List<DynamicGuid>();
            foreach (var id in m_EnabledGameObjects)
            {
                var excludeFromScene = !m_VisibleInstances.TryGetValue(id, out var priority);
                if (excludeFromScene || priority > m_MaxNbGameObjects)
                    toDestroy.Add(id);
            }

            if (toDestroy.Count > 0)
                m_DestroyGameObjectLifecycleOutput.Send(new DestroyGameObjectLifecycle(toDestroy));
        }
    }
}
