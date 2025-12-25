using System;
using System.Collections.Generic;

public static class BleUuidExtensions
{
    public static IEnumerable<Guid> To128BitGuids(this byte[] data)
    {
        if (data == null || data.Length % 16 != 0)
            yield break;

        for (int i = 0; i < data.Length; i += 16)
        {
            var uuidBytes = new byte[16];
            Array.Copy(data, i, uuidBytes, 0, 16);

            // BLE UUIDs are little-endian
            yield return new Guid(uuidBytes);
        }
    }
}