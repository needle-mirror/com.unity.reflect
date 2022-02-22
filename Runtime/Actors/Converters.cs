using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Collections;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;
using UnityEngine.Rendering;
using IReflectMaterialConverter = Unity.Reflect.ActorFramework.IReflectMaterialConverter;
using Object = UnityEngine.Object;

namespace Unity.Reflect.Actors
{
    [Actor]
    public abstract class ResourceConverterActor<TInput>
        where TInput : class
    {
        [RpcInput]
        void OnConvert(RpcContext<ConvertResource<TInput>> ctx)
        {
            var streamKey = new StreamKey(ctx.Data.Entry.SourceId, ctx.Data.Entry.IdInSource);
            Import(ctx, new SyncedData<TInput>(streamKey, ctx.Data.Resource));
        }
        
        protected abstract void Import(RpcContext<ConvertResource<TInput>> ctx, SyncedData<TInput> resource);
    }

    [Actor("fdc0e0e2-ed3d-4de9-94a5-21077d02daae")]
    public class MeshConverterActor
    {
#pragma warning disable 649
        Settings m_Settings;

        RpcOutput<DelegateJob> m_DelegateJobOutput;
#pragma warning restore 649

        CancellationToken m_Token;

        public void Inject(CancellationToken token)
        {
            m_Token = token;
        }

        [RpcInput]
        void OnConvert(RpcContext<ConvertResource<SyncMesh>> ctx)
        {
            if (m_Token.IsCancellationRequested)
            {
                ctx.SendFailure(new OperationCanceledException());
                return;
            }

            var resource = ctx.Data.Resource;
            var streamKey = new StreamKey(ctx.Data.Entry.SourceId, ctx.Data.Entry.IdInSource);
            Import(ctx, new SyncedData<SyncMesh>(streamKey, resource));
        }

        void Import(RpcContext<ConvertResource<SyncMesh>> ctx, SyncedData<SyncMesh> resource)
        {
            MeshImportJob jobData;
            var vCount = resource.data.Vertices.Count;

            if (vCount > k_MaxUInt16VertexCount)
                jobData = ImportMesh<uint>(resource.data, m_Settings.IsReadable);
            else
                jobData = ImportMesh<ushort>(resource.data, m_Settings.IsReadable);

            var rpc = m_DelegateJobOutput.Call(this, ctx, jobData, new DelegateJob(jobData, MeshConverterMainThreadJob));
            rpc.Success<MeshImportJob>((self, ctx, jobData, _) =>
            {
                ctx.SendSuccess(new ConvertedResource<NullData>(jobData.Result, null));
            });

            rpc.Failure((self, ctx, meshImport, ex) =>
            {
                ctx.SendFailure(ex);
            });
        }

        class MeshImportJob
        {
            public SyncMesh SyncMesh;
            public Vertex[] Vertices;
            public uint[] Indices32;
            public ushort[] Indices16;
            public int[] SubMeshStarts;
            public int[] SubMeshLengths;
            public Bounds MeshBounds;
            public Mesh Result;
            public bool IsReadable;
        }

        static MeshImportJob ImportMesh<T>(SyncMesh syncMesh, bool isReadable)
            where T : struct
        {
            var data = new MeshImportJob{ SyncMesh = syncMesh };
            var toggle = new IndexToggle<T>(data);

            var count = syncMesh.Vertices.Count;

            data.Vertices = new Vertex[count];
            
            for (var i = 0; i < count; ++i)
            {
                var v = syncMesh.Vertices[i];
                data.Vertices[i].Position = new Vector3(v.X, v.Y, v.Z);
            }

            count = syncMesh.Normals.Count;
            for (var i = 0; i < count; ++i)
            {
                var n = syncMesh.Normals[i];
                data.Vertices[i].Normal = new Vector3(n.X, n.Y, n.Z);
            }
            
            count = syncMesh.Uvs.Count;
            for (var i = 0; i < count; ++i)
            {
                var u = syncMesh.Uvs[i];
                data.Vertices[i].UV = new Vector2(u.X, u.Y);
            }

            var subMeshCount = syncMesh.SubMeshes.Count;
            var nbIndices = syncMesh.SubMeshes.Sum(x => x.Triangles.Count);

            toggle.InstantiateArray(nbIndices);

            data.SubMeshStarts = new int[syncMesh.SubMeshes.Count];
            data.SubMeshLengths = new int[syncMesh.SubMeshes.Count];

            var offset = 0;
            for (var i = 0; i < subMeshCount; ++i)
            {
                count = syncMesh.SubMeshes[i].Triangles.Count;

                data.SubMeshStarts[i] = offset;
                data.SubMeshLengths[i] = count;

                var index = 0;
                foreach (var triangleIndex in syncMesh.SubMeshes[i].Triangles)
                    toggle.Set(offset + index++, triangleIndex);

                offset += count;
            }
            
            CalculateBounds(data);
            CalculateMeshTangents(data, toggle);

            data.IsReadable = isReadable;

            return data;
        }

        // Use this so the jitter is able to get rid of the if on type because it's known at compile time
        struct IndexToggle<T> where T : struct
        {
            MeshImportJob m_JobData;

            public IndexToggle(MeshImportJob jobData)
            {
                m_JobData = jobData;
            }

            public long Get(long i)
            {
                if (typeof(T) == typeof(uint))
                    return m_JobData.Indices32[i];
                if (typeof(T) == typeof(ushort))
                    return m_JobData.Indices16[i];

                throw new NotSupportedException();
            }

            public void Set(long i, int value)
            {
                if (typeof(T) == typeof(uint))
                    m_JobData.Indices32[i] = (uint)value;
                else if (typeof(T) == typeof(ushort))
                    m_JobData.Indices16[i] = (ushort)value;
                else
                    throw new NotSupportedException();
            }

            public int GetLength()
            {
                if (typeof(T) == typeof(uint))
                    return m_JobData.Indices32.Length;
                if (typeof(T) == typeof(ushort))
                    return m_JobData.Indices16.Length;

                throw new NotSupportedException();
            }

            public void InstantiateArray(int nbIndices)
            {
                if (typeof(T) == typeof(uint))
                    m_JobData.Indices32 = new uint[nbIndices];
                else if (typeof(T) == typeof(ushort))
                    m_JobData.Indices16 = new ushort[nbIndices];
                else
                    throw new NotSupportedException();
            }
        }

        const int k_MaxUInt16VertexCount = 65530;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector4 Tangent;
            public Vector2 UV;
        }

        static VertexAttributeDescriptor[] s_VertexLayout = {
            new VertexAttributeDescriptor(VertexAttribute.Position),
            new VertexAttributeDescriptor(VertexAttribute.Normal),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        };

        static void MeshConverterMainThreadJob(RpcContext<DelegateJob> ctx, object data)
        {
            var jobData = (MeshImportJob)data;
            var syncMesh = jobData.SyncMesh;

            var mesh = new Mesh{ name = syncMesh.Name };
            jobData.Result = mesh;
            
            var count = syncMesh.Vertices.Count;

            if (count == 0)
            {
                ctx.SendSuccess(jobData);
                return;
            }

            // Note GPU support for 32 bit indices is not guaranteed on all platforms (from the doc).
            mesh.indexFormat = count > k_MaxUInt16VertexCount ? IndexFormat.UInt32 : IndexFormat.UInt16;
            
            mesh.SetVertexBufferParams(count, s_VertexLayout);
            mesh.SetVertexBufferData(jobData.Vertices, 0, 0, count, 0, MeshUpdateFlags.DontResetBoneBounds);

            if (jobData.Indices32 != null)
            {
                mesh.SetIndexBufferParams(jobData.Indices32.Length, mesh.indexFormat);
                mesh.SetIndexBufferData(jobData.Indices32, 0, 0, jobData.Indices32.Length, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds);
            }
            else
            {
                mesh.SetIndexBufferParams(jobData.Indices16.Length, mesh.indexFormat);
                mesh.SetIndexBufferData(jobData.Indices16, 0, 0, jobData.Indices16.Length, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds);
            }

            var subMeshCount = syncMesh.SubMeshes.Count;
            mesh.subMeshCount = subMeshCount;

            for (var i = 0; i < subMeshCount; ++i)
                mesh.SetSubMesh(i, new SubMeshDescriptor(jobData.SubMeshStarts[i], jobData.SubMeshLengths[i]), MeshUpdateFlags.DontRecalculateBounds);

            mesh.bounds = jobData.MeshBounds;

            if (syncMesh.Normals.Count != syncMesh.Vertices.Count)
            {
                // We should transfer that to background task, but no mesh seems to need this...
                mesh.RecalculateNormals();
            }

            mesh.UploadMeshData(!jobData.IsReadable);

            ctx.SendSuccess(jobData);
        }
        
        static void CalculateMeshTangents<T>(MeshImportJob jobData, IndexToggle<T> toggle) where T : struct
        {
            // Todo: Validate that this function is yielding the same result as the internal unity function
            var vertices = jobData.Vertices;
            
            var triangleCount = toggle.GetLength();
            var vertexCount = vertices.Length;
            
            var tan1 = new Vector3[vertexCount];
            var tan2 = new Vector3[vertexCount];

            for (long a = 0; a < triangleCount; a += 3)
            {
                long i1 = toggle.Get(a + 0);
                long i2 = toggle.Get(a + 1);
                long i3 = toggle.Get(a + 2);

                var v1 = vertices[i1].Position;
                var v2 = vertices[i2].Position;
                var v3 = vertices[i3].Position;

                var w1 = vertices[i1].UV;
                var w2 = vertices[i2].UV;
                var w3 = vertices[i3].UV;

                var x1 = v2.x - v1.x;
                var x2 = v3.x - v1.x;
                var y1 = v2.y - v1.y;
                var y2 = v3.y - v1.y;
                var z1 = v2.z - v1.z;
                var z2 = v3.z - v1.z;

                var s1 = w2.x - w1.x;
                var s2 = w3.x - w1.x;
                var t1 = w2.y - w1.y;
                var t2 = w3.y - w1.y;

                var div = s1 * t2 - s2 * t1;
                var r = div == 0.0f ? 0.0f : 1.0f / (s1 * t2 - s2 * t1);

                var sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                var tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

                tan1[i1] += sdir;
                tan1[i2] += sdir;
                tan1[i3] += sdir;

                tan2[i1] += tdir;
                tan2[i2] += tdir;
                tan2[i3] += tdir;
            }

            for (long a = 0; a < vertexCount; ++a)
            {
                var n = vertices[a].Normal;
                var t = tan1[a];
                
                Vector3.OrthoNormalize(ref n, ref t);
                vertices[a].Tangent.Set(t.x, t.y, t.z, Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f ? -1.0f : 1.0f);
            }
        }

        static void CalculateBounds(MeshImportJob jobData)
        {
            var upperBound = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var lowerBound = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

            var count = jobData.Vertices.Length;
            for (var i = 0; i < count; ++i)
            {
                var vertex = jobData.Vertices[i];

                if (vertex.Position.x > upperBound.x)
                    upperBound.x = vertex.Position.x;
                if (vertex.Position.y > upperBound.y)
                    upperBound.y = vertex.Position.y;
                if (vertex.Position.z > upperBound.z)
                    upperBound.z = vertex.Position.z;

                if (vertex.Position.x < lowerBound.x)
                    lowerBound.x = vertex.Position.x;
                if (vertex.Position.y < lowerBound.y)
                    lowerBound.y = vertex.Position.y;
                if (vertex.Position.z < lowerBound.z)
                    lowerBound.z = vertex.Position.z;
            }

            jobData.MeshBounds = new Bounds((lowerBound + upperBound) * 0.5f, upperBound - lowerBound);
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            [Tooltip("Required for some features (ex: Measure Tool vertex snapping)")]
            public bool IsReadable = true;

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }

    [Actor("6870d6d0-8b34-47b7-9c04-bf65f70eb351", true)]
    public class TextureConverterActor : ResourceConverterActor<SyncTexture>
    {
        readonly TextureImporter m_Importer;

        public TextureConverterActor()
        {
            m_Importer = new TextureImporter();
        }

        protected override void Import(RpcContext<ConvertResource<SyncTexture>> ctx, SyncedData<SyncTexture> resource)
        {
            var texture = m_Importer.CreateNew(resource.data);

            try
            {
                m_Importer.Import(resource.data, texture);
            }
            catch
            {
                Object.Destroy(texture);
                throw;
            }

            ctx.SendSuccess(new ConvertedResource<NullData>(texture, null));
        }
        
        class TextureImporter
        {
		    static readonly float k_NormalMapIntensity = 2.0f;

            static float[] s_GrayScaleBuffer;

            public Texture2D CreateNew(SyncTexture syncTexture)
            {
                var linear = syncTexture.ConvertToNormalMap;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, Texture.GenerateAllMips, linear)
                {
                    name = syncTexture.Name,
                    anisoLevel = 4,
                    wrapMode = TextureWrapMode.Repeat
                };

                return texture;
            }

            public void Import(SyncTexture syncTexture, Texture2D texture)
            {
                var data = syncTexture.Source;

                texture.LoadImage(data);

                if (syncTexture.ConvertToNormalMap)
                {
                    ConvertToNormalMap(texture, k_NormalMapIntensity, false);
                }
            }
            
            static void ConvertToNormalMap(Texture2D image, float intensity, bool inverse)
            {
                var format = image.format;
                var bytes = image.GetRawTextureData<byte>();
                var width = image.width;
                var height = image.height;

                CreateGrayScaleBuffer(bytes, format, width, height);

                var color = Color.white;
                for (var x = 0; x < width; ++x)
                {
                    for (var y = 0; y < height; ++y)
                    {
                        var left = GetValue(x - 1, y, width, height);
                        var right = GetValue(x + 1, y, width, height);
                        var up = GetValue(x, y + 1, width, height);
                        var down = GetValue(x, y - 1, width, height);
                        
                        var dx = (left - right + 1) * 0.5f;
                        var dy = (up - down + 1) * 0.5f;

                        var index = x + y * width;
                        color.r = Mathf.Clamp01(ApplySCurve(dx, intensity));
                        color.g = Mathf.Clamp01(ApplySCurve(inverse ? 1.0f - dy : dy, intensity));
                        color.b = 1f;
                        color.a = 1f;
                        ColorHelper.WriteColor(bytes, format, index, color);
                    }
                }
                
                image.LoadRawTextureData(bytes);
                image.Apply();
            }

            static void CreateGrayScaleBuffer(NativeArray<byte> bytes, TextureFormat format, int width, int height)
            {
                if (s_GrayScaleBuffer == null || s_GrayScaleBuffer.Length < width * height)
                    s_GrayScaleBuffer = new float[width * height];

                var color = Color.white;
                for (var x = 0; x < width; ++x)
                {
                    for (var y = 0; y < height; ++y)
                    {
                        var index = x + y * width;
                        ColorHelper.ReadColor(bytes, format, index, ref color);
                        s_GrayScaleBuffer[index] = color.grayscale;
                    }
                }
            }
            
            static float GetValue(int x, int y, int width, int height)
            {
                x = Mathf.Clamp(x, 0, width - 1);
                y = Mathf.Clamp(y, 0, height - 1);

                return s_GrayScaleBuffer[x + y * width];
            }
            
            static float ApplySCurve(float value, float intensity)
            {
                return 1.0f / (1.0f + Mathf.Exp(-intensity * (value - 0.5f)));
            }
        }
    }

    [Actor("992fab72-c5af-49b5-a01d-34e82b4538c9", true)]
    public class MaterialConverterActor
    {
#pragma warning disable 649
        Settings m_Settings;

        NetOutput<ReleaseUnityTexture> m_ReleaseUnityTextureOutput;
        RpcOutput<AcquireUnityTexture> m_AcquireUnityTextureOutput;
#pragma warning restore 649

        CancellationToken m_Token;

        public void Inject(CancellationToken token)
        {
            m_Token = token;
        }
        
        [RpcInput]
        void OnConvert(RpcContext<ConvertSyncMaterial> ctx)
        {
            if (m_Token.IsCancellationRequested)
            {
                ctx.SendFailure(new OperationCanceledException());
                return;
            }

            var streamKey = new StreamKey(ctx.Data.Entry.SourceId, ctx.Data.Entry.IdInSource);
            Import(ctx, new SyncedData<SyncMaterial>(streamKey, ctx.Data.Resource));
        }
        
        [RpcInput]
        void OnGetProjectWideDefaultMaterial(RpcContext<GetProjectWideDefaultMaterial> ctx)
        {
            ctx.SendSuccess(currentConverter.DefaultMaterial);
        }

        IReflectMaterialConverter currentConverter
        {
            get
            {
                return m_Settings.Converters?.FirstOrDefault(c => c.IsAvailable);
            }
        }

        void ComputeMaterial(Tracker tracker, SyncedData<SyncMaterial> syncMaterial, ITextureCache textureCache)
        {
            tracker.Material = currentConverter.ConstructMaterial(syncMaterial, textureCache);
            tracker.Material.name = syncMaterial.data.Name;
        }

        void Import(RpcContext<ConvertSyncMaterial> ctx, SyncedData<SyncMaterial> resource)
        {
            var tracker = new Tracker
            {
                Resource = resource,
                Textures = new List<Texture2D>(Enumerable.Repeat((Texture2D)null, ctx.Data.TextureEntries.Count)),
                NbRemaining = ctx.Data.TextureEntries.Count
            };

            if (tracker.NbRemaining == 0)
            {
                ComputeMaterial(tracker, resource, tracker);
                ctx.SendSuccess(new ConvertedResource<Texture2D>(tracker.Material, tracker.Textures));
                return;
            }

            AcquireUnityTextures(ctx, tracker);
        }

        void AcquireUnityTextures(RpcContext<ConvertSyncMaterial> ctx, Tracker tracker)
        {
            for (var i = 0; i < ctx.Data.TextureEntries.Count; ++i)
            {
                var entry = ctx.Data.TextureEntries[i];
                var waitAllTracker = new WaitAllTracker { Tracker = tracker, Position = i };

                var rpc = m_AcquireUnityTextureOutput.Call(this, ctx, waitAllTracker, new AcquireUnityTexture(new StreamState(), entry));
                rpc.Success<Texture2D>((self, ctx, waitAllTracker, texture) =>
                {
                    var tracker = waitAllTracker.Tracker;
                    var entry = ctx.Data.TextureEntries[waitAllTracker.Position];
                    var streamKey = new StreamKey(entry.SourceId, entry.IdInSource);

                    tracker.Textures[waitAllTracker.Position] = texture;
                    tracker.TextureDic[streamKey] = texture;
                    --tracker.NbRemaining;

                    if (tracker.NbRemaining == 0)
                    {
                        if (tracker.LatestException == null)
                        {
                            ComputeMaterial(tracker, tracker.Resource, tracker);
                            ctx.SendSuccess(new ConvertedResource<Texture2D>(tracker.Material, tracker.Textures));
                        }
                        else
                            self.CompleteAsFailure(ctx, tracker, tracker.LatestException);
                    }
                });

                rpc.Failure((self, ctx, waitAllTracker, ex) =>
                {
                    --waitAllTracker.Tracker.NbRemaining;
                    waitAllTracker.Tracker.LatestException = ex;
                    if (waitAllTracker.Tracker.NbRemaining == 0)
                        self.CompleteAsFailure(ctx, waitAllTracker.Tracker, waitAllTracker.Tracker.LatestException);
                });
            }
        }

        void CompleteAsFailure(RpcContext<ConvertSyncMaterial> ctx, Tracker tracker, Exception ex)
        {
            if (ex != null)
            {
                for (var i = 0; i < tracker.Textures.Count; ++i)
                {
                    if (!(tracker.Textures[i] is null))
                        m_ReleaseUnityTextureOutput.Send(new ReleaseUnityTexture(tracker.Textures[i]));
                }
            }

            if (tracker.Material != null)
                Object.Destroy(tracker.Material);

            ctx.SendFailure(tracker.LatestException);
        }

        class WaitAllTracker
        {
            public Tracker Tracker;
            public int Position;
        }

        class Tracker : ITextureCache
        {
            public SyncedData<SyncMaterial> Resource;
            public Material Material;
            public Exception LatestException;
            public int NbRemaining;
            public List<Texture2D> Textures;
            public Dictionary<StreamKey, Texture2D> TextureDic = new Dictionary<StreamKey, Texture2D>();

            public Texture2D GetTexture(StreamKey id)
            {
                return TextureDic[id];
            }
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            [ImplPicker]
            [SerializeReference]
            public IReflectMaterialConverter[] Converters;

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
