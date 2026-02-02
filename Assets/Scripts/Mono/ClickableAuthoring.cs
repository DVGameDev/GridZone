using Unity.Entities;
using UnityEngine;

public class ClickableAuthoring : MonoBehaviour
{
    // Пустой класс для Authoring
}

public class ClickableBaker : Baker<ClickableAuthoring>
{
    // В ClickableAuthoring.cs / ClickableBaker
    public override void Bake(ClickableAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new ClickableComponent());
        AddComponent(entity, new GridCellState { IsSelected = false }); // Добавляем состояние
    }

}
