/*
using System;
using System.Collections;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using UnityEngine.TestTools;

namespace UnityEngine.Reflect.Pipeline.Tests
{
    class ProjectStreamerTests
    {
        SimpleUpdateDelegate m_UpdateDelegate;
        ReflectClientMock m_ReflectClientMock;

        TestStreamAssetReceiver m_AssetReceiver;
        TestStreamInstanceReceiver m_InstanceReceiver;

        const int k_Timeout = 2000;

        [SetUp]
        public void SetUp()
        {
            if (m_UpdateDelegate != null)
            {
                Object.Destroy(m_UpdateDelegate.gameObject);
            }
            
            m_UpdateDelegate = new GameObject("UpdateDelegate").AddComponent<SimpleUpdateDelegate>();
            m_ReflectClientMock = new ReflectClientMock();
            m_AssetReceiver = new TestStreamAssetReceiver();
            m_InstanceReceiver = new TestStreamInstanceReceiver();
        }

        ProjectStreamer CreateProjectStreamer()
        {
            var projectStreamer = new ProjectStreamer(new ProjectStreamerSettings());
            projectStreamer.ConnectInput(m_AssetReceiver);
            projectStreamer.ConnectInput(m_InstanceReceiver);

            return projectStreamer;
        }

        [UnityTest, Timeout(k_Timeout)]
        public IEnumerator StreamAsset_AddingAssets()
        {
            var dummyProject = new UnityProject(null, "dummy", "Dummy");
            
            var streamSourceA = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceA");
            var syncManifestA = streamSourceA.manifest;

            var projectStreamer = CreateProjectStreamer();
            
            //----------------------------------------------------------------------------------------------
            // Adding First Asset
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "111", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);

            // Assert
            AssertReceiverState(1, 0, 0, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","111", "A", m_AssetReceiver.added);
            
            //----------------------------------------------------------------------------------------------
            // Adding New Assets
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Append(PersistentKey.GetKey<SyncMesh>("B"), null, "222", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "111", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncMaterial>("A"), null, "111", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncObject>("A"), null, "111", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);

            // Assert
            AssertReceiverState(4, 0, 0, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","222", "B", m_AssetReceiver.added);
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceA","111", "A", m_AssetReceiver.added);
            AssertStreamAsset<SyncMaterial>(dummyProject, "SourceA","111", "A", m_AssetReceiver.added);
            AssertStreamAsset<SyncObject>(dummyProject, "SourceA","111", "A", m_AssetReceiver.added);
            
            //----------------------------------------------------------------------------------------------
            // Adding A New Asset From Another Source
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            var streamSourceB = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceB");
            var syncManifestB = streamSourceB.manifest;
            
            syncManifestB.Append(PersistentKey.GetKey<SyncMesh>("C"), null, "111", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);

            // Assert
            AssertReceiverState(1, 0, 0, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceB","111", "C", m_AssetReceiver.added);
            
            //----------------------------------------------------------------------------------------------
            // Adding Existing Asset From Another Source
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestB.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "111", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);

            // Assert
            AssertReceiverState(1, 0, 0, m_AssetReceiver);
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceB","111", "A", m_AssetReceiver.added);
            
            //----------------------------------------------------------------------------------------------
            // Adding From Multiple Sources
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Append(PersistentKey.GetKey<SyncMesh>("D"), null, "111", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncMaterial>("D"), null, "333", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);

            // Assert
            AssertReceiverState(2, 0, 0, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","111", "D", m_AssetReceiver.added);
            AssertStreamAsset<SyncMaterial>(dummyProject, "SourceB","333", "D", m_AssetReceiver.added);
        }
        
        [UnityTest, Timeout(k_Timeout)]
        public IEnumerator StreamAsset_ChangingAssets()
        {
            var dummyProject = new UnityProject(null, "dummy", "Dummy");
            
            var streamSourceA = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceA");
            var syncManifestA = streamSourceA.manifest;

            var streamSourceB = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceB");
            var syncManifestB = streamSourceB.manifest;

            var projectStreamer = CreateProjectStreamer();
            
            // Send 1st version of Manifests
            syncManifestA.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "111", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "111", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncMaterial>("A"), null, "111", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncObject>("A"), null, "111", k_DefaultBBox);
            
            syncManifestB.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "222", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "222", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncMaterial>("A"), null, "222", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncObject>("A"), null, "222", k_DefaultBBox);
            
            yield return StartStreaming(projectStreamer, dummyProject);
            
            //----------------------------------------------------------------------------------------------
            // Change One Asset (hash)
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "222", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);

            // Assert
            AssertReceiverState(0, 1, 0, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","222", "A", m_AssetReceiver.changed);
            
            //----------------------------------------------------------------------------------------------
            // Change One Asset (bounding box)
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Append(PersistentKey.GetKey<SyncObject>("A"), null, "111", CreateBBox(1.0f, 2.0f, 3.0f));
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(0, 1, 0, m_AssetReceiver);
            AssertStreamAsset<SyncObject>(dummyProject, "SourceA","111", "A", m_AssetReceiver.changed);
            
            //----------------------------------------------------------------------------------------------
            // Change All Assets
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "333", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "333", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncMaterial>("A"), null, "444", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncObject>("A"), null, "111", CreateBBox(3.0f, 4.0f, 5.0f));
            
            syncManifestB.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "555", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "555", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncMaterial>("A"), null, "555", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncObject>("A"), null, "555", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(0, syncManifestA.Content.Count + syncManifestB.Content.Count, 0, m_AssetReceiver);
            
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","333", "A", m_AssetReceiver.changed);
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceA","333", "A", m_AssetReceiver.changed);
            AssertStreamAsset<SyncMaterial>(dummyProject, "SourceA","444", "A", m_AssetReceiver.changed);
            AssertStreamAsset<SyncObject>(dummyProject, "SourceA","111", "A", m_AssetReceiver.changed);
            
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceB","555", "A", m_AssetReceiver.changed);
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceB","555", "A", m_AssetReceiver.changed);
            AssertStreamAsset<SyncMaterial>(dummyProject, "SourceB","555", "A", m_AssetReceiver.changed);
            AssertStreamAsset<SyncObject>(dummyProject, "SourceB","555", "A", m_AssetReceiver.changed);
            
            //----------------------------------------------------------------------------------------------
            // No Change
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            // Do Nothing
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(0, 0, 0, m_AssetReceiver);
        }
        
        [UnityTest, Timeout(k_Timeout)]
        public IEnumerator StreamAsset_RemovingAssets()
        {
            var dummyProject = new UnityProject(null, "dummy", "Dummy");
            
            var streamSourceA = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceA");
            var syncManifestA = streamSourceA.manifest;

            var streamSourceB = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceB");
            var syncManifestB = streamSourceB.manifest;

            var projectStreamer = CreateProjectStreamer();
            
            // Send 1st version of Manifests
            syncManifestA.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "111", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "111", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncMaterial>("A"), null, "111", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncObject>("A"), null, "111", k_DefaultBBox);
            
            syncManifestB.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "222", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "222", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncMaterial>("A"), null, "222", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncObject>("A"), null, "222", k_DefaultBBox);
            
            yield return StartStreaming(projectStreamer, dummyProject);
            
            //----------------------------------------------------------------------------------------------
            // Remove One Asset From Source A
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Remove(PersistentKey.GetKey<SyncObject>("A"));
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(0, 0, 1, m_AssetReceiver);
            AssertStreamAsset<SyncObject>(dummyProject, "SourceA","111", "A", m_AssetReceiver.removed);
            
            //----------------------------------------------------------------------------------------------
            // Remove One Asset From Source B
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestB.Remove(PersistentKey.GetKey<SyncMesh>("A"));
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(0, 0, 1, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceB","222", "A", m_AssetReceiver.removed);
            
            //----------------------------------------------------------------------------------------------
            // Remove One Asset From Source A and B
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Remove(PersistentKey.GetKey<SyncTexture>("A"));
            syncManifestB.Remove(PersistentKey.GetKey<SyncTexture>("A"));
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(0, 0, 2, m_AssetReceiver);
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceA","111", "A", m_AssetReceiver.removed);
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceB","222", "A", m_AssetReceiver.removed);
            
            //----------------------------------------------------------------------------------------------
            // Remove All Assets
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Remove(PersistentKey.GetKey<SyncMesh>("A"));
            syncManifestA.Remove(PersistentKey.GetKey<SyncMaterial>("A"));
            
            syncManifestB.Remove(PersistentKey.GetKey<SyncMaterial>("A"));
            syncManifestB.Remove(PersistentKey.GetKey<SyncObject>("A"));
            
            Assert.IsEmpty(syncManifestA.Content);
            Assert.IsEmpty(syncManifestB.Content);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(0, 0, 4, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","111", "A", m_AssetReceiver.removed);
            AssertStreamAsset<SyncMaterial>(dummyProject, "SourceA","111", "A", m_AssetReceiver.removed);
            AssertStreamAsset<SyncMaterial>(dummyProject, "SourceB","222", "A", m_AssetReceiver.removed);
            AssertStreamAsset<SyncObject>(dummyProject, "SourceB","222", "A", m_AssetReceiver.removed);
        }

        [UnityTest, Timeout(k_Timeout)]
        public IEnumerator StreamAsset_AddingRemovingAndChangingAssets()
        {
            var dummyProject = new UnityProject(null, "dummy", "Dummy");
            
            var streamSourceA = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceA");
            var syncManifestA = streamSourceA.manifest;

            var streamSourceB = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceB");
            var syncManifestB = streamSourceB.manifest;

            var projectStreamer = CreateProjectStreamer();
            
            // Send 1st version of Manifests
            syncManifestA.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "111", k_DefaultBBox);
            syncManifestA.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "111", k_DefaultBBox);

            syncManifestB.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "222", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "222", k_DefaultBBox);

            yield return StartStreaming(projectStreamer, dummyProject);
            
            AssertReceiverState(4, 0, 0, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","111", "A", m_AssetReceiver.added);
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceA","111", "A", m_AssetReceiver.added);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceB","222", "A", m_AssetReceiver.added);
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceB","222", "A", m_AssetReceiver.added);

            //----------------------------------------------------------------------------------------------
            // Adding And Removing Some Assets
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Remove(PersistentKey.GetKey<SyncMesh>("A"));
            syncManifestA.Append(PersistentKey.GetKey<SyncMesh>("B"), null, "333", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(1, 0, 1, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","111", "A", m_AssetReceiver.removed);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","333", "B", m_AssetReceiver.added);
            
            //----------------------------------------------------------------------------------------------
            // Adding And Changing Some Assets
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Append(PersistentKey.GetKey<SyncMesh>("B"), null, "444", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncObject>("C"), null, "222", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(1, 1, 0, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","444", "B", m_AssetReceiver.changed);
            AssertStreamAsset<SyncObject>(dummyProject, "SourceB","222", "C", m_AssetReceiver.added);
            
            //----------------------------------------------------------------------------------------------
            // Changing And Removing Some Assets
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Remove(PersistentKey.GetKey<SyncMesh>("B"));
            syncManifestB.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "333", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(0, 1, 1, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","444", "B", m_AssetReceiver.removed);
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceB","333", "A", m_AssetReceiver.changed);
            
            //----------------------------------------------------------------------------------------------
            // Adding, Changing And Removing Some Assets
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Remove(PersistentKey.GetKey<SyncTexture>("A"));
            syncManifestA.Append(PersistentKey.GetKey<SyncObject>("C"), null, "555", k_DefaultBBox);
            syncManifestB.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "333", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(1, 1, 1, m_AssetReceiver);
            AssertStreamAsset<SyncObject>(dummyProject, "SourceA","555", "C", m_AssetReceiver.added);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceB","333", "A", m_AssetReceiver.changed);
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceA","111", "A", m_AssetReceiver.removed);
        }

        [UnityTest, Timeout(k_Timeout)]
        public IEnumerator StreamAsset_CompletelyNewAssets()
        {
            var dummyProject = new UnityProject(null, "dummy", "Dummy");
            
            var streamSource = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceA");
            var syncManifest = streamSource.manifest;

            var projectStreamer = CreateProjectStreamer();
            
            // Send 1st version of Manifests
            syncManifest.Append(PersistentKey.GetKey<SyncMesh>("A"), null, "111", k_DefaultBBox);
            syncManifest.Append(PersistentKey.GetKey<SyncMaterial>("A"), null, "111", k_DefaultBBox);
            syncManifest.Append(PersistentKey.GetKey<SyncObject>("A"), null, "111", k_DefaultBBox);
            syncManifest.Append(PersistentKey.GetKey<SyncTexture>("A"), null, "111", k_DefaultBBox);

            yield return StartStreaming(projectStreamer, dummyProject);
            
            AssertReceiverState(4, 0, 0, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","111", "A", m_AssetReceiver.added);
            AssertStreamAsset<SyncMaterial>(dummyProject, "SourceA","111", "A", m_AssetReceiver.added);
            AssertStreamAsset<SyncObject>(dummyProject, "SourceA","111", "A", m_AssetReceiver.added);
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceA","111", "A", m_AssetReceiver.added);
            
            // Arrange
            
            // Clear All Assets in the Manifests
            syncManifest.Remove(PersistentKey.GetKey<SyncMesh>("A"));
            syncManifest.Remove(PersistentKey.GetKey<SyncMaterial>("A"));
            syncManifest.Remove(PersistentKey.GetKey<SyncObject>("A"));
            syncManifest.Remove(PersistentKey.GetKey<SyncTexture>("A"));
            
            Assert.IsEmpty(syncManifest.Content);
            
            // Add Assets With No Intersections From Previous Assets
            syncManifest.Append(PersistentKey.GetKey<SyncMesh>("B"), null, "222", k_DefaultBBox);
            syncManifest.Append(PersistentKey.GetKey<SyncMaterial>("B"), null, "222", k_DefaultBBox);
            syncManifest.Append(PersistentKey.GetKey<SyncObject>("B"), null, "222", k_DefaultBBox);
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);
            
            // Assert
            AssertReceiverState(3, 0, 4, m_AssetReceiver);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","222", "B", m_AssetReceiver.added);
            AssertStreamAsset<SyncMaterial>(dummyProject, "SourceA","222", "B", m_AssetReceiver.added);
            AssertStreamAsset<SyncObject>(dummyProject, "SourceA","222", "B", m_AssetReceiver.added);
            
            AssertStreamAsset<SyncTexture>(dummyProject, "SourceA","111", "A", m_AssetReceiver.removed);
            AssertStreamAsset<SyncMesh>(dummyProject, "SourceA","111", "A", m_AssetReceiver.removed);
            AssertStreamAsset<SyncMaterial>(dummyProject, "SourceA","111", "A", m_AssetReceiver.removed);
            AssertStreamAsset<SyncObject>(dummyProject, "SourceA","111", "A", m_AssetReceiver.removed);
        }

        [UnityTest, Timeout(k_Timeout)]
        public IEnumerator StreamInstance_AddingInstances()
        {
            var dummyProject = new UnityProject(null, "dummy", "Dummy");
            
            var streamSourceA = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceA");
            var syncManifestA = streamSourceA.manifest;

            var projectStreamer = CreateProjectStreamer();
            
            //----------------------------------------------------------------------------------------------
            // Adding First Instance
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "111", k_DefaultBBox);
            CreateInstance(dummyProject, "SourceA", syncManifestA, "A1", "ObjectA", "InstanceA1");
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);

            // Assert
            AssertReceiverState(1, 0, 0, m_InstanceReceiver);
            AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "InstanceA1", m_InstanceReceiver.added);
            
            //----------------------------------------------------------------------------------------------
            // Adding New Instances
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectB"), null, "222", k_DefaultBBox);
            CreateInstance(dummyProject, "SourceA", syncManifestA, "A2", "ObjectA", "InstanceA2");
            CreateInstance(dummyProject, "SourceA", syncManifestA, "B1", "ObjectB", "InstanceB1");
            CreateInstance(dummyProject, "SourceA", syncManifestA, "B2", "ObjectB", "InstanceB2");

            // Act
            yield return StartStreaming(projectStreamer, dummyProject);

            // Assert
            AssertReceiverState(3, 0, 0, m_InstanceReceiver);
            AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "InstanceA2", m_InstanceReceiver.added);
            AssertStreamInstance(dummyProject, "SourceA", "B1", "ObjectB", "InstanceB1", m_InstanceReceiver.added);
            AssertStreamInstance(dummyProject, "SourceA", "B2", "ObjectB", "InstanceB2", m_InstanceReceiver.added);
            
            //----------------------------------------------------------------------------------------------
            // Adding An Instance From Another Source
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            var streamSourceB = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceB");
            var syncManifestB = streamSourceB.manifest;

            syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectC"), null, "333", k_DefaultBBox);
            CreateInstance(dummyProject, "SourceB", syncManifestB, "C1", "ObjectC", "InstanceC1");
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);

            // Assert
            AssertReceiverState(1, 0, 0, m_InstanceReceiver);
            AssertStreamInstance(dummyProject, "SourceB", "C1", "ObjectC", "InstanceC1", m_InstanceReceiver.added);
            
            //----------------------------------------------------------------------------------------------
            // Adding Existing Instance From Another Source
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "111", k_DefaultBBox);
            CreateInstance(dummyProject, "SourceB", syncManifestB,"A3", "ObjectA", "InstanceA3");
            
            // Act
            yield return StartStreaming(projectStreamer, dummyProject);

            // Assert
            AssertReceiverState(1, 0, 0, m_InstanceReceiver);
            AssertStreamInstance(dummyProject, "SourceB", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.added);
            
            //----------------------------------------------------------------------------------------------
            // Adding From Multiple Sources
            //----------------------------------------------------------------------------------------------
            
            // Arrange
            CreateInstance(dummyProject, "SourceB", syncManifestB,"C2", "ObjectC", "InstanceC2");
            CreateInstance(dummyProject, "SourceB", syncManifestB,"A4", "ObjectA", "InstanceA4");
            CreateInstance(dummyProject, "SourceA", syncManifestA, "A3", "ObjectA", "InstanceA3");
            CreateInstance(dummyProject, "SourceA", syncManifestA, "B3", "ObjectB", "InstanceB3");

            // Act
            yield return StartStreaming(projectStreamer, dummyProject);

            // Assert
            AssertReceiverState(4, 0, 0, m_InstanceReceiver);
            AssertStreamInstance(dummyProject, "SourceB", "C2", "ObjectC", "InstanceC2", m_InstanceReceiver.added);
            AssertStreamInstance(dummyProject, "SourceB", "A4", "ObjectA", "InstanceA4", m_InstanceReceiver.added);
            AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.added);
            AssertStreamInstance(dummyProject, "SourceA", "B3", "ObjectB", "InstanceB3", m_InstanceReceiver.added);
        }
        
    //    [UnityTest, Timeout(k_Timeout)]
    //    public IEnumerator StreamInstance_RemovingInstances()
    //    {
    //        var dummyProject = new UnityProject(null, "dummy", "Dummy");
    //        
    //        var streamSourceA = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceA");
    //        var syncManifestA = streamSourceA.manifest;
    //
    //        var streamSourceB = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceB");
    //        var syncManifestB = streamSourceB.manifest;
    //
    //        var projectStreamer = CreateProjectStreamer();
    //        
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "111", k_DefaultBBox);
    //        var instanceA1 = CreateInstance(dummyProject, "SourceA", syncManifestA,"A1", "ObjectA", "InstanceA1");
    //        var instanceA2 = CreateInstance(dummyProject, "SourceA", syncManifestA,"A2", "ObjectA", "InstanceA2");
    //        var instanceA3 = CreateInstance(dummyProject, "SourceA", syncManifestA,"A3", "ObjectA", "InstanceA3");
    //        var instanceA4 = CreateInstance(dummyProject, "SourceA", syncManifestA,"A4", "ObjectA", "InstanceA4");
    //
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "333", k_DefaultBBox);
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectB"), null, "222", k_DefaultBBox);
    //        var instanceB1 = CreateInstance(dummyProject, "SourceB", syncManifestB,"B1", "ObjectB", "InstanceB1");
    //        var instanceB2 = CreateInstance(dummyProject, "SourceB", syncManifestB,"B2", "ObjectA", "InstanceB2");
    //
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        AssertReceiverState(6, 0, 0, m_InstanceReceiver);
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Removing Instance From SourceA
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Arrange
    //        syncPrefabA.Instances.Remove(instanceA1);
    //
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 0, 1, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "InstanceA1", m_InstanceReceiver.removed);
    //
    //        //----------------------------------------------------------------------------------------------
    //        // Removing Instance From SourceB
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Arrange
    //        syncManifestB.Remove(PersistentKey.GetKey<SyncObject>("ObjectB"));
    //        syncPrefabB.Instances.Remove(instanceB1);
    //
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 0, 1, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceB", "B1", "ObjectB", "InstanceB1", m_InstanceReceiver.removed);
    //
    //        //----------------------------------------------------------------------------------------------
    //        // Removing Instance From SourceA And SourceB
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Arrange
    //        syncPrefabA.Instances.Remove(instanceA2);
    //        
    //        syncManifestB.Remove(PersistentKey.GetKey<SyncObject>("ObjectA"));
    //        syncPrefabB.Instances.Remove(instanceB2);
    //
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 0, 2, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "InstanceA2", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceB", "B2", "ObjectA", "InstanceB2", m_InstanceReceiver.removed);
    //
    //        //----------------------------------------------------------------------------------------------
    //        // Removing All Instances
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Arrange
    //        syncManifestA.Remove(PersistentKey.GetKey<SyncObject>("ObjectA"));
    //        syncPrefabA.Instances.Clear();
    //        syncPrefabB.Instances.Clear();
    //
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 0, 2, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A4", "ObjectA", "InstanceA4", m_InstanceReceiver.removed);
    //    }
    //    
    //    [UnityTest, Timeout(k_Timeout)]
    //    public IEnumerator StreamInstance_ChangingInstances()
    //    {
    //        var dummyProject = new UnityProject(null, "dummy", "Dummy");
    //        
    //        var streamSourceA = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceA");
    //        var syncManifestA = streamSourceA.manifest;
    //        var syncPrefabA = streamSourceA.prefab;
    //        
    //        var streamSourceB = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceB");
    //        var syncManifestB = streamSourceB.manifest;
    //        var syncPrefabB = streamSourceB.prefab;
    //
    //        var projectStreamer = CreateProjectStreamer();
    //        
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "111", k_DefaultBBox);
    //        var instanceA1 = CreateInstance("A1", "ObjectA", "InstanceA1");
    //        var instanceA2 = CreateInstance("A2", "ObjectA", "InstanceA2");
    //        var instanceA3 = CreateInstance("A3", "ObjectA", "InstanceA3");
    //        var instanceA4 = CreateInstance("A4", "ObjectA", "InstanceA4");
    //        syncPrefabA.Instances.Add(instanceA1);
    //        syncPrefabA.Instances.Add(instanceA2);
    //        syncPrefabA.Instances.Add(instanceA3);
    //        syncPrefabA.Instances.Add(instanceA4);
    //
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "333", k_DefaultBBox);
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectB"), null, "222", k_DefaultBBox);
    //        var instanceB1 = CreateInstance("B1", "ObjectB", "InstanceB1");
    //        var instanceB2 = CreateInstance("B2", "ObjectA", "InstanceB2");
    //        syncPrefabB.Instances.Add(instanceB1);
    //        syncPrefabB.Instances.Add(instanceB2);
    //        
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        AssertReceiverState(6, 0, 0, m_InstanceReceiver);
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Changing Instance Name
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Arrange
    //        instanceA1.Name = "Renamed InstanceA1";
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 1, 0, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "Renamed InstanceA1", m_InstanceReceiver.changed);
    //        
    //        // Renaming back to original
    //        
    //        // Arrange
    //        instanceA1.Name = "InstanceA1";
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 1, 0, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "InstanceA1", m_InstanceReceiver.changed);
    //
    //        //----------------------------------------------------------------------------------------------
    //        // Changing Instance Transform
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Identity
    //        
    //        // Arrange
    //        var transform = new SyncTransform(Vector3.Zero, Quaternion.Identity, Vector3.One);
    //        instanceA2.Transform = transform;
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 0, 0, m_InstanceReceiver);
    //        
    //        // Changing Position
    //        
    //        // Arrange
    //        transform = new SyncTransform(new Vector3(1.0f, 2.0f, 3.0f), Quaternion.Identity, Vector3.One);
    //        instanceA2.Transform = transform;
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 1, 0, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "InstanceA2", m_InstanceReceiver.changed);
    //        
    //        Assert.AreEqual(new Vector3(1.0f, 2.0f, 3.0f), m_InstanceReceiver.changed[0].instance.Transform.Position);
    //        Assert.AreEqual(Quaternion.Identity, m_InstanceReceiver.changed[0].instance.Transform.Rotation);
    //        Assert.AreEqual(Vector3.One, m_InstanceReceiver.changed[0].instance.Transform.Scale);
    //        
    //        // Changing Rotation
    //        
    //        // Arrange
    //        transform = new SyncTransform(Vector3.Zero, new Quaternion(1.0f, 2.0f, 3.0f, 1.0f), Vector3.One);
    //        instanceA3.Transform = transform;
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 1, 0, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.changed);
    //        
    //        Assert.AreEqual(Vector3.Zero, m_InstanceReceiver.changed[0].instance.Transform.Position);
    //        Assert.AreEqual(Quaternion.Normalize(new Quaternion(1.0f, 2.0f, 3.0f, 1.0f)), m_InstanceReceiver.changed[0].instance.Transform.Rotation);
    //        Assert.AreEqual(Vector3.One, m_InstanceReceiver.changed[0].instance.Transform.Scale);
    //        
    //        // Changing Scale
    //        
    //        // Arrange
    //        transform = new SyncTransform(Vector3.Zero, Quaternion.Identity, new Vector3(1.0f, 2.0f, 3.0f));
    //        instanceA4.Transform = transform;
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 1, 0, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A4", "ObjectA", "InstanceA4", m_InstanceReceiver.changed);
    //        
    //        Assert.AreEqual(Vector3.Zero, m_InstanceReceiver.changed[0].instance.Transform.Position);
    //        Assert.AreEqual(Quaternion.Identity, m_InstanceReceiver.changed[0].instance.Transform.Rotation);
    //        Assert.AreEqual(new Vector3(1.0f, 2.0f, 3.0f), m_InstanceReceiver.changed[0].instance.Transform.Scale);
    //        
    //        // Changing All The Transform
    //        
    //        // Arrange
    //        transform = new SyncTransform(new Vector3(1.0f, 2.0f, 3.0f), new Quaternion(1.0f, 2.0f, 3.0f, 1.0f), new Vector3(4.0f, 5.0f, 6.0f));
    //        instanceB1.Transform = transform;
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 1, 0, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceB", "B1", "ObjectB", "InstanceB1", m_InstanceReceiver.changed);
    //        
    //        Assert.AreEqual(new Vector3(1.0f, 2.0f, 3.0f), m_InstanceReceiver.changed[0].instance.Transform.Position);
    //        Assert.AreEqual(Quaternion.Normalize(new Quaternion(1.0f, 2.0f, 3.0f, 1.0f)), m_InstanceReceiver.changed[0].instance.Transform.Rotation);
    //        Assert.AreEqual(new Vector3(4.0f, 5.0f, 6.0f), m_InstanceReceiver.changed[0].instance.Transform.Scale);
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Changing Instance Metadata
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Adding Metadata
    //        
    //        // Arrange
    //        instanceA2.Metadata = new SyncMetadata(new Dictionary<string, SyncParameter>
    //        {
    //            {"AAA", new SyncParameter("AA", "123", true)}
    //        });
    //
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 1, 0, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "InstanceA2", m_InstanceReceiver.changed);
    //        
    //        Assert.IsTrue(m_InstanceReceiver.changed[0].instance.Metadata.ContainsKey("AAA"));
    //        Assert.IsTrue(m_InstanceReceiver.changed[0].instance.Metadata.Parameters["AAA"].Value == "AA");
    //        Assert.IsTrue(m_InstanceReceiver.changed[0].instance.Metadata.Parameters["AAA"].ParameterGroup == "123");
    //        Assert.IsTrue(m_InstanceReceiver.changed[0].instance.Metadata.Parameters["AAA"].Visible);
    //        
    //        // Changing Metadata
    //        
    //        // Arrange
    //        instanceA2.Metadata.Parameters["AAA"] = new SyncParameter("BB", "123", true);
    //
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 1, 0, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "InstanceA2",
    //            m_InstanceReceiver.changed);
    //        
    //        Assert.IsTrue(m_InstanceReceiver.changed[0].instance.Metadata.ContainsKey("AAA"));
    //        Assert.IsTrue(m_InstanceReceiver.changed[0].instance.Metadata.Parameters["AAA"].Value == "BB");
    //        Assert.IsTrue(m_InstanceReceiver.changed[0].instance.Metadata.Parameters["AAA"].ParameterGroup == "123");
    //        Assert.IsTrue(m_InstanceReceiver.changed[0].instance.Metadata.Parameters["AAA"].Visible);
    //        
    //        // Remove Metadata
    //        
    //        // Arrange
    //        instanceA2.Metadata.Parameters.Clear();
    //
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(0, 1, 0, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "InstanceA2", m_InstanceReceiver.changed);
    //        
    //        Assert.IsTrue(m_InstanceReceiver.changed[0].instance.Metadata.Parameters.Count == 0);
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Changing Instance BoundingBox
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // TODO
    //
    //        //----------------------------------------------------------------------------------------------
    //        // Changing Instance SyncObject Id
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Added new Object
    //        
    //        // Arrange
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectC"), null, "111", k_DefaultBBox);
    //        instanceA3.ObjectId = new SyncId("ObjectC");
    //
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(1, 0, 1, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectC", "InstanceA3", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.removed);
    //        
    //        // Switching to existing Object
    //        
    //        // Arrange
    //        instanceA3.ObjectId = new SyncId("ObjectA");
    //
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(1, 0, 1, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectC", "InstanceA3", m_InstanceReceiver.removed);
    //    }
    //    
    //    [UnityTest, Timeout(k_Timeout)]
    //    public IEnumerator StreamInstance_SyncObjectChanges()
    //    {
    //        var dummyProject = new UnityProject(null, "dummy", "Dummy");
    //        
    //        var streamSourceA = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceA");
    //        var syncManifestA = streamSourceA.manifest;
    //        var syncPrefabA = streamSourceA.prefab;
    //        
    //        var streamSourceB = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceB");
    //        var syncManifestB = streamSourceB.manifest;
    //        var syncPrefabB = streamSourceB.prefab;
    //
    //        var projectStreamer = CreateProjectStreamer();
    //        
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "111", k_DefaultBBox);
    //        var instanceA1 = CreateInstance("A1", "ObjectA", "InstanceA1");
    //        var instanceA2 = CreateInstance("A2", "ObjectA", "InstanceA2");
    //        var instanceA3 = CreateInstance("A3", "ObjectA", "InstanceA3");
    //        var instanceA4 = CreateInstance("A4", "ObjectA", "InstanceA4");
    //        syncPrefabA.Instances.Add(instanceA1);
    //        syncPrefabA.Instances.Add(instanceA2);
    //        syncPrefabA.Instances.Add(instanceA3);
    //        syncPrefabA.Instances.Add(instanceA4);
    //
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "333", k_DefaultBBox);
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectB"), null, "222", k_DefaultBBox);
    //        var instanceB1 = CreateInstance("B1", "ObjectB", "InstanceB1");
    //        var instanceB2 = CreateInstance("B2", "ObjectA", "InstanceB2");
    //        syncPrefabB.Instances.Add(instanceB1);
    //        syncPrefabB.Instances.Add(instanceB2);
    //        
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        AssertReceiverState(6, 0, 0, m_InstanceReceiver);
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Changing SyncObject From SourceA
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Arrange
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "1111", k_DefaultBBox);
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(4, 0, 4, m_InstanceReceiver); // TODO Investigate this behaviour
    //        AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "InstanceA1", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "InstanceA2", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A4", "ObjectA", "InstanceA4", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "InstanceA1", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "InstanceA2", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A4", "ObjectA", "InstanceA4", m_InstanceReceiver.removed);
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Changing SyncObject From SourceB
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Arrange
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "2222", k_DefaultBBox);
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(1, 0, 1, m_InstanceReceiver); // TODO Investigate this behaviour
    //        AssertStreamInstance(dummyProject, "SourceB", "B2", "ObjectA", "InstanceB2", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceB", "B2", "ObjectA", "InstanceB2", m_InstanceReceiver.removed);
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Changing Both SyncInstance And SyncObject
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Arrange
    //        instanceA2.Name = "Renamed InstanceA2";
    //        instanceA1.Transform = new SyncTransform(new Vector3(4.0f, 5.0f, 6.0f), Quaternion.Identity, Vector3.One);
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "3333", k_DefaultBBox);
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(4, 0, 4, m_InstanceReceiver); // TODO Investigate this behaviour
    //        AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "InstanceA1", new SyncTransform(new Vector3(4.0f, 5.0f, 6.0f), Quaternion.Identity, Vector3.One), m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "Renamed InstanceA2", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A4", "ObjectA", "InstanceA4", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "InstanceA1", new SyncTransform(Vector3.Zero, Quaternion.Identity, Vector3.One), m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "InstanceA2", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A4", "ObjectA", "InstanceA4", m_InstanceReceiver.removed);
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Changing SyncObject And Removing SyncInstance
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Arrange
    //        syncPrefabA.Instances.Remove(instanceA2);
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "4444", k_DefaultBBox);
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(3, 0, 4, m_InstanceReceiver); // TODO Investigate this behaviour
    //        AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "InstanceA1", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A4", "ObjectA", "InstanceA4", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "InstanceA1", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "Renamed InstanceA2", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A4", "ObjectA", "InstanceA4", m_InstanceReceiver.removed);
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Removing SyncObject
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // TODO: Should this remove referenced instances?
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Changing SyncInstance And Removing SyncObject
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // TODO: Should this remove referenced instances?
    //    }
    //    
    //    [UnityTest, Timeout(k_Timeout)]
    //    public IEnumerator StreamInstance_AddingRemovingAndChangingInstances()
    //    {
    //        var dummyProject = new UnityProject(null, "dummy", "Dummy");
    //        
    //        var streamSourceA = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceA");
    //        var syncManifestA = streamSourceA.manifest;
    //        var syncPrefabA = streamSourceA.prefab;
    //        
    //        var streamSourceB = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceB");
    //        var syncManifestB = streamSourceB.manifest;
    //        var syncPrefabB = streamSourceB.prefab;
    //
    //        var projectStreamer = CreateProjectStreamer();
    //        
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "111", k_DefaultBBox);
    //        var instanceA1 = CreateInstance("A1", "ObjectA", "InstanceA1");
    //        var instanceA2 = CreateInstance("A2", "ObjectA", "InstanceA2");
    //        var instanceA3 = CreateInstance("A3", "ObjectA", "InstanceA3");
    //        var instanceA4 = CreateInstance("A4", "ObjectA", "InstanceA4");
    //        syncPrefabA.Instances.Add(instanceA1);
    //        syncPrefabA.Instances.Add(instanceA2);
    //        syncPrefabA.Instances.Add(instanceA3);
    //        syncPrefabA.Instances.Add(instanceA4);
    //
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "333", k_DefaultBBox);
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectB"), null, "222", k_DefaultBBox);
    //        var instanceB1 = CreateInstance("B1", "ObjectB", "InstanceB1");
    //        var instanceB2 = CreateInstance("B2", "ObjectA", "InstanceB2");
    //        syncPrefabB.Instances.Add(instanceB1);
    //        syncPrefabB.Instances.Add(instanceB2);
    //        
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        AssertReceiverState(6, 0, 0, m_InstanceReceiver);
    //        
    //        // Arrange
    //        instanceA1.Name = "Renamed InstanceA1";
    //        instanceA2.Transform = new SyncTransform(new Vector3(1.0f, 2.0f, 3.0f), Quaternion.Identity, Vector3.One);
    //        syncPrefabB.Instances.Add(CreateInstance("B3", "ObjectB", "InstanceB3"));
    //        syncPrefabA.Instances.Remove(instanceA3);
    //        syncPrefabB.Instances.Remove(instanceB1);
    //        syncPrefabA.Instances.Remove(instanceA4);
    //
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(1, 2, 3, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceB", "B3", "ObjectB", "InstanceB3", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "Renamed InstanceA1", m_InstanceReceiver.changed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "InstanceA2", new SyncTransform(new Vector3(1.0f, 2.0f, 3.0f), Quaternion.Identity, Vector3.One), m_InstanceReceiver.changed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A4", "ObjectA", "InstanceA4", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceB", "B1", "ObjectB", "InstanceB1", m_InstanceReceiver.removed);
    //    }
    //    
    //    [UnityTest, Timeout(k_Timeout)]
    //    public IEnumerator StreamInstance_CompletelyNewInstances()
    //    {
    //        var dummyProject = new UnityProject(null, "dummy", "Dummy");
    //        
    //        var streamSourceA = m_ReflectClientMock.CreateStreamSource(dummyProject, "SourceA");
    //        var syncManifestA = streamSourceA.manifest;
    //        var syncPrefabA = streamSourceA.prefab;
    //
    //        var projectStreamer = CreateProjectStreamer();
    //        
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "111", k_DefaultBBox);
    //        CreateInstance(dummyProject, "SourceA", syncManifestA, "A1", "ObjectA", "InstanceA1"));
    //        CreateInstance(dummyProject, "SourceA", syncManifestA, "A2", "ObjectA", "InstanceA2"));
    //        CreateInstance(dummyProject, "SourceA", syncManifestA, "A3", "ObjectA", "InstanceA3"));
    //
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        AssertReceiverState(3, 0, 0, m_InstanceReceiver);
    //        
    //        // Arrange
    //        syncPrefabA.Instances.Clear();
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectB"), null, "222", k_DefaultBBox);
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectC"), null, "333", k_DefaultBBox);
    //        CreateInstance(dummyProject, "SourceA", syncManifestA, "B1", "ObjectB", "InstanceB1"));
    //        CreateInstance(dummyProject, "SourceA", syncManifestA, "B2", "ObjectB", "InstanceB2"));
    //        CreateInstance(dummyProject, "SourceA", syncManifestA, "C1", "ObjectC", "InstanceC1"));
    //        CreateInstance(dummyProject, "SourceA", syncManifestA, "C2", "ObjectC", "InstanceC2"));
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProject);
    //        
    //        // Assert
    //        AssertReceiverState(4, 0, 3, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProject, "SourceA", "A1", "ObjectA", "InstanceA1", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A2", "ObjectA", "InstanceA2", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "A3", "ObjectA", "InstanceA3", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProject, "SourceA", "B1", "ObjectB", "InstanceB1", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "B2", "ObjectB", "InstanceB2", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "C1", "ObjectC", "InstanceC1", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProject, "SourceA", "C2", "ObjectC", "InstanceC2", m_InstanceReceiver.added);
    //    }
    //
    //    [UnityTest, Timeout(k_Timeout)]
    //    public IEnumerator MultipleProjects()
    //    {
    //        // Project A
    //        var dummyProjectA = new UnityProject(null, "dummyA", "DummyA");
    //        
    //        var streamSourceA = m_ReflectClientMock.CreateStreamSource(dummyProjectA, "SourceA");
    //        var syncManifestA = streamSourceA.manifest;
    //        var syncPrefabA = streamSourceA.prefab;
    //
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectA"), null, "111", k_DefaultBBox);
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectB"), null, "111", k_DefaultBBox);
    //        var instanceA1 = CreateInstance("A1", "ObjectA", "InstanceA1");
    //        var instanceA2 = CreateInstance("A2", "ObjectA", "InstanceA2");
    //        var instanceB1 = CreateInstance("B1", "ObjectB", "InstanceB1");
    //        var instanceB2 = CreateInstance("B2", "ObjectB", "InstanceB2");
    //        syncPrefabA.Instances.Add(instanceA1);
    //        syncPrefabA.Instances.Add(instanceA2);
    //        syncPrefabA.Instances.Add(instanceB1);
    //        syncPrefabA.Instances.Add(instanceB2);
    //        
    //        // Project B
    //        var dummyProjectB = new UnityProject(null, "dummyB", "DummyB");
    //        
    //        var streamSourceB = m_ReflectClientMock.CreateStreamSource(dummyProjectB, "SourceA");
    //        var syncManifestB = streamSourceB.manifest;
    //
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectB"), null, "111", k_DefaultBBox);
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectC"), null, "111", k_DefaultBBox);
    //        var instanceB1B = CreateInstance("B1", "ObjectB", "InstanceB1");
    //        var instanceC1 = CreateInstance("C1", "ObjectC", "InstanceC1");
    //        var instanceC2 = CreateInstance("C2", "ObjectC", "InstanceC2");
    //        syncPrefabB.Instances.Add(instanceB1B);
    //        syncPrefabB.Instances.Add(instanceC1);
    //        syncPrefabB.Instances.Add(instanceC2);
    //        
    //        var projectStreamer = CreateProjectStreamer();
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Stream Project A
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProjectA);
    //        
    //        // Assert
    //        AssertReceiverState(2, 0, 0, m_AssetReceiver);
    //        AssertStreamAsset<SyncObject>(dummyProjectA, "SourceA", "111", "ObjectA", m_AssetReceiver.added);
    //        AssertStreamAsset<SyncObject>(dummyProjectA, "SourceA", "111", "ObjectB", m_AssetReceiver.added);
    //        
    //        AssertReceiverState(4, 0, 0, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProjectA, "SourceA", "A1", "ObjectA", "InstanceA1", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProjectA, "SourceA", "A2", "ObjectA", "InstanceA2", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProjectA, "SourceA", "B1", "ObjectB", "InstanceB1", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProjectA, "SourceA", "B2", "ObjectB", "InstanceB2", m_InstanceReceiver.added);
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Stream Project B
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProjectB);
    //        
    //        // Assert
    //        AssertReceiverState(2, 0, 0, m_AssetReceiver);
    //        AssertStreamAsset<SyncObject>(dummyProjectB, "SourceA", "111", "ObjectB", m_AssetReceiver.added);
    //        AssertStreamAsset<SyncObject>(dummyProjectB, "SourceA", "111", "ObjectC", m_AssetReceiver.added);
    //        
    //        AssertReceiverState(3, 0, 0, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProjectB, "SourceA", "B1", "ObjectB", "InstanceB1", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProjectB, "SourceA", "C1", "ObjectC", "InstanceC1", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProjectB, "SourceA", "C2", "ObjectC", "InstanceC2", m_InstanceReceiver.added);
    //        
    //        //----------------------------------------------------------------------------------------------
    //        // Modify
    //        //----------------------------------------------------------------------------------------------
    //        
    //        // Arrange
    //        
    //        // Modify Project A
    //        instanceA1.Name = "Renamed InstanceA1";
    //        instanceB1.Name = "Renamed Project A Instance B1";
    //        instanceB1.Transform = new SyncTransform(Vector3.Zero, Quaternion.Identity, new Vector3(1.0f, 2.0f, 3.0f));
    //        syncPrefabA.Instances.Remove(instanceA2);
    //        
    //        syncManifestA.Append(PersistentKey.GetKey<SyncObject>("ObjectD"), null, "111", k_DefaultBBox);
    //        var instanceD1 = CreateInstance("D1", "ObjectD", "InstanceD1");
    //        syncPrefabA.Instances.Add(instanceD1);
    //        
    //        // Modify Project B
    //        instanceB1B.Name = "Renamed Project B Instance B1";
    //        syncPrefabB.Instances.Remove(instanceC1);
    //        syncManifestB.Append(PersistentKey.GetKey<SyncObject>("ObjectB"), null, "222", k_DefaultBBox);
    //
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProjectB);
    //
    //        // Assert
    //        AssertReceiverState(0, 1, 0, m_AssetReceiver);
    //        AssertStreamAsset<SyncObject>(dummyProjectB, "SourceA", "222", "ObjectB", m_AssetReceiver.changed);
    //        
    //        AssertReceiverState(1, 0, 2, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProjectB, "SourceA", "B1", "ObjectB", "Renamed Project B Instance B1", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProjectB, "SourceA", "B1", "ObjectB", "InstanceB1", m_InstanceReceiver.removed);
    //        AssertStreamInstance(dummyProjectB, "SourceA", "C1", "ObjectC", "InstanceC1", m_InstanceReceiver.removed);
    //        
    //        // Act
    //        yield return StartStreaming(projectStreamer, dummyProjectA);
    //
    //        // Assert
    //        AssertReceiverState(1, 0, 0, m_AssetReceiver);
    //        AssertStreamAsset<SyncObject>(dummyProjectA, "SourceA", "111", "ObjectD", m_AssetReceiver.added);
    //        
    //        AssertReceiverState(1, 2, 1, m_InstanceReceiver);
    //        AssertStreamInstance(dummyProjectA, "SourceA", "D1", "ObjectD", "InstanceD1", m_InstanceReceiver.added);
    //        AssertStreamInstance(dummyProjectA, "SourceA", "A1", "ObjectA", "Renamed InstanceA1", m_InstanceReceiver.changed);
    //        AssertStreamInstance(dummyProjectA, "SourceA", "B1", "ObjectB", "Renamed Project A Instance B1", new SyncTransform(Vector3.Zero, Quaternion.Identity, new Vector3(1.0f, 2.0f, 3.0f)), m_InstanceReceiver.changed);
    //        AssertStreamInstance(dummyProjectA, "SourceA", "A2", "ObjectA", "InstanceA2", m_InstanceReceiver.removed);
    //    }

        static readonly SyncBoundingBox k_DefaultBBox = CreateBBox(0.0f, 0.0f, 0.0f);

        static SyncBoundingBox CreateBBox(float sizeX, float sizeY, float sizeZ)
        {
            var h = new System.Numerics.Vector3(sizeX * 0.5f, sizeY  * 0.5f, sizeZ * 0.5f);
            return new SyncBoundingBox(h , -h);
        }
        
        IEnumerator StartStreaming(ProjectStreamer projectStreamer, UnityProject project)
        {
            m_AssetReceiver.Reset();
            m_InstanceReceiver.Reset();
            projectStreamer.Start();
            
            // Wait for Task to finish. This loop is protected with a test Timeout
            while (m_AssetReceiver.end <= 0)
                yield return null;
        }

        static void AssertReceiverState<T>(int expectedAdded, int expectedChanged, int expectedRemoved, TestProjectStreamReceiver<T> receiver)
        {
            Assert.AreEqual(1, receiver.begin, "Begin");

            Assert.AreEqual(expectedAdded, receiver.added.Count, "Added Count Mismatch");
            Assert.AreEqual(expectedChanged, receiver.changed.Count, "Changed Count Mismatch");
            Assert.AreEqual(expectedRemoved, receiver.removed.Count, "Removed Count Mismatch");
            
            Assert.AreEqual(1, receiver.end, "End");
        }

        static void AssertStreamAsset<T>(UnityProject expectedProject, string expectedSourceId, string expectedHash,
            string expectedKey, ICollection streamAssets) where T : ISyncModel
        {
            var key = PersistentKey.GetKey<T>(expectedKey);
            var streamAsset = new StreamAsset(PersistentKey.GetKey<T>(expectedKey), expectedProject, expectedSourceId, expectedHash);

            Assert.Contains(streamAsset, streamAssets, $"StreamAsset Not Found: {key}");
        }

        static void AssertStreamInstance(UnityProject expectedProject, string expectedSourceId, string id, string objectId, string name, IList<StreamInstance> streamInstances)
        {
            var result = streamInstances.FirstOrDefault(
                s => s.project == expectedProject &&
                    s.sourceId == expectedSourceId &&
                    s.instance.Id == new SyncId(id) &&
                    s.instance.ObjectId == new SyncId(objectId) &&
                    s.instance.Name == name
            );
            
            Assert.IsTrue(result.instance != null, $"StreamInstance Not Found: {name}");
        }
        
        static void AssertStreamInstance(UnityProject expectedProject, string expectedSourceId, string id, string objectId, string name, SyncTransform transform, IList<StreamInstance> streamInstances)
        {
            var result = streamInstances.FirstOrDefault(
                s => s.project == expectedProject &&
                    s.sourceId == expectedSourceId &&
                    s.instance.Id == new SyncId(id) &&
                    s.instance.ObjectId == new SyncId(objectId) &&
                    s.instance.Name == name &&
                    s.instance.Transform == transform
            );
            
            Assert.IsTrue(result.instance != null, $"StreamInstance Not Found: {name}");
        }

        SyncObjectInstance CreateInstance(UnityProject project, string sourceId, SyncManifest manifest, string id, string objectId, string name)
        {
            var key = PersistentKey.GetKey<SyncObjectInstance>(id);
            manifest.Append(key, null, $"{id}_{id}", k_DefaultBBox);
            return m_ReflectClientMock.AddSyncObjectInstance(project, sourceId, id, objectId, name, key);
        }
    }

    abstract class TestProjectStreamReceiver<T> : IStreamInput<T>
    {
        public int begin = 0;
        public List<T> added = new List<T>();
        public List<T> changed = new List<T>();
        public List<T> removed = new List<T>();
        public int end = 0;

        public void Reset()
        {
            begin = 0;
            added.Clear();
            changed.Clear();
            removed.Clear();
            end = 0;
        }
        
        public void OnBegin(IStreamOutput<T> output)
        {
            ++begin;
        }

        public void OnStreamAdded(IStreamOutput<T> output, T stream)
        {
            added.Add(stream);
        }

        public void OnStreamChanged(IStreamOutput<T> output, T stream)
        {
            changed.Add(stream);
        }

        public void OnStreamRemoved(IStreamOutput<T> output, T stream)
        {
            removed.Add(stream);
        }

        public void OnEnd(IStreamOutput<T> output)
        {
            ++end;
        }
    }

    class TestStreamAssetReceiver : TestProjectStreamReceiver<StreamAsset>
    {
    }

    class TestStreamInstanceReceiver : TestProjectStreamReceiver<StreamInstance>
    {
    }

    class ReflectClientMock : IReflectClient
    {
        Dictionary<string, List<StreamSource>> m_StreamSources = new Dictionary<string, List<StreamSource>>();
        
        Dictionary<string, ISyncModel> m_Instances = new Dictionary<string, ISyncModel>();

        public StreamSource CreateStreamSource(UnityProject project, string sourceId)
        {
            var streamSource = new StreamSource(sourceId, new SyncManifest());
            
            if (!m_StreamSources.TryGetValue(project.ProjectId, out var sources))
            {
                sources = m_StreamSources[project.ProjectId] = new List<StreamSource>();
            }
            
            sources.Add(streamSource);
                
            return streamSource;
        }

        public SyncObjectInstance AddSyncObjectInstance(UnityProject project, string sourceId, string id, string objectId, string name, PersistentKey key)
        {
            var instance = new SyncObjectInstance(new SyncId(id), name, new SyncId(objectId));

            var k = GetKey(project, sourceId, key);
            m_Instances[k] = instance;

            return instance;
        }

        static string GetKey(UnityProject project, string sourceId, PersistentKey key)
        {
            return $"{project.ProjectId}_{sourceId}_{key.ToString()}";
        }

        public void Initialize(IUpdateDelegate updateDelegate, UnityUser user, IReflectStorage storage)
        {
            // nothing to do
        }

        public Task<UnityProjectCollection> ListProjects() // TODO Should we remove this from IReflectClient?
        {
            return null; // Not Needed
        }

        public Task<ISyncModel> GetSyncModelAsync(UnityProject project, string sourceId, PersistentKey key, string hash) // TODO Should we remove this from IReflectClient?
        {
            if (!key.IsKeyFor<SyncObjectInstance>())
                return null;

            var k = GetKey(project, sourceId, key);
            return Task.FromResult(m_Instances[k]);
        }

        public Action manifestUpdated { get; set; }

        public Task<IEnumerable<StreamSource>> GetStreamSourcesAsync(UnityProject project)
        {
            return Task.FromResult(m_StreamSources[project.ProjectId].AsEnumerable());
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    class SimpleUpdateDelegate : MonoBehaviour, IUpdateDelegate
    {
        public event Action<float> update;

        void Update()
        {
            update?.Invoke(Time.unscaledDeltaTime);
        }
    }
}
*/
