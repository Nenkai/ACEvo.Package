//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/ACEvo.Package
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

namespace ACEvo.Package;

// Size = 0x100
public unsafe struct PackFileEntry
{
    public fixed byte FileNameBuffer[0xE0];
    public int UnkAlways0; // Always 0
    public FileFlags Flags;
    public short FileNameLength;

    // ENTRIES MUST BE ORDERED BY THIS FOR BSEARCH.
    // Hash Algorithm: FNV1A64
    // Path is lower case, UNICODE bytes, dir separators are \'s.
    public ulong PathHash;

    public long FileSize;
    public long FileOffset;
}

[Flags]
public enum FileFlags : ushort
{
    IsDirectory = 1 << 0,
    Encrypted = 1 << 8,
}
