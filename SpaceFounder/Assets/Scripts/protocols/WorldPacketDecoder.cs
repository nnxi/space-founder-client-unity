using System;
using System.IO;
using UnityEngine;

public struct DecodedPlanetSnapshot
{
    public ushort id;
    public Vector3Int sectorIndex; 
    public Vector3 localPosition;  
    public Vector3 velocity;
}

public struct DecodedWorldUpdatePacket
{
    public double timestamp;
    public DecodedPlanetSnapshot[] planets;
}

public static class WorldPacketDecoder
{
    public const int HEADER_BYTES = 8;
    // ushort(2) + Int32x3(12) + floatx3(12) + floatx3(12) = 38바이트
    public const int PLANET_BYTES = 38; 

    public static DecodedWorldUpdatePacket Decode(byte[] rawData)
    {
        if (rawData == null || rawData.Length < HEADER_BYTES)
        {
            throw new ArgumentException("Invalid world update packet data.");
        }

        using (MemoryStream ms = new MemoryStream(rawData))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            double timestamp = reader.ReadDouble();

            int planetCount = (rawData.Length - HEADER_BYTES) / PLANET_BYTES;
            DecodedPlanetSnapshot[] planets = new DecodedPlanetSnapshot[planetCount];

            for (int i = 0; i < planetCount; i++)
            {
                ushort id = reader.ReadUInt16();

                // 32비트 정수 섹터 인덱스 (12바이트)
                int secX = reader.ReadInt32();
                int secY = reader.ReadInt32();
                int secZ = reader.ReadInt32();

                // 32비트 실수 로컬 좌표 (12바이트)
                float locX = reader.ReadSingle();
                float locY = reader.ReadSingle();
                float locZ = reader.ReadSingle();

                // 32비트 실수 속도 벡터 (12바이트)
                float velX = reader.ReadSingle();
                float velY = reader.ReadSingle();
                float velZ = reader.ReadSingle();

                planets[i] = new DecodedPlanetSnapshot
                {
                    id = id,
                    sectorIndex = new Vector3Int(secX, secY, secZ),
                    localPosition = new Vector3(locX, locY, locZ),
                    velocity = new Vector3(velX, velY, velZ)
                };
            }

            return new DecodedWorldUpdatePacket
            {
                timestamp = timestamp,
                planets = planets
            };
        }
    }
}