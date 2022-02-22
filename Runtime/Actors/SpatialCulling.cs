using Unity.Collections;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Reflect.Actors
{
    public class SpatialCulling
    {
        // temporary variables to avoid garbage collection
        Vector3 m_Min,
            m_Max,
            m_Point,
            m_ScreenMin,
            m_ScreenMax,
            m_Normal,
            m_DirectionalLightForward,
            m_DirectionalLightForwardOffset;

        Vector4 m_Point4;
        readonly int m_DepthCullingResolution;
        Bounds m_Bounds;

        float m_Distance,
            m_Dot,
            m_Z,
            m_Dz,
            m_ScreenAreaRatio,
            m_DepthOffset,
            m_AvoidCullingWithinSqrDistance,
            m_ShadowDistance;

        float[,] m_DepthMapSlice, m_PrevDepthMapSlice;
        readonly float[][,] m_DepthMap;
        Matrix4x4 m_CamWorldToViewportMatrix, m_RootMatrix;
        NativeArray<float> m_DepthTextureArray;
        NativeArray<Color> m_DepthTextureArrayNoAsync;
        readonly RenderTexture m_DepthRenderTexture;
        readonly Texture2D m_DepthTexture;
        readonly Rect m_DepthTextureRect;
        bool m_IsCapturingDepthMap, m_UseShadowCullingAvoidance;
        float m_MinScreenAreaRatioMesh;

        bool m_IsShutdown;
        readonly bool m_SupportsAsyncGpuReadback;
        CameraDataChanged m_CameraData;
        readonly Vector3[] m_FrustumNormals = new Vector3[6];
        readonly float[] m_FrustumDistances = new float[6];
        readonly Transform m_DirectionalLight;

        readonly SpatialActor.Settings m_Settings;

        public SpatialCulling(SpatialActor.Settings settings)
        {
            m_Settings = settings;
            m_RootMatrix = m_Settings.Root.localToWorldMatrix;

            m_DirectionalLight = m_Settings.DirectionalLight;

            m_SupportsAsyncGpuReadback = SystemInfo.supportsAsyncGPUReadback;
            m_DepthRenderTexture = m_SupportsAsyncGpuReadback
                ? m_Settings.DepthRenderTexture
                : m_Settings.DepthRenderTextureNoAsync;

            var depthSize = Mathf.Min(m_DepthRenderTexture.width, m_DepthRenderTexture.height);
            m_DepthCullingResolution = (int) Mathf.Log(depthSize, 2);
            m_DepthMap = new float[m_DepthCullingResolution + 1][,];
            for (int i = 0, size = 1; i <= m_DepthCullingResolution; ++i, size <<= 1)
                m_DepthMap[i] = new float[size, size];

            if (m_SupportsAsyncGpuReadback)
                return;

            // depth culling is disabled by default on devices that don't support AsyncGPUReadback
            // but still init the slower (non-async) version's assets in case a user wants to enable it anyway
            m_DepthTexture = new Texture2D(depthSize, depthSize, TextureFormat.RGBAFloat, false);
            m_DepthTextureRect = new Rect(0, 0, depthSize, depthSize);
        }

        public void Shutdown()
        {
            m_IsShutdown = true;
        }

        public void OnUpdate(RpcContext<DelegateJob> ctx)
        {
            if (m_IsCapturingDepthMap)
            {
                ctx.SendSuccess(NullData.Null);
                return;
            }

            if (!m_Settings.UseDepthCulling)
            {
                ctx.SendSuccess(NullData.Null);
                return;
            }

            m_IsCapturingDepthMap = true;
            CaptureDepthMap(ctx);
        }

        public void SetCameraData(CameraDataChanged cameraData)
        {
            m_CameraData = cameraData;
            CalculateCameraData();
        }

        public bool IsVisible(ISpatialObject obj)
        {
            var (min, max) = SpatialActor.RecalculateBounds(obj, m_RootMatrix);
            m_Bounds.SetMinMax(min, max);

            if (m_Settings.UseDistanceCullingAvoidance && m_Bounds.SqrDistance(m_CameraData.Position) < m_AvoidCullingWithinSqrDistance)
                return true;

            if (m_UseShadowCullingAvoidance) // use member because we check if shadows are enabled in QualitySettings
            {
                // expand bounds to include potential shadow casting area
                m_DirectionalLightForwardOffset = m_DirectionalLightForward * m_ShadowDistance;
                m_Bounds.min = Vector3.Min(m_Bounds.min, m_Bounds.min + m_DirectionalLightForwardOffset);
                m_Bounds.max = Vector3.Max(m_Bounds.max, m_Bounds.max + m_DirectionalLightForwardOffset);
            }

            // do the frustum check, necessary for size and depth culling since both techniques are based on the screen projection
            if (!IsInCameraFrustum(m_Bounds.min, m_Bounds.max))
                return false;

            // early exit before screen projection if it won't be used
            if (!m_Settings.UseSizeCulling && !m_Settings.UseDepthCulling)
                return true;

            // if screen rect is invalid, safer to assume object is visible
            if (!TryCalculateScreenRect(m_Bounds.min, m_Bounds.max, out m_ScreenMin, out m_ScreenMax))
                return true;

            if (m_Settings.UseSizeCulling)
            {
                m_ScreenAreaRatio = (m_ScreenMax.x - m_ScreenMin.x) * (m_ScreenMax.y - m_ScreenMin.y);
                if (m_ScreenAreaRatio < m_MinScreenAreaRatioMesh)
                    return false;
            }

            return !m_Settings.UseDepthCulling || !IsDepthOccluded(m_ScreenMin, m_ScreenMax);
        }

        void CalculateCameraData()
        {
            for (var i = 0; i < m_CameraData.FrustumPlanes.Length; ++i)
            {
                var plane = m_CameraData.FrustumPlanes[i];
                var normal = plane.normal;
                plane.Translate(normal * m_Settings.CameraFrustumNormalOffset);
                m_FrustumNormals[i] = normal;
                m_FrustumDistances[i] = plane.distance;
            }

            m_CamWorldToViewportMatrix =  m_CameraData.ViewProjectionMatrix;
            m_DirectionalLightForward = m_DirectionalLight.forward;
            m_RootMatrix = m_Settings.Root.localToWorldMatrix;

            if (m_Settings.UseDistanceCullingAvoidance)
                m_AvoidCullingWithinSqrDistance = m_Settings.AvoidCullingWithinDistance * m_Settings.AvoidCullingWithinDistance;

            m_UseShadowCullingAvoidance = m_Settings.UseShadowCullingAvoidance && QualitySettings.shadows != ShadowQuality.Disable;
            m_ShadowDistance = QualitySettings.shadowDistance;

            if (m_Settings.UseSizeCulling)
                m_MinScreenAreaRatioMesh = Mathf.Pow(10, -m_Settings.MinimumScreenAreaRatioMesh);
        }

        bool TryCalculateScreenRect(Vector3 boundsMin, Vector3 boundsMax, out Vector3 min, out Vector3 max)
        {
            m_Min = min = boundsMin;
            m_Max = max = boundsMax;

            for (byte i = 0; i < 8; ++i)
            {
                // calculate each corner of the bounding box
                m_Point.x = (i >> 2) % 2 > 0 ? m_Max.x : m_Min.x;
                m_Point.y = (i >> 1) % 2 > 0 ? m_Max.y : m_Min.y;
                m_Point.z = i % 2 > 0 ? m_Max.z : m_Min.z;

                // screen rect will be invalid if a point is behind the camera due to the projection matrix
                // GetDistanceToPoint is faster here than GetSide since it uses floats instead of doubles internally
                if (m_CameraData.ForwardPlane.GetDistanceToPoint(m_Point) < 0f)
                    return false;

                m_Point = WorldToViewportPoint(m_Point);

                if (i == 0)
                {
                    min = max = m_Point;
                    continue;
                }

                if (m_Point.x < min.x) min.x = m_Point.x;
                if (m_Point.y < min.y) min.y = m_Point.y;
                if (m_Point.z < min.z) min.z = m_Point.z;
                if (m_Point.x > max.x) max.x = m_Point.x;
                if (m_Point.y > max.y) max.y = m_Point.y;
                if (m_Point.z > max.z) max.z = m_Point.z;
            }

            // normalize the z value to match depth shader value
            min.z = Mathf.InverseLerp(m_CameraData.NearClipPlane, m_CameraData.FarClipPlane, min.z);
            max.z = Mathf.InverseLerp(m_CameraData.NearClipPlane, m_CameraData.FarClipPlane, max.z);

            return true;
        }

        Vector3 WorldToViewportPoint(Vector3 point)
        {
            // convert to Vector4 for Matrix4x4 multiplication
            m_Point4.Set(point.x, point.y, point.z, 1f);
            m_Point4 = m_CamWorldToViewportMatrix * m_Point4;

            // normalize
            point = m_Point4;
            point /= -m_Point4.w;

            // convert from clip to Unity viewport space, z is distance from camera
            point.x += 1f;
            point.x /= 2f;
            point.y += 1f;
            point.y /= 2f;
            point.z = -m_Point4.w;

            return point;
        }

        bool IsInCameraFrustum(Vector3 min, Vector3 max)
        {
            for (var i = 0; i < m_CameraData.FrustumPlanes.Length; ++i)
            {
                m_Normal = m_FrustumNormals[i];
                m_Distance = m_FrustumDistances[i];

                // get the closest bounding box vertex along each plane's normal
                m_Min.x = m_Normal.x < 0 ? min.x : max.x;
                m_Min.y = m_Normal.y < 0 ? min.y : max.y;
                m_Min.z = m_Normal.z < 0 ? min.z : max.z;

                // the object is hidden if a closest point is behind any plane
                m_Dot = m_Normal.x * m_Min.x + m_Normal.y * m_Min.y + m_Normal.z * m_Min.z;
                if (m_Dot + m_Distance < 0)
                    return false;
            }

            return true;
        }

        void CaptureDepthMap(RpcContext<DelegateJob> ctx)
        {
            Graphics.Blit(Texture2D.whiteTexture, m_DepthRenderTexture, m_Settings.DepthRenderMaterial);

            if (!m_SupportsAsyncGpuReadback)
            {
                RenderTexture.active = m_DepthRenderTexture;
                m_DepthTexture.ReadPixels(m_DepthTextureRect, 0, 0);
                RenderTexture.active = null;
                m_DepthTextureArrayNoAsync = m_DepthTexture.GetRawTextureData<Color>();
                GenerateDepthMaps();
                ctx.SendSuccess(NullData.Null);
                return;
            }

            // send the gpu request
            AsyncGPUReadback.Request(m_DepthRenderTexture, 0, request => CaptureDepthMapAsyncCallback(request, ctx));
        }

        void CaptureDepthMapAsyncCallback(AsyncGPUReadbackRequest request, RpcContext<DelegateJob> ctx)
        {
            if (m_IsShutdown)
                return;

            if (!request.done || request.hasError)
            {
                ctx.SendSuccess(NullData.Null);
                m_IsCapturingDepthMap = false;
                return;
            }

            m_DepthTextureArray = request.GetData<float>();

            GenerateDepthMaps();

            ctx.SendSuccess(NullData.Null);
        }

        void GenerateDepthMaps()
        {
            m_DepthOffset = m_Settings.DepthOffset;

            // save the highest resolution depth map, then sample down
            for (int i = m_DepthCullingResolution, size = 1 << i; i >= 0; --i, size >>= 1)
            {
                var isFirstIteration = i == m_DepthCullingResolution;
                m_DepthMapSlice = m_DepthMap[i];
                m_PrevDepthMapSlice = isFirstIteration ? m_DepthMap[i] : m_DepthMap[i + 1];

                for (int x = 0, dx = 0; x < size; ++x, dx += 2)
                {
                    for (int y = 0, dy = 0; y < size; ++y, dy += 2)
                    {
                        if (isFirstIteration)
                        {
                            // add m_DepthOffset to reduce flickering since the depth map is not at the full screen resolution
                            m_DepthMapSlice[x, y] = m_DepthOffset + (m_SupportsAsyncGpuReadback
                                ? m_DepthTextureArray[x + y * size]
                                // the depth is stored in the R channel for the non-async array
                                : m_DepthTextureArrayNoAsync[x + y * size].r);
                            continue;
                        }

                        // we need the largest depth value rather than the average, else we'd just use the texture mip maps
                        // doing manual comparisons to avoid garbage allocations in Mathf.Max()
                        m_Z = m_PrevDepthMapSlice[dx, dy];

                        m_Dz = m_PrevDepthMapSlice[dx + 1, dy];
                        if (m_Dz > m_Z)
                            m_Z = m_Dz;

                        m_Dz = m_PrevDepthMapSlice[dx, dy + 1];
                        if (m_Dz > m_Z)
                            m_Z = m_Dz;

                        m_Dz = m_PrevDepthMapSlice[dx + 1, dy + 1];
                        if (m_Dz > m_Z)
                            m_Z = m_Dz;

                        m_DepthMapSlice[x, y] = m_Z;
                    }
                }
            }

            m_IsCapturingDepthMap = false;
        }

        bool IsDepthOccluded(Vector3 min, Vector3 max)
        {
            // crop the rect so it fits in screen space
            if (min.x < 0f)
                min.x = 0f;

            if (max.x > 1f)
                max.x = 1f;

            if (min.y < 0f)
                min.y = 0f;

            if (max.y > 1f)
                max.y = 1f;

            // recursion
            return IsDepthOccluded(min, max, 0);
        }

        bool IsDepthOccluded(Vector3 min, Vector3 max, int resolution, int x = 0, int y = 0)
        {
            if (min.z > m_DepthMap[resolution][x, y])
                return true;

            ++resolution;

            if (resolution > m_DepthCullingResolution)
                return false;

            var mapSize = 1 << resolution;
            x <<= 1;
            y <<= 1;

            // find the indices for the 4 rect points in the depth map
            var xMin = Mathf.Max((int) (mapSize * min.x), x);
            var xMax = Mathf.Min((int) (mapSize * max.x), x + 1);
            var yMin = Mathf.Max((int) (mapSize * min.y), y);
            var yMax = Mathf.Min((int) (mapSize * max.y), y + 1);

            for (x = xMin; x <= xMax; ++x)
                for (y = yMin; y <= yMax; ++y)
                    if (!IsDepthOccluded(min, max, resolution, x, y))
                        return false;

            return true;
        }
    }
}
