using System;
using System.Collections;
using Grpc.Core;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public class ProjectMenuManager : MonoBehaviour
    {
        public const string ReflectLandingPageUrl = "https://unity.com/aec/reflect";

#pragma warning disable 0649
        [SerializeField]
        ListControl m_ListControl;

        [SerializeField]
        ProjectsTopMenu m_Menu;// TODO Find another way to close menu when opening a item.

        [SerializeField]
        ProjectManager m_ProjectManager;

        [SerializeField]
        SyncManager m_SyncManager;

        [SerializeField]
        ProgressBar m_ProgressBar;

        [SerializeField]
        Dialog m_Dialog;

        [SerializeField]
        AlertDialog m_AlertDialog;
#pragma warning restore 0649

        ListControlDataSource m_Source = new ListControlDataSource();

        ListControlItemData ProjectToListItem(Project project)
        {
            var listItem = new ListControlItemData
            {
                id = project.serverProjectId,
                projectId = project.projectId,
                title = project.name,
                description = project.description,
                date = DateTime.Today, // TODO
                options = ResolveAvailableOptions(project),
                enabled = true,
                payload = project
            };

            return listItem;
        }

        void OnEnable()
        {
            m_ProjectManager.onProjectsRefreshBegin += OnProjectsRefreshBegin;
            m_ProjectManager.onProjectsRefreshEnd += OnProjectsRefreshEnd;

            m_ProjectManager.onProjectAdded += OnProjectChanged;
            m_ProjectManager.onProjectChanged += OnProjectChanged;
            m_ProjectManager.onProjectRemoved += OnProjectRemoved;

            m_ProjectManager.onError += OnError;
            m_SyncManager.onError += OnError;
            
            m_ProgressBar.Register(m_ProjectManager);

            if (m_ListControl != null)
            {
                m_ListControl.SetDataSource(m_Source);
                m_ListControl.onOpen += OpenProject;
                m_ListControl.onDownload += DownloadProject;
                m_ListControl.onDelete += DeleteProject;
            }

            if(m_Menu != null)
            {
                m_Menu.OnVisiblityChanged += OnProjectMenuVisibilityChanged;
            }

            RefreshProjectLists();
        }

        void RefreshProjectLists()
        {
            foreach (var project in m_ProjectManager.Projects)
            {
                OnProjectChanged(project);
            }
        }

        void OnProjectMenuVisibilityChanged(bool isVisible)
        {
            if (m_ProjectManager != null)
            {
                if (isVisible)
                {
                    RefreshProjectLists();
                    m_ProjectManager.StartDiscovery();
                }
            }
        }
        void OnProjectChanged(Project project)
        {
            var item = ProjectToListItem(project);
            m_Source.AddOrUpdateItem(item);
        }

        void OnProjectRemoved(Project project)
        {
            m_Source.RemoveItem(project.serverProjectId);
        }

        void OnDisable()
        {
            m_ProjectManager.onProjectsRefreshBegin -= OnProjectsRefreshBegin;
            m_ProjectManager.onProjectsRefreshEnd -= OnProjectsRefreshEnd;

            m_ProjectManager.onProjectAdded -= OnProjectChanged;
            m_ProjectManager.onProjectChanged -= OnProjectChanged;
            m_ProjectManager.onProjectRemoved -= OnProjectRemoved;

            m_ProgressBar.UnRegister(m_ProjectManager);

            if (m_ListControl != null)
            {
                m_ListControl.onOpen -= OpenProject;
                m_ListControl.onDownload -= DownloadProject;
            }
        }


        void OnProjectsRefreshBegin()
        {
            m_Menu.RefreshButtonEnabled = false;
        }

        void OnProjectsRefreshEnd()
        {
            m_Menu.RefreshButtonEnabled = true;
        }

        void OpenProject(ListControlItemData itemData)
        {
            Debug.Log($"Opening : {itemData.id}");
            StartCoroutine(OpenProject((Project)itemData.payload, true));
        }

        void OnError(Exception exception)
        {
            var rpcException = exception as RpcException;
            var msg = rpcException != null ? rpcException.Status.Detail : exception.Message;
            var isComplianceError = false;

            #if UNITY_EDITOR            
            isComplianceError = (exception is ProjectListRefreshException projectException &&
                projectException.Status == Unity.Reflect.UnityProjectCollection.StatusOption.ComplianceError);
            #endif

            if (isComplianceError)
            {
                var buttonOptions = new AlertDialog.ButtonOptions()
                {
                    onClick = () => Application.OpenURL(ReflectLandingPageUrl),
                    text = "Learn More"
                };
                m_AlertDialog.Show(exception.Message, buttonOptions);
            }
            else
            {
                m_AlertDialog.Close();
                m_Dialog.ShowError("Error", msg);
            }
        }

        IEnumerator OpenProject(Project project, bool tryDownloadProject)
        {
            if (m_SyncManager.IsProjectOpened(project))
            {
                yield break;
            }

            if (m_ProjectManager.IsProjectAvailableOnline(project) && tryDownloadProject)
            {                
                yield return m_ProjectManager.DownloadProjectLocally(project, true, _ =>
                {
                    // We want to catch download errors but still open the project if it is available offline
                    if (m_ProjectManager.IsProjectAvailableOffline(project))
                    {
                        StartCoroutine(OpenProject(project, false));
                    }
                });                    
            }

            m_Menu.OnCancel();
            yield return m_SyncManager.Open(project);
        }

        void DownloadProject(ListControlItemData itemData)
        {
            Debug.Log($"Downloading : {itemData.id}");
            var project = (Project)itemData.payload;
            StartCoroutine(m_ProjectManager.DownloadProjectLocally(project, true, null));
        }

        void DeleteProject(ListControlItemData itemData)
        {
            const string kDeleteTitle = "Delete Project";
            var confirmDeleteMessage = $"Delete local data associated with {itemData.title} from this device?";
            m_Dialog.Show(kDeleteTitle, confirmDeleteMessage, () =>
            {
                var project = (Project)itemData.payload;
                if (m_SyncManager.IsProjectOpened(project))
                {
                    m_SyncManager.Close();
                }
                StartCoroutine(m_ProjectManager.DeleteProjectLocally(project));
            });
        }

        ListControlItemData.Option ResolveAvailableOptions(Project project)
        {
            var availableOffline = m_ProjectManager.IsProjectAvailableOffline(project);
            var availableOnline = m_ProjectManager.IsProjectAvailableOnline(project);

            var options = ListControlItemData.Option.None;

            if (availableOffline)
            {
                options |= ListControlItemData.Option.LocalFiles;
            }

            if ((availableOnline || availableOffline) && !m_SyncManager.IsProjectOpened(project))
            {
                options |= ListControlItemData.Option.Open;
            }

            if (availableOnline && availableOffline)
            {
                options |= ListControlItemData.Option.UpToDate;
            }

            if (availableOnline)
                // TODO Check if out of date.
                // TODO Check if project is not empty.
            {
                options |= ListControlItemData.Option.Download;
                options |= ListControlItemData.Option.Connected;
            }

            return options;
        }
    }
}