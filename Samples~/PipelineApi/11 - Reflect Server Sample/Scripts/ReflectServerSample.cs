using System;
using System.Collections.Generic;
using System.IO;
using Unity.Reflect.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Reflect;
using UnityEngine.Reflect.Pipeline;

namespace Unity.Reflect.Samples
{
    // This sample shows how to use the Reflect authentication to connect the cloud service, list the available projects
    // hosted on the cloud, then open a selected project using a basic Reflect pipeline asset.
    public class ReflectServerSample : MonoBehaviour, IUpdateDelegate
    {
        public Transform root;

        public event Action<float> update;
        
        PlayerStorage m_Storage;
        ReflectClient m_ReflectClient;
        AuthClient m_AuthClient;
        
        ProjectsLister m_ProjectsLister;

        ReflectPipeline m_ReflectPipeline;
        
        List<Project> m_AvailableProjects;

        enum State
        {
            WaitForLogin,
            LoginFailed,
            ListingProjects,
            ProjectListed,
            OpeningProject
        }

        State m_State = State.WaitForLogin;

        // This method is called from the LoginManager component inside the sample Scene.
        // This is the same LoginManager used in the Reflect Viewers.
        // While in the Editor, the user is the current Unity account logged into the Unity Editor.
        // Once logged in, we can list available projects by using a ProjectLister with an AuthClient.
        public void OnUserLoggedIn(UnityUser user)
        {
            m_State = State.ListingProjects;

            // A storage is required to specify where data are saved and cached
            m_Storage = new PlayerStorage(Path.Combine(Application.dataPath, ".ReflectSamplesTemp"), false, false);
            
            // Create a Authentication client from the current Unity user
            m_AuthClient = new AuthClient(user, m_Storage);
            
            var projectListerSettings = new ProjectListerSettings
            {
                OnProjectsRefreshCompleted = new ProjectListerSettings.ProjectsEvents(),
                OnProjectsRefreshStarted = new UnityEvent()
            };
            
            projectListerSettings.OnProjectsRefreshCompleted.AddListener( (projects) =>
            {
                // This is the callback for when all projects are done listing.
                // For this sample, we populate a list that will be displayed as UI in the OnGUI method.
                m_State = State.ProjectListed;
                m_AvailableProjects = projects;
            });

            // Create a ProjectLister to enumerate all available projects in the cloud for this user
            m_ProjectsLister = new ProjectsLister(projectListerSettings) { client = m_AuthClient };

            // Since ProjectLister runs in another thread, make sure to set the UpdateDelegate to be able to collect received data into the main thread.
            m_ProjectsLister.SetUpdateDelegate(this);
            
            // Start the ProjectLister thread. OnProjectsRefreshCompleted callback will be called when all projects are done listing.
            m_ProjectsLister.Run();
        }

        public void OnAuthenticationFailed(string exception)
        {
            Debug.LogError($"Authentication failed: {exception}");
            m_State = State.LoginFailed;
        }

        void Update()
        {
            // As part of the IUpdateDelegate, the update event needs to be called periodically to get notifications coming from other threads.
            // In this sample, both the ProjectLister and the ReflectClient require an IUpdateDelegate to execute properly.
            update?.Invoke(Time.unscaledDeltaTime);
        }
        
        void OpenProject(Project project)
        {
            m_State = State.OpeningProject;
            
            // Prepare the folder for project data
            var projectFolder = m_Storage.GetProjectFolder(project);
            Directory.CreateDirectory(projectFolder);
            
            // Create the ISyncProvider (ReflectClient).
            m_ReflectClient = new ReflectClient(this, m_AuthClient.user,  m_AuthClient.storage, project);
            
            // Create a ReflectPipeline component
            m_ReflectPipeline = gameObject.AddComponent<ReflectPipeline>();
            
            // Create a basic PipelineAsset 
            m_ReflectPipeline.pipelineAsset = CreateBasicPipelineAsset();
            
            // Assign root transform to the InstanceConverterNode
            m_ReflectPipeline.TryGetNode<InstanceConverterNode>(out var instanceConverter);
            instanceConverter.SetRoot(root, m_ReflectPipeline);

            // Initialize and run the pipeline
            m_ReflectPipeline.InitializeAndRefreshPipeline(m_ReflectClient);
        }
        
        void OnDisable()
        {
            // Make sure all task are stopped before quitting
            if (m_ReflectPipeline != null)
            {
                m_ReflectPipeline.ShutdownPipeline();
            }

            m_ProjectsLister?.Dispose();
            m_ReflectClient?.Dispose();
            
            update = null;
        }
        
        void OnGUI()
        {
            var r = new Rect(10.0f, 10.0f, 300.0f, 20.0f);

            switch (m_State)
            {
                case State.WaitForLogin:
                    GUI.Label(r, "Logging in...");
                    return;

                case State.LoginFailed:
                    GUI.Label(r, "Authentication failed...");
                    return;
            }
            
            GUI.Label(r, $"Logged in as {m_AuthClient.user.DisplayName}");
            r.y += 30.0f;

            switch (m_State)
            {
                case State.ListingProjects:
                    
                    GUI.Label(r, "Fetching projects...");
                    break;
                
                case State.ProjectListed when m_AvailableProjects.Count == 0:
                    GUI.Label(r, "No Project Found.");
                    break;
                
                case State.ProjectListed:
                {
                    GUI.Label(r, "Available Projects:");
                    r.y += 5.0f;
                    r.height = 25.0f;
                    
                    foreach (var project in m_AvailableProjects)
                    {
                        r.y += 30.0f;
                        if (GUI.Button(r, $"Name: {project.name} - {project.description}"))
                        {
                            // Open selected Project
                            OpenProject(project);
                        }
                    }

                    break;
                }
            }
        }
        
        static PipelineAsset CreateBasicPipelineAsset()
        {
            var pipelineAsset = ScriptableObject.CreateInstance<PipelineAsset>();

            // Nodes

            var projectStreamer = pipelineAsset.CreateNode<ProjectStreamerNode>();
            var instanceProvider = pipelineAsset.CreateNode<SyncObjectInstanceProviderNode>();
            var dataProvider = pipelineAsset.CreateNode<DataProviderNode>();
            var meshConverter = pipelineAsset.CreateNode<MeshConverterNode>();
            var materialConverter = pipelineAsset.CreateNode<MaterialConverterNode>();
            var textureConverter = pipelineAsset.CreateNode<TextureConverterNode>();
            var instanceConverter = pipelineAsset.CreateNode<InstanceConverterNode>();

            // Inputs / Outputs

            pipelineAsset.CreateConnection(projectStreamer.assetOutput, instanceProvider.input);
            pipelineAsset.CreateConnection(instanceProvider.output, dataProvider.instanceInput);
            pipelineAsset.CreateConnection(dataProvider.syncMeshOutput, meshConverter.input);
            pipelineAsset.CreateConnection(dataProvider.syncMaterialOutput, materialConverter.input);
            pipelineAsset.CreateConnection(dataProvider.syncTextureOutput, textureConverter.input);
            pipelineAsset.CreateConnection(dataProvider.instanceDataOutput, instanceConverter.input);

            // Params

            pipelineAsset.SetParam(dataProvider.hashCacheParam, projectStreamer);
            pipelineAsset.SetParam(materialConverter.textureCacheParam, textureConverter);
            pipelineAsset.SetParam(instanceConverter.materialCacheParam, materialConverter);
            pipelineAsset.SetParam(instanceConverter.meshCacheParam, meshConverter);
            
            return pipelineAsset;
        }
    }
}
