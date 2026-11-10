//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/ACEvo.Package
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

namespace ACEvo.Package.Hashing;

public class FNV1A64
{
    public static ulong Hash(byte[] bytes)
    {
        const ulong fnv64Offset = 14695981039346656037;
        const ulong fnv64Prime = 0x100000001b3;
        ulong hash = fnv64Offset;

        for (var i = 0; i < bytes.Length; i++)
        {
            hash = hash ^ bytes[i];
            hash *= fnv64Prime;
        }

        return hash;
    }
}
