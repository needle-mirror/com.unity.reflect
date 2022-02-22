using System;
using System.Collections.Generic;
using Unity.Reflect.ActorFramework;
using UnityEngine;

namespace Unity.Reflect.Actors
{
    [Actor("ed17f959-6785-46a8-9d2c-6be5290a8a94")]
    public class GameObjectDependencyTrackerActor
    {
#pragma warning disable 649
        NetOutput<ReleaseUnityMaterial> m_ReleaseUnityMaterialOutput;
        NetOutput<ReleaseUnityMesh> m_ReleaseUnityMeshOutput;
#pragma warning restore 649

        Dictionary<GameObject, GameObjectDependencies> m_Dependencies = new Dictionary<GameObject, GameObjectDependencies>(new Comparer());

        [NetInput]
        void OnSetGameObjectDependencies(NetContext<SetGameObjectDependencies> ctx)
        {
            if (m_Dependencies.ContainsKey(ctx.Data.GameObject))
                throw new NotSupportedException("Cannot set many times the resource dependencies of a GameObject");

            m_Dependencies.Add(ctx.Data.GameObject, new GameObjectDependencies(ctx.Data.Meshes, ctx.Data.Materials));
        }

        [PipeInput]
        void OnGameObjectDestroying(PipeContext<GameObjectDestroying> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
            {
                if (!m_Dependencies.TryGetValue(go.GameObject, out var dependencies))
                    continue;
                
                foreach(var material in dependencies.Materials)
                    m_ReleaseUnityMaterialOutput.Send(new ReleaseUnityMaterial(material));
                foreach(var mesh in dependencies.Meshes)
                    m_ReleaseUnityMeshOutput.Send(new ReleaseUnityMesh(mesh));

                m_Dependencies.Remove(go.GameObject);
            }

            ctx.Continue();
        }

        class GameObjectDependencies
        {
            public List<Mesh> Meshes;
            public List<Material> Materials;

            public GameObjectDependencies(List<Mesh> meshes, List<Material> materials)
            {
                Meshes = meshes;
                Materials = materials;
            }
        }

        class Comparer : IEqualityComparer<GameObject>
        {
            public bool Equals(GameObject x, GameObject y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(GameObject obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
