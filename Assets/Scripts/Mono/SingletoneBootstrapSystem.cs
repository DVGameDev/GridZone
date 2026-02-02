using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class SingletoneBootstrapSystem : SystemBase
{
    protected override void OnCreate()
    {
        // Создаём синглтон для ActiveUnitComponent
        if (!SystemAPI.HasSingleton<ActiveUnitComponent>())
        {
            var entity = EntityManager.CreateEntity(typeof(ActiveUnitComponent));
            EntityManager.SetComponentData(entity, new ActiveUnitComponent
            {
                Unit = Entity.Null
            });
        }

        // Создаём синглтон-буфер для команд эффектов
        var entity2 = EntityManager.CreateEntity();
        EntityManager.AddComponent<EffectCommandBufferTag>(entity2);
        EntityManager.AddBuffer<EffectCommandBuffer>(entity2);

        UnityEngine.Debug.Log("✅ [SingletoneBootstrapSystem] Effect buffer singleton created!");

        Enabled = false;
    }

    protected override void OnUpdate() { }
}
