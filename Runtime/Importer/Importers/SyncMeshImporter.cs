// using array slices is optimal, but only available since 2019.3
#if UNITY_2019_3_OR_NEWER
#define USE_ARRAY_SLICES
#endif

#if !USE_ARRAY_SLICES
using System.Collections.Generic;
#endif
using Unity.Reflect;
using Unity.Reflect.Model;
using UnityEngine.Rendering;

namespace UnityEngine.Reflect
{  
    public class SyncMeshImporter : RuntimeImporter<SyncMesh, Mesh>
    {
        static readonly int k_MaxUInt16VertexCount = 65530;
        
        #if USE_ARRAY_SLICES
        private Vector3[] _vector3Buffer;
        private Vector2[] _vector2Buffer;
        private int[] _intBuffer;
        #else
        private List<Vector3> _vector3Buffer;
        private List<Vector2> _vector2Buffer;
        private List<int> _intBuffer;
        #endif

        public override Mesh CreateNew(SyncMesh syncMesh, object settings)
        {
            return new Mesh { name = syncMesh.Name };
        }

        protected override void Clear(Mesh mesh)
        {
            mesh.Clear();
        }

        protected override void ImportInternal(SyncedData<SyncMesh> syncedMesh, Mesh mesh, object settings)
        {
            var syncMesh = syncedMesh.data;
            var count = syncMesh.Vertices.Count;
            
            // expand capacity beforehand since count is known
            #if USE_ARRAY_SLICES
            if (_vector3Buffer == null || _vector3Buffer.Length < count)
                _vector3Buffer = new Vector3[count];
            
            if (_vector2Buffer == null || _vector2Buffer.Length < count)
                _vector2Buffer = new Vector2[count];
            #else
            if (_vector3Buffer == null || _vector3Buffer.Count < count)
                _vector3Buffer = new List<Vector3>(count);

            if (_vector2Buffer == null || _vector2Buffer.Count < count)
                _vector2Buffer = new List<Vector2>(count);
            #endif
            
            // Note GPU support for 32 bit indices is not guaranteed on all platforms (from the doc).
            mesh.indexFormat = count > k_MaxUInt16VertexCount ? IndexFormat.UInt32 : IndexFormat.UInt16;

            // vertices
            #if USE_ARRAY_SLICES
            var index = 0;
            foreach (var vertex in syncMesh.Vertices)
                _vector3Buffer[index++].Set(vertex.X, vertex.Y, vertex.Z);
            mesh.SetVertices(_vector3Buffer, 0, count);
            #else
            _vector3Buffer.Clear();
            foreach (var vertex in syncMesh.Vertices)
                _vector3Buffer.Add(new Vector3(vertex.X, vertex.Y, vertex.Z));
            mesh.SetVertices(_vector3Buffer);
            #endif

            // UVs
            #if USE_ARRAY_SLICES
            index = 0;
            foreach (var uv in syncMesh.Uvs)
                _vector2Buffer[index++].Set(uv.X, uv.Y);
            mesh.SetUVs(0, _vector2Buffer, 0, count);
            #else
            _vector2Buffer.Clear();
            foreach (var uv in syncMesh.Uvs)
                _vector2Buffer.Add(new Vector2(uv.X, uv.Y));
            mesh.SetUVs(0, _vector2Buffer);
            #endif

            // normals
            #if USE_ARRAY_SLICES
            index = 0;
            foreach (var normal in syncMesh.Normals)
                _vector3Buffer[index++].Set(normal.X, normal.Y, normal.Z);
            mesh.SetNormals(_vector3Buffer, 0, count);
            #else
            _vector3Buffer.Clear();
            foreach (var normal in syncMesh.Normals)
                _vector3Buffer.Add(new Vector3(normal.X, normal.Y, normal.Z));
            mesh.SetNormals(_vector3Buffer);
            #endif
            
            var subMeshCount = syncMesh.SubMeshes.Count;
            mesh.subMeshCount = subMeshCount;
            for (var i = 0; i < subMeshCount; ++i)
            {
                count = syncMesh.SubMeshes[i].Triangles.Count;
                // must expand here since triangle counts may vary
                #if USE_ARRAY_SLICES
                if (_intBuffer == null || _intBuffer.Length < count)
                    _intBuffer = new int[count];
                #else
                if (_intBuffer == null || _intBuffer.Count < count)
                    _intBuffer = new List<int>(count);
                #endif
                
                // triangles
                #if USE_ARRAY_SLICES
                index = 0;
                foreach (var triangleIndex in syncMesh.SubMeshes[i].Triangles)
                    _intBuffer[index++] = triangleIndex;
                mesh.SetTriangles(_intBuffer, 0, count, i);
                #else
                foreach (var triangleIndex in syncMesh.SubMeshes[i].Triangles)
                    _intBuffer.Add(triangleIndex);
                mesh.SetTriangles(_intBuffer, i);
                #endif
            }

            if (syncMesh.Normals.Count != syncMesh.Vertices.Count)
                mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }
    }
}
