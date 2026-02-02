using Unity.Entities;
using UnityEngine;

public class UnitAuthoring : MonoBehaviour
{
    // Можно добавить сюда стартовые параметры, если хотите их видеть в инспекторе префаба
}
public class UnitBaker : Baker<UnitAuthoring>
{
    public override void Bake(UnitAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        //var entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);
        //DependsOn(authoring.transform);// Dynamic - так как юнит будет двигаться
        AddComponent(entity, new SpawnUnitsTag()); // Какой-то тег
        // AddComponent(entity, new UnitStats...); // Если статы не только из таблицы
    }
}
