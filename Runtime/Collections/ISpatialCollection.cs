using System;
using System.Collections.Generic;
using Unity.Reflect.Actors;
using UnityEngine;

namespace Unity.Reflect.Collections
{
    public interface ISpatialCollection<TObject> : IDisposable
    {
        int ObjectCount { get; }
        int Depth { get; }
        Bounds Bounds { get; }

        void Search<T>(Predicate<ISpatialObject> predicate,
            Func<ISpatialObject, float> prioritizer,
            Action<T> onObjectMatch,
            int maxResultsCount = int.MaxValue,
            float nodePriorityThreshold = float.MaxValue) where T : TObject;

        void Add(TObject obj);
        void Remove(TObject obj);

        // TODO: this should be more generic (not necessarily a tree)
        void DrawDebug(Gradient nodeGradient, Gradient objectGradient, float maxPriority, int maxDepth);
    }

    public interface ISpatialObject : IDisposable
    {
        DynamicGuid Id { get; }
        EntryData Entry { get; }
        Vector3 Min { get; }
        Vector3 Max { get; }
        Vector3 Center { get; }
        float Priority { get; set; }
        bool IsVisible { get; set; }
        GameObject LoadedObject { get; set; }
    }

    [Obsolete("Please use `ISpatialPickerAsync<T>` instead.")]
    public interface ISpatialPicker<T>
    {
        void Pick(Ray ray, List<T> results, string[] flagsExcluded = null);
        void VRPick(Ray ray, List<T> results, string[] flagsExcluded = null);
        void Pick(Vector3[] samplePoints, int samplePointCount, List<T> results, string[] flagsExcluded = null);
        void Pick(float distance, List<T> results, Transform origin, string[] flagsExcluded = null);
    }

    public interface ISpatialPickerAsync<T>
    {
        void Pick(Ray ray, Action<List<T>> callback, string[] flagsExcluded = null);
        void Pick(Vector3[] samplePoints, int samplePointCount, Action<List<T>> callback, string[] flagsExcluded = null);
        void Pick(Vector3 origin, float distance, Action<List<T>> callback, string[] flagsExcluded = null);
    }
}
