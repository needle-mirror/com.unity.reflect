#if UNITY_ANDROID
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor.Android;

/// <summary>
/// See https://stackoverflow.com/a/54894488 for more info
/// </summary>
public class AndroidBuildPostProcess : IPostGenerateGradleAndroidProject
{
    public void OnPostGenerateGradleAndroidProject(string basePath)
    {
        AndroidXmlDocument androidManifest = new AndroidXmlDocument(GetManifestPath(basePath));
        androidManifest.ApplyReflectChanges();
        androidManifest.Save();
    }

    public int callbackOrder { get { return 1; } }

    private string _manifestFilePath;

    private string GetManifestPath(string basePath)
    {
        if (string.IsNullOrEmpty(_manifestFilePath))
        {
            StringBuilder pathBuilder = new StringBuilder(basePath);
            pathBuilder.Append(Path.DirectorySeparatorChar).Append("src");
            pathBuilder.Append(Path.DirectorySeparatorChar).Append("main");
            pathBuilder.Append(Path.DirectorySeparatorChar).Append("AndroidManifest.xml");
            _manifestFilePath = pathBuilder.ToString();
        }
        return _manifestFilePath;
    }
}

internal class AndroidXmlDocument : XmlDocument
{
    private string m_Path;
    protected XmlNamespaceManager nsMgr;
    public readonly string AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";

    public AndroidXmlDocument(string path)
    {
        m_Path = path;
        using (XmlTextReader reader = new XmlTextReader(m_Path))
        {
            reader.Read();
            Load(reader);
        }
        nsMgr = new XmlNamespaceManager(NameTable);
        nsMgr.AddNamespace("android", AndroidXmlNamespace);
    }

    public void Save()
    {
        using (XmlTextWriter writer = new XmlTextWriter(m_Path, new UTF8Encoding(false)))
        {
            writer.Formatting = Formatting.Indented;
            Save(writer);
        }
    }

    internal XmlElement CreateElementWithAttribute(string elementName, string attributeName, string attributeValue)
    {
        XmlElement element = CreateElement(elementName);
        XmlAttribute attribute = CreateAttribute("android", attributeName, AndroidXmlNamespace);
        attribute.Value = attributeValue;
        element.Attributes.Append(attribute);
        return element;
    }

    internal XmlNode GetActivityWithLaunchIntent()
    {
        return SelectSingleNode("/manifest/application/activity[intent-filter/action/@android:name='android.intent.action.MAIN' and " +
                "intent-filter/category/@android:name='android.intent.category.LAUNCHER']", nsMgr);
    }

    internal XmlNode RenameActivityForDeepLink(XmlNode mainActivity)
    {
        mainActivity.Attributes.GetNamedItem("android:name").Value = "com.unity.reflect.viewer.ReflectUnityPlayerActivity";
        return mainActivity;
    }

    internal void AddReflectScheme(XmlNode mainActivity)
    {
        XmlElement intentNode = CreateElement("intent-filter");
        intentNode.AppendChild(CreateElementWithAttribute("data", "scheme", "reflect"));
        intentNode.AppendChild(CreateElementWithAttribute("action", "name", "android.intent.action.VIEW"));
        intentNode.AppendChild(CreateElementWithAttribute("category", "name", "android.intent.category.DEFAULT"));
        intentNode.AppendChild(CreateElementWithAttribute("category", "name", "android.intent.category.BROWSABLE"));
        mainActivity.AppendChild(intentNode);
    }

    internal void ApplyReflectChanges()
    {
        AddReflectScheme(RenameActivityForDeepLink(GetActivityWithLaunchIntent()));
    }
}
#endif