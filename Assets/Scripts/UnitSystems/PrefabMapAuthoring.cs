using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;

public class PrefabMapAuthoring : MonoBehaviour
{
    [System.Serializable]
    public struct UnitConfig
    {
        public string Name;
        public GameObject Prefab;
    }
    public List<UnitConfig> UnitPrefabs;

    class Baker : Baker<PrefabMapAuthoring>
    {
        public override void Bake(PrefabMapAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            // Создаем буфер UnitPrefabElement (структура теперь в GridComponents.cs)
            var buffer = AddBuffer<UnitPrefabElement>(entity);

            foreach (var config in authoring.UnitPrefabs)
            {
                if (config.Prefab != null)
                {
                    buffer.Add(new UnitPrefabElement
                    {
                        UnitName = new FixedString64Bytes(config.Name),
                        PrefabEntity = GetEntity(config.Prefab, TransformUsageFlags.Dynamic)
                    });
                }
            }
        }
    }
}
// Struct UnitPrefabElement удалена отсюда, так как перенесена в GridComponents.cs
