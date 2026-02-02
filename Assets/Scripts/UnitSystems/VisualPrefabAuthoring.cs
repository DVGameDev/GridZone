using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring: указываем какой визуальный prefab использовать
/// </summary>
public class VisualPrefabAuthoring : MonoBehaviour
{
    public GameObject VisualPrefab; // Prefab с Animator

    class Baker : Baker<VisualPrefabAuthoring>
    {
        public override void Bake(VisualPrefabAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Добавляем ССЫЛКУ на prefab (не сам GameObject!)
            AddComponentObject(entity, new VisualPrefab
            {
                Value = authoring.VisualPrefab
            });

            // Помечаем что визуал еще не создан
            AddComponent<NeedsVisualInstantiation>(entity);
        }
    }
}
