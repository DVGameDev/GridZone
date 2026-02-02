using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring для Hex Cell (добавьте на prefab)
/// </summary>
public class HexCellAuthoring : MonoBehaviour
{
    public class Baker : Baker<HexCellAuthoring>
    {
        public override void Bake(HexCellAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            // Добавляем компоненты, необходимые для работы с гридом
            AddComponent(entity, new GridCoordinates { Value = default });
            AddComponent(entity, new ClickableComponent());
            AddComponent(entity, new GridCellState { IsSelected = false });
        }
    }
}
