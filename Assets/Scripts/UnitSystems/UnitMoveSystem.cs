using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;

[BurstCompile]
public partial struct UnitMoveSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gridConfig = SystemAPI.GetSingleton<GridConfig>();
        var facingMode = gridConfig.FacingMode;
        var layoutType = gridConfig.Layout; // 🔥 ДОБАВЛЕНО
        float dt = SystemAPI.Time.DeltaTime;

        new MoveJob
        {
            DeltaTime = dt,
            FacingMode = facingMode,
            LayoutType = layoutType // 🔥 ДОБАВЛЕНО
        }.ScheduleParallel();
    }


    /// <summary>
    /// Burst-компилируемая Job для параллельного движения юнитов
    /// </summary>
    [BurstCompile]
    private partial struct MoveJob : IJobEntity
    {
        public float DeltaTime;
        public UnitFacingMode FacingMode;
        public GridLayoutType LayoutType;

        private void Execute(ref LocalTransform transform, ref MoveCommand moveCmd, ref UnitFacing facing)
        {
            if (!moveCmd.IsMoving) return;

            float3 currentPos = transform.Position;
            float3 targetPos = moveCmd.TargetPosition;
            float speed = moveCmd.MoveSpeed * 0.3f;
            float dist = math.distance(currentPos, targetPos);

            if (dist < 0.05f)
            {
                transform.Position = targetPos;
                moveCmd.IsMoving = false;

                // 🔥 Поворот при остановке
                if (FacingMode != UnitFacingMode.Fixed)
                {
                    if (LayoutType == GridLayoutType.Quad)
                    {
                        // Quad: snap к 4 направлениям
                        quaternion currentRot = transform.Rotation;
                        float3 forward = math.rotate(currentRot, math.forward());
                        float3 snappedForward = new float3(0, 0, 1);

                        if (math.abs(forward.x) > math.abs(forward.z))
                            snappedForward = new float3(math.sign(forward.x), 0, 0);
                        else
                            snappedForward = new float3(0, 0, math.sign(forward.z));

                        transform.Rotation = quaternion.LookRotation(snappedForward, math.up());
                        facing.Value = new int2((int)snappedForward.x, (int)snappedForward.z);
                    }
                    else if (LayoutType == GridLayoutType.HexFlatTop)
                    {
                        // 🔥 HEX: сохраняем текущий поворот (плавный)
                        quaternion currentRot = transform.Rotation;
                        float3 forward = math.rotate(currentRot, math.forward());
                        facing.Value = new int2((int)math.round(forward.x), (int)math.round(forward.z));
                    }
                }
            }
            else
            {
                // Движение
                float3 dir = math.normalize(targetPos - currentPos);
                transform.Position += dir * speed * DeltaTime;

                // Поворот во время движения
                if (FacingMode == UnitFacingMode.Fixed) return;

                float3 flatDir = new float3(dir.x, 0, dir.z);
                if (math.lengthsq(flatDir) > 0.001f)
                {
                    quaternion targetRotation = transform.Rotation;

                    if (FacingMode == UnitFacingMode.Free)
                    {
                        // Плавный поворот (для Quad и Hex)
                        targetRotation = quaternion.LookRotation(flatDir, math.up());
                    }
                    else if (FacingMode == UnitFacingMode.OnlyX)
                    {
                        // 🔥 Только для Quad
                        if (LayoutType == GridLayoutType.Quad && math.abs(flatDir.x) > 0.01f)
                        {
                            float signX = math.sign(flatDir.x);
                            float3 lookX = new float3(signX, 0, 0);
                            targetRotation = quaternion.LookRotation(lookX, math.up());
                        }
                    }

                    transform.Rotation = targetRotation;
                }
            }
        }
    }


}
