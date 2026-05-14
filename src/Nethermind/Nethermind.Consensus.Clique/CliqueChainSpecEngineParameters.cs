// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Int256;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Consensus.Clique;

public class CliqueChainSpecEngineParameters : IChainSpecEngineParameters
{
    public string? EngineName => SealEngineType;
    public string? SealEngineType => Core.SealEngineType.Clique;
    // Bourse fork: Clique parameters are hardcoded regardless of chainspec input -
    // block-on-demand (period 0) with the standard 30000-block checkpoint epoch.
    public ulong Epoch { get => 30000UL; set { } }
    public ulong Period { get => 0UL; set { } }
    public UInt256? Reward { get; set; } = UInt256.Zero;
}
