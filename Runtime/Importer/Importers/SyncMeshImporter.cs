using System;
using System.Linq;
using Unity.Reflect.Model;
using UnityEngine.Rendering;

namespace UnityEngine.Reflect
{  
    public class SyncMeshImporter : RuntimeImporter<SyncMesh, Mesh>
    {
        static readonly int k_MaxUInt16VertexCount = 65530;

        public override Mesh CreateNew(SyncMesh syncMesh)
        {
            return new Mesh { name = syncMesh.Name };
        }

        protected override void Clear(Mesh mesh)
        {
            mesh.Clear();
        }

        protected override void ImportInternal(SyncMesh syncMesh, Mesh mesh, object settings)
        {            
            var vertices = syncMesh.Vertices.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
            
            // Note GPU support for 32 bit indices is not guaranteed on all platforms (from the doc).
            mesh.indexFormat = vertices.Length > k_MaxUInt16VertexCount ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = syncMesh.Uvs.Select(uv => new Vector2(uv.X, uv.Y)).ToArray();
            mesh.normals = syncMesh.Normals.Select(n => new Vector3(n.X, n.Y, n.Z)).ToArray();
            mesh.subMeshCount = syncMesh.SubMeshes.Count;

            for (int i = 0; i < mesh.subMeshCount; ++i)
            {
                mesh.SetTriangles(syncMesh.SubMeshes[i].Triangles.ToArray(), i);
            }
            
            mesh.RecalculateTangents();
        }
    }
}
