using UnityEngine;
using UnityEngine.UIElements;

public class FlowerHexagonController : MonoBehaviour
{
    public UIDocument uiDocument;
    private VisualElement flowerContainer;

    void Start()
    {
        flowerContainer = uiDocument.rootVisualElement.Q<VisualElement>("flower-container");
        SetupHexagons();
    }

    void SetupHexagons()
    {
        for (int i = 0; i < 7; i++)
        {
            var hex = flowerContainer.Q<VisualElement>($"hex-{i}");
            var label = flowerContainer.Q<Label>($"label-{i}");

            // Пример: установка цвета и текста
            hex.style.backgroundColor = new StyleColor(Color.white);
            label.text = $"Hex {i}";

            // Пример обработки клика
            hex.RegisterCallback<ClickEvent>(evt => {
                hex.style.backgroundColor = new StyleColor(Color.red);
            });
        }
    }

    public void SetHexColor(int index, Color color)
    {
        var hex = flowerContainer.Q<VisualElement>($"hex-{index}");
        hex.style.backgroundColor = new StyleColor(color);
    }

    public void SetHexLabel(int index, string text)
    {
        var label = flowerContainer.Q<Label>($"label-{index}");
        label.text = text;
    }
}
