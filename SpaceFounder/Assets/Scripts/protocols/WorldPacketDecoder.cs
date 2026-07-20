using System;
using System.IO;
using UnityEngine;

public struct DecodedPlanetSnapshot
{
    public ushort id;
    public Vector3 position;
    public Vector3 velocity;
}

public struct DecodedWorldUpdatePacket
{
    public double timestamp;
    public DecodedPlanetSnapshot[] planets;
}

public static class WorldPacketDecoder
{
    // 상수 정의 (constants.ts)
    public const int HEADER_BYTES = 8;
    public const int PLANET_BYTES = 26;

    // 패킷 디코딩 함수 (decoder.ts)
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

                float posX = reader.ReadSingle();
                float posY = reader.ReadSingle();
                float posZ = reader.ReadSingle();

                float velX = reader.ReadSingle();
                float velY = reader.ReadSingle();
                float velZ = reader.ReadSingle();

                planets[i] = new DecodedPlanetSnapshot
                {
                    id = id,
                    position = new Vector3(posX, posY, posZ),
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