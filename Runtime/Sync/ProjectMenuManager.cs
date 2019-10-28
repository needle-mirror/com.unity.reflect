// Prevent warnings for field not assigned to
#pragma warning disable 0649

using System;
using System.Collections;
using UnityEngine;

namespace UnityEngine.Reflect
{    
    public class ProjectMenuManager : MonoBehaviour
    {     
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
            m_ProjectManager.onProjectAdded += OnProjectChanged;
            m_ProjectManager.onProjectChanged += OnProjectChanged;

            m_ProgressBar.Register(m_ProjectManager);
            
            if (m_ListControl != null)
            {
                m_ListControl.SetDataSource(m_Source);
                m_ListControl.onOpen += OnProjectOpen;
                m_ListControl.onDownload += OnProjectDownload;
                m_ListControl.onDelete += OnProjectDelete;
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

        void OnDisable()
        {
            m_ProjectManager.onProjectAdded -= OnProjectChanged;
            m_ProjectManager.onProjectChanged -= OnProjectChanged;
            
            m_ProgressBar.UnRegister(m_ProjectManager);
            
            if (m_ListControl != null)
            {
                m_ListControl.onOpen -= OnProjectOpen;
                m_ListControl.onDownload -= OnProjectDownload;
            }
        }

        void OnProjectOpen(ListControlItemData itemData)
        {           
            Debug.Log($"Opening : {itemData.id}");
            m_SyncManager.Close();
            m_Menu.OnCancel();
            StartCoroutine(DownloadAndOpen((Project)itemData.payload));
        }

        IEnumerator DownloadAndOpen(Project project)
        {
            if(!m_SyncManager.IsProjectOpened(project))
            {
                yield return m_ProjectManager.DownloadProjectLocally(project.serverProjectId, true);
                yield return m_SyncManager.Open(project);
            }
        }

        void OnProjectDownload(ListControlItemData itemData)
        {
            Debug.Log($"Downloading : {itemData.id}");
            if(itemData.payload is Project)
            {
                StartCoroutine(m_ProjectManager.DownloadProjectLocally(((Project)itemData.payload).serverProjectId, true));
            }
        }
        
        void OnProjectDelete(ListControlItemData itemData)
        {
            const string kDeleteTitle = "Delete Project";
            var confirmDeleteMessage = $"Delete local data associated with {itemData.title} from this device?";
            m_Dialog.Show(kDeleteTitle, confirmDeleteMessage, () =>
            {
                m_SyncManager.Close();
                StartCoroutine(m_ProjectManager.DeleteProjectLocally((Project)itemData.payload));
            });
        }

        ListControlItemData.Option ResolveAvailableOptions(Project project)
        {
            var availableOffline = m_ProjectManager.IsProjectAvailableOffline(project);
            var availableOnline = m_ProjectManager.IsProjectAvailableOnline(project);
            
            var options = ListControlItemData.Option.None;

            if (!m_ProjectManager.IsProjectVisibleToUser(project))
            {
                return options;
            }

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