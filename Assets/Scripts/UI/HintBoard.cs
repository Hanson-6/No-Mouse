using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class HintBoard : MonoBehaviour
{
    [Header("Content")]
    [TextArea(2, 8)]
    [SerializeField] private string hintText = "Edit this hint text in Inspector.";
    [SerializeField] private Text hintLabel;

    [Header("Style")]
    [SerializeField] private Color textColor = new Color(0.16f, 0.11f, 0.08f, 1f);
    [SerializeField, Min(12)] private int fontSize = 46;

    private void Reset()
    {
        AutoBind();
        Apply();
    }

    private void Awake()
    {
        AutoBind();
        Apply();
    }

    private void OnValidate()
    {
        AutoBind();
        Apply();
    }

    public void SetHintText(string newText)
    {
        hintText = newText;
        Apply();
    }

    private void AutoBind()
    {
        if (hintLabel == null)
            hintLabel = GetComponentInChildren<Text>(true);
    }

    private void Apply()
    {
        if (hintLabel == null)
            return;

        hintLabel.text = hintText;
        hintLabel.color = textColor;
        hintLabel.fontSize = Mathf.Max(12, fontSize);
    }
}
