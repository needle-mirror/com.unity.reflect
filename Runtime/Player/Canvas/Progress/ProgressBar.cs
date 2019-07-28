using UnityEngine;
using UnityEngine.UI;

public class ProgressBar : MonoBehaviour
{
    public Text label;
    public RectTransform background;
    public RectTransform progress;
    public GameObject ui;
    public Color color = new Color(18f / 256f, 185f / 256f, 89f / 256f);

    void Start()
    {
        progress.GetComponent<Image>().color = color;
    }

    public void Register(IProgressTask progressTask)
    {
        progressTask.progressChanged += OnProgressChanged;
        progressTask.taskCompleted += OnTaskCompleted;
    }
    
    public void UnRegister(IProgressTask progressTask)
    {
        progressTask.progressChanged -= OnProgressChanged;
        progressTask.taskCompleted -= OnTaskCompleted;
    }
    
    void OnProgressChanged(float percent, string text)
    {
        if (!ui.activeSelf)
        {
            ui.SetActive(true);
        }

        if (percent > 1f)
        {
            percent = 1;
        }

        if (label.text != text)
            label.text = text;
        
        var max = progress.offsetMin;
        max.x += percent * background.rect.width;
        progress.offsetMax = max;
    }

    void OnTaskCompleted()
    {
        OnCancel();
    }

    void OnCancel()
    {
        ui.SetActive(false);
    }
}
