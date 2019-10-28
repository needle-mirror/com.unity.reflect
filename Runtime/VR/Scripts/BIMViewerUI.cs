using System.Collections.Generic;
using Unity.Labs.Utils;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Menus;
using UnityEngine.UI;
using static UnityEngine.Reflect.Metadata;

namespace UnityEngine.Reflect
{
    public class BIMViewerUI : MonoBehaviour
    {
        public Transform ContentParent;
        [SerializeField] protected BIMParameterUI m_ParameterTemplate;

        public void RefreshMetaData(Metadata metadata)
        {
            for (int i = 0; i < ContentParent.childCount; ++i)
            {
                ContentParent.GetChild(i).gameObject.SetActive(false);
            }

            if (metadata == null)
            {
                SetParameter(0, "No metadata", "No metadata");
            }
            else
            { 
                int index = 0;
                Dictionary<string, Parameter>.Enumerator parametersEnumerator = metadata.GetParameters().GetEnumerator();
                while (parametersEnumerator.MoveNext())
                {
                    KeyValuePair<string, Parameter> current = parametersEnumerator.Current;
                    if (!string.IsNullOrEmpty(current.Key) && !string.IsNullOrEmpty(current.Value.value))
                    {
                        SetParameter(index, current.Key, current.Value.value);
                        ++index;
                    }
                }
            }
        }

        protected void SetParameter(int index, string title, string value)
        {
            BIMParameterUI parameterUI;
            if (index < ContentParent.childCount)
            {
                parameterUI = ContentParent.transform.GetChild(index).GetComponent<BIMParameterUI>();
            }
            else
            {
                parameterUI = Instantiate(m_ParameterTemplate.gameObject, ContentParent).GetComponent<BIMParameterUI>();
            }

            parameterUI.Init(title, value);
            parameterUI.gameObject.SetActive(true);
        }
    }
}
