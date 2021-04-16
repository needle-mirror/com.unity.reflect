using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Reflect.Collections
{
    public interface ISpatialCollection<TObject> : IDisposable
    {
        int ObjectCount { get; }
        int Depth { get; }
        Bounds Bounds { get; }

        void Search<T>(Func<TObject, bool> predicate,
            Func<TObject, float> prioritizer,
            int maxResultsCount,
            ICollection<T> results) where T : TObject;

        void Add(TObject obj);
        void Remove(TObject obj);

        // TODO: this should be more generic (not necessarily a tree)
        void DrawDebug(Gradient nodeGradient, Gradient objectGradient, float maxPriority, int maxDepth);
    }

    public interface ISpatialObject : IDisposable
    {
        Guid Id { get; }
        Vector3 Min { get; }
        Vector3 Max { get; }
        Vector3 Center { get; }
        float Priority { get; set; }
        bool IsVisible { get; set; }
        GameObject LoadedObject { get; set; }
    }

    public interface IDelayedSpatialPicker<T>
    {
        void DelayedPick(Ray ray, Action<List<T>> callback);
        void DelayedPick(Vector3[] samplePoints, int samplePointCount, Action<List<T>> callback);
        void DelayedPick(int distance, Action<List<T>> callback);
    }
}
