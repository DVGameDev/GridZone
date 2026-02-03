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
        //
        if (SystemAPI.HasSingleton<RadiationDebugState>())
            return;

        var e = EntityManager.CreateEntity(typeof(RadiationDebugState));
        EntityManager.SetComponentData(e, new RadiationDebugState
        {
            RevealAll = false,
            Dirty = true // первичная инициализация
        });
    }

    protected override void OnUpdate() { }
}
