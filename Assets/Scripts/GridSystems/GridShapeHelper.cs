using Unity.Mathematics;
using Unity.Collections;
using System;

public static class GridShapeHelper
{
    // === ГЕНЕРАЦИЯ ДЛЯ AIM SHAPES (Желтая зона) ===
    // === AIM SHAPES ===
    public static void GetAimOffsets(AimShapeConfig aimConfig, EffectShapeConfig effectConfig, int2 unitSize, int2 unitFacing, ref NativeList<int2> offsets)
    {
        offsets.Clear();
        switch (aimConfig.Type)
        {
            case AimShapeType.UnitPoint:
                if (effectConfig.Type == EffectShapeType.Cross) GetOffsets_BodyCross(unitSize, effectConfig.SizeX, ref offsets);
                else GetOffsets_BodySquareAura(unitSize, effectConfig.SizeX, 0, ref offsets); // Square Aura for UnitPoint
                break;

            case AimShapeType.FacePoint:
                GetOffsets_FaceProjection(unitSize, unitFacing, effectConfig.Type, effectConfig.SizeX, effectConfig.SizeZ, 0, ref offsets);
                break;

            case AimShapeType.Rect:
            case AimShapeType.Cone:
                // Это НАПРАВЛЕННЫЕ зоны с Offset (сдвигом старта)
                // Используем FaceProjection, но с типом Rect (или Cone) и Offset

                // Маппим тип
                EffectShapeType projectionType = (aimConfig.Type == AimShapeType.Cone) ? EffectShapeType.Cone : EffectShapeType.Rect;

                // Вызываем FaceProjection с aimConfig.Offset
                GetOffsets_FaceProjection(unitSize, unitFacing, projectionType, aimConfig.SizeX, aimConfig.SizeZ, aimConfig.Offset, ref offsets);
                break;


            case AimShapeType.Ring:
                // RING - теперь Квадратная аура (с углами)
                GetOffsets_BodySquareAura(unitSize, aimConfig.SizeX, aimConfig.SizeZ, ref offsets);
                break;

            case AimShapeType.HalfRing:
                // HALFRING - Квадратная аура + Фильтр
                // Вызываем Generate напрямую с useEuclidean=false
                GenerateBodyAura(unitSize, aimConfig.SizeX, aimConfig.SizeZ, ref offsets, false, true, unitFacing);
                break;

            case AimShapeType.Radius:
                // RADIUS - Тоже Квадратная (для единообразия Grid Tactics)
                GetOffsets_BodySquareAura(unitSize, aimConfig.SizeX, 0, ref offsets);
                break;
        }
    }


    // === ГЕНЕРАЦИЯ ДЛЯ EFFECT SHAPES (Сиреневая) ===
    public static void GetEffectOffsets_CursorBased(EffectShapeConfig config, int2 direction, ref NativeList<int2> offsets)
    {
        offsets.Clear();

        switch (config.Type)
        {
            case EffectShapeType.Cell:
                offsets.Add(int2.zero);
                break;

            case EffectShapeType.Cross:
                offsets.Add(int2.zero);
                for (int i = 1; i <= config.SizeX; i++)
                {
                    offsets.Add(new int2(i, 0)); offsets.Add(new int2(-i, 0));
                    offsets.Add(new int2(0, i)); offsets.Add(new int2(0, -i));
                }
                break;

            case EffectShapeType.Rect:
                // ИСПРАВЛЕНИЕ БАГА 2: Центрированный прямоугольник
                GenerateCenteredRect(config.SizeX, config.SizeZ, ref offsets);
                break;

            case EffectShapeType.Cone:
                // Конус всегда направлен (от курсора в сторону?)
                // Обычно взрыв-конус это странно, но допустим он направлен по Facing юнита (direction)
                GenerateProjectedShape_Internal(InternalShapeType.Cone, config.SizeX, 0, direction, ref offsets);
                break;

            case EffectShapeType.Circle:
                // Круг (заполненный)
                int r = config.SizeX;
                for (int x = -r; x <= r; x++) for (int y = -r; y <= r; y++)
                    {
                        if (x * x + y * y <= r * r) offsets.Add(new int2(x, y)); // Euclidean
                    }
                break;

            case EffectShapeType.Ring:
                // Кольцо (заполненное)
                int r2 = config.SizeX; int ir2 = config.SizeZ;
                for (int x = -r2; x <= r2; x++) for (int y = -r2; y <= r2; y++)
                    {
                        int dSq = x * x + y * y;
                        if (dSq <= r2 * r2 && dSq >= ir2 * ir2) offsets.Add(new int2(x, y)); // Euclidean
                    }
                break;
        }
    }

    // === ПРИВАТНЫЕ АЛГОРИТМЫ ===

    // 1. ЕЖ (Cross) из тела
    public static void GetOffsets_BodyCross(int2 unitSize, int len, ref NativeList<int2> offsets)
    {
        NativeList<int2> bodyCells = GetBodyCells(unitSize);
        offsets.AddRange(bodyCells.AsArray());

        // Up (Y=0, X=0..SizeX-1)
        for (int x = 0; x < unitSize.x; x++) for (int i = 1; i <= len; i++) offsets.Add(new int2(x, i));
        // Down
        int bottomY = -(unitSize.y - 1);
        for (int x = 0; x < unitSize.x; x++) for (int i = 1; i <= len; i++) offsets.Add(new int2(x, bottomY - i));
        // Right
        int rightX = unitSize.x - 1;
        for (int y = 0; y > -unitSize.y; y--) for (int i = 1; i <= len; i++) offsets.Add(new int2(rightX + i, y));
        // Left
        for (int y = 0; y > -unitSize.y; y--) for (int i = 1; i <= len; i++) offsets.Add(new int2(0 - i, y));

        bodyCells.Dispose();
    }

    // 2. КВАДРАТНАЯ АУРА (Rect Aim) - Chebyshev
    public static void GetOffsets_BodySquareAura(int2 unitSize, int range, int minRange, ref NativeList<int2> offsets)
    {
        GenerateBodyAura(unitSize, range, minRange, ref offsets, false, false, default);
    }

    // Wrapper for generic BodyAura (defaults to Square for now to be safe)
    public static void GetOffsets_BodyAura(int2 unitSize, int range, int minRange, ref NativeList<int2> offsets)
    {
        GenerateBodyAura(unitSize, range, minRange, ref offsets, false, false, default);
    }

    // 3. КРУГЛАЯ АУРА (Ring, Radius) - Euclidean
    public static void GetOffsets_BodyCircleAura(int2 unitSize, int range, int minRange, ref NativeList<int2> offsets, bool filterHalfRing = false, int2 facing = default)
    {
        GenerateBodyAura(unitSize, range, minRange, ref offsets, true, filterHalfRing, facing);
    }

    // Универсальный генератор Ауры
    private static void GenerateBodyAura(int2 unitSize, int range, int minRange, ref NativeList<int2> offsets, bool useEuclidean, bool filterHalfRing, int2 facing)
    {
        NativeList<int2> bodyCells = GetBodyCells(unitSize);
        int minX = -range; int maxX = (unitSize.x - 1) + range;
        int minY = -(unitSize.y - 1) - range; int maxY = range;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                int2 pos = new int2(x, y);

                // Проверка на тело
                bool isBody = false;
                foreach (var b in bodyCells) if (b.Equals(pos)) { isBody = true; break; }

                if (isBody)
                {
                    if (minRange == 0) offsets.Add(pos);
                    continue;
                }

                // Дистанция
               // int distMetric = 0;

                // Для дистанции от ТЕЛА нам нужно найти минимальное расстояние до любой клетки тела
                if (useEuclidean)
                {
                    // Euclidean (squared)
                    int minDSq = int.MaxValue;
                    foreach (var b in bodyCells)
                    {
                        int dSq = (pos.x - b.x) * (pos.x - b.x) + (pos.y - b.y) * (pos.y - b.y);
                        if (dSq < minDSq) minDSq = dSq;
                    }

                    // range - это количество клеток. В Euclidean range^2.
                    // Для "Radius 1" (крест) rangeSq=1. (1,1) -> distSq=2 (>1). Углы отсекаются.
                    if (minDSq <= range * range && minDSq >= minRange * minRange)
                    {
                        if (filterHalfRing) ApplyHalfRingFilter(pos, unitSize, facing, ref offsets);
                        else offsets.Add(pos);
                    }
                }
                else
                {
                    // Chebyshev (Square)
                    int minD = int.MaxValue;
                    foreach (var b in bodyCells)
                    {
                        int d = math.max(math.abs(pos.x - b.x), math.abs(pos.y - b.y));
                        if (d < minD) minD = d;
                    }
                    if (minD <= range && minD >= minRange)
                    {
                        // Поддержка HalfRing фильтра для Square Aura
                        if (filterHalfRing) ApplyHalfRingFilter(pos, unitSize, facing, ref offsets);
                        else offsets.Add(pos);
                    }
                }

            }
        }
        bodyCells.Dispose();
    }

    // Фильтр для HalfRing
    private static void ApplyHalfRingFilter(int2 pos, int2 unitSize, int2 facing, ref NativeList<int2> offsets)
    {
        // Простая проверка "Не сзади"
        bool isBehind = false;
        if (facing.Equals(new int2(0, 1))) { if (pos.y < -(unitSize.y - 1)) isBehind = true; } // Up -> Behind is Below Bottom Edge
        else if (facing.Equals(new int2(0, -1))) { if (pos.y > 0) isBehind = true; } // Down -> Behind is Above Top Edge
        else if (facing.Equals(new int2(1, 0))) { if (pos.x < 0) isBehind = true; } // Right -> Behind is Left of Left Edge
        else if (facing.Equals(new int2(-1, 0))) { if (pos.x > unitSize.x - 1) isBehind = true; } // Left -> Behind is Right of Right Edge

        if (!isBehind) offsets.Add(pos);
    }

    // Проекция от грани
    public static void GetOffsets_FaceProjection(int2 unitSize, int2 unitFacing, EffectShapeType effectType, int len, int width, int startOffset, ref NativeList<int2> offsets)
    {
        InternalShapeType shape = InternalShapeType.Rect;
        if (effectType == EffectShapeType.Cone) shape = InternalShapeType.Cone;
        else if (effectType == EffectShapeType.Cell) { shape = InternalShapeType.Rect; len = 1; width = 1; }

        int2 offsetVec = unitFacing * startOffset;
        int unitW_Perp = (unitFacing.x != 0) ? unitSize.y : unitSize.x;

        // Корректировка ширины для компенсации наложения лучей от широкого фронта
        int effectiveWidth = math.max(1, width - unitW_Perp + 1);

        // Получаем клетки грани
        NativeList<int2> edgeCells = new NativeList<int2>(Allocator.Temp);
        if (unitFacing.Equals(new int2(0, 1))) for (int x = 0; x < unitSize.x; x++) edgeCells.Add(new int2(x, 0));
        else if (unitFacing.Equals(new int2(0, -1))) { int y = -(unitSize.y - 1); for (int x = 0; x < unitSize.x; x++) edgeCells.Add(new int2(x, y)); }
        else if (unitFacing.Equals(new int2(1, 0))) { int x = unitSize.x - 1; for (int y = 0; y < unitSize.y; y++) edgeCells.Add(new int2(x, -y)); }
        else if (unitFacing.Equals(new int2(-1, 0))) for (int y = 0; y < unitSize.y; y++) edgeCells.Add(new int2(0, -y));

        foreach (var edge in edgeCells)
        {
            NativeList<int2> parts = new NativeList<int2>(Allocator.Temp);
            GenerateProjectedShape_Internal(shape, len, effectiveWidth, unitFacing, ref parts);
            foreach (var p in parts) offsets.Add(edge + offsetVec + p);
            parts.Dispose();
        }
        edgeCells.Dispose();
    }


    private static NativeList<int2> GetBodyCells(int2 size)
    {
        NativeList<int2> cells = new NativeList<int2>(Allocator.Temp);
        for (int x = 0; x < size.x; x++) for (int y = 0; y < size.y; y++) cells.Add(new int2(x, -y));
        return cells;
    }

    // Центрированный прямоугольник (для курсора)
    private static void GenerateCenteredRect(int sizeX, int sizeZ, ref NativeList<int2> offsets)
    {
        // SizeX (Len), SizeZ (Width). Centered at 0,0.
        // Even sizes are biased to negative? Or positive?
        // Let's bias to include 0,0.
        // Range: -Size/2 ... +Size/2 - (1 if even)
        // e.g. 2 -> -1..0. 3 -> -1..1.

        int minX = -sizeX / 2;
        int maxX = minX + sizeX - 1;

        int minZ = -sizeZ / 2;
        int maxZ = minZ + sizeZ - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++) // z maps to y
            {
                offsets.Add(new int2(x, z));
            }
        }
    }

    private static void GenerateProjectedShape_Internal(InternalShapeType shape, int length, int width, int2 dir, ref NativeList<int2> outOffsets)
    {
        if (shape == InternalShapeType.Rect)
        {
            int halfW = width / 2;
            int xStart = -halfW;
            int xEnd = (width % 2 == 0) ? (halfW - 1) : halfW;
            for (int y = 1; y <= length; y++)
                for (int x = xStart; x <= xEnd; x++)
                    outOffsets.Add(Rotate(new int2(x, y), dir));
        }
        else if (shape == InternalShapeType.Cone)
        {
            for (int y = 1; y <= length; y++)
            {
                int w = (y - 1);
                for (int x = -w; x <= w; x++) outOffsets.Add(Rotate(new int2(x, y), dir));
            }
        }
    }

    private enum InternalShapeType { Rect, Cone }
    private static int2 Rotate(int2 p, int2 dir)
    {
        if (dir.Equals(new int2(0, 1))) return p;
        if (dir.Equals(new int2(1, 0))) return new int2(p.y, -p.x);
        if (dir.Equals(new int2(0, -1))) return new int2(-p.x, -p.y);
        if (dir.Equals(new int2(-1, 0))) return new int2(-p.y, p.x);
        return p;
    }
}
