using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Создает и обновляет Mesh Overlay для Area режима
/// Генерирует динамический mesh по highlighted клеткам с Greedy Meshing оптимизацией
/// Добавляет LineRenderer границы для визуального эффекта
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GridVisualizationManager))]
public partial class AreaOverlaySystem : SystemBase
{
    private Material _overlayMaterial;

    protected override void OnCreate()
    {
        // Загружаем материал overlay
        _overlayMaterial = Resources.Load<Material>("Materials/GridOverlayMaterial");

        if (_overlayMaterial == null)
        {
            Debug.LogWarning("[AreaOverlaySystem] GridOverlayMaterial not found in Resources/Materials/. Using default material.");
        }
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // обработка AreaOverlayRequest (как у тебя сейчас)
        foreach (var (request, cellBuffer, entity)
            in SystemAPI.Query<RefRO<AreaOverlayRequest>, DynamicBuffer<OverlayCell>>()
                .WithEntityAccess())
        {
            if (cellBuffer.Length == 0)
            {
                DestroyActiveOverlay(ecb);
                ecb.DestroyEntity(entity);
                continue;
            }

            bool needsUpdate = false;

            if (SystemAPI.TryGetSingletonEntity<ActiveOverlayData>(out var overlayEntity))
            {
                var activeData = EntityManager.GetComponentData<ActiveOverlayData>(overlayEntity);

                if (activeData.Mode != request.ValueRO.Mode || activeData.CellCount != cellBuffer.Length)
                {
                    needsUpdate = true;
                    DestroyActiveOverlay(ecb);
                }
            }
            else
            {
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                CreateOverlay(cellBuffer, request.ValueRO.Mode, ecb);
            }

            ecb.DestroyEntity(entity);
        }

        // 🔥 НОВОЕ: авточистка, если режим сброшен
        if (SystemAPI.HasSingleton<ActiveUnitComponent>())
        {
            var selection = SystemAPI.GetSingleton<ActiveUnitComponent>();
            // Если нет юнита или режим None → overlay больше не нужен
            if (selection.Unit == Entity.Null || selection.Mode == InteractionMode.None)
            {
                DestroyActiveOverlay(ecb);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }


    private void CreateOverlay(DynamicBuffer<OverlayCell> cells, InteractionMode mode, EntityCommandBuffer ecb)
    {
        if (cells.Length == 0) return;

        var config = SystemAPI.GetSingleton<GridConfig>();
        var colors = SystemAPI.GetSingleton<GridColorConfig>();

        // 1. Генерируем Mesh
        var meshEntity = GenerateOverlayMesh(cells, mode, config, colors, ecb);

        // 2. Генерируем границы через LineRenderer
        var borderEntity = GenerateBorderLines(cells, mode, config, colors, ecb);

        // 3. Создаем синглтон с данными
        var dataEntity = ecb.CreateEntity();
        ecb.AddComponent(dataEntity, new AreaOverlayTag());
        ecb.AddComponent(dataEntity, new ActiveOverlayData
        {
            MeshEntity = meshEntity,
            DecalEntity = borderEntity,
            Mode = mode,
            CellCount = cells.Length
        });
        ecb.AddComponent(dataEntity, new OverlayAnimationData
        {
            PulsePhase = 0,
            PulseSpeed = 1.5f,
            PulseIntensity = 0.15f
        });
    }

    /// <summary>
    /// Объединяет смежные клетки в прямоугольники (Greedy Meshing)
    /// Возвращает список прямоугольников: (x, y, width, height)
    /// </summary>
    private NativeList<int4> MergeAdjacentCells(DynamicBuffer<OverlayCell> cells, int2 gridSize)
    {
        var rects = new NativeList<int4>(Allocator.Temp);

        // Создаем 2D карту занятых клеток
        var occupiedMap = new NativeArray<bool>(gridSize.x * gridSize.y, Allocator.Temp);

        foreach (var cell in cells)
        {
            int index = cell.GridPos.x * gridSize.y + cell.GridPos.y;
            if (index >= 0 && index < occupiedMap.Length)
                occupiedMap[index] = true;
        }

        // Greedy meshing алгоритм
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                int idx = x * gridSize.y + y;
                if (!occupiedMap[idx]) continue;

                // Находим максимальную ширину прямоугольника
                int width = 1;
                while (x + width < gridSize.x && occupiedMap[(x + width) * gridSize.y + y])
                    width++;

                // Находим максимальную высоту
                int height = 1;
                bool canExtendHeight = true;
                while (y + height < gridSize.y && canExtendHeight)
                {
                    for (int wx = 0; wx < width; wx++)
                    {
                        if (!occupiedMap[(x + wx) * gridSize.y + (y + height)])
                        {
                            canExtendHeight = false;
                            break;
                        }
                    }
                    if (canExtendHeight) height++;
                }

                // Сохраняем прямоугольник
                rects.Add(new int4(x, y, width, height));

                // Помечаем клетки как обработанные
                for (int wx = 0; wx < width; wx++)
                {
                    for (int hy = 0; hy < height; hy++)
                    {
                        occupiedMap[(x + wx) * gridSize.y + (y + hy)] = false;
                    }
                }
            }
        }

        occupiedMap.Dispose();
        return rects;
    }

    private Entity GenerateOverlayMesh(DynamicBuffer<OverlayCell> cells, InteractionMode mode, GridConfig config, GridColorConfig colors, EntityCommandBuffer ecb)
    {
        float spacing = config.Spacing;
        float meshHeight = config.HeightGround + 0.02f;

        // Объединяем смежные клетки
        var rects = MergeAdjacentCells(cells, config.GridSize);

        var vertices = new NativeList<float3>(rects.Length * 4, Allocator.Temp);
        var triangles = new NativeList<int>(rects.Length * 6, Allocator.Temp);
        var uvs = new NativeList<float2>(rects.Length * 4, Allocator.Temp);

        int vertexIndex = 0;
        float halfCell = spacing * 0.5f;
        float margin = spacing * 0.02f;

        foreach (var rect in rects)
        {
            int startX = rect.x;
            int startY = rect.y;
            int rectWidth = rect.z;
            int rectHeight = rect.w;

            // Координаты левого нижнего угла (центр первой клетки)
            float x0 = startX * spacing;
            float z0 = startY * spacing;

            // Координаты правого верхнего угла (центр последней клетки)
            float x1 = (startX + rectWidth - 1) * spacing;
            float z1 = (startY + rectHeight - 1) * spacing;

            // Расширяем до краев клеток
            float xMin = x0 - halfCell + margin;
            float xMax = x1 + halfCell - margin;
            float zMin = z0 - halfCell + margin;
            float zMax = z1 + halfCell - margin;

            // 4 вершины прямоугольника
            vertices.Add(new float3(xMin, meshHeight, zMin)); // BL
            vertices.Add(new float3(xMax, meshHeight, zMin)); // BR
            vertices.Add(new float3(xMax, meshHeight, zMax)); // TR
            vertices.Add(new float3(xMin, meshHeight, zMax)); // TL

            // UV
            uvs.Add(new float2(0, 0));
            uvs.Add(new float2(1, 0));
            uvs.Add(new float2(1, 1));
            uvs.Add(new float2(0, 1));

            // Треугольники
            triangles.Add(vertexIndex + 0);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 0);
            triangles.Add(vertexIndex + 3);
            triangles.Add(vertexIndex + 2);

            vertexIndex += 4;
        }

        var mesh = new Mesh
        {
            vertices = System.Array.ConvertAll(vertices.AsArray().ToArray(), v => new Vector3(v.x, v.y, v.z)),
            triangles = triangles.AsArray().ToArray(),
            uv = System.Array.ConvertAll(uvs.AsArray().ToArray(), uv => new Vector2(uv.x, uv.y))
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        vertices.Dispose();
        triangles.Dispose();
        uvs.Dispose();
        rects.Dispose();

        // Создаем GameObject
        var go = new GameObject("GridOverlay_Mesh");
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();

        meshFilter.mesh = mesh;

        var material = _overlayMaterial != null
            ? Object.Instantiate(_overlayMaterial)
            : CreateDefaultMaterial();

        Color baseColor = mode == InteractionMode.Move
            ? new Color(colors.ColorBlue.x, colors.ColorBlue.y, colors.ColorBlue.z, 0.15f)
            : new Color(colors.ColorYellow.x, colors.ColorYellow.y, colors.ColorYellow.z, 0.15f);

        material.SetColor("_BaseColor", baseColor);
        meshRenderer.material = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        var entity = ecb.CreateEntity();
        ecb.AddComponent(entity, new AreaOverlayTag());
        ecb.AddComponent(entity, new MeshRendererReference
        {
            GameObject = go,
            Renderer = meshRenderer,
            Material = material
        });

        return entity;
    }

    /// <summary>
    /// Создает LineRenderer для границ highlighted области
    /// Рисует контуры прямоугольников из Greedy Meshing
    /// </summary>
    /// <summary>
    /// Создает LineRenderer для контура highlighted области (XCOM style)
    /// Объединяет смежные рёбра в непрерывные контуры
    /// </summary>
    private Entity GenerateBorderLines(DynamicBuffer<OverlayCell> cells, InteractionMode mode, GridConfig config, GridColorConfig colors, EntityCommandBuffer ecb)
    {
        if (cells.Length == 0)
            return Entity.Null;

        float spacing = config.Spacing;
        float lineHeight = config.HeightGround + 0.03f;

        // Создаем HashSet для быстрой проверки highlighted клеток
        var cellSet = new NativeHashSet<int2>(cells.Length, Allocator.Temp);
        foreach (var cell in cells)
        {
            cellSet.Add(cell.GridPos);
        }

        // Собираем все рёбра на границе области
        var edges = new NativeHashSet<int4>(cells.Length * 4, Allocator.Temp); // (x0, z0, x1, z1)

        foreach (var cell in cells)
        {
            int2 pos = cell.GridPos;

            // Проверяем 4 направления
            int2 north = pos + new int2(0, 1);
            int2 east = pos + new int2(1, 0);
            int2 south = pos + new int2(0, -1);
            int2 west = pos + new int2(-1, 0);

            // Добавляем рёбра только на границе
            if (!cellSet.Contains(north))
                edges.Add(new int4(pos.x, pos.y + 1, pos.x + 1, pos.y + 1)); // Верхнее
            if (!cellSet.Contains(east))
                edges.Add(new int4(pos.x + 1, pos.y, pos.x + 1, pos.y + 1)); // Правое
            if (!cellSet.Contains(south))
                edges.Add(new int4(pos.x, pos.y, pos.x + 1, pos.y)); // Нижнее
            if (!cellSet.Contains(west))
                edges.Add(new int4(pos.x, pos.y, pos.x, pos.y + 1)); // Левое
        }

        cellSet.Dispose();

        if (edges.Count == 0)
        {
            edges.Dispose();
            return Entity.Null;
        }

        // Теперь объединяем рёбра в непрерывные контуры
        var contours = TraceContours(edges, spacing, lineHeight);
        edges.Dispose();

        if (contours.Length == 0)
        {
            contours.Dispose();
            return Entity.Null;
        }

        // Создаем GameObject-контейнер
        var containerGo = new GameObject("GridOverlay_BorderLines");

        // Цвет линии
        Color lineColor = mode == InteractionMode.Move
            ? new Color(colors.ColorBlue.x, colors.ColorBlue.y, colors.ColorBlue.z, 1f)
            : new Color(colors.ColorYellow.x, colors.ColorYellow.y, colors.ColorYellow.z, 1f);

        // Материал
        var lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_ZWrite", 0);
        lineMaterial.renderQueue = 3001;
        lineMaterial.SetColor("_BaseColor", lineColor);

        // Создаем LineRenderer для каждого контура
        int contourIndex = 0;
        foreach (var contour in contours)
        {
            if (contour.Length < 2) continue;

            var lineGo = new GameObject($"Contour_{contourIndex++}");
            lineGo.transform.SetParent(containerGo.transform);

            var lineRenderer = lineGo.AddComponent<LineRenderer>();
            lineRenderer.positionCount = contour.Length;
            lineRenderer.loop = false;
            lineRenderer.useWorldSpace = true;
            lineRenderer.material = lineMaterial;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;

            // Заполняем позиции
            var positions = new Vector3[contour.Length];
            for (int i = 0; i < contour.Length; i++)
            {
                positions[i] = new Vector3(contour[i].x, contour[i].y, contour[i].z);
            }
            lineRenderer.SetPositions(positions);
        }

        contours.Dispose();

        // Создаем Entity
        var entity = ecb.CreateEntity();
        ecb.AddComponent(entity, new AreaOverlayTag());
        ecb.AddComponent(entity, new LineRendererReference
        {
            GameObject = containerGo,
            Renderer = null
        });

        return entity;
    }

    /// <summary>
    /// Объединяет рёбра в непрерывные контуры
    /// </summary>
    /// <summary>
    /// Объединяет рёбра в непрерывные контуры (упрощенная версия)
    /// </summary>
    private NativeList<NativeList<float3>> TraceContours(NativeHashSet<int4> edges, float spacing, float height)
    {
        var contours = new NativeList<NativeList<float3>>(Allocator.Temp);
        var edgeList = edges.ToNativeArray(Allocator.Temp);
        var visited = new NativeArray<bool>(edgeList.Length, Allocator.Temp);

        for (int i = 0; i < edgeList.Length; i++)
        {
            if (visited[i]) continue;

            var contour = new NativeList<float3>(Allocator.Temp);
            var edge = edgeList[i];

            int2 start = new int2(edge.x, edge.y);
            int2 current = new int2(edge.x, edge.y);
            int2 next = new int2(edge.z, edge.w);

            contour.Add(GridToWorld(current, spacing, height));
            visited[i] = true;

            // Идём по рёбрам
            int maxSteps = 1000;
            int steps = 0;

            while (steps++ < maxSteps)
            {
                contour.Add(GridToWorld(next, spacing, height));

                // Ищем следующее ребро, которое начинается в точке 'next'
                bool foundNext = false;

                for (int j = 0; j < edgeList.Length; j++)
                {
                    if (visited[j]) continue;

                    var testEdge = edgeList[j];
                    int2 p0 = new int2(testEdge.x, testEdge.y);
                    int2 p1 = new int2(testEdge.z, testEdge.w);

                    // Проверяем совпадение начала ребра
                    if (p0.Equals(next) && !p1.Equals(current))
                    {
                        visited[j] = true;
                        current = next;
                        next = p1;
                        foundNext = true;
                        break;
                    }
                    // Проверяем обратное направление
                    else if (p1.Equals(next) && !p0.Equals(current))
                    {
                        visited[j] = true;
                        current = next;
                        next = p0;
                        foundNext = true;
                        break;
                    }
                }

                // Если вернулись в начало - замыкаем контур
                if (next.Equals(start))
                {
                    contour.Add(GridToWorld(next, spacing, height));
                    break;
                }

                if (!foundNext) break;
            }

            if (contour.Length >= 2)
            {
                contours.Add(contour);
            }
            else
            {
                contour.Dispose();
            }
        }

        edgeList.Dispose();
        visited.Dispose();

        return contours;
    }

   

    private float3 GridToWorld(int2 gridPos, float spacing, float height)
    {
        return new float3((gridPos.x - 0.5f) * spacing, height, (gridPos.y - 0.5f) * spacing);
    }


    private void DestroyActiveOverlay(EntityCommandBuffer ecb)
    {
        if (!SystemAPI.TryGetSingletonEntity<ActiveOverlayData>(out var entity))
            return;

        var data = EntityManager.GetComponentData<ActiveOverlayData>(entity);

        // Удаляем mesh GameObject
        if (data.MeshEntity != Entity.Null && EntityManager.Exists(data.MeshEntity))
        {
            if (EntityManager.HasComponent<MeshRendererReference>(data.MeshEntity))
            {
                var meshRef = EntityManager.GetComponentObject<MeshRendererReference>(data.MeshEntity);
                if (meshRef != null && meshRef.GameObject != null)
                    Object.Destroy(meshRef.GameObject);
            }
            ecb.DestroyEntity(data.MeshEntity);
        }

        // Удаляем Border (LineRenderer)
        if (data.DecalEntity != Entity.Null && EntityManager.Exists(data.DecalEntity))
        {
            if (EntityManager.HasComponent<LineRendererReference>(data.DecalEntity))
            {
                var lineRef = EntityManager.GetComponentObject<LineRendererReference>(data.DecalEntity);
                if (lineRef != null && lineRef.GameObject != null)
                    Object.Destroy(lineRef.GameObject);
            }

            ecb.DestroyEntity(data.DecalEntity);
        }

        ecb.DestroyEntity(entity);
    }

    private Material CreateDefaultMaterial()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;

        Debug.LogWarning("[AreaOverlaySystem] Using auto-generated default material");
        return mat;
    }
}
